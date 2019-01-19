using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioFormatLib
{
    public class WavFileFormat
    {
        public static void Normalize(string inputFile, out string outputFile)
        {
            outputFile = String.Format("{0}\\{1}_norm.wav", Path.GetDirectoryName(inputFile), Path.GetFileNameWithoutExtension(inputFile));
            float max = 0;
            using (var reader = new AudioFileReader(inputFile))
            {
                // find the max peak
                float[] buffer = new float[reader.WaveFormat.SampleRate];
                int read;
                do
                {
                    read = reader.Read(buffer, 0, buffer.Length);
                    for (int n = 0; n < read; n++)
                    {
                        var abs = Math.Abs(buffer[n]);
                        if (abs > max) max = abs;
                    }
                } while (read > 0);

                if (max == 0 || max > 1.0f) throw new InvalidOperationException("Arquivo não pode ser normalizado");

                // rewind and amplify
                reader.Position = 0;
                reader.Volume = 1.0f / max;

                // write out to a new WAV file
                WaveFileWriter.CreateWaveFile16(outputFile, reader);
            }
        }
    }
}
