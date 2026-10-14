using System;
namespace BardBox;
internal static class RadioRules
{
    internal const double Lease=3.5;
    internal static bool Finite(double value)=>!double.IsNaN(value) && !double.IsInfinity(value);
    internal static float Range(float value)=>Finite(value)?Math.Max(1,Math.Min(2000,value)):500;
    internal static float Attenuation(float distance,float radius)
    {
        radius=Range(radius);
        if(!Finite(distance) || distance>=radius)return 0;
        float near=Math.Min(3,radius*.5f);
        return distance<=near?1:Math.Max(0,(radius-distance)/(radius-near));
    }
    internal static bool Fresh(double now,double seen)=>Finite(now) && Finite(seen) && now-seen>=-1 && now-seen<Lease;
    internal static bool Valid(string tape,int quality,string session,string hash,double start,double now)=>
        Catalog.Find(tape)!=null && quality>=1 && quality<=3 && Guid.TryParseExact(session,"N",out _) &&
        hash.Length==64 && Array.TrueForAll(hash.ToCharArray(),Uri.IsHexDigit) && Finite(start) && start>0 && start<=now+1;
    internal static bool NewSequence(long incoming,long previous)=>incoming>0 && incoming>previous;
}
