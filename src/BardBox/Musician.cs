using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace BardBox;

internal sealed class Musician : MonoBehaviour
{
    private const string ActiveKey=Catalog.Prefix+"active",TapeKey=Catalog.Prefix+"tape",QualityKey=Catalog.Prefix+"quality",StartKey=Catalog.Prefix+"start",HeartbeatKey=Catalog.Prefix+"heartbeat";
    private static readonly AccessTools.FieldRef<Humanoid,ItemDrop.ItemData> Right=AccessTools.FieldRefAccess<Humanoid,ItemDrop.ItemData>("m_rightItem");
    private Player player=null!;
    private ZNetView view=null!;
    private readonly PlaybackInput controls=new PlaybackInput();
    private float lastInput=-100,nextPublish,nextBuff;
    private long started;
    private string audioId="";
    private long audioStart;
    private const string EpochKey=Catalog.Prefix+"epoch3",SessionKey=Catalog.Prefix+"session3",HashKey=Catalog.Prefix+"hash3";
    private string epoch="",session="",songHash="",warnedSession="";
    private double localStarted,nextSync;
    private bool mediaMatches;
    private bool LocalOwner=>view && view.IsOwner() && player==Player.m_localPlayer;
    private double Elapsed=>Math.Max(0,LocalOwner && (started==0 || !MusicClock.Ready)?MusicClock.Local-localStarted:Now-started/(double)TimeSpan.TicksPerSecond);
    private readonly CueClock cueClock=new CueClock();
    private readonly Shockwave shockwave=new Shockwave();
    private AudioSource? source;
    private MusicAmplifier? amplifier;
    private GameObject? emitter;
    private readonly Contributions contributions=new Contributions();
    internal Tape? ActiveTape {get;private set;}
    internal int Quality {get;private set;}
    internal Player Player=>player;
    internal static double Now=>MusicClock.Now;
    private static long Ticks=>(long)(Now*TimeSpan.TicksPerSecond);
    internal static bool HasBox(Player p)=>Right(p)?.m_dropPrefab?.name==Catalog.Box;
    private void Awake(){player=GetComponent<Player>();view=GetComponent<ZNetView>();gameObject.AddComponent<CarryPose>();}
    internal void Input(bool hold,bool interrupted)
    {
        lastInput=Time.unscaledTime;
        controls.Update(hold,interrupted || Blocked(),Plugin.Instance.TogglePlayback.Value);
    }
    private bool Blocked()=>!Application.isFocused || !HasBox(player) || player.IsDead() || player.IsTeleporting() || player.InCutscene() || player.IsStaggering() || player.InDodge() || player.IsSwimming() || player.IsAttached() ||
        InventoryGui.IsVisible() || Menu.IsVisible() || Console.IsVisible() || TextInput.IsVisible() || StoreGui.IsVisible() || (Chat.instance && Chat.instance.HasFocus());
    private void Update()
    {
        if(!Plugin.Ready || !view || !view.IsValid())return;
        if(view.IsOwner() && player==Player.m_localPlayer)UpdateLocal();
        else ReadState();
        mediaMatches=CheckMedia();
        UpdateSound();
        UpdateShockwave();
        if(view.IsOwner() && player==Player.m_localPlayer && Time.unscaledTime>=nextBuff)
        {nextBuff=Time.unscaledTime+.25f;UpdateBuffs();}
    }
    private void UpdateLocal()
    {
        if(Blocked() || Time.unscaledTime-lastInput>.3f)
        {if(controls.Playing || ActiveTape!=null)controls.Interrupt();}
        var ammo=player.GetAmmoItem();
        Tape? selected=ammo?.m_dropPrefab?Catalog.Find(ammo.m_dropPrefab.name):null;
        if(ammo==null || !player.GetInventory().ContainsItem(ammo))selected=null;
        if(ActiveTape!=null && (selected!=ActiveTape || ammo!.m_quality!=Quality))controls.Interrupt();
        if(controls.Playing && selected==null)
        {player.Message(MessageHud.MessageType.Center,"Equip a BardBox tape first.");controls.Interrupt();}
        var next=controls.Playing?selected:null;
        bool changed=next!=ActiveTape;
        if(changed)
        {
            ActiveTape=next;Quality=next!=null?Mathf.Clamp(ammo!.m_quality,1,3):0;
            localStarted=MusicClock.Local;started=0;epoch="";
            session=next!=null?Guid.NewGuid().ToString("N"):"";
            songHash=next!=null?AudioLibrary.Hash(next.Id):"";
            cueClock.Reset();shockwave.Hide();
        }
        if(ActiveTape!=null && MusicClock.Ready && (started==0 || epoch!=MusicClock.Epoch))
        {
            started=(long)((Now-(MusicClock.Local-localStarted))*TimeSpan.TicksPerSecond);
            epoch=MusicClock.Epoch;changed=true;
        }
        if(changed || Time.unscaledTime>=nextPublish)
        {
            nextPublish=Time.unscaledTime+1;
            var zdo=view.GetZDO();
            zdo.Set(TapeKey,ActiveTape?.Id??"");zdo.Set(QualityKey,Quality);zdo.Set(StartKey,started);zdo.Set(HeartbeatKey,Ticks);zdo.Set(EpochKey,epoch);zdo.Set(SessionKey,session);zdo.Set(HashKey,songHash);zdo.Set(ActiveKey,ActiveTape!=null && started>0 && MusicClock.Ready);
        }
    }
    private void ReadState()
    {
        var zdo=view.GetZDO();
        var age=(Ticks-zdo.GetLong(HeartbeatKey,0))/(double)TimeSpan.TicksPerSecond;
        var tape=Catalog.Find(zdo.GetString(TapeKey,""));int quality=zdo.GetInt(QualityKey,0);
        started=zdo.GetLong(StartKey,0);epoch=zdo.GetString(EpochKey,"");session=zdo.GetString(SessionKey,"");songHash=zdo.GetString(HashKey,"");
        // Remote inventories are not replicated. Use the game's replicated hand item,
        // exactly as VisEquipment does, rather than the local Humanoid inventory field.
        bool holding=zdo.GetInt(ZDOVars.s_rightItem,0)==Catalog.Box.GetStableHashCode();
        ActiveTape=MusicClock.Ready && epoch==MusicClock.Epoch && session.Length==32 && zdo.GetBool(ActiveKey,false) && PlaybackRules.Valid(tape?.Id??"",quality,started,age,holding,player.IsDead())?tape:null;
        Quality=ActiveTape!=null?quality:0;
    }
    private bool CheckMedia()
    {
        if(ActiveTape==null)return false;
        if(LocalOwner)return true;
        bool match=PlaybackRules.SameSong(songHash,AudioLibrary.Hash(ActiveTape.Id));
        if(!match && warnedSession!=session)
        {
            warnedSession=session;
            string message="BardBox: song files differ for "+ActiveTape.Name+". Install the same song package as the musician.";
            Plugin.Instance.Warn(message);if(Player.m_localPlayer)Player.m_localPlayer.Message(MessageHud.MessageType.Center,message);
        }
        return match;
    }
    private void UpdateSound()
    {
        var local=Player.m_localPlayer;
        var distance=local?Vector3.Distance(player.transform.position,local.transform.position):float.PositiveInfinity;
        var clip=ActiveTape!=null?AudioLibrary.Get(ActiveTape.Id):null;
        if(!mediaMatches || !clip || distance>=20 || Plugin.Instance.Volume.Value<=0 || Plugin.Instance.ListenerVolume.Value<=0)
        {if(source)source.Stop();audioId="";return;}
        if(!source)
        {
            emitter=new GameObject("BardBox audio");source=emitter.AddComponent<AudioSource>();
            amplifier=emitter.AddComponent<MusicAmplifier>();
            source.playOnAwake=false;source.loop=true;source.spatialBlend=1;source.dopplerLevel=0;
            source.minDistance=3;source.maxDistance=20;source.rolloffMode=AudioRolloffMode.Linear;
            source.outputAudioMixerGroup=Resources.FindObjectsOfTypeAll<AudioMixerGroup>().FirstOrDefault(g=>g.name=="SFX");
        }
        emitter!.transform.position=player.transform.position+Vector3.up*2;
        source!.volume=Mathf.Min(1,Plugin.Instance.Volume.Value)*Plugin.Instance.ListenerVolume.Value;
        amplifier!.Gain=Mathf.Max(1,Plugin.Instance.Volume.Value);
        if(audioId!=ActiveTape!.Id || audioStart!=started || !source.isPlaying)
        {
            source.Stop();source.clip=clip;audioId=ActiveTape.Id;audioStart=started;
            double elapsed=Elapsed;
            source.timeSamples=PlaybackRules.Sample(elapsed,clip.frequency,clip.samples);source.Play();
            nextSync=MusicClock.Local+.25;
        }
        else if(MusicClock.Local>=nextSync)
        {
            nextSync=MusicClock.Local+.25;
            int expected=PlaybackRules.Sample(Elapsed,clip.frequency,clip.samples),actual=source.timeSamples;
            double error=PlaybackRules.ErrorSeconds(actual,expected,clip.frequency,clip.samples);
            if(Math.Abs(error)>PlaybackRules.CorrectionThreshold){source.timeSamples=expected;cueClock.Reset();shockwave.Hide();}
            Plugin.Instance.Trace(player.GetZDOID()+" session="+session+" expected="+(expected/(double)clip.frequency).ToString("F6")+" actual="+(actual/(double)clip.frequency).ToString("F6")+" error="+error.ToString("F6"));
        }
    }
    private void UpdateShockwave()
    {
        var local=Player.m_localPlayer;
        var clip=ActiveTape!=null?AudioLibrary.Get(ActiveTape.Id):null;
        if(!mediaMatches || !Plugin.Instance.ShockwavesEnabled.Value || !clip || !local || Vector3.Distance(player.transform.position,local.transform.position)>=20)
        {cueClock.Reset();shockwave.Hide();return;}
        double elapsed=Elapsed;
        if(cueClock.Advance(session,elapsed,clip.samples/(double)clip.frequency,AudioLibrary.Cues(ActiveTape!.Id)))
            shockwave.Trigger(player.transform.position+Vector3.up*2);
        shockwave.Tick();
    }
    private void UpdateBuffs()
    {
        if(player.IsDead() || player.IsTeleporting()){ClearBuffs();return;}
        var ship=Ship.GetLocalShip();
        foreach(var p in Player.GetAllPlayers())
        {
            var musician=p?p.GetComponent<Musician>():null;
            if(p==null || musician==null || musician.ActiveTape==null || Vector3.Distance(p.transform.position,player.transform.position)>20)continue;
            if(musician.ActiveTape.Bonus==Bonus.Sail && (!ship || !ship.IsPlayerInBoat(p)))continue;
            contributions.Observe(p.GetZDOID().ToString(),musician.ActiveTape.Id,musician.Quality,MusicClock.Local);
        }
        var active=contributions.Resolve(MusicClock.Local);var seman=player.GetSEMan();
        foreach(var tape in Catalog.Tapes)
        {
            int hash=tape.Effect.GetStableHashCode();
            if(active.TryGetValue(tape.Id,out var quality))seman.AddStatusEffect(hash,true,quality,0);
            else seman.RemoveStatusEffect(hash,true);
        }
    }
    private void ClearBuffs()
    {
        contributions.Clear();
        if(player)foreach(var tape in Catalog.Tapes)player.GetSEMan().RemoveStatusEffect(tape.Effect.GetStableHashCode(),true);
    }
    private void OnDisable()
    {
        if(view && view.IsValid() && view.IsOwner())view.GetZDO().Set(ActiveKey,false);
        ActiveTape=null;controls.Interrupt();cueClock.Reset();shockwave.Hide();
        if(player==Player.m_localPlayer)ClearBuffs();
        if(source)source.Stop();
    }
    private void OnDestroy(){OnDisable();shockwave.Destroy();if(emitter)Destroy(emitter);var pose=GetComponent<CarryPose>();if(pose)Destroy(pose);}
}

