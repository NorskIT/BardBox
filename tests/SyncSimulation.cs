using System;
using BardBox;

internal static class SyncSimulation
{
    internal static void Run(Action<bool,string> check)
    {
        var clock=new ClockEstimator();
        check(clock.Add(100,200.05,200.06,100.11),"Four timestamp clock sample");
        check(Math.Abs(clock.Offset-100)<1e-9 && Math.Abs(clock.RoundTrip-.1)<1e-9,"Processing delay excluded from RTT");
        clock.Add(101,201.4,201.41,101.51);
        check(Math.Abs(clock.Offset-100)<1e-9,"Prefer lowest RTT among recent eight");
        for(int i=0;i<8;i++)clock.Add(110+i,212+i,212.01+i,110.21+i);
        check(Math.Abs(clock.Offset-101.9)<1e-9,"Old clock estimate ages out");
        check(!clock.Add(double.NaN,0,0,0)&&!clock.Add(5,6,5,7)&&!clock.Add(5,6,7,4),"Reject malformed clock samples");
        check(!clock.Add(1,2,2,12),"Reject obsolete clock sample");
        const int rate=44100,length=83*rate;
        check(PlaybackRules.Sample(.5,rate,length)==22050,"Half-second delivery starts half-second into song");
        check(PlaybackRules.Sample(5,rate,length)==220500,"Five-second delivery skips missed opening");
        check(Math.Abs(PlaybackRules.ErrorSeconds(length-441,441,rate,length)+.02)<1e-9,"Circular error across loop boundary");
        check(PlaybackRules.SameSong(new string('A',64),new string('A',64)),"Identical song fingerprints");
        check(!PlaybackRules.SameSong("","")&&!PlaybackRules.SameSong(new string('A',64),new string('B',64)),"Missing/different files reject audio");

        // 30 minutes, two independent client clocks and sound-card clocks.
        // 60 FPS rendering, 250 ms correction checks, probes every five seconds.
        var estimates=new[]{new ClockEstimator(),new ClockEstimator()};
        double[] offsets={13,-9},clockRates={1.00015,.99985},audioRates={1.0003,.9997};
        double[] audio={0,0};int[] corrections={0,0};var random=new Random(1303);
        double maxPairError=0,maxClockError=0;
        for(int frame=0;frame<=1800*60;frame++)
        {
            double now=frame/60.0;
            for(int client=0;client<2;client++)
            {
                double Local(double t)=>offsets[client]+clockRates[client]*t;
                if(frame%300==0)
                {
                    double outbound=.03+random.NextDouble()*.02,inbound=.03+random.NextDouble()*.02;
                    estimates[client].Add(Local(now),1000+now+outbound,1000+now+outbound+.002,Local(now+outbound+.002+inbound));
                }
                double elapsed=Local(now)+estimates[client].Offset-1000;
                maxClockError=Math.Max(maxClockError,Math.Abs(elapsed-now));
                if(frame>0)audio[client]+=audioRates[client]/60;
                int expected=PlaybackRules.Sample(elapsed,rate,length);
                int actual=PlaybackRules.Sample(audio[client],rate,length);
                if(frame%15==0 && Math.Abs(PlaybackRules.ErrorSeconds(actual,expected,rate,length))>PlaybackRules.CorrectionThreshold)
                {audio[client]=elapsed;corrections[client]++;}
            }
            double pair=Math.Abs(PlaybackRules.ErrorSeconds(PlaybackRules.Sample(audio[0],rate,length),PlaybackRules.Sample(audio[1],rate,length),rate,length));
            maxPairError=Math.Max(maxPairError,pair);
        }
        Console.WriteLine($"Measured pair error {maxPairError*1000:F2} ms, clock error {maxClockError*1000:F2} ms");
        check(maxClockError<.02,"Clock estimate remains within 20 ms over 30 simulated minutes");
        check(maxPairError<=.1,"Independent audio positions remain within 100 ms over 30 simulated minutes");
        check(corrections[0]>0 && corrections[1]>0,"Drifting sound cards corrected on both clients");
        double delayed=PlaybackRules.ErrorSeconds(PlaybackRules.Sample(25,rate,length),PlaybackRules.Sample(30,rate,length),rate,length);
        check(Math.Abs(delayed)>.05,"Five-second injected error exceeds correction threshold");
        Console.WriteLine($"30-minute sync simulation: maximum pair error {maxPairError*1000:F2} ms, clock error {maxClockError*1000:F2} ms; corrections {corrections[0]}/{corrections[1]}.");
    }
}
