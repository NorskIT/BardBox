using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEngine;

namespace BardBox;

internal static class AudioLibrary
{
    private static readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    private static readonly Dictionary<string,double[]> cues=new Dictionary<string,double[]>();
    private static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    internal static string Hash(string id)=>hashes.TryGetValue(id,out var hash)?hash:"";
    internal static double[] Cues(string id)=>cues.TryGetValue(id,out var times)?times:Array.Empty<double>();
    internal static void Load(string folder,Action<string> warn)
    {
        foreach(var tape in Catalog.Tapes)
        {
            try
            {
                var path=Path.Combine(folder,"songs",tape.Id+".wav");
                var info=new FileInfo(path);
                if(info.Length>120*1024*1024)throw new InvalidDataException("WAV exceeds 120 MB.");
                var bytes=File.ReadAllBytes(path);
                var data=WaveReader.Read(bytes,out var rate,out var channels);
                using(var sha=SHA256.Create())hashes.Add(tape.Id,BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""));
                var clip=AudioClip.Create(tape.Id,data.Length,channels,rate,false);
                if(!clip.SetData(data,0))throw new InvalidDataException("Could not upload audio samples.");
                clips.Add(tape.Id,clip);
                cues.Add(tape.Id,MusicOnsets.Analyze(data,rate));
            }
            catch(Exception e){warn("Cannot load songs/"+tape.Id+".wav: "+e.Message+". This tape is silent locally; its buff still works.");}
        }
    }
    internal static AudioClip? Get(string id)=>clips.TryGetValue(id,out var clip)?clip:null;
    internal static void Clear(){foreach(var clip in clips.Values)UnityEngine.Object.Destroy(clip);clips.Clear();cues.Clear();hashes.Clear();}
}
