using System;
namespace BardBox;
internal static class PlaybackRules
{
    // Leave margin for opposite client errors and the clock estimate itself.
    internal const double CorrectionThreshold=.035;
    internal static bool SameSong(string advertised,string local)=>advertised.Length==64 && advertised==local;
    internal static double ErrorSeconds(int actual,int expected,int rate,int samples)
    {
        if(rate<=0 || samples<=0)return 0;
        long delta=(long)actual-expected;
        if(delta>samples/2.0)delta-=samples;
        if(delta<-samples/2.0)delta+=samples;
        return delta/(double)rate;
    }
    internal static bool Valid(string tape,int quality,long start,double age,bool holding,bool dead)
        =>Catalog.Find(tape)!=null && quality>=1 && quality<=3 && start>0 && age>=-1 && age<3.5 && holding && !dead;
    internal static int Sample(double elapsed,int rate,int samples)
    {
        if(rate<=0 || samples<=0 || double.IsNaN(elapsed) || double.IsInfinity(elapsed))return 0;
        return (int)((Math.Max(0,elapsed)*rate)%samples);
    }
}
