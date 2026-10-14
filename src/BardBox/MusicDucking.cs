using HarmonyLib;
using UnityEngine;
namespace BardBox;

internal sealed class MusicDuckFilter : MonoBehaviour
{
    internal volatile float Gain=1;
    private void OnAudioFilterRead(float[] data,int channels)=>AudioGain.Apply(data,Gain);
}
internal static class MusicDucking
{
    private static readonly AccessTools.FieldRef<MusicMan,AudioSource> ReadSource=AccessTools.FieldRefAccess<MusicMan,AudioSource>("m_musicSource");
    private static AudioSource? source;
    private static MusicDuckFilter? filter;
    internal static float Gain {get;private set;}=1;
    internal static void Tick(float delta)
    {
        bool duck=Player.m_localPlayer && Plugin.Instance.MuteGameMusic.Value && RadioPlayback.Audible;
        Gain=Mathf.MoveTowards(Gain,duck?0:1,Mathf.Max(0,delta)*(duck?2:1));
        var current=MusicMan.instance?ReadSource(MusicMan.instance):null;
        if(source!=current)
        {
            ReleaseFilter();source=current;
        }
        if(source && Gain<1)
        {
            if(!filter)filter=source.gameObject.AddComponent<MusicDuckFilter>();
            filter!.Gain=Gain;
        }
        else if(Gain>=1)ReleaseFilter();
    }
    private static void ReleaseFilter(){if(filter){filter.Gain=1;UnityEngine.Object.Destroy(filter);}filter=null;}
    internal static void Clear(){ReleaseFilter();source=null;Gain=1;}
}
