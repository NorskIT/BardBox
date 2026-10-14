using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
namespace BardBox;

internal static class RadioPlayback
{
    internal sealed class Voice
    {
        internal readonly GameObject Root;
        internal readonly AudioSource Source;
        internal readonly MusicAmplifier Amplifier;
        internal string Tape="",Session="",Warned="";
        internal double Start,NextSync,LastUsed;
        internal bool Corrected;
        internal Voice()
        {
            Root=new GameObject("BardBox radio audio");Source=Root.AddComponent<AudioSource>();Amplifier=Root.AddComponent<MusicAmplifier>();
            Source.playOnAwake=false;Source.loop=true;Source.spatialBlend=1;Source.dopplerLevel=0;
            // Distance is measured against the listener's character, not a movable camera.
            Source.rolloffMode=AudioRolloffMode.Custom;Source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,AnimationCurve.Linear(0,1,1,1));
            Source.outputAudioMixerGroup=Resources.FindObjectsOfTypeAll<AudioMixerGroup>().FirstOrDefault(g=>g.name=="SFX");
        }
    }
    private static readonly Dictionary<ZDOID,Voice> voices=new();
    private static readonly Dictionary<ZDOID,RadioState> active=new();
    internal static IEnumerable<RadioState> Active=>active.Values;
    internal static bool Audible {get;private set;}
    internal static AudioSource? Source(ZDOID id)=>voices.TryGetValue(id,out var v)?v.Source:null;
    internal static bool ConsumeCorrection(ZDOID id)
    {if(!voices.TryGetValue(id,out var v) || !v.Corrected)return false;v.Corrected=false;return true;}
    internal static void Tick()
    {
        var local=Player.m_localPlayer;
        if(!Plugin.Ready || !local || !ZNet.instance){Clear();return;}
        Audible=false;active.Clear();
        foreach(var s in RadioNetwork.States)active[s.Id]=s;
        // Immediate local playback; never replay an older server echo of our own stop.
        active.Remove(local.GetZDOID());
        var musician=local.GetComponent<Musician>();var own=musician?musician.AudioState():null;
        if(own!=null)active[own.Id]=own;
        float radius=RadioRules.Range(Plugin.Instance.AudioRange.Value);
        var listener=local.transform.position+Vector3.up*2;
        foreach(var pair in active)
        {
            var s=pair.Value;float attenuation=RadioRules.Attenuation(Vector3.Distance(listener,s.Position),radius);
            var clip=AudioLibrary.Get(s.Tape);
            bool matches=PlaybackRules.SameSong(s.Hash,AudioLibrary.Hash(s.Tape));
            if(!voices.TryGetValue(s.Id,out var v))
            {
                if(attenuation<=0 || !clip)continue;
                voices[s.Id]=v=new Voice();
            }
            v.LastUsed=MusicClock.Local;
            if(!matches && v.Warned!=s.Session){v.Warned=s.Session;Plugin.Instance.Warn("BardBox song files differ for "+s.Tape+". Install the same song package as the musician.");}
            float volume=Mathf.Min(1,Plugin.Instance.Volume.Value)*Plugin.Instance.ListenerVolume.Value*attenuation;
            if(!matches || !clip || volume<=0){v.Source.Stop();continue;}
            v.Root.transform.position=s.Position;v.Source.minDistance=Mathf.Min(3,radius*.5f);v.Source.maxDistance=radius;
            v.Source.volume=volume;v.Amplifier.Gain=Mathf.Max(1,Plugin.Instance.Volume.Value);
            double elapsed=Math.Max(0,MusicClock.Now-s.Start);v.Start=s.Start;
            if(v.Tape!=s.Tape || v.Session!=s.Session || !v.Source.isPlaying)
            {
                v.Source.Stop();v.Source.clip=clip;v.Tape=s.Tape;v.Session=s.Session;v.Start=s.Start;
                v.Source.timeSamples=PlaybackRules.Sample(elapsed,clip.frequency,clip.samples);v.Source.Play();v.NextSync=MusicClock.Local+.25;
            }
            else if(MusicClock.Local>=v.NextSync)
            {
                v.NextSync=MusicClock.Local+.25;
                int expected=PlaybackRules.Sample(elapsed,clip.frequency,clip.samples),actual=v.Source.timeSamples;
                double error=PlaybackRules.ErrorSeconds(actual,expected,clip.frequency,clip.samples);
                if(Math.Abs(error)>PlaybackRules.CorrectionThreshold){v.Source.timeSamples=expected;v.Corrected=true;}
                Plugin.Instance.Trace(s.Id+" session="+s.Session+" error="+error.ToString("F6"));
            }
            Audible|=v.Source.isPlaying && !v.Source.mute && AudioListener.volume>0 && (!v.Source.outputAudioMixerGroup || AudioMan.GetSFXVolume()>0);
        }
        foreach(var pair in voices.ToArray())if(!active.ContainsKey(pair.Key))
        {
            pair.Value.Source.Stop();
            if(MusicClock.Local-pair.Value.LastUsed>30){UnityEngine.Object.Destroy(pair.Value.Root);voices.Remove(pair.Key);}
        }
    }
    internal static void Clear(){foreach(var v in voices.Values)if(v.Root)UnityEngine.Object.Destroy(v.Root);voices.Clear();active.Clear();Audible=false;}
}
