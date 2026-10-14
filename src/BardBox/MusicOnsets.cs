using System;
using System.Collections.Generic;

namespace BardBox;

// Analyze decoded mono samples once, independent of volume and rendering rate.
internal static class MusicOnsets
{
    internal static double[] Analyze(float[] samples,int rate)
    {
        if(rate<=0)throw new ArgumentOutOfRangeException(nameof(rate));
        int window=Math.Max(1,rate/20),count=samples.Length/window;
        var bass=new double[count];var energy=new double[count];var cues=new List<double>();
        double low=0,alpha=1-Math.Exp(-2*Math.PI*180/rate),last=-2;
        for(int w=0;w<count;w++)
        {
            for(int i=w*window;i<(w+1)*window;i++)
            {double sample=samples[i];low+=alpha*(sample-low);bass[w]+=low*low;energy[w]+=sample*sample;}
            bass[w]=Math.Sqrt(bass[w]/window);energy[w]=Math.Sqrt(energy[w]/window);
            if(w<10)continue;
            double baseBass=0,baseEnergy=0;int first=Math.Max(0,w-30);
            for(int j=first;j<w;j++){baseBass+=bass[j];baseEnergy+=energy[j];}
            baseBass/=w-first;baseEnergy/=w-first;
            double time=w*window/(double)rate;
            if(time-last>=1.5 && bass[w]>.015 && energy[w]>.025 && bass[w]>1.8*baseBass && energy[w]>1.25*baseEnergy)
            {cues.Add(time);last=time;}
        }
        return cues.ToArray();
    }
}

// Returns only fresh crossed cues. Entry/restart/clock discontinuities never replay history.
internal sealed class CueClock
{
    private string identity="";
    private double previous;
    internal void Reset(){identity="";previous=0;}
    internal bool Advance(string key,double elapsed,double duration,double[] cues)
    {
        if(duration<=0 || elapsed<0 || double.IsNaN(elapsed) || double.IsInfinity(elapsed)){Reset();return false;}
        if(identity!=key || elapsed<previous || elapsed-previous>.5)
        {identity=key;previous=elapsed;return false;}
        double before=previous;previous=elapsed;
        foreach(double cue in cues)
        {
            // Prune a closing cue too near the next opening cue on ALL clients,
            // including listeners who have just entered range. No observer history.
            if(cues.Length>1 && cue!=cues[0] && duration+cues[0]-cue<1.5)continue;
            double loop=Math.Floor(elapsed/duration),occurrence=loop*duration+cue;
            if(occurrence>elapsed){occurrence-=duration;loop--;}
            if(duration<1.5 && loop%Math.Ceiling(1.5/duration)!=0)continue;
            if(occurrence>before && occurrence<=elapsed)return true;
        }
        return false;
    }
}
