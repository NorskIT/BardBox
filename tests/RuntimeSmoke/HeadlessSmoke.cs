using System;
using System.Collections;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
[BepInPlugin("norskit_bardbox_headless_smoke","BardBox headless regression","0.1.0")]
[BepInDependency(BardBox.Plugin.Id)]
public sealed class HeadlessSmoke : BaseUnityPlugin
{
    private static int debugCalls,playbackCalls,duckCalls,clockCalls,relayCalls;
    private Harmony? patch;
    private static bool Focus(ref bool __result){__result=true;return false;}
    private static void DebugTick(){debugCalls++;}
    private static void Playback(){playbackCalls++;}
    private static void Duck(){duckCalls++;}
    private static void Clock(){clockCalls++;}
    private static void Relay(){relayCalls++;}
    private void Awake()
    {
        if(Environment.GetEnvironmentVariable("BARD_HEADLESS_TEST")!="1")return;
        patch=new Harmony("norskit_bardbox_headless_guard_test");
        patch.Patch(AccessTools.PropertyGetter(typeof(Application),"isFocused"),prefix:new HarmonyMethod(typeof(HeadlessSmoke),nameof(Focus)));
        var a=typeof(BardBox.Plugin).Assembly;
        patch.Patch(AccessTools.Method(a.GetType("BardBox.MusicClock"),"Tick"),prefix:new HarmonyMethod(typeof(HeadlessSmoke),nameof(Clock)));
        patch.Patch(AccessTools.Method(a.GetType("BardBox.RadioNetwork"),"Tick"),prefix:new HarmonyMethod(typeof(HeadlessSmoke),nameof(Relay)));
        // Audit the boundary: presentation must not be scheduled at all without graphics.
        patch.Patch(AccessTools.Method(a.GetType("BardBox.AudioRangeDebug"),"Tick"),prefix:new HarmonyMethod(typeof(HeadlessSmoke),nameof(DebugTick)));
        patch.Patch(AccessTools.Method(a.GetType("BardBox.RadioPlayback"),"Tick"),prefix:new HarmonyMethod(typeof(HeadlessSmoke),nameof(Playback)));
        patch.Patch(AccessTools.Method(a.GetType("BardBox.MusicDucking"),"Tick"),prefix:new HarmonyMethod(typeof(HeadlessSmoke),nameof(Duck)));
    }
    private IEnumerator Start()
    {
        if(patch==null)yield break;
        var path=Path.Combine(Environment.GetEnvironmentVariable("BARD_SMOKE_OUTPUT")!,"headless-result.txt");
        var a=typeof(BardBox.Plugin).Assembly;var ready=AccessTools.Field(typeof(BardBox.Plugin),"Ready");
        double deadline=Time.realtimeSinceStartup+90;
        while(!(bool)ready.GetValue(null) && Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.2f);
        yield return new WaitForSecondsRealtime(4);
        string result;
        try
        {
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)throw new Exception("Test did not start without graphics");
            if(!(bool)ready.GetValue(null))throw new Exception("Registration not ready");
            if(debugCalls!=0 || playbackCalls!=0 || duckCalls!=0)throw new Exception("Headless reached client paths: debug="+debugCalls+" playback="+playbackCalls+" duck="+duckCalls);
            if(clockCalls<10 || relayCalls<10)throw new Exception("Headless disabled the server clock/relay loop");
            var plugin=AccessTools.Field(typeof(BardBox.Plugin),"Instance").GetValue(null);
            ((BepInEx.Configuration.ConfigEntry<bool>)AccessTools.Field(typeof(BardBox.Plugin),"ShowAudioRange").GetValue(plugin)).Value=true;
            if(AccessTools.Field(typeof(BardBox.Plugin),"ToggleAudioRangeKey")!=null || AccessTools.Method(a.GetType("BardBox.AudioRangeDebug"),"TextFocused")!=null)throw new Exception("Removed shortcut/focus code still present");
            AccessTools.Method(a.GetType("BardBox.AudioRangeDebug"),"Tick").Invoke(null,null);
            AccessTools.Method(a.GetType("BardBox.AudioRangeDebug"),"Draw").Invoke(null,null);
            if(debugCalls!=1)throw new Exception("Unexpected debug scheduling");
            if(GameObject.Find("BardBox audio range") || GameObject.Find("BardBox radio audio"))throw new Exception("Client presentation created on headless host");
            result="PASS: actual Null graphics startup and item registration; zero scheduled debug, playback or ducking calls; configured debug Tick/Draw safely return; server clock/relay loops continue.";
        }
        catch(Exception e){result="FAIL: "+e;}
        File.WriteAllText(path,result);Logger.LogInfo(result);Application.Quit();
    }
}
