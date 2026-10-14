using System;
using System.IO;
using System.Linq;

namespace BardBox;

internal sealed class Song
{
    internal readonly string Tape,File,Title,Artist,Channel,Url,Permission;
    internal Song(string[] columns)
    {Tape=columns[0];File=columns[1];Title=columns[2];Artist=columns[3];Channel=columns[4];Url=columns[5];Permission=columns[6];}
    internal string Display=>Title+(Artist.Length>0?" — "+Artist:"");
}
internal static class SongCatalog
{
    internal static readonly Song[] Songs=Load();
    private static Song[] Load()
    {
        using(var stream=typeof(SongCatalog).Assembly.GetManifestResourceStream("BardBox.Songs.tsv"))
        using(var reader=new StreamReader(stream??throw new InvalidDataException("Song manifest missing.")))
        {
            var songs=reader.ReadToEnd().Split('\n').Where(l=>l.Trim().Length>0&&!l.StartsWith("#"))
                .Select(l=>l.TrimEnd('\r').Split('\t')).Select(c=>c.Length==7?new Song(c):throw new InvalidDataException("Invalid song manifest row.")).ToArray();
            if(songs.Length!=Catalog.Tapes.Length || songs.Select(s=>s.Tape).Distinct().Count()!=songs.Length || songs.Any(s=>Catalog.Find(s.Tape)==null))
                throw new InvalidDataException("Song manifest must map each tape exactly once.");
            return songs;
        }
    }
    internal static Song For(string tape)=>Songs.Single(s=>s.Tape==tape);
}
