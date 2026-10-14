using UnityEngine;

namespace BardBox;

internal sealed class Shockwave
{
    private GameObject? root;
    private LineRenderer? ring;
    private float began;
    internal void Trigger(Vector3 origin)
    {
        if(!Plugin.Instance.ShockwaveMaterial)return;
        if(!root)
        {
            root=new GameObject("BardBox music shockwave");ring=root.AddComponent<LineRenderer>();
            ring.sharedMaterial=Plugin.Instance.ShockwaveMaterial;
            ring.useWorldSpace=false;ring.loop=true;ring.positionCount=96;
            ring.widthMultiplier=.035f;ring.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows=false;ring.numCornerVertices=2;
            for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96;ring.SetPosition(i,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}
        }
        root!.transform.position=origin;began=Time.unscaledTime;root.SetActive(true);Tick();
    }
    internal void Tick()
    {
        if(!root || !root.activeSelf)return;
        float t=(Time.unscaledTime-began)/.6f;
        if(t>=1){Hide();return;}
        root.transform.localScale=Vector3.one*Mathf.Lerp(.15f,4,t);
        var color=new Color(.15f,.9f,1,1-t);ring!.startColor=color;ring.endColor=color;
    }
    internal void Hide(){if(root)root.SetActive(false);}
    internal void Destroy(){if(root)Object.Destroy(root);root=null;ring=null;}
}
