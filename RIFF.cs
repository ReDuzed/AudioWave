using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioWave
{
    public sealed class RIFF
    {
        static int sampleRate;
        static int channels;
        static int bitsPerSample;
        static ushort sampleLength;
        const UInt32 PCM = 16;
        public static byte[] WaveFormatBuffer(int sampleRate, int channels, int bitsPerSample, ushort sampleLength, byte[] rawInput = null)
        {
            //FileStream fs = new FileStream(@"C:\Users\Makoto\Music\carbuncle3a.wav", FileMode.Open);
            //byte[] buf = new byte[44];
            //fs.Read(buf, 0, buf.Length);
            byte[] data = rawInput.Skip(44).ToArray();
            byte[] buffer = new byte[6 + rawInput.Length];
            MemoryStream ms = new MemoryStream(buffer, 0, buffer.Length);
            BinaryWriter bw = new BinaryWriter(ms);
            //RIFF?
            bw.Write("RIFF"); // "RIFF" 0x52494646
            //\u0018I\0
            bw.Write((UInt32)(4 + 8 + PCM + 8 + data.Length)); //36 + sampleRate * channels * sampleLength
            //WAVE
            bw.Write("WAVE");   // "WAVE" 0x57415645
            //fmt 
            bw.Write("fmt ");   //"fmt " 0x666d7420
            //\u0012\0\0\0
            bw.Write(PCM);
            bw.Write((UInt32)1);
            //\u0002\0 ??\0\0\0 ?\u0002\0\u0004\0\u0010\0\0\0data ?\u0018
            bw.Write((Int16)channels);
            bw.Write((Int32)sampleRate);
            bw.Write((Int32)(sampleRate * sampleLength * channels / 8));
            bw.Write((Int16)(bitsPerSample * channels / 8.1));
            bw.Write((Int16)bitsPerSample);
            bw.Write("data");
            bw.Write((UInt32)data.Length); //sampleRate * sampleLength
            bw.Write(data);
            bw.Flush();

            //  Getting rid of the 4 byte string prefix in the Header
            ms.Position = 1;                                 // Reset position
            byte[] result = new byte[buffer.Length];         // result buffer
            ms.Read(result, 0, (int)ms.Length);              // Read MemoryStream into result
            var header = result.Take(44).Where(t => t != 4); // Temporary array using Where as replace
            var replace = result.Skip(44).ToList();          // Temporary array skipping result header
            replace.InsertRange(0, header);                  // Inserting new header
            //replace.RemoveAt(4);                           // Removing fifth index: "X"
            result = replace.ToArray();                      // Casting array back into result
            return rawInput;
        }
    }
}
