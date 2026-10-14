using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace BardBox;

[BepInPlugin(Id,"BardBox",Version)]
[BepInDependency(Jotunn.Main.ModGuid,"2.30.2")]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Patch)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id="norskit_bardbox_plugin",Version="0.1.6";
    internal static Plugin Instance=null!;
    internal string Folder="";
    internal ConfigEntry<float> Volume=null!;
    internal ConfigEntry<float> ListenerVolume=null!;
    internal ServerSync.ConfigSync Sync=null!;
    private float serverVolume,serverRange;
    internal ConfigEntry<float> AudioRange=null!;
    internal ConfigEntry<bool> MuteGameMusic=null!,ShowAudioRange=null!;
    internal ConfigEntry<KeyboardShortcut> ToggleAudioRangeKey=null!;
    internal ConfigEntry<bool> ShockwavesEnabled=null!;
    internal ConfigEntry<bool> SyncDiagnostics=null!;
    internal ConfigEntry<bool> TogglePlayback=null!;
    internal Material? ShockwaveMaterial;
    internal static bool Ready;
    private Harmony harmony=null!;
    private AssetBundle? bundle;
    private void Awake()
    {
        Instance=this;Folder=Path.GetDirectoryName(Info.Location)!;
        Sync=new ServerSync.ConfigSync(Id){DisplayName="BardBox",CurrentVersion=Version,MinimumRequiredVersion=Version,ModRequired=true,IsLocked=true};
        Volume=Config.Bind("Audio","Volume",0.5f,new ConfigDescription("Server music volume: 1 = 100%, 5 = 500%. High gain may distort loud recordings. [Synced with Server]",new AcceptableValueRange<float>(0,5)));
        Sync.AddConfigEntry(Volume).SynchronizedConfig=true;
        serverVolume=Volume.Value;Volume.SettingChanged+=VolumeChanged;
        AudioRange=Config.Bind("Audio","Range",500f,new ConfigDescription("Music radius in metres. Buffs remain within 20 m. [Synced with Server]",new AcceptableValueRange<float>(1,2000)));
        Sync.AddConfigEntry(AudioRange).SynchronizedConfig=true;
        AudioRange.Value=RadioRules.Range(AudioRange.Value);serverRange=AudioRange.Value;AudioRange.SettingChanged+=RangeChanged;
        MuteGameMusic=Config.Bind("Audio","MuteGameMusic",true,"Fade game music out while a boombox is audible. [Local]");
        ShowAudioRange=Config.Bind("Debug","ShowAudioRange",false,"Show boombox sound radii in the world. [Local]");
        ToggleAudioRangeKey=Config.Bind("Debug","ToggleAudioRangeKey",new KeyboardShortcut(KeyCode.None),"Optional shortcut to toggle audio range visualization. [Local]");
        Sync.AddConfigEntry(MuteGameMusic).SynchronizedConfig=false;
        Sync.AddConfigEntry(ShowAudioRange).SynchronizedConfig=false;
        Sync.AddConfigEntry(ToggleAudioRangeKey).SynchronizedConfig=false;
        ListenerVolume=Config.Bind("Audio","ListenerVolume",1f,new ConfigDescription("Personal volume multiplier: 0 mutes, 1 uses the full server volume. Does not change buffs. [Local]",new AcceptableValueRange<float>(0,1)));
        Sync.AddConfigEntry(ListenerVolume).SynchronizedConfig=false;
        ShockwavesEnabled=Config.Bind("Visuals","ShockwavesEnabled",true,"Show local music shockwaves on strong bass/energy onsets.");
        SyncDiagnostics=Config.Bind("Diagnostics","SyncLogging",false,"Log music clock estimates and actual/expected audio positions for multiplayer testing.");
        TogglePlayback=Config.Bind("Controls","TogglePlayback",false,"False: hold Attack to play. True: press Attack once to play, again to stop. Interruptions still stop music.");
        Sync.AddConfigEntry(ShockwavesEnabled).SynchronizedConfig=false;
        Sync.AddConfigEntry(TogglePlayback).SynchronizedConfig=false;
        Sync.AddConfigEntry(SyncDiagnostics).SynchronizedConfig=false;
        harmony=new Harmony(Id);
        // ServerSync installs its own patches; only patch BardBox gameplay classes here.
        foreach(var type in new[]{typeof(MusicControls),typeof(MusicSail),typeof(MusicTooltip),typeof(BossTapeDrops),typeof(ChestTapeDrops)})harmony.CreateClassProcessor(type).Patch();
        PrefabManager.OnVanillaPrefabsAvailable+=Register;
    }
    private void VolumeChanged(object sender,EventArgs args)
    {
        // ServerSync locks UI edits and network writes. Keep manual local reloads
        // from overriding the effective server gain on a locked client as well.
        if(!Sync.IsSourceOfTruth && Sync.IsLocked && !ServerSync.ConfigSync.ProcessingServerUpdate)
        {if(Volume.Value!=serverVolume)Volume.Value=serverVolume;return;}
        serverVolume=Volume.Value;
    }
    private void RangeChanged(object sender,EventArgs args)
    {
        if(!Sync.IsSourceOfTruth && Sync.IsLocked && !ServerSync.ConfigSync.ProcessingServerUpdate)
        {if(AudioRange.Value!=serverRange)AudioRange.Value=serverRange;return;}
        float valid=RadioRules.Range(AudioRange.Value);serverRange=valid;
        if(AudioRange.Value!=valid)AudioRange.Value=valid;
    }
    internal void Warn(string message)=>Logger.LogWarning(message);
    internal void Trace(string message){if(SyncDiagnostics.Value)Logger.LogInfo("Music sync: "+message);}
    private void Register()
    {
        PrefabManager.OnVanillaPrefabsAvailable-=Register;
        try
        {
            bool headless=SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null;
            if(!headless)
            {
                var path=Path.Combine(Folder,"bardbox.assets");
                if(!File.Exists(path))throw new FileNotFoundException("BardBox model bundle is missing. Install the complete package.",path);
                bundle=AssetBundle.LoadFromFile(path);
                if(!bundle || !bundle.LoadAsset<GameObject>("BardBoxVisual"))throw new InvalidOperationException("BardBoxVisual is missing from the asset bundle.");
            }
            if(bundle)ShockwaveMaterial=bundle.LoadAsset<Material>("ShockwaveMaterial");
            AddItem(Catalog.Blank,"Blank Tape","Upgrade a found music tape at a workbench.","blank_tape",ItemDrop.ItemData.ItemType.Material,
                new[]{new RequirementConfig("Thistle",2),new RequirementConfig("GreydwarfEye",2)},false,headless);
            AddItem(Catalog.Box,"Boombox","Equip a tape and use Attack to play. Nearby players receive its bonus.","boombox",ItemDrop.ItemData.ItemType.TwoHandedWeapon,
                new[]{new RequirementConfig("FineWood",8),new RequirementConfig("Bronze",4),new RequirementConfig("SurtlingCore",2)},false,headless);
            foreach(var tape in Catalog.Tapes)
            {
                var cost=new List<RequirementConfig>{new RequirementConfig(Catalog.Blank,1,1)};
                cost.AddRange(tape.Materials.Select(x=>new RequirementConfig(x.Item,x.Count,x.Count)));
                var item=AddItem(tape.Prefab,tape.Name,"Reusable music tape. Select like arrows; play with the Boombox.",tape.Id,ItemDrop.ItemData.ItemType.Ammo,cost.ToArray(),true,headless);
                var effect=ScriptableObject.CreateInstance<MusicEffect>();effect.name=tape.Effect;
                effect.Definition=tape;effect.m_ttl=2.5f;effect.SetLevel(1,0);
                effect.m_icon=item.ItemDrop.m_itemData.m_shared.m_icons.FirstOrDefault();
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect,false));
            }
            if(!headless)AudioLibrary.Load(Folder,Warn);
            Ready=true;Logger.LogInfo("BardBox registered: boombox, blank tape and 11 reusable tapes.");
        }
        catch(Exception e){Logger.LogError("BardBox could not initialize: "+e);}
    }
    private CustomItem AddItem(string id,string name,string description,string icon,ItemDrop.ItemData.ItemType type,RequirementConfig[] cost,bool upgrade,bool headless)
    {
        var item=new CustomItem(id,"TankardAnniversary",new ItemConfig {Name=name,Description=description,CraftingStation="piece_workbench",MinStationLevel=1,Requirements=cost});
        if(upgrade)item.Recipe.Recipe.m_noCraftOnlyUpgrade=true;
        var shared=item.ItemDrop.m_itemData.m_shared;
        shared.m_itemType=type;shared.m_maxQuality=upgrade?3:1;shared.m_maxStackSize=id==Catalog.Blank?50:1;
        shared.m_weight=id==Catalog.Box?4:id==Catalog.Blank?0.1f:0.2f;
        shared.m_ammoType=upgrade?Catalog.Ammo:"";shared.m_useDurability=false;shared.m_skillType=Skills.SkillType.None;
        shared.m_damages=default;shared.m_damagesPerLevel=default;shared.m_attackForce=0;shared.m_backstabBonus=1;
        shared.m_blockPower=0;shared.m_blockPowerPerLevel=0;shared.m_deflectionForce=0;shared.m_timedBlockBonus=1;
        shared.m_attack=new Attack {m_attackType=Attack.AttackType.None,m_attackStamina=0};
        shared.m_secondaryAttack=new Attack {m_attackType=Attack.AttackType.None,m_attackStamina=0};
        shared.m_startEffect=new EffectList();shared.m_triggerEffect=new EffectList();shared.m_holdStartEffect=new EffectList();
        shared.m_consumeStatusEffect=null;shared.m_equipStatusEffect=null;
        shared.m_movementModifier=0;shared.m_teleportable=true;
        item.ItemDrop.m_autoDestroy=false;
        var attach=item.ItemPrefab.transform.Find("attach");
        Material? material=attach?attach.GetComponentsInChildren<Renderer>(true).FirstOrDefault()?.sharedMaterial:null;
        if(attach)DestroyImmediate(attach.gameObject);
        GameObject visual;
        if(id==Catalog.Box && !headless)visual=Instantiate(bundle!.LoadAsset<GameObject>("BardBoxVisual"));
        else if(!headless)
        {
            visual=new GameObject("Tape");
            var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.transform.SetParent(visual.transform,false);body.transform.localScale=new Vector3(.12f,.075f,.012f);
            DestroyImmediate(body.GetComponent<Collider>());
            if(material)body.GetComponent<Renderer>().sharedMaterial=new Material(material){color=new Color(.12f,.13f,.15f)};
            // Two reels and a colored label make dropped tapes recognizable without additional meshes.
            for(int i=0;i<2;i++)
            {
                var reel=GameObject.CreatePrimitive(PrimitiveType.Cylinder);reel.transform.SetParent(visual.transform,false);
                reel.transform.localPosition=new Vector3(i==0?-.028f:.028f,0,-.008f);reel.transform.localRotation=Quaternion.Euler(90,0,0);reel.transform.localScale=new Vector3(.025f,.002f,.025f);
                DestroyImmediate(reel.GetComponent<Collider>());
                if(material)reel.GetComponent<Renderer>().sharedMaterial=new Material(material){color=Color.gray};
            }
            var label=GameObject.CreatePrimitive(PrimitiveType.Cube);label.transform.SetParent(visual.transform,false);
            label.transform.localPosition=new Vector3(0,.025f,-.008f);label.transform.localScale=new Vector3(.105f,.018f,.002f);
            DestroyImmediate(label.GetComponent<Collider>());
            var colors=new[]{new Color(.25f,.65f,.2f),new Color(.1f,.6f,.6f),new Color(.3f,.4f,.6f),new Color(.8f,.55f,.15f),new Color(.7f,.15f,.15f),new Color(.95f,.8f,.15f),new Color(.6f,.7f,.8f),new Color(.1f,.4f,.8f),new Color(.9f,.45f,.1f),new Color(.6f,.25f,.8f),new Color(.45f,.05f,.05f)};
            int tapeIndex=Array.FindIndex(Catalog.Tapes,t=>t.Prefab==id);
            if(material)label.GetComponent<Renderer>().sharedMaterial=new Material(material){color=tapeIndex>=0?colors[tapeIndex]:new Color(.85f,.8f,.65f)};
        }
        else visual=new GameObject("Visual");
        visual.name="attach";visual.transform.SetParent(item.ItemPrefab.transform,false);
        var collider=item.ItemPrefab.AddComponent<BoxCollider>();collider.size=id==Catalog.Box?new Vector3(.6f,.36f,.2f):new Vector3(.12f,.075f,.02f);
        if(id==Catalog.Box)collider.center=new Vector3(-.25f,.18f,0);
        if(!headless)
        {
            var path=Path.Combine(Folder,"icons",icon+".png");
            if(!File.Exists(path))throw new FileNotFoundException("Missing item icon",path);
            shared.m_icons=new[]{AssetUtils.LoadSpriteFromFile(path)};
        }
        ItemManager.Instance.AddItem(item);return item;
    }
    private void Update()
    {
        MusicClock.Tick();RadioNetwork.Tick();
        if(!Ready)return;
        foreach(var player in Player.GetAllPlayers())
            if(player && !player.GetComponent<Musician>())player.gameObject.AddComponent<Musician>();
    }
    private void LateUpdate()
    {
        if(!Ready)return;
        RadioPlayback.Tick();MusicDucking.Tick(Time.unscaledDeltaTime);AudioRangeDebug.Tick();
    }
    private void OnGUI(){if(Ready)AudioRangeDebug.Draw();}
    private void OnDestroy()
    {
        Ready=false;PrefabManager.OnVanillaPrefabsAvailable-=Register;
        foreach(var musician in FindObjectsByType<Musician>(FindObjectsSortMode.None))Destroy(musician);
        Volume.SettingChanged-=VolumeChanged;AudioRange.SettingChanged-=RangeChanged;
        RadioPlayback.Clear();RadioNetwork.Reset();MusicDucking.Clear();AudioRangeDebug.Clear();
        harmony?.UnpatchSelf();AudioLibrary.Clear();MusicClock.Reset();
        if(bundle)bundle.Unload(false);
    }
}
