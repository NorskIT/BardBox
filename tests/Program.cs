using System;
using System.IO;
using System.Linq;
using BardBox;

int count=0;
void Assert(bool value,string message){if(!value)throw new Exception(message);count++;}
foreach(var gain in new[]{0f,.5f,1f,2f,5f,10f,-1f,float.NaN,float.PositiveInfinity})
{
    var samples=new[]{-.1f,0f,.1f,.8f,-.8f};
    var expectedGain=float.IsNaN(gain)||float.IsInfinity(gain)?1:Math.Clamp(gain,0,5);
    var expected=samples.Select(x=>Math.Clamp(x*expectedGain,-1,1)).ToArray();
    AudioGain.Apply(samples,gain);
    Assert(samples.SequenceEqual(expected),"PCM gain, mute, saturation and bounds: "+gain);
}
var louder=new[]{.1f,-.1f};AudioGain.Apply(louder,5);
Assert(louder[0]==.5f && louder[1]==-.5f,"500% amplifies PCM beyond AudioSource's 100% limit");
Assert(RadioRules.Range(float.NaN)==500 && RadioRules.Range(float.PositiveInfinity)==500,"Invalid audio range fallback");
Assert(RadioRules.Range(0)==1 && RadioRules.Range(2500)==2000,"Audio range bounds");
foreach(float radius in new[]{1f,20f,500f,2000f})
{
    Assert(RadioRules.Attenuation(0,radius)==1,"Full near gain "+radius);
    Assert(RadioRules.Attenuation(radius,radius)==0 && RadioRules.Attenuation(radius+1,radius)==0,"Hard range boundary "+radius);
    Assert(RadioRules.Attenuation(radius*.75f,radius)>0 && RadioRules.Attenuation(radius*.75f,radius)<1,"Gradual distance attenuation "+radius);
}
Assert(RadioRules.Attenuation(float.NaN,500)==0,"Invalid position silenced");
Assert(RadioRules.Fresh(10,7) && !RadioRules.Fresh(10,6.5) && !RadioRules.Fresh(10,12),"Lease timeout and future heartbeat");
Assert(RadioRules.NewSequence(2,1) && !RadioRules.NewSequence(1,2) && !RadioRules.NewSequence(2,2),"Old and duplicate snapshots rejected");
var session=Guid.NewGuid().ToString("N");var hash=new string('A',64);
Assert(RadioRules.Valid("trailblazer",1,session,hash,5,10),"Valid radio announcement");
Assert(!RadioRules.Valid("missing",1,session,hash,5,10) && !RadioRules.Valid("trailblazer",4,session,hash,5,10),"Invalid tape/quality rejected");
Assert(!RadioRules.Valid("trailblazer",1,"bad",hash,5,10) && !RadioRules.Valid("trailblazer",1,session,new string('Z',64),5,10),"Malformed session/hash rejected");
Assert(!RadioRules.Valid("trailblazer",1,session,hash,double.NaN,10) && !RadioRules.Valid("trailblazer",1,session,hash,12,10),"Invalid/future start rejected");
var contributions=new Contributions();
var input=new PlaybackInput();
input.Update(true,false,false);Assert(input.Playing,"Hold starts music");
input.Update(false,false,false);Assert(!input.Playing,"Release stops hold mode");
input.Update(false,false,true);input.Update(true,false,true);Assert(input.Playing,"Toggle starts on press");
input.Update(true,false,true);input.Update(false,false,true);Assert(input.Playing,"Holding/releasing does not retrigger toggle");
input.Update(true,false,true);Assert(!input.Playing,"Second toggle press stops");
input.Update(false,false,true);input.Update(true,false,true);Assert(input.Playing,"Third press starts again");
input.Update(true,true,true);Assert(!input.Playing,"Interruption stops toggle");
input.Update(true,false,true);Assert(!input.Playing,"Held key cannot restart after interruption");
input.Update(false,false,true);input.Update(true,false,true);Assert(input.Playing,"Release then press resumes toggle");
input.Update(true,false,false);Assert(!input.Playing,"Changing control mode stops playback");
input.Update(false,false,false);input.Update(true,false,false);Assert(input.Playing,"New hold works after setting change");
foreach(var tape in Catalog.Tapes)
for(int q=1;q<=3;q++)
    Assert(tape.HudName(q).Contains(tape.Value(q).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)) && !tape.HudName(q).Contains(tape.Name),"HUD describes current bonus: "+tape.Id+q);
