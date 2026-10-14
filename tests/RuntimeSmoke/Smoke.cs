using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

[BepInPlugin("norskit_bardbox_smoke","BardBox isolated smoke","0.1.0")]
[BepInDependency(BardBox.Plugin.Id)]
[DefaultExecutionOrder(20000)]
public sealed class Smoke : BaseUnityPlugin
{
    private string folder="";
    private int rigErrors;
    private void Awake()
    {
        Application.logMessageReceived+=(message,stack,type)=>{if(message.Contains("Ambiguous Transform") || message.Contains("avatar is invalid"))rigErrors++;};
        if(Environment.GetEnvironmentVariable("BARD_POSE_BASELINE")=="1")
            new Harmony("norskit_bardbox_pose_baseline").Patch(
                AccessTools.Method(typeof(BardBox.Plugin).Assembly.GetType("BardBox.CarryPose"),"Update"),
                prefix:new HarmonyMethod(typeof(Smoke),nameof(SkipPose)));
    }
    private static bool SkipPose()=>false;
    private IEnumerator Start()
    {
        if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BARD_NETWORK_ROLE")))yield break;
        folder=Environment.GetEnvironmentVariable("BARD_SMOKE_OUTPUT")??"";
        if(folder.Length==0)yield break;
        yield return new WaitForSecondsRealtime(25);
        try { PrepareDressedPlayer(); }
        catch(Exception e){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL preparing player\n"+e);Application.Quit();yield break;}
        yield return new WaitForSecondsRealtime(2);
        try { CheckPlayerAnimation();Run();CheckNoRangedCompatibility(); }
        catch(Exception e){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL\n"+e);Logger.LogError(e);}
        if(!File.Exists(Path.Combine(folder,"result.txt")))
        {
            var cases=AllCases();
            while(true)
            {
                bool more;
                try{more=cases.MoveNext();}
                catch(Exception e){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL animation\n"+e);Logger.LogError(e);break;}
                if(!more){File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS\nNative registration/audio/inventory plus dressed animated player regression, both models, active/idle/stop/disable pose, non-arm bones preserved.\nWorld tests: "+(Environment.GetEnvironmentVariable("BARD_WORLD_TEST")=="1")+"\n");break;}
                yield return cases.Current;
            }
        }
        Application.Quit();
    }
    private void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private IEnumerator AllCases()
    {
        var cases=AnimationCases();while(cases.MoveNext())yield return cases.Current;
        if(Environment.GetEnvironmentVariable("BARD_WORLD_TEST")=="1")
        {cases=WorldCases();while(cases.MoveNext())yield return cases.Current;}
    }
    private bool worldInput,playInput,jumpInput,runInput;
    private Vector3 moveInput;
    private GameObject? capture;
    private string captureName="";
    private void LateUpdate()
    {
        if(!capture)return;
        var target=capture;capture=null;
        var p=target.GetComponentInParent<Player>();
        var pose=p.GetComponent(typeof(BardBox.Plugin).Assembly.GetType("BardBox.CarryPose"));
        Logger.LogInfo("CAPTURE before pose: "+(p.GetVisEquipment().m_rightHand.position.y-p.transform.position.y)+" mode="+p.GetComponentInChildren<Animator>().updateMode+" weight="+AccessTools.Field(pose.GetType(),"weight").GetValue(pose));
        AccessTools.Method(pose.GetType(),"LateUpdate").Invoke(pose,null);
        Logger.LogInfo("CAPTURE after pose: "+(p.GetVisEquipment().m_rightHand.position.y-p.transform.position.y));
        Camera.CameraCallback pre=c=>Logger.LogInfo("RENDER preCull "+(p.GetVisEquipment().m_rightHand.position.y-p.transform.position.y));
        Camera.CameraCallback render=c=>Logger.LogInfo("RENDER preRender "+(p.GetVisEquipment().m_rightHand.position.y-p.transform.position.y));
        Camera.CameraCallback post=c=>Logger.LogInfo("RENDER postRender "+(p.GetVisEquipment().m_rightHand.position.y-p.transform.position.y));
        Camera.onPreCull+=pre;Camera.onPreRender+=render;Camera.onPostRender+=post;
        PreviewVisual(target,captureName,true);
        Camera.onPreCull-=pre;Camera.onPreRender-=render;Camera.onPostRender-=post;
        Logger.LogInfo("CAPTURE after render: "+(p.GetVisEquipment().m_rightHand.position.y-p.transform.position.y));
        if(p.GetVisEquipment().m_rightHand.position.y-p.transform.position.y<1.8f)
            File.WriteAllText(Path.Combine(folder,"render-failure.txt"),"Render interpolation overwrote the overhead grip");
    }
    private void Update()
    {
        if(worldInput && Player.m_localPlayer)
            Player.m_localPlayer.SetControls(moveInput, false,playInput,false,false,false,false,jumpInput,false,runInput,false,false);
    }
    private static bool Focused(ref bool __result){__result=true;return false;}
    private ItemDrop.ItemData Equip(Player player,string name)
    {
        var prefab=PrefabManager.Instance.GetPrefab(name);var item=prefab.GetComponent<ItemDrop>().m_itemData.Clone();item.m_dropPrefab=prefab;
        player.GetInventory().GetAllItems().Add(item);Check(player.EquipItem(item,false),"Equip "+name);return item;
    }
    private IEnumerator WorldCases()
    {
        new Harmony("norskit_bardbox_test_focus").Patch(AccessTools.PropertyGetter(typeof(Application),"isFocused"),prefix:new HarmonyMethod(typeof(Smoke),nameof(Focused)));
        var profile=new PlayerProfile("bardbox_regression",FileHelpers.FileSource.Local);
        profile.SetName("BardBox regression");profile.m_firstSpawn=false;Check(profile.Save(),"Save isolated test profile");
        Game.SetProfile("bardbox_regression",FileHelpers.FileSource.Local);
        var world=new World("BardBoxRegression","BardBoxRegression");world.m_fileSource=FileHelpers.FileSource.Local;
        ZNet.SetServer(true,false,false,world.m_name,"",world);ZNet.ResetServerHost();
        AccessTools.Method(typeof(FejdStartup),"LoadMainScene").Invoke(FejdStartup.instance,null);
        float deadline=Time.realtimeSinceStartup+180;
        while(!Player.m_localPlayer || Player.m_localPlayer.InCutscene())
        {Check(Time.realtimeSinceStartup<deadline,"Timed out spawning test player");yield return new WaitForSecondsRealtime(1);}
        var player=Player.m_localPlayer;player.SetGodMode(true);
        player.GetComponent<PlayerController>().enabled=false; // Harness supplies the complete input stream.
        Equip(player,"ArmorLeatherChest");Equip(player,"CapeDeerHide");
        Equip(player,"NorskIT_BardBox_Boombox");Equip(player,"NorskIT_BardBox_trailblazer");
        yield return new WaitForSecondsRealtime(2);
        var musician=player.GetComponent(typeof(BardBox.Plugin).Assembly.GetType("BardBox.Musician"));
        var pose=player.GetComponent(typeof(BardBox.Plugin).Assembly.GetType("BardBox.CarryPose"));
        worldInput=true;playInput=true;
        yield return new WaitForSecondsRealtime(2);
        if(AccessTools.Property(musician.GetType(),"ActiveTape").GetValue(musician)==null)
        {
            var inputState=AccessTools.Field(musician.GetType(),"controls").GetValue(musician);
            Logger.LogError("INPUT DIAGNOSTIC blocked="+AccessTools.Method(musician.GetType(),"Blocked").Invoke(musician,null)+
                " playing="+AccessTools.Property(inputState.GetType(),"Playing").GetValue(inputState)+
                " latched="+AccessTools.Field(inputState.GetType(),"latched").GetValue(inputState)+
                " pressed="+AccessTools.Field(inputState.GetType(),"pressed").GetValue(inputState)+
                " toggle="+AccessTools.Field(inputState.GetType(),"toggle").GetValue(inputState)+
                " inputAge="+(Time.unscaledTime-(float)AccessTools.Field(musician.GetType(),"lastInput").GetValue(musician))+
                " ammo="+player.GetAmmoItem()?.m_dropPrefab?.name);
        }
        Check(AccessTools.Property(musician.GetType(),"ActiveTape").GetValue(musician)!=null,"World input did not start music");
        Check(player.GetSEMan().HaveStatusEffect("NorskIT_BardBox_SE_trailblazer".GetStableHashCode()),"World music buff missing");
        var audio=(AudioSource)AccessTools.Field(musician.GetType(),"source").GetValue(musician);
        Check(audio && audio.isPlaying,"World audio not playing");
        Check((float)AccessTools.Field(pose.GetType(),"weight").GetValue(pose)>.95f,"World pose never reached full weight");
        AccessTools.Method(pose.GetType(),"LateUpdate").Invoke(pose,null);
        Check(player.GetVisEquipment().m_leftHand.position.y>player.transform.position.y+1.8f,"World left grip not overhead");
        Check(player.GetVisEquipment().m_rightHand.position.y>player.transform.position.y+1.8f,"World right grip not overhead");
        capture=player.GetVisual();captureName="world-playing.png";
        yield return new WaitForSecondsRealtime(.2f);
        var origin=player.transform.position;moveInput=Vector3.forward;
        yield return new WaitForSecondsRealtime(1);
        moveInput=Vector3.zero;
        Check(Vector3.ProjectOnPlane(player.transform.position-origin,Vector3.up).magnitude>.1f,"Player could not walk during music");
        var clockType=typeof(BardBox.Plugin).Assembly.GetType("BardBox.MusicClock");
        Check((bool)AccessTools.Property(clockType,"Ready").GetValue(null),"Host music clock not ready");
        double MusicNow()=>(double)AccessTools.Property(musician.GetType(),"Now").GetValue(null);
        double Expected()=>(MusicNow()-(long)AccessTools.Field(musician.GetType(),"started").GetValue(musician)/(double)TimeSpan.TicksPerSecond)%audio.clip.length;
        double Error(){double e=audio.time-Expected();return Math.Abs(e-Math.Round(e/audio.clip.length)*audio.clip.length);}
        var priorWorld=ZNet.instance.GetTimeSeconds();double beforeClock=MusicNow();
        ZNet.instance.SetNetTime(priorWorld+7200);
        yield return new WaitForSecondsRealtime(.1f);
        Check(MusicNow()-beforeClock<.5,"World time change moved music clock");
        ZNet.instance.SetNetTime(priorWorld);
        audio.time=(audio.time+5)%audio.clip.length;
        yield return new WaitForSecondsRealtime(.45f);
        Check(Error()<.1,"Five-second audio drift was not corrected");
        Logger.LogInfo("SYNC runtime: injected 5 s drift corrected, remaining error="+Error());
        var startField=AccessTools.Field(musician.GetType(),"started");
        var songStart=(long)startField.GetValue(musician);
        runInput=true;moveInput=Vector3.forward;
        for(int jump=0;jump<3;jump++)
        {
            float height=player.transform.position.y;
            jumpInput=true;yield return new WaitForSecondsRealtime(.12f);jumpInput=false;
            yield return new WaitForSecondsRealtime(.18f);
            Check(player.transform.position.y>height+.1f,"Jump did not lift player");
            Check(audio.isPlaying && (long)startField.GetValue(musician)==songStart,"Jump interrupted/restarted music");
            Check(player.GetSEMan().HaveStatusEffect("NorskIT_BardBox_SE_trailblazer".GetStableHashCode()),"Jump removed buff");
            Check((float)AccessTools.Field(pose.GetType(),"weight").GetValue(pose)>.95f,"Jump lowered arms");
            if(jump==0){capture=player.GetVisual();captureName="world-jumping.png";}
            yield return new WaitForSecondsRealtime(1.3f);
        }
        runInput=false;moveInput=Vector3.zero;
        Check(audio.isPlaying && (long)startField.GetValue(musician)==songStart,"Landing interrupted music");
        var wave=AccessTools.Field(musician.GetType(),"shockwave").GetValue(musician);
        var waveType=wave.GetType();
        var pluginType=typeof(BardBox.Plugin);
        var plugin=AccessTools.Field(pluginType,"Instance").GetValue(null);
        var volume=(BepInEx.Configuration.ConfigEntry<float>)AccessTools.Field(pluginType,"Volume").GetValue(plugin);
        var enabled=(BepInEx.Configuration.ConfigEntry<bool>)AccessTools.Field(pluginType,"ShockwavesEnabled").GetValue(plugin);
        var library=pluginType.Assembly.GetType("BardBox.AudioLibrary");
        var cues=(double[])AccessTools.Method(library,"Cues").Invoke(null,new object[]{"trailblazer"});
        Check(cues.Length>0,"No analyzed cues in real song");
        volume.Value=0;enabled.Value=false;
        yield return new WaitForSecondsRealtime(.1f);
        enabled.Value=true;
        startField.SetValue(musician,(long)((double)AccessTools.Property(musician.GetType(),"Now").GetValue(null)*TimeSpan.TicksPerSecond)-(long)((cues[0]-.2)*TimeSpan.TicksPerSecond));
        yield return new WaitForSecondsRealtime(.4f);
        var automaticRing=(GameObject)AccessTools.Field(waveType,"root").GetValue(wave);
        Check(automaticRing && automaticRing.activeSelf,"Real song cue did not automatically trigger while muted");
        Check(!audio.isPlaying,"Muted audio still playing");
        enabled.Value=false;yield return new WaitForSecondsRealtime(.1f);
        Check(!automaticRing.activeSelf,"Disabled shockwaves remain visible");
        enabled.Value=true;volume.Value=.5f;
        yield return new WaitForSecondsRealtime(.1f);
        Check(!automaticRing.activeSelf,"Enabling shockwaves replayed history");
        var trigger=AccessTools.Method(waveType,"Trigger");
        var tick=AccessTools.Method(waveType,"Tick");
        trigger.Invoke(wave,new object[]{player.transform.position+Vector3.up*2});
        var ringRoot=(GameObject)AccessTools.Field(waveType,"root").GetValue(wave);
        Check(ringRoot && ringRoot.activeSelf,"Shockwave failed to spawn");
        var line=ringRoot.GetComponent<LineRenderer>();
        Check(line.sharedMaterial.shader.isSupported && line.positionCount==96,"Shockwave shader/geometry invalid");
        AccessTools.Field(waveType,"began").SetValue(wave,Time.unscaledTime-.3f);tick.Invoke(wave,null);
        Check(ringRoot.transform.localScale.x>2 && line.startColor.a>.4f && line.startColor.a<.6f,"Shockwave expansion/fade");
        PreviewVisual(ringRoot,"shockwave-preview.png",true);
        AccessTools.Field(waveType,"began").SetValue(wave,Time.unscaledTime-.7f);tick.Invoke(wave,null);
        Check(!ringRoot.activeSelf,"Shockwave did not expire");
        trigger.Invoke(wave,new object[]{player.transform.position+Vector3.up*2});
        playInput=false;yield return new WaitForSecondsRealtime(3);
        Check(!ringRoot.activeSelf,"Release left shockwave visible");
        Check(!audio.isPlaying,"Release did not stop audio");
        Check(!player.GetSEMan().HaveStatusEffect("NorskIT_BardBox_SE_trailblazer".GetStableHashCode()),"Released music buff did not expire");
        Check((float)AccessTools.Field(pose.GetType(),"weight").GetValue(pose)==0,"World pose did not release");
        playInput=true;yield return new WaitForSecondsRealtime(1);
        Check(audio.isPlaying && audio.time<2,"New attack did not restart audio");
        var toggle=(BepInEx.Configuration.ConfigEntry<bool>)AccessTools.Field(pluginType,"TogglePlayback").GetValue(plugin);
        playInput=false;toggle.Value=true;yield return new WaitForSecondsRealtime(.3f);
        Check(!audio.isPlaying,"Changing input mode did not stop music");
        playInput=true;yield return new WaitForSecondsRealtime(.2f);
        var toggledStart=(long)startField.GetValue(musician);
        playInput=false;yield return new WaitForSecondsRealtime(.6f);
        Check(audio.isPlaying && (long)startField.GetValue(musician)==toggledStart,"Toggle did not continue after release");
        playInput=true;yield return new WaitForSecondsRealtime(.2f);
        Check(!audio.isPlaying,"Second toggle press did not stop music");
        playInput=false;yield return new WaitForSecondsRealtime(.2f);
        playInput=true;yield return new WaitForSecondsRealtime(.3f);
        Check(audio.isPlaying && audio.time<1 && (long)startField.GetValue(musician)!=toggledStart,"Toggle restart did not begin a new song");
        toggle.Value=false;playInput=false;yield return new WaitForSecondsRealtime(.2f);
        playInput=true;yield return new WaitForSecondsRealtime(.3f);
        Equip(player,"NorskIT_BardBox_second_wind");
        yield return new WaitForSecondsRealtime(.4f);
        Check(!audio.isPlaying,"Changing tape did not stop playback");
        playInput=false;yield return new WaitForSecondsRealtime(.3f);
        playInput=true;yield return new WaitForSecondsRealtime(.5f);
        Check(audio.isPlaying,"New tape did not start after new press");
        Check(player.TeleportTo(player.transform.position+Vector3.forward*3,player.transform.rotation,false),"Test teleport rejected");
        yield return new WaitForSecondsRealtime(.3f);
        Check(!audio.isPlaying && (float)AccessTools.Field(pose.GetType(),"weight").GetValue(pose)==0,"Teleport did not clear playback/pose");
        playInput=false;
        deadline=Time.realtimeSinceStartup+30;
        while(player.IsTeleporting()){Check(Time.realtimeSinceStartup<deadline,"Teleport timed out");yield return new WaitForSecondsRealtime(.5f);}
        playInput=true;yield return new WaitForSecondsRealtime(.5f);
        Check(audio.isPlaying,"Could not play after teleport");
        player.SetGodMode(false);var hit=new HitData();hit.m_damage.m_damage=10000;player.Damage(hit);playInput=false;
        yield return new WaitForSecondsRealtime(.5f);
        Check(player.IsDead(),"Test damage did not kill player");
        Check(!audio || !audio.isPlaying,"Death did not stop sound");
        if(pose)Check((float)AccessTools.Field(pose.GetType(),"weight").GetValue(pose)==0,"Death did not reset pose");
        deadline=Time.realtimeSinceStartup+60;
        while(!Player.m_localPlayer || Player.m_localPlayer==player || Player.m_localPlayer.IsDead())
        {Check(Time.realtimeSinceStartup<deadline,"Respawn timed out");yield return new WaitForSecondsRealtime(1);}
        CheckPlayerAnimation();
        worldInput=false;
        Check(!File.Exists(Path.Combine(folder,"render-failure.txt")),"Rendered pose was overwritten by interpolation");
        Logger.LogInfo("WORLD PASS: cape, walking, hold/release/restart, tape swap, audio/buff expiry, teleport, death and respawn");
    }
    private void PrepareDressedPlayer()
    {
        AccessTools.Method(typeof(FejdStartup),"SetupCharacterPreview").Invoke(FejdStartup.instance,new object?[]{null});
        var player=FejdStartup.instance.GetPreviewPlayer();
        foreach(var name in new[]{"ArmorLeatherChest","ArmorLeatherLegs","CapeDeerHide"})
        {
            var prefab=PrefabManager.Instance.GetPrefab(name);
            var item=prefab.GetComponent<ItemDrop>().m_itemData.Clone();item.m_dropPrefab=prefab;
            player.GetInventory().GetAllItems().Add(item);Check(player.EquipItem(item,false),"Equip "+name);
        }
        player.GetVisEquipment().CustomUpdate(0,0);
        Logger.LogInfo("DRESSED hips="+player.GetComponentsInChildren<Transform>().Count(t=>t.name=="Hips"));
    }
    private void CheckPlayerAnimation()
    {
        Check(rigErrors==0,"BardBox caused "+rigErrors+" skeleton binding errors");
        var players=Player.GetAllPlayers();
        Check(players.Count>0,"No animated menu players available");
        foreach(var player in players)
        {
            var animator=player.GetComponentInChildren<Animator>();
            Logger.LogInfo("POSE player="+player.name+" animator="+animator.name+" initialized="+animator.isInitialized+" avatar="+animator.avatar.isValid+" state="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            Check(animator.isInitialized,"Player Animator stopped evaluating after BardBox initialization");
            Check(animator.avatar && animator.avatar.isValid,"Player Avatar invalid after BardBox initialization");
        }
    }
    private IEnumerator AnimationCases()
    {
        var player=FejdStartup.instance.GetPreviewPlayer();
        var assembly=typeof(BardBox.Plugin).Assembly;
        var poseType=assembly.GetType("BardBox.CarryPose")!;
        var musicianType=assembly.GetType("BardBox.Musician")!;
        var musician=player.GetComponent(musicianType);
        var pose=(Behaviour)player.GetComponent(poseType);
        var active=AccessTools.Property(musicianType,"ActiveTape");
        var tape=AccessTools.Method(assembly.GetType("BardBox.Catalog"),"Find").Invoke(null,new object[]{"trailblazer"});
        var animator=player.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var controller=animator.runtimeAnimatorController;var avatar=animator.avatar;
        var box=PrefabManager.Instance.GetPrefab("NorskIT_BardBox_Boombox");
        var item=box.GetComponent<ItemDrop>().m_itemData.Clone();item.m_dropPrefab=box;
        player.GetInventory().GetAllItems().Add(item);Check(player.EquipItem(item,false),"Equip box");
        for(int model=0;model<2;model++)
        {
            player.SetPlayerModel(model);player.GetVisEquipment().CustomUpdate(0,0);
            yield return new WaitForSecondsRealtime(.25f);
            var vis=player.GetVisEquipment();
            Logger.LogInfo("HAND "+vis.m_rightHand.name+" parent="+vis.m_rightHand.parent.name);
            var bones=player.GetVisual().GetComponentsInChildren<Transform>();
            var arms=new[]{vis.m_leftHand.parent,vis.m_leftHand.parent.parent,vis.m_leftHand.parent.parent.parent,vis.m_rightHand.parent,vis.m_rightHand.parent.parent,vis.m_rightHand.parent.parent.parent};
            for(int cycle=0;cycle<3;cycle++)
            {
                active.SetValue(musician,tape);AccessTools.Field(poseType,"weight").SetValue(pose,1f);
                var rotations=bones.Select(t=>t.localRotation).ToArray();var positions=bones.Select(t=>t.localPosition).ToArray();
                AccessTools.Method(poseType,"LateUpdate").Invoke(pose,null);
                for(int i=0;i<bones.Length;i++)
                {
                    Check(Vector3.Distance(bones[i].localPosition,positions[i])<.00001f,"Pose moved bone position: "+bones[i].name);
                    if(!arms.Contains(bones[i]))Check(Quaternion.Angle(bones[i].localRotation,rotations[i])<.05f,"Pose rotated non-arm bone: "+bones[i].name);
                }
                Check(vis.m_rightHand.position.y>player.transform.position.y+1.8f,"Right grip not overhead");
                Check(vis.m_leftHand.position.y>player.transform.position.y+1.8f,"Left grip not overhead");
                AccessTools.Method(poseType,"Update").Invoke(pose,null);
                for(int i=0;i<bones.Length;i++)Check(Quaternion.Angle(bones[i].localRotation,rotations[i])<.05f,"Pose did not restore bone: "+bones[i].name);
                var time=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return new WaitForSecondsRealtime(.3f);
                Check(animator.GetCurrentAnimatorStateInfo(0).normalizedTime>time,"Animator stopped during music");
                Check(animator.avatar==avatar && animator.runtimeAnimatorController==controller,"Animator configuration changed");
                if(cycle==0){capture=player.GetVisual();captureName="pose-model-"+model+".png";yield return new WaitForSecondsRealtime(.2f);}
                active.SetValue(musician,null);
                yield return new WaitForSecondsRealtime(.3f);
                Check((float)AccessTools.Field(poseType,"weight").GetValue(pose)==0,"Pose did not blend out");
            }
            active.SetValue(musician,tape);
            yield return new WaitForSecondsRealtime(.25f);
            pose.enabled=false;
            Check((float)AccessTools.Field(poseType,"weight").GetValue(pose)==0,"Disabled pose retained weight");
            active.SetValue(musician,null);pose.enabled=true;
        }
        animator.updateMode=AnimatorUpdateMode.Fixed;
        active.SetValue(musician,tape);yield return new WaitForSecondsRealtime(.5f);
        capture=player.GetVisual();captureName="pose-fixed.png";yield return new WaitForSecondsRealtime(.2f);
        active.SetValue(musician,null);animator.updateMode=AnimatorUpdateMode.Normal;
        CheckPlayerAnimation();
    }
    private void Run()
    {
        const string prefix="NorskIT_BardBox_";
        Check((bool)AccessTools.Field(typeof(BardBox.Plugin),"Ready").GetValue(null),"Plugin did not finish registration");
        var names=new[]{"trailblazer","second_wind","stonebeat","hauler","heartbeat","steady_rhythm","iron_chorus","tailwind","needle_guard","arcane_pulse","war_drums"};
        var db=ObjectDB.instance;
        Check(db!=null,"ObjectDB missing");
        // Jotunn only registers recipes/effects on world startup, not in the menu.
        // Exercise those exact registration routines against the disposable menu DB.
        AccessTools.Method(typeof(ItemManager),"RegisterCustomRecipes").Invoke(ItemManager.Instance,new object[]{db!});
        AccessTools.Method(typeof(ItemManager),"RegisterCustomStatusEffects").Invoke(ItemManager.Instance,new object[]{db!});
        foreach(var id in names.Concat(new[]{"Boombox","BlankTape"}))
        {
            var prefab=PrefabManager.Instance.GetPrefab(prefix+id);Check(prefab!=null,"Missing item "+id);
            var data=prefab.GetComponent<ItemDrop>().m_itemData;
            Check(data.m_shared.m_icons.Length>0 && data.m_shared.m_icons[0],"Missing icon "+id);
            var recipe=db!.GetRecipe(data);Check(recipe!=null,"Missing recipe "+id);
            Check(recipe!.m_craftingStation!=null,"Missing station "+id);
            foreach(var req in recipe.m_resources)Check(req.m_resItem!=null,"Unresolved recipe material for "+id);
            if(id=="Boombox" || id=="BlankTape")continue;
            Check(data.m_shared.m_itemType==ItemDrop.ItemData.ItemType.Ammo,"Incorrect tape type");
            Check(data.m_shared.m_maxQuality==3 && data.m_shared.m_maxStackSize==1,"Incorrect quality/stack");
            for(int level=1;level<=3;level++)
            {
                var effect=db.GetStatusEffect((prefix+"SE_"+id).GetStableHashCode());Check(effect!=null,"Missing effect "+id);
                var clone=effect.Clone();clone.SetLevel(level,0);
                Check(clone.m_icon!=null && clone.m_name.Contains("♫"),"Missing visible music buff");
                Check(clone.GetTooltipString().Contains("Quality "+level+"/3"),"Missing effect quality tooltip");
                var definition=AccessTools.Field(clone.GetType(),"Definition").GetValue(clone);
                var expected=(string)AccessTools.Method(definition.GetType(),"HudName").Invoke(definition,new object[]{level});
                Check(clone.m_name==expected && !clone.m_name.Contains(data.m_shared.m_name),"Buff HUD is not the current bonus");
                // StatusEffect.Clone is a managed MemberwiseClone, not a Unity clone.
                // Destroying it would destroy the shared native prototype.
            }
            var inv=new Inventory("BardBox smoke",null,8,4);var copy=data.Clone();copy.m_dropPrefab=prefab;copy.m_quality=3;
            inv.GetAllItems().Add(copy);var packet=new ZPackage();inv.Save(packet);packet.SetPos(0);
            var loaded=new Inventory("BardBox loaded",null,8,4);loaded.Load(packet);
            Check(loaded.GetAllItems().Count==1 && loaded.GetAllItems()[0].m_quality==3,"Tape save/load quality: "+id);
            Check(ItemDrop.ItemData.GetTooltip(copy,3,false,0).Contains("Song: "),"Song metadata missing: "+id);
            Check(ItemDrop.ItemData.GetTooltip(copy,3,false,0).Contains("Quality 3/3"),"Tooltip patch did not apply: "+id);
        }
        Check(Harmony.GetAllPatchedMethods().Count(m=>Harmony.GetPatchInfo(m).Owners.Contains(BardBox.Plugin.Id))==3,"Expected three scoped Harmony hooks");
        var library=typeof(BardBox.Plugin).Assembly.GetType("BardBox.AudioLibrary")!;
        foreach(var id in names)
        {
            var clip=(AudioClip)AccessTools.Method(library,"Get").Invoke(null,new object[]{id});
            Check(clip && clip.length>0,"Audio did not decode: "+id);
            Logger.LogInfo(id+" audio: "+clip.length+" s");
        }
        Preview(PrefabManager.Instance.GetPrefab(prefix+"Boombox"));
    }
    private void CheckNoRangedCompatibility()
    {
        if(!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("norskit_noranged_plugin",out var info))return;
        var plugin=info.Instance;
        var player=FejdStartup.instance.GetPreviewPlayer();
        var prior=Player.m_localPlayer;
        Player.m_localPlayer=player;
        try
        {
            var names=new[]{"trailblazer","second_wind","stonebeat","hauler","heartbeat","steady_rhythm","iron_chorus","tailwind","needle_guard","arcane_pulse","war_drums"};
            AccessTools.Method(plugin.GetType(),"DisableRangedRecipes").Invoke(plugin,null);
            foreach(var id in names)
            {
                var item=Equip(player,"NorskIT_BardBox_"+id);
                Check(player.GetAmmoItem()==item,"Tape not selected: "+id);
                Check(ObjectDB.instance.GetRecipe(item).m_enabled,"Tape recipe disabled: "+id);
                var craftGate=plugin.GetType().Assembly.GetType("NoRanged.CraftGate");
                object[] args={ObjectDB.instance.GetRecipe(item),true};
                Check((bool)AccessTools.Method(craftGate,"Prefix").Invoke(null,args),"Tape crafting gate rejected: "+id);
                player.UnequipItem(item,false);player.GetInventory().RemoveItem(item);
            }
            foreach(var name in new[]{"ArrowWood","Bow"})
            {
                var prefab=PrefabManager.Instance.GetPrefab(name);
                var item=prefab.GetComponent<ItemDrop>().m_itemData.Clone();item.m_dropPrefab=prefab;
                player.GetInventory().GetAllItems().Add(item);
                Check(!player.EquipItem(item,false),"No Ranged allowed "+name);
                Check(!ObjectDB.instance.GetRecipe(item).m_enabled,"No Ranged enabled recipe "+name);
                player.GetInventory().RemoveItem(item);
            }
            File.WriteAllText(Path.Combine(folder,"noranged-result.txt"),"PASS: No Ranged "+info.Metadata.Version+"; all 11 tapes equip/select and retain enabled recipes; crafting gate allows tapes; bow/arrow equipment and recipes remain blocked.");
        }
        finally {Player.m_localPlayer=prior;}
    }
    private void Preview(GameObject prefab)
    {
        PreviewVisual(prefab.transform.Find("attach").gameObject,"model-preview.png",false);
    }
    private void PreviewVisual(GameObject original,string filename,bool live)
    {
        var visual=live?original:Instantiate(original);visual.SetActive(true);
        var transforms=visual.GetComponentsInChildren<Transform>(true);var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        foreach(var t in visual.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
        foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
            foreach(var material in renderer.sharedMaterials)Check(material && material.shader && material.shader.isSupported,"Unsupported model material");
        var renderers=visual.GetComponentsInChildren<Renderer>();Check(renderers.Length>0,"Model has no visible meshes");
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        var light=new GameObject("Preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(40,-30,0);
        var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.16f);
        camera.transform.position=bounds.center+(live?visual.transform.rotation*new Vector3(2,1,5.8f):new Vector3(.7f,.45f,1.1f));camera.transform.LookAt(bounds.center);camera.fieldOfView=35;
        var rt=new RenderTexture(768,512,24);camera.targetTexture=rt;camera.Render();
        var prior=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(768,512,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,768,512),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(folder,filename),texture.EncodeToPNG());RenderTexture.active=prior;
        Destroy(texture);Destroy(rt);Destroy(camera.gameObject);Destroy(light.gameObject);
        if(!live)Destroy(visual);else for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];
    }
}
