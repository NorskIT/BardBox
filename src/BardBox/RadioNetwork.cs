using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BardBox;

internal sealed class RadioState
{
    internal ZDOID Id;
    internal long Owner;
    internal string Tape="",Session="",Hash="";
    internal int Quality;
    internal double Start,FreshAt;
    internal Vector3 Position;
}

// Explicit server relay: audio metadata does not depend on scene/ZDO interest on receivers.
internal static class RadioNetwork
{
    private const string PublishRpc="NorskIT_BardBox_RadioPublish5",SnapshotRpc="NorskIT_BardBox_RadioSnapshot5";
    private static ZRoutedRpc? router;
    private static bool server;
    private static long sequence,snapshotSequence,receivedSequence;
    private static double nextSnapshot;
    private static readonly Dictionary<long,long> received=new();
    private static readonly Dictionary<long,ZNetPeer> connections=new();
    private static readonly Dictionary<ZDOID,RadioState> states=new();
    internal static IEnumerable<RadioState> States=>states.Values;
    internal static void Tick()
    {
        if(!ZNet.instance || ZRoutedRpc.instance==null){Reset();return;}
        if(!ReferenceEquals(router,ZRoutedRpc.instance))
        {
            Reset();router=ZRoutedRpc.instance;server=ZNet.instance.IsServer();
            router.Register<ZPackage>(PublishRpc,ReceivePublish);router.Register<ZPackage>(SnapshotRpc,ReceiveSnapshot);
        }
        if(!MusicClock.Ready)return;
        bool changed=false;
        foreach(var pair in states.ToArray())
        {
            var s=pair.Value;
            bool expired=!RadioRules.Fresh(MusicClock.Now,s.FreshAt);
            if(server)
            {
                var zdo=ZDOMan.instance?.GetZDO(s.Id);
                bool gone=s.Owner!=ZNet.GetUID() && ZNet.instance.GetPeer(s.Owner)?.IsReady()!=true;
                if(zdo==null || gone || zdo.GetBool(ZDOVars.s_dead))expired=true;
                else {s.Position=zdo.GetPosition()+Vector3.up*2;if(!Finite(s.Position))expired=true;}
            }
            if(expired){states.Remove(pair.Key);changed=true;}
        }
        if(server)
        {
            foreach(var peer in received.Keys.ToArray())if(peer!=ZNet.GetUID() && ZNet.instance.GetPeer(peer)==null){received.Remove(peer);connections.Remove(peer);}
            if(changed || MusicClock.Local>=nextSnapshot){Broadcast();nextSnapshot=MusicClock.Local+1;}
        }
    }
    internal static void Publish(Player player,RadioState? state)
    {
        if(!ZNet.instance || router==null || !MusicClock.Ready || !player)return;
        var p=new ZPackage();p.Write(MusicClock.Epoch);p.Write(++sequence);p.Write(state!=null);
        if(state!=null){p.Write(state.Tape);p.Write(state.Quality);p.Write(state.Session);p.Write(state.Hash);p.Write(state.Start);}
        if(server){p.SetPos(0);ReceivePublish(ZNet.GetUID(),p);}
        else if(ZNet.instance.GetServerPeer() is { } peer)router.InvokeRoutedRPC(peer.m_uid,PublishRpc,p);
    }
    private static void ReceivePublish(long sender,ZPackage p)
    {
        if(!server || !MusicClock.Ready)return;
        var peer=sender==ZNet.GetUID()?null:ZNet.instance.GetPeer(sender);
        if(sender!=ZNet.GetUID() && peer?.IsReady()!=true)return;
        try
        {
            if(p.Size()>1024 || p.ReadString()!=MusicClock.Epoch)return;
            if(peer!=null){if(!connections.TryGetValue(sender,out var old) || !ReferenceEquals(old,peer))received.Remove(sender);connections[sender]=peer;}
            long serial=p.ReadLong();received.TryGetValue(sender,out var prior);
            if(!RadioRules.NewSequence(serial,prior))return;
            bool active=p.ReadBool();
            var id=peer!=null?peer.m_characterID:ZNet.instance.LocalPlayerCharacterID;
            RadioState? s=null;
            if(active)
            {
                s=new RadioState{Id=id,Owner=sender,Tape=p.ReadString(),Quality=p.ReadInt(),Session=p.ReadString(),Hash=p.ReadString(),Start=p.ReadDouble(),FreshAt=MusicClock.Now};
                if(!RadioRules.Valid(s.Tape,s.Quality,s.Session,s.Hash,s.Start,MusicClock.Now)){Plugin.Instance.Trace("Radio invalid metadata "+s.Tape+" start="+s.Start+" now="+MusicClock.Now);return;}
                var zdo=ZDOMan.instance?.GetZDO(id);
                if(zdo==null || zdo.GetBool(ZDOVars.s_dead) || zdo.GetInt(ZDOVars.s_rightItem)!=Catalog.Box.GetStableHashCode()){Plugin.Instance.Trace("Radio invalid character "+id+" zdo="+(zdo!=null)+" right="+zdo?.GetInt(ZDOVars.s_rightItem));return;}
                s.Position=zdo.GetPosition()+Vector3.up*2;
                if(!Finite(s.Position))return;
            }
            received[sender]=serial;
            // A respawn replaces the identity; keep at most one active entry per peer.
            bool changed=false;
            foreach(var item in states.Where(x=>x.Value.Owner==sender).ToArray()){states.Remove(item.Key);changed=true;}
            if(s!=null){states[id]=s;changed=true;}
            if(changed){Plugin.Instance.Trace("Radio accepted sender="+sender+" states="+states.Count);Broadcast();}
        }
        catch(Exception e){Plugin.Instance.Trace("Rejected radio publish: "+e.Message);}
    }
    private static bool Finite(Vector3 p)=>RadioRules.Finite(p.x)&&RadioRules.Finite(p.y)&&RadioRules.Finite(p.z);
    private static void Broadcast()
    {
        if(router==null || !MusicClock.Ready)return;
        var p=new ZPackage();p.Write(MusicClock.Epoch);p.Write(++snapshotSequence);p.Write(states.Count);
        foreach(var s in states.Values)
        {p.Write(s.Id);p.Write(s.Tape);p.Write(s.Quality);p.Write(s.Session);p.Write(s.Hash);p.Write(s.Start);p.Write(s.FreshAt);p.Write(s.Position);}
        foreach(var peer in ZNet.instance.GetPeers())if(peer.IsReady())router.InvokeRoutedRPC(peer.m_uid,SnapshotRpc,p);
    }
    private static void ReceiveSnapshot(long sender,ZPackage p)
    {
        if(server || !MusicClock.Ready || ZNet.instance.GetServerPeer()?.m_uid!=sender)return;
        try
        {
            if(p.Size()>256*1024 || p.ReadString()!=MusicClock.Epoch)return;
            long serial=p.ReadLong();if(!RadioRules.NewSequence(serial,receivedSequence))return;
            int count=p.ReadInt();if(count<0 || count>256)return;
            var next=new Dictionary<ZDOID,RadioState>();
            for(int i=0;i<count;i++)
            {
                var s=new RadioState{Id=p.ReadZDOID(),Tape=p.ReadString(),Quality=p.ReadInt(),Session=p.ReadString(),Hash=p.ReadString(),Start=p.ReadDouble(),FreshAt=p.ReadDouble(),Position=p.ReadVector3()};
                if(!RadioRules.Valid(s.Tape,s.Quality,s.Session,s.Hash,s.Start,MusicClock.Now) || !Finite(s.Position))return;
                if(RadioRules.Fresh(MusicClock.Now,s.FreshAt))next.Add(s.Id,s);
            }
            states.Clear();foreach(var pair in next)states.Add(pair.Key,pair.Value);receivedSequence=serial;Plugin.Instance.Trace("Radio snapshot entries="+states.Count);
        }
        catch(Exception e){Plugin.Instance.Trace("Rejected radio snapshot: "+e.Message);}
    }
    internal static void Reset(){router=null;states.Clear();received.Clear();connections.Clear();sequence=snapshotSequence=receivedSequence=0;nextSnapshot=0;}
}
