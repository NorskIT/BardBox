$ErrorActionPreference='Stop'
$destination=Join-Path $PSScriptRoot '../assets/audio'
New-Item -ItemType Directory -Force $destination | Out-Null
Add-Type -TypeDefinition @'
using System;
using System.IO;
public static class BardBoxTestAudio {
 public static void Write(string path,int index) {
  const int rate=44100,frames=176400;
  using(var w=new BinaryWriter(File.Create(path))) {
   w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+frames*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
   w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
   w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(frames*2);
   int[] notes={0,4,7,12,7,4,2,7};
   for(int i=0;i<frames;i++) {
    double t=i/(double)rate;int step=(int)(t*2);double within=t%0.5;
    double freq=110*Math.Pow(2,(index*2+notes[(step+index)%8])/12.0);
    double envelope=Math.Min(1,within/.012)*Math.Min(1,(.5-within)/.04);
    double sample=Math.Sin(2*Math.PI*freq*t)+.22*Math.Sin(2*Math.PI*freq*2*t)+.1*Math.Sin(2*Math.PI*freq*3*t);
    w.Write((short)(sample*envelope*6500));
   }
  }
 }
}
'@
$names=@('trailblazer','second_wind','stonebeat','hauler','heartbeat','steady_rhythm','iron_chorus','tailwind','needle_guard','arcane_pulse','war_drums')
for($i=0;$i -lt $names.Count;$i++){[BardBoxTestAudio]::Write((Join-Path $destination ($names[$i]+'.wav')),$i)}
Write-Output "Created eleven original four-second test loops in $destination"
