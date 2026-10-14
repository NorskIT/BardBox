using System;

namespace BardBox;

internal static class AudioGain
{
    internal static void Apply(float[] samples,float gain)
    {
        if(float.IsNaN(gain) || float.IsInfinity(gain))gain=1;
        gain=Math.Max(0,Math.Min(5,gain));
        if(gain==1)return;
        for(int i=0;i<samples.Length;i++)samples[i]=Math.Max(-1,Math.Min(1,samples[i]*gain));
    }
}