[HarmonyPatch(typeof(Player),nameof(Player.SetControls))]
internal static class MusicControls
{
    private static void Prefix(Player __instance,ref bool attack,ref bool attackHold,ref bool secondaryAttack,ref bool secondaryAttackHold,ref bool block,ref bool blockHold,bool jump,bool dodge)
    {
        if(!Plugin.Ready || __instance!=Player.m_localPlayer || !Musician.HasBox(__instance))return;
        var musician=__instance.GetComponent<Musician>();
        if(musician)musician.Input(attackHold || attack,dodge);
        attack=attackHold=secondaryAttack=secondaryAttackHold=block=blockHold=false;
    }
}
[HarmonyPatch(typeof(Ship),"GetSailForce")]
internal static class MusicSail
{
    private static void Postfix(Ship __instance,ref Vector3 __result)
    {
        if(!Plugin.Ready || !__instance.IsOwner())return;
        float bonus=0;
        foreach(var p in Player.GetAllPlayers())
        {
            var musician=p?p.GetComponent<Musician>():null;
            if(musician && musician.ActiveTape?.Bonus==Bonus.Sail && __instance.IsPlayerInBoat(p))
                bonus=Mathf.Max(bonus,musician.ActiveTape.Value(musician.Quality));
        }
        __result*=1+bonus/100;
    }
}
[HarmonyPatch(typeof(ItemDrop.ItemData),nameof(ItemDrop.ItemData.GetTooltip),new[]{typeof(ItemDrop.ItemData),typeof(int),typeof(bool),typeof(float),typeof(int),typeof(bool)})]
internal static class MusicTooltip
{
    private static bool Prefix(ItemDrop.ItemData item,int qualityLevel,ref string __result)
    {
        var name=item.m_dropPrefab?item.m_dropPrefab.name:"";var tape=Catalog.Find(name);
        if(tape==null && name!=Catalog.Box)return true;
        __result="<color=orange>"+item.m_shared.m_name+"</color>\n"+item.m_shared.m_description;
        if(name==Catalog.Box)__result+="\n"+(Plugin.Instance.TogglePlayback.Value?"Press Attack to start or stop music.":"Hold Attack to play; release to stop.");
        if(tape!=null)__result+="\n\nSong: "+SongCatalog.For(tape.Id).Display;
        if(tape!=null)__result+="\n\nQuality "+qualityLevel+"/3\n"+tape.Description(qualityLevel)+"\n20 m music aura · reusable";
        __result+="\nWeight: "+item.GetWeight().ToString("0.#");return false;
    }
}
