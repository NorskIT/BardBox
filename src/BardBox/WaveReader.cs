using System;
using System.IO;
namespace BardBox;
internal static class WaveReader
{
    internal static float[] Read(byte[] bytes,out int rate,out int channels)
    {
        using var reader=new BinaryReader(new MemoryStream(bytes));
        if(bytes.Length<44 || new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("Expected a RIFF WAV file.");
        reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Expected WAVE.");
        rate=0;channels=0;ushort format=0,bits=0,align=0;byte[]? data=null;
        while(reader.BaseStream.Position+8<=bytes.Length)
        {
            string id=new string(reader.ReadChars(4));uint size=reader.ReadUInt32();long next=reader.BaseStream.Position+size+(size&1);
            if(next>bytes.Length)throw new InvalidDataException("Truncated WAV chunk.");
            if(id=="fmt ")
            {
                if(size<16)throw new InvalidDataException("Invalid WAV format.");
                format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadInt32();align=reader.ReadUInt16();bits=reader.ReadUInt16();
            }
            else if(id=="data")data=reader.ReadBytes(checked((int)size));
            reader.BaseStream.Position=next;
        }
        if(format!=1 || bits!=16 || channels<1 || channels>2 || rate<8000 || rate>96000 || align!=channels*2 || data==null || data.Length==0 || data.Length%align!=0)
            throw new InvalidDataException("Use uncompressed PCM WAV, 16-bit, mono or stereo, 8â€“96 kHz.");
        // Positional music uses mono. Stereo input is downmixed, not played as global stereo music.
        var samples=new float[data.Length/align];
        for(int i=0;i<samples.Length;i++)
        {float sum=0;for(int c=0;c<channels;c++)sum+=BitConverter.ToInt16(data,i*align+c*2)/32768f;samples[i]=sum/channels;}
        channels=1;return samples;
    }
}