Assert(Catalog.Tapes[0].HudName(2)=="♫ Movement speed +7.5%","Exact movement HUD");
Assert(MusicOnsets.Analyze(new float[80000],8000).Length==0,"Silence has no onsets");
var constant=Enumerable.Range(0,80000).Select(i=>(float)(.3*Math.Sin(i*2*Math.PI*80/8000))).ToArray();
Assert(MusicOnsets.Analyze(constant,8000).Length==0,"Constant tone has no drops");
var bursts=new float[80000];
foreach(double onset in new[]{2.0,2.3,5.0,8.0})
for(int i=(int)(onset*8000);i<(int)((onset+.15)*8000);i++)bursts[i]=(float)(.6*Math.Sin(i*2*Math.PI*80/8000));
var detected=MusicOnsets.Analyze(bursts,8000);
Assert(detected.Length==3,"Detect separate bass bursts, suppress close repeat");
Assert(detected.SequenceEqual(MusicOnsets.Analyze(bursts,8000)),"Deterministic analysis");
Assert(detected.Zip(detected.Skip(1),(a,b)=>b-a).All(d=>d>=1.5),"Onset cooldown");
var clock=new CueClock();var cueTimes=new[]{.2,1.8};
Assert(!clock.Advance("song1",0,2,cueTimes),"No cue on entry");
Assert(clock.Advance("song1",.25,2,cueTimes),"Crossing triggers");
Assert(!clock.Advance("song1",.3,2,cueTimes),"No repeat");
Assert(!clock.Advance("song1",1.7,2,cueTimes),"Seek skips history");
Assert(!clock.Advance("song1",1.9,2,cueTimes),"Prune closing cue too near next opening cue");
Assert(clock.Advance("song1",2.25,2,cueTimes),"Opening cue consistently survives loop boundary");
var newcomer=new CueClock();newcomer.Advance("song1",2.0,2,cueTimes);
Assert(newcomer.Advance("song1",2.25,2,cueTimes),"New listener agrees with existing listener across loop boundary");
var loopClock=new CueClock();loopClock.Advance("loop",1.9,2,new[]{.2});
Assert(loopClock.Advance("loop",2.25,2,new[]{.2}),"Cue after loop");
Assert(!clock.Advance("song2",2.3,2,cueTimes),"Tape switch resets");
Assert(!clock.Advance("song2",0,2,cueTimes),"Backward clock resets");
clock.Reset();Assert(!clock.Advance("song2",8.25,2,cueTimes),"Range reentry skips history");
var otherClock=new CueClock();clock.Reset();
clock.Advance("same",0,2,cueTimes);otherClock.Advance("same",0,2,cueTimes);
Assert(clock.Advance("same",.25,2,cueTimes)==otherClock.Advance("same",.25,2,cueTimes),"Independent clients agree");
contributions.Observe("a","trailblazer",1,0);contributions.Observe("b","trailblazer",3,0);
Assert(contributions.Resolve(.1)["trailblazer"]==3,"Strongest source wins");
contributions.Observe("a","trailblazer",1,1.9);
Assert(contributions.Resolve(2.01)["trailblazer"]==1,"Weaker source resumes after strong source expires");
contributions.Observe("a","second_wind",2,2.02);
Assert(contributions.Resolve(2.1).Count==2,"Different tapes combine");
Assert(contributions.Resolve(4.03).Count==0,"All sources expire");
contributions.Observe("a","unknown",1,5);contributions.Observe("b","trailblazer",4,5);
Assert(contributions.Resolve(5).Count==0,"Reject invalid tape and quality");
contributions.Observe("a","hauler",3,6);contributions.Clear();
Assert(contributions.Resolve(6).Count==0,"Clear on death/logout");
Assert(Catalog.Tapes.Length==11,"11 tapes");
Assert(Catalog.Tapes.Select(t=>t.Id).Distinct().Count()==11,"Unique tape IDs");
Assert(Catalog.Tapes.Count(t=>t.Trophy.Length==0)==1 && Catalog.Tapes.Single(t=>t.Trophy.Length==0).Bonus==Bonus.Sail,"Only sailing has no boss gate");
foreach(var t in Catalog.Tapes)
{
    Assert(t.Values.Length==3 && t.Values[0]<t.Values[1] && t.Values[1]<t.Values[2],t.Id+" upgrades");
    Assert(t.Materials.All(m=>m.Count>0),t.Id+" positive costs");
    Assert(!string.IsNullOrWhiteSpace(t.Description(3)),t.Id+" tooltip");
}
byte[] MakeWave(short[] samples,ushort channels=1,bool junk=false)
{
    using var memory=new MemoryStream();using var w=new BinaryWriter(memory);
    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(0);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
    if(junk){w.Write(System.Text.Encoding.ASCII.GetBytes("JUNK"));w.Write(3);w.Write(new byte[]{1,2,3,0});}
    w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));w.Write(16);w.Write((ushort)1);w.Write(channels);w.Write(44100);w.Write(44100*channels*2);w.Write((ushort)(channels*2));w.Write((ushort)16);
    w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(samples.Length*2);foreach(var s in samples)w.Write(s);
    w.Seek(4,SeekOrigin.Begin);w.Write((int)memory.Length-8);return memory.ToArray();
}
var mono=WaveReader.Read(MakeWave(new short[]{-32768,0,32767},junk:true),out var rate,out var channel);
Assert(rate==44100 && channel==1 && mono.Length==3,"WAV metadata and padded chunk");
Assert(mono[0]==-1 && mono[1]==0 && mono[2]>.99,"PCM conversion");
var stereo=WaveReader.Read(MakeWave(new short[]{32767,-32767,1000,1000},2),out _,out _);
Assert(stereo.Length==2 && stereo[0]==0 && Math.Abs(stereo[1]-1000/32768f)<.0001,"Stereo downmix");
bool rejected=false;try{WaveReader.Read(new byte[10],out _,out _);}catch(InvalidDataException){rejected=true;}
Assert(rejected,"Malformed WAV rejected");
var malformed=MakeWave(new short[]{1,2});Array.Resize(ref malformed,malformed.Length-1);rejected=false;
try{WaveReader.Read(malformed,out _,out _);}catch(InvalidDataException){rejected=true;}
Assert(rejected,"Truncated WAV rejected");
Assert(PlaybackRules.Sample(0,44100,176400)==0,"New press restarts at zero");
Assert(PlaybackRules.Sample(5.5,44100,176400)==66150,"Late listener joins current loop position");
Assert(PlaybackRules.Sample(4,44100,176400)==0,"Exact loop boundary");
Assert(PlaybackRules.Sample(-1,44100,176400)==0,"Clock skew cannot create negative sample");
Assert(PlaybackRules.Valid("trailblazer",2,1,1,true,false),"Valid replicated playback");
Assert(!PlaybackRules.Valid("trailblazer",2,1,3.5,true,false),"Old heartbeat expires");
Assert(!PlaybackRules.Valid("trailblazer",2,1,-5,true,false),"Future heartbeat rejected");
Assert(!PlaybackRules.Valid("trailblazer",2,1,1,false,false),"Remote player must visibly hold box");
Assert(!PlaybackRules.Valid("trailblazer",2,1,1,true,true),"Dead musician rejected");
Assert(!PlaybackRules.Valid("unknown",2,1,1,true,false),"Unknown replicated tape rejected");
Assert(!PlaybackRules.Valid("trailblazer",4,1,1,true,false),"Invalid replicated quality rejected");
var project=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
SyncSimulation.Run(Assert);
var expectedSongs=new[]{"fishy","Rubbin' And Tuggin' My Nips","Storm-Crowned","Where There's a Whip There's a Way!","brevik sangen","Noise Of Scandinavia (Preview)","Final Fantasy 7 Boss battle","Drunken Sailor","Pickle Rick Trap Remix","Xanadu (Extended Version)","Advent: One-Winged Angel"};
Assert(SongCatalog.Songs.Length==11,"Eleven song metadata entries");
for(int i=0;i<Catalog.Tapes.Length;i++)
{
    var song=SongCatalog.For(Catalog.Tapes[i].Id);
    Assert(song.Title==expectedSongs[i],"Approved song mapping: "+song.Tape);
    Assert(File.Exists(Path.Combine(project,"assets-source",song.File)),"Preserved original: "+song.Tape);
}
foreach(var tape in Catalog.Tapes)
{
    var samples=WaveReader.Read(File.ReadAllBytes(Path.Combine(project,"assets/audio",tape.Id+".wav")),out var hz,out var ch);
    Assert(hz>=8000 && hz<=96000 && ch==1 && samples.Length>0,tape.Id+" valid packaged clip (also accepts replacement songs)");
    Assert(samples.All(s=>s>=-1 && s<=1),tape.Id+" no clipping");
    var onsets=MusicOnsets.Analyze(samples,hz);
    Console.WriteLine(tape.Id+": "+(samples.Length/(double)hz).ToString("0.00")+" s, "+onsets.Length+" shockwaves");
    Assert(onsets.All(t=>t>=0 && t<samples.Length/(double)hz),"Valid cue times "+tape.Id);
    if(new[]{"trailblazer","second_wind","steady_rhythm","tailwind"}.Contains(tape.Id))Assert(onsets.Length>0,"Real song detects onsets "+tape.Id);
}
foreach(var id in Catalog.Tapes.Select(t=>t.Id).Concat(new[]{"boombox","blank_tape"}))
{
    var png=File.ReadAllBytes(Path.Combine(project,"assets/icons",id+".png"));
    int width=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4));
    int height=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4));
    Assert(width==128 && height==128 && png[25]==6,id+" transparent RGBA icon dimensions");
}
var game=Environment.GetEnvironmentVariable("VALHEIM_INSTALL")??@"C:\Program Files (x86)\Steam\steamapps\common\Valheim";
using(var stream=File.OpenRead(Path.Combine(game,"valheim_Data/Managed/assembly_valheim.dll")))
using(var pe=new System.Reflection.PortableExecutable.PEReader(stream))
{
    var metadata=System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
    bool Method(string type,string name,int parameters)
    {
        foreach(var handle in metadata.TypeDefinitions)
        {
            var definition=metadata.GetTypeDefinition(handle);if(metadata.GetString(definition.Name)!=type)continue;
            foreach(var methodHandle in definition.GetMethods())
            {
                var method=metadata.GetMethodDefinition(methodHandle);
                if(metadata.GetString(method.Name)==name && method.GetParameters().Count(h=>metadata.GetParameter(h).SequenceNumber>0)==parameters)return true;
            }
        }
        return false;
    }
    Assert(Method("ItemData","GetTooltip",6),"Installed game tooltip Harmony target");
    Assert(Method("Player","SetControls",12),"Installed game control Harmony target");
    Assert(Method("Ship","GetSailForce",2),"Installed game sail Harmony target");
}
Console.WriteLine($"PASS: {count} assertions");
