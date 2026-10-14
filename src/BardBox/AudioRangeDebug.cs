using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BardBox;
internal static class AudioRangeDebug
{
    private static readonly Dictionary<ZDOID,GameObject> rings=new();
    internal static bool TextFocused()=>GUIUtility.keyboardControl!=0 || Console.IsVisible() || TextInput.IsVisible() || (Chat.instance && Chat.instance.HasFocus());
    internal static void Tick()
    {
        var config=Plugin.Instance;
        if(Application.isFocused && !TextFocused() && config.ToggleAudioRangeKey.Value.MainKey!=KeyCode.None && config.ToggleAudioRangeKey.Value.IsDown())config.ShowAudioRange.Value=!config.ShowAudioRange.Value;
        var local=Player.m_localPlayer;
        if(!config.ShowAudioRange.Value || !local || !config.ShockwaveMaterial){Clear();return;}
        float radius=RadioRules.Range(config.AudioRange.Value);
        var positions=new Dictionary<ZDOID,Vector3>();
        foreach(var s in RadioPlayback.Active)if(Vector3.Distance(local.transform.position+Vector3.up*2,s.Position)<radius)positions[s.Id]=s.Position;
        if(Musician.HasBox(local))positions[local.GetZDOID()]=local.transform.position+Vector3.up*2;
        foreach(var pair in positions)
        {
            if(!rings.TryGetValue(pair.Key,out var root))
            {
                rings[pair.Key]=root=new GameObject("BardBox audio range");
                for(int plane=0;plane<3;plane++)
                {
                    var line=new GameObject("Range circle").AddComponent<LineRenderer>();line.transform.SetParent(root.transform,false);
                    line.sharedMaterial=config.ShockwaveMaterial;line.useWorldSpace=false;line.loop=true;line.positionCount=128;
                    line.startColor=line.endColor=plane==0?new Color(.2f,1,.9f,.85f):new Color(.3f,.7f,1,.4f);
                    line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
                    for(int i=0;i<128;i++){float a=i*Mathf.PI*2/128,x=Mathf.Cos(a),y=Mathf.Sin(a);line.SetPosition(i,plane==0?new Vector3(x,0,y):plane==1?new Vector3(x,y,0):new Vector3(0,x,y));}
                }
            }
            root.transform.position=pair.Value;root.transform.localScale=Vector3.one*radius;
            foreach(var line in root.GetComponentsInChildren<LineRenderer>())line.widthMultiplier=Mathf.Clamp(radius*.002f,.04f,2);
        }
        foreach(var pair in rings.ToArray())if(!positions.ContainsKey(pair.Key)){UnityEngine.Object.Destroy(pair.Value);rings.Remove(pair.Key);}
    }
    internal static void Draw()
    {
        if(!Plugin.Instance.ShowAudioRange.Value || rings.Count==0 || !Camera.main)return;
        var previousColor=GUI.color;GUI.color=Color.cyan;
        foreach(var root in rings.Values)
        {
            var p=Camera.main.WorldToScreenPoint(root.transform.position);
            if(p.z>0)GUI.Label(new Rect(p.x-80,Screen.height-p.y-35,240,24),"BardBox audio: "+RadioRules.Range(Plugin.Instance.AudioRange.Value).ToString("0.#")+" m");
        }
        GUI.color=previousColor;
    }
    internal static void Clear(){foreach(var root in rings.Values)if(root)UnityEngine.Object.Destroy(root);rings.Clear();}
}
