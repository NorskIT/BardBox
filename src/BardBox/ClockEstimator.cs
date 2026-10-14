using System;
using System.Collections.Generic;

namespace BardBox;

// Four timestamps: client send, server receive/send, client receive.
internal sealed class ClockEstimator
{
    private readonly Queue<(double Offset,double Rtt)> samples=new Queue<(double,double)>();
    internal bool Ready=>samples.Count>0;
    internal double Offset {get;private set;}
    internal double RoundTrip {get;private set;}
    internal bool Add(double t1,double t2,double t3,double t4)
    {
        if(!Finite(t1)||!Finite(t2)||!Finite(t3)||!Finite(t4)||t4<t1||t3<t2)return false;
        double rtt=(t4-t1)-(t3-t2);
        if(rtt<-.001 || rtt>10)return false;
        samples.Enqueue(((t2-t1+t3-t4)/2,Math.Max(0,rtt)));
        if(samples.Count>8)samples.Dequeue();
        RoundTrip=double.PositiveInfinity;
        foreach(var sample in samples)if(sample.Rtt<RoundTrip){RoundTrip=sample.Rtt;Offset=sample.Offset;}
        return true;
    }
    private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
}
