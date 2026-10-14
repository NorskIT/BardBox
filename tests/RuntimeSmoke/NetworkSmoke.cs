using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

// Test-only native TCP backend; listener is bound exclusively to loopback.
[BepInPlugin("norskit_bardbox_network_smoke","BardBox isolated network smoke","0.1.0")]
[BepInDependency(BardBox.Plugin.Id)]
public sealed class NetworkSmoke : BaseUnityPlugin
{
    private string role="",folder="",coord="";
    private TcpListener? listener;
    private bool playing,controls;
    private Vector3? fixedPosition;
    private void Awake()
    {
        role=Environment.GetEnvironmentVariable("BARD_NETWORK_ROLE")??"";
        folder=Environment.GetEnvironmentVariable("BARD_SMOKE_OUTPUT")??"";
        coord=Environment.GetEnvironmentVariable("BARD_NETWORK_COORD")??"";
        if(role.Length>0)new Harmony("norskit_network_smoke_focus").Patch(AccessTools.PropertyGetter(typeof(Application),nameof(Application.isFocused)),prefix:new HarmonyMethod(typeof(NetworkSmoke),nameof(Focus)));
        if(role=="client")new Harmony("norskit_network_smoke_connector").Patch(AccessTools.Method(typeof(ZNet),"ClientConnect"),prefix:new HarmonyMethod(typeof(NetworkSmoke),nameof(ConnectLoopback)));
    }
    private static bool Focus(ref bool __result){__result=true;return false;}
    private static bool ConnectLoopback(ZNet __instance)
    {
        string coordination=Environment.GetEnvironmentVariable("BARD_NETWORK_COORD")!;
        var tcp=ZSocket2.CreateSocket();tcp.Connect(IPAddress.Loopback,int.Parse(File.ReadAllText(Path.Combine(coordination,"port.txt"))));
        AccessTools.Method(typeof(ZNet),"Connect",new[]{typeof(ISocket)}).Invoke(__instance,new object[]{new ZSocket2(tcp,"127.0.0.1")});
        return false;
    }
    private void Update()
    {
        if(listener!=null && listener.Pending())
        {
            var socket=new ZSocket2(listener.AcceptTcpClient(),"127.0.0.1");
            AccessTools.Method(typeof(ZNet),"OnNewConnection").Invoke(ZNet.instance,new object[]{new ZNetPeer(socket,false)});
        }
        if(fixedPosition.HasValue && Player.m_localPlayer)
        {
            var p=Player.m_localPlayer;p.transform.position=fixedPosition.Value;
            var rb=p.GetComponent<Rigidbody>();rb.position=fixedPosition.Value;rb.linearVelocity=Vector3.zero;rb.useGravity=false;
        }
    }
    // Native PlayerController submits controls in FixedUpdate, before gameplay Update.
    private void FixedUpdate()
    {
        if(controls && Player.m_localPlayer)
            Player.m_localPlayer.SetControls(Vector3.zero,false,playing,false,false,false,false,false,false,false,false,false);
    }
    private IEnumerator Start()
    {
        if(role.Length==0)yield break;
        yield return new WaitForSecondsRealtime(25);
        var cases=Run();
        while(true)
        {
            bool more;
            try{more=cases.MoveNext();}
            catch(Exception e){File.WriteAllText(Path.Combine(folder,"network-result.txt"),"FAIL\n"+e);Logger.LogError(e);break;}
            if(!more){File.WriteAllText(Path.Combine(folder,"network-result.txt"),"PASS "+role+"\nNative ZSocket2 / ZNet / ZRoutedRpc / ZDO playback integration.");break;}
            yield return cases.Current;
        }
        listener?.Stop();Application.Quit();
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static Type MusicType=>typeof(BardBox.Plugin).Assembly.GetType("BardBox.Musician")!;
    private static Type ClockType=>typeof(BardBox.Plugin).Assembly.GetType("BardBox.MusicClock")!;
    private static double Now=>(double)AccessTools.Property(ClockType,"Now").GetValue(null);
    private IEnumerator Run()
    {
        bool host=role=="host";
        var plugin=AccessTools.Field(typeof(BardBox.Plugin),"Instance").GetValue(null);
        var serverVolume=(BepInEx.Configuration.ConfigEntry<float>)AccessTools.Field(typeof(BardBox.Plugin),"Volume").GetValue(plugin);
        var personalVolume=(BepInEx.Configuration.ConfigEntry<float>)AccessTools.Field(typeof(BardBox.Plugin),"ListenerVolume").GetValue(plugin);
        var radius=(BepInEx.Configuration.ConfigEntry<float>)AccessTools.Field(typeof(BardBox.Plugin),"AudioRange").GetValue(plugin);
        var sync=(ServerSync.ConfigSync)AccessTools.Field(typeof(BardBox.Plugin),"Sync").GetValue(plugin);
        ((BepInEx.Configuration.ConfigEntry<bool>)AccessTools.Field(typeof(BardBox.Plugin),"SyncDiagnostics").GetValue(plugin)).Value=true;
        serverVolume.Value=host?5:.2f;personalVolume.Value=1;radius.Value=host?500:20;
        var profile=new PlayerProfile("bardbox_network_"+role,FileHelpers.FileSource.Local);
        profile.SetName("BardBox "+role);profile.m_firstSpawn=false;profile.Save();Game.SetProfile(profile.GetFilename(),profile.m_fileSource);
        ZNet.m_onlineBackend=OnlineBackendType.CustomSocket;
        if(host){ZNet.SetServer(true,false,false,"BardBox network regression","",new World("BardBoxNetwork","BardBoxNetwork"));ZNet.ResetServerHost();}
        else
        {
            double wait=Time.realtimeSinceStartup+180;
            while(!File.Exists(Path.Combine(coord,"port.txt"))){Check(Time.realtimeSinceStartup<wait,"Host listener timeout");yield return new WaitForSecondsRealtime(.2f);}
            ZNet.SetServer(false,false,false,"","",null);
            ZNet.SetServerHost("127.0.0.1",int.Parse(File.ReadAllText(Path.Combine(coord,"port.txt"))),OnlineBackendType.CustomSocket);
        }
        AccessTools.Method(typeof(FejdStartup),"LoadMainScene").Invoke(FejdStartup.instance,null);
        double deadline=Time.realtimeSinceStartup+180;
        while(!Player.m_localPlayer || Player.m_localPlayer.InCutscene())
        {Check(Time.realtimeSinceStartup<deadline,"Network world timeout "+ZNet.GetConnectionStatus());yield return new WaitForSecondsRealtime(.2f);}
        yield return new WaitForSecondsRealtime(2);
        while(Player.m_localPlayer.InCutscene())
        {Check(Time.realtimeSinceStartup<deadline,"Spawn cutscene timeout");yield return new WaitForSecondsRealtime(.2f);}
        var player=Player.m_localPlayer;player.SetGodMode(true);player.GetComponent<PlayerController>().enabled=false;controls=true;
        if(host)
        {
            listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            File.WriteAllText(Path.Combine(coord,"port.txt"),((IPEndPoint)listener.LocalEndpoint).Port.ToString());
            foreach(var name in new[]{"NorskIT_BardBox_Boombox","NorskIT_BardBox_trailblazer"})
            {
                var prefab=PrefabManager.Instance.GetPrefab(name);var item=prefab.GetComponent<ItemDrop>().m_itemData.Clone();item.m_dropPrefab=prefab;
                player.GetInventory().GetAllItems().Add(item);Check(player.EquipItem(item,false),"Equip "+name);
            }
            playing=true; // Start before the listener joins, exercising late-join seeking.
            deadline=Time.realtimeSinceStartup+180;
            while(!File.Exists(Path.Combine(coord,"client-ready.txt"))){Check(Time.realtimeSinceStartup<deadline,"Client readiness timeout");yield return new WaitForSecondsRealtime(.2f);}
            playing=true;
        }
        else
        {
            deadline=Time.realtimeSinceStartup+30;
            while(!(bool)AccessTools.Property(ClockType,"Ready").GetValue(null)){Check(Time.realtimeSinceStartup<deadline,"Client clock synchronization timeout");yield return new WaitForSecondsRealtime(.1f);}
            while(!sync.InitialSyncDone){Check(Time.realtimeSinceStartup<deadline,"ServerSync initial configuration timeout");yield return new WaitForSecondsRealtime(.1f);}
            Check(serverVolume.Value==5,"Server volume did not replace local 20% with 500%");
            Check(sync.IsLocked && !sync.IsAdmin,"Client must be locked non-admin");
            Check(radius.Value==500,"Server radius not synchronized");radius.Value=10;Check(radius.Value==500,"Non-admin changed server radius");
            serverVolume.Value=.1f;
            Check(serverVolume.Value==5,"Non-admin changed server volume");
            File.WriteAllText(Path.Combine(coord,"client-ready.txt"),"ready");
        }
        Component? musician=null;AudioSource? audio=null;
        deadline=Time.realtimeSinceStartup+30;
        while(!audio || !audio.isPlaying)
        {
            var sourcePlayer=Player.GetAllPlayers().FirstOrDefault(p=>host?p==player:p!=player);
            if(sourcePlayer){musician=sourcePlayer.GetComponent(MusicType);if(musician)audio=(AudioSource)AccessTools.Field(MusicType,"source").GetValue(musician);}
            if(Time.realtimeSinceStartup>=deadline)
            {
                var mine=player.GetComponent(MusicType);
                var registry=(System.Collections.IEnumerable)AccessTools.Property(typeof(BardBox.Plugin).Assembly.GetType("BardBox.RadioNetwork"),"States").GetValue(null);
                Logger.LogError("RADIO DIAGNOSTIC role="+role+" input="+playing+" active="+AccessTools.Property(MusicType,"ActiveTape").GetValue(mine)+" blocked="+AccessTools.Method(MusicType,"Blocked").Invoke(mine,null)+" states="+registry.Cast<object>().Count()+" audio="+audio+" ownId="+player.GetZDOID()+" registeredId="+ZNet.instance.LocalPlayerCharacterID);
            }
            Check(Time.realtimeSinceStartup<deadline,"Replicated music did not start; players="+Player.GetAllPlayers().Count);
            yield return new WaitForSecondsRealtime(.1f);
        }
        Check(musician!=null && audio!=null,"Missing playback");
        if(!host)Check(audio!.time>3,"Late join restarted the song");
        var actual=audio!;var component=musician!;
        var amplifier=actual.GetComponent(typeof(BardBox.Plugin).Assembly.GetType("BardBox.MusicAmplifier"));
        Check(amplifier!=null && (float)AccessTools.Field(amplifier.GetType(),"Gain").GetValue(amplifier)==5,"Emitter did not apply synchronized 500% gain");
        var pcm=new[]{.1f,-.1f};AccessTools.Method(amplifier.GetType(),"OnAudioFilterRead").Invoke(amplifier,new object[]{pcm,1});
        Check(pcm[0]==.5f && pcm[1]==-.5f,"Emitter PCM is not amplified fivefold");
        if(!host)File.WriteAllText(Path.Combine(coord,"client-gain-checked.txt"),"ready");
        else
        {
            deadline=Time.realtimeSinceStartup+30;
            while(!File.Exists(Path.Combine(coord,"client-gain-checked.txt"))){Check(Time.realtimeSinceStartup<deadline,"Client gain check timeout");yield return new WaitForSecondsRealtime(.1f);}
            serverVolume.Value=3;
        }
        deadline=Time.realtimeSinceStartup+15;
        while(serverVolume.Value!=3 || (float)AccessTools.Field(amplifier.GetType(),"Gain").GetValue(amplifier)!=3)
        {Check(Time.realtimeSinceStartup<deadline,"Live server volume update timeout");yield return new WaitForSecondsRealtime(.1f);}
        File.WriteAllText(Path.Combine(folder,"serversync-result.txt"),"PASS "+role+": 500% initial sync, locked client config, emitter PCM gain 5x, live update to 300%.");
        double maxError=0;int count=0;
        using(var writer=new StreamWriter(Path.Combine(folder,"network-sync.csv")))
        {
            writer.WriteLine("clock,start,actual,expected,error,epoch,session");
            for(int i=0;i<150;i++)
            {
                double now=Now,start=(long)AccessTools.Field(MusicType,"started").GetValue(component)/(double)TimeSpan.TicksPerSecond;
                double expected=(now-start)%actual.clip.length,error=actual.time-expected;
                error-=Math.Round(error/actual.clip.length)*actual.clip.length;
                maxError=Math.Max(maxError,Math.Abs(error));count++;
                writer.WriteLine(FormattableString.Invariant($"{now:F6},{start:F6},{actual.time:F6},{expected:F6},{error:F6},{AccessTools.Field(MusicType,"epoch").GetValue(component)},{AccessTools.Field(MusicType,"session").GetValue(component)}"));
                Check(actual.isPlaying,"Playback stopped during network sampling");
                yield return new WaitForSecondsRealtime(.1f);
            }
        }
        Check(maxError<.1,"Network audio error exceeds 100 ms: "+maxError);
        Logger.LogInfo("NETWORK "+role+": "+count+" samples, maximum timeline error="+maxError);
        if(host)
        {
            File.WriteAllText(Path.Combine(coord,"host-position.txt"),FormattableString.Invariant($"{player.transform.position.x},{player.transform.position.y},{player.transform.position.z}"));
            deadline=Time.realtimeSinceStartup+180;
            while(!File.Exists(Path.Combine(coord,"range-500-done.txt"))){Check(Time.realtimeSinceStartup<deadline,"500m range test timeout");yield return new WaitForSecondsRealtime(.2f);}
            radius.Value=2000;
            deadline=Time.realtimeSinceStartup+180;
            while(!File.Exists(Path.Combine(coord,"range-2000-done.txt"))){Check(Time.realtimeSinceStartup<deadline,"2000m range test timeout");yield return new WaitForSecondsRealtime(.2f);}
            radius.Value=500;
            deadline=Time.realtimeSinceStartup+30;
            while(!File.Exists(Path.Combine(coord,"range-shrink-done.txt"))){Check(Time.realtimeSinceStartup<deadline,"Range shrink timeout");yield return new WaitForSecondsRealtime(.1f);}
        }
        else
        {
            var origin=player.transform.position;
            deadline=Time.realtimeSinceStartup+30;
            while(!File.Exists(Path.Combine(coord,"host-position.txt"))){Check(Time.realtimeSinceStartup<deadline,"Host position timeout");yield return new WaitForSecondsRealtime(.1f);}
            var xyz=File.ReadAllText(Path.Combine(coord,"host-position.txt")).Split(',').Select(x=>float.Parse(x,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var hostPosition=new Vector3(xyz[0],xyz[1],xyz[2]);
            var playback=typeof(BardBox.Plugin).Assembly.GetType("BardBox.RadioPlayback");
            foreach(int range in new[]{500,2000})
            {
                deadline=Time.realtimeSinceStartup+30;
                while(radius.Value!=range){Check(Time.realtimeSinceStartup<deadline,"Server radius update missing");yield return new WaitForSecondsRealtime(.1f);}
                fixedPosition=hostPosition+Vector3.right*(range-10);
                deadline=Time.realtimeSinceStartup+45;
                while(Player.GetAllPlayers().Any(p=>p!=player))
                {Check(Time.realtimeSinceStartup<deadline,"Remote player still loaded at "+range+"m");yield return new WaitForSecondsRealtime(.5f);}
                yield return new WaitForSecondsRealtime(4);
                Check(actual.isPlaying,"Unloaded musician lost audio at "+range+"m");
                Check(!player.GetSEMan().HaveStatusEffect("NorskIT_BardBox_SE_trailblazer".GetStableHashCode()),"Buff escaped 20m radius");
                Check(actual.volume>0 && actual.volume<.1f,"Distance attenuation missing");
                Check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a=>a.name=="BardBox radio audio" && a.isPlaying)==1,"Duplicate radio emitter");
                fixedPosition=hostPosition+Vector3.right*(range+10);
                yield return new WaitForSecondsRealtime(.5f);Check(!actual.isPlaying,"Audio outside radius "+range);
                fixedPosition=hostPosition+Vector3.right*(range-10);
                yield return new WaitForSecondsRealtime(.5f);Check(actual.isPlaying && actual.time>10,"Range reentry restarted song");
                File.WriteAllText(Path.Combine(coord,"range-"+range+"-done.txt"),"PASS: absent player, attenuated audio, exact radius cutoff/reentry, no distant buff");
            }
            deadline=Time.realtimeSinceStartup+30;
            while(radius.Value!=500){Check(Time.realtimeSinceStartup<deadline,"Shrink not synced");yield return new WaitForSecondsRealtime(.1f);}
            yield return new WaitForSecondsRealtime(.5f);Check(!actual.isPlaying,"Shrinking radius left distant sound active");
            File.WriteAllText(Path.Combine(coord,"range-shrink-done.txt"),"PASS");
            fixedPosition=hostPosition;
            deadline=Time.realtimeSinceStartup+30;
            while(!actual.isPlaying || !player.GetSEMan().HaveStatusEffect("NorskIT_BardBox_SE_trailblazer".GetStableHashCode()))
            {Check(Time.realtimeSinceStartup<deadline,"Return to musician timeout");yield return new WaitForSecondsRealtime(.5f);}
            File.WriteAllText(Path.Combine(folder,"long-range-result.txt"),"PASS: 500/2000m sound without loaded musician; bounds, attenuation, live radius, one emitter, buffs stay at 20m.");
        }
        if(!host)
        {
            Check(player.GetSEMan().HaveStatusEffect("NorskIT_BardBox_SE_trailblazer".GetStableHashCode()),"Remote music buff missing");
            // A genuine receiver can mute/reenter and must catch up, not restart.
            var volume=personalVolume;
            volume.Value=0;yield return new WaitForSecondsRealtime(.6f);Check(!actual.isPlaying,"Mute failed");
            volume.Value=.5f;yield return new WaitForSecondsRealtime(.4f);
            Check(actual.isPlaying && actual.time>5,"Unmute restarted remote song");
            File.WriteAllText(Path.Combine(coord,"client-done.txt"),"done");
            deadline=Time.realtimeSinceStartup+10;
            while(actual.isPlaying){Check(Time.realtimeSinceStartup<deadline,"Remote release did not stop audio");yield return new WaitForSecondsRealtime(.1f);}
        }
        else
        {
            deadline=Time.realtimeSinceStartup+30;
            while(!File.Exists(Path.Combine(coord,"client-done.txt"))){Check(Time.realtimeSinceStartup<deadline,"Client completion timeout");yield return new WaitForSecondsRealtime(.1f);}
            playing=false;yield return new WaitForSecondsRealtime(3);
        }
        // Reverse direction: exercise a remote owner's publish RPC through the server.
        if(!host)
        {
            foreach(var name in new[]{"NorskIT_BardBox_Boombox","NorskIT_BardBox_trailblazer"})
            {
                var prefab=PrefabManager.Instance.GetPrefab(name);var item=prefab.GetComponent<ItemDrop>().m_itemData.Clone();item.m_dropPrefab=prefab;
                player.GetInventory().GetAllItems().Add(item);Check(player.EquipItem(item,false),"Reverse equip "+name);
            }
            playing=false;yield return new WaitForSecondsRealtime(.5f);playing=true;
            deadline=Time.realtimeSinceStartup+30;
            while(!File.Exists(Path.Combine(coord,"reverse-heard.txt"))){Check(Time.realtimeSinceStartup<deadline,"Server did not hear client-owned radio");yield return new WaitForSecondsRealtime(.1f);}
            playing=false;yield return new WaitForSecondsRealtime(3);
        }
        else
        {
            AudioSource? reverse=null;deadline=Time.realtimeSinceStartup+30;
            while(!reverse || !reverse.isPlaying)
            {
                var other=Player.GetAllPlayers().FirstOrDefault(p=>p!=player);
                if(other)reverse=(AudioSource)AccessTools.Method(typeof(BardBox.Plugin).Assembly.GetType("BardBox.RadioPlayback"),"Source").Invoke(null,new object[]{other.GetZDOID()});
                Check(Time.realtimeSinceStartup<deadline,"Client publish relay timeout");yield return new WaitForSecondsRealtime(.1f);
            }
            yield return new WaitForSecondsRealtime(1);
            Check(reverse.isPlaying && player.GetSEMan().HaveStatusEffect("NorskIT_BardBox_SE_trailblazer".GetStableHashCode()),"Client-owned music/buff absent on host");
            File.WriteAllText(Path.Combine(coord,"reverse-heard.txt"),"PASS");deadline=Time.realtimeSinceStartup+10;
            while(reverse.isPlaying){Check(Time.realtimeSinceStartup<deadline,"Client stop did not relay");yield return new WaitForSecondsRealtime(.1f);}
            File.WriteAllText(Path.Combine(folder,"reverse-relay-result.txt"),"PASS: remote client publishes, host receives audio/buff, remote release stops host emitter.");
        }
    }
}
