using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BardBox;

internal static class MusicClock
{
    private const string Ping=Catalog.Prefix+"ClockPing3",Pong=Catalog.Prefix+"ClockPong3";
    private static readonly Stopwatch timer=Stopwatch.StartNew();
    private static readonly Dictionary<int,double> pending=new Dictionary<int,double>();
    private static ClockEstimator estimator=new ClockEstimator();
    private static ZRoutedRpc? routed;
    private static bool server;
    private static int sequence,accepted,initial;
    private static double nextProbe,lastReply;
    internal static double Local=>1+timer.Elapsed.TotalSeconds;
    internal static string Epoch {get;private set;}="";
    internal static bool Ready=>routed!=null && (server || estimator.Ready);
    internal static double Now=>Local+(server?0:estimator.Offset);
    internal static double RoundTrip=>server?0:estimator.RoundTrip;

    internal static void Tick()
    {
        var current=ZRoutedRpc.instance;
        if(!ZNet.instance || current==null){if(routed!=null)Reset();return;}
        if(!ReferenceEquals(routed,current))
        {
            Reset();routed=current;server=ZNet.instance.IsServer();
            if(server)Epoch=Guid.NewGuid().ToString("N");
            current.Register<ZPackage>(Ping,ReceivePing);
            current.Register<ZPackage>(Pong,ReceivePong);
        }
        if(server || Local<nextProbe)return;
        var peer=ZNet.instance.GetServerPeer();
        if(peer==null || !peer.IsReady())return;
        // Lost replies must not accumulate while a connection is stalled.
        var expired=new List<int>();foreach(var item in pending)if(Local-item.Value>10)expired.Add(item.Key);
        foreach(int key in expired)pending.Remove(key);
        if(estimator.Ready && Local-lastReply>30){estimator=new ClockEstimator();initial=0;}
        int id=++sequence;double sent=Local;pending[id]=sent;
        var packet=new ZPackage();packet.Write(id);packet.Write(sent);
        current.InvokeRoutedRPC(peer.m_uid,Ping,packet);
        nextProbe=Local+(++initial<5?.2:5);
    }
    private static void ReceivePing(long sender,ZPackage packet)
    {
        if(!server || !ZNet.instance || ZNet.instance.GetPeer(sender)==null || routed==null)return;
        double received=Local;
        try
        {
            int id=packet.ReadInt();double sent=packet.ReadDouble();
            var reply=new ZPackage();reply.Write(id);reply.Write(sent);reply.Write(received);reply.Write(Epoch);reply.Write(Local);
            routed.InvokeRoutedRPC(sender,Pong,reply);
        }
        catch(Exception e){Plugin.Instance.Warn("Invalid music clock request: "+e.Message);}
    }
    private static void ReceivePong(long sender,ZPackage packet)
    {
        double received=Local;
        if(server || !ZNet.instance || ZNet.instance.GetServerPeer()?.m_uid!=sender)return;
        try
        {
            int id=packet.ReadInt();double sent=packet.ReadDouble(),remoteReceived=packet.ReadDouble();
            string epoch=packet.ReadString();double remoteSent=packet.ReadDouble();
            if(!pending.TryGetValue(id,out var expected) || sent!=expected)return;
            pending.Remove(id);
            if(id<=accepted || received-sent>10 || epoch.Length!=32)return;
            if(Epoch.Length>0 && Epoch!=epoch){estimator=new ClockEstimator();initial=0;}
            if(!estimator.Add(sent,remoteReceived,remoteSent,received))return;
            Epoch=epoch;accepted=id;lastReply=received;
            Plugin.Instance.Trace("Clock offset="+estimator.Offset.ToString("F6")+" RTT="+RoundTrip.ToString("F6"));
        }
        catch(Exception e){Plugin.Instance.Warn("Invalid music clock response: "+e.Message);}
    }
    internal static void Reset()
    {
        routed=null;server=false;Epoch="";pending.Clear();estimator=new ClockEstimator();
        sequence=accepted=initial=0;nextProbe=lastReply=0;
    }
}
