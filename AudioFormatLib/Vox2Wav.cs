using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NAudio.Wave;

namespace AudioFormatLib
{
    public class Vox2Wav
    {
        static float signal = 0;
        static int previousStepSizeIndex = 0;
        static bool computedNextStepSizeOnce = false;
        static int[] possibleStepSizes = new int[49] { 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552 };

        public static void Decode(string inputFile, string outputFile, bool normalize)
        {
            string decodedOutput = outputFile + "decoded";
            using (FileStream inputStream = File.Open(inputFile, FileMode.Open))
            using (BinaryReader reader = new BinaryReader(inputStream))
            using (FileStream outputStream = File.Create(decodedOutput))
            using (BinaryWriter writer = new BinaryWriter(outputStream))
            {
                // Note that 32-bit integer values always take up 4 bytes.
                // Note that 16-bit integer values (shorts) always take up 2 bytes.
                // Note that HEX values resolve as 32-bit integers unless casted as something else, such as short values.
                // ChunkID: "RIFF"
                writer.Write(0x46464952);
                // ChunkSize: The size of the entire file in bytes minus 8 bytes for the two fields not included in this count: ChunkID and ChunkSize.
                writer.Write((int)(reader.BaseStream.Length * 4) + 36);
                // Format: "WAVE"
                writer.Write(0x45564157);
                // Subchunk1ID: "fmt " (with the space).
                writer.Write(0x20746D66);
                // Subchunk1Size: 16 for PCM.
                writer.Write(16);
                // AudioFormat: 1 for PCM.
                writer.Write((short)1);
                // NumChannels: 1 for Mono. 2 for Stereo.
                writer.Write((short)1);
                // SampleRate: 8000 is usually the default for VOX.
                writer.Write(8000);
                // ByteRate: SampleRate * NumChannels * BitsPerSample / 8.
                writer.Write(16000);
                // BlockAlign: NumChannels * BitsPerSample / 8. I rounded this up to 2. It sounds best this way.
                writer.Write((short)2);
                // BitsPerSample: I will set this as 16 (16 bits per raw output sample as per the VOX specification).
                writer.Write((short)16);
                // Subchunk2ID: "data"
                writer.Write(0x61746164);
                // Subchunk2Size: NumSamples * NumChannels * BitsPerSample / 8. You can also think of this as the size of the read of the subchunk following this number.
                writer.Write((int)(reader.BaseStream.Length * 4));
                // Write the data stream to the file in linear audio.
                while (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    byte b = reader.ReadByte();
                    float firstDifference = GetDifference((byte)(b / 16));
                    signal += firstDifference;
                    writer.Write(TruncateSignalIfNeeded());
                    float secondDifference = GetDifference((byte)(b % 16));
                    signal += secondDifference;
                    writer.Write(TruncateSignalIfNeeded());
                }
            }
            if (normalize)
            {
                float max = 0;
                using (var reader = new AudioFileReader(decodedOutput))
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
                File.Delete(decodedOutput);
            }
            else
            {
                File.Move(decodedOutput, outputFile);
            }

        }

        static short TruncateSignalIfNeeded()
        {
            // Keep signal truncated to 12 bits since, as per the VOX spec, each 4 bit input has 12 output bits.
            // Note that 12 bits is 0b111111111111. That's 0xFFF in HEX. That's also 4095 in decimal.
            // The sound wave is a signed signal, so factoring in 1 unused bit for the sign, that's 4095/2 rounded down to 2047.
            if (signal > 1023)
            {
                signal = 1023;
            }
            if (signal < -1023)
            {
                signal = -1023;
            }
            return (short)signal;
        }

        static float GetDifference(byte nibble)
        {
            int stepSize = GetNextStepSize(nibble);
            float difference = ((stepSize * GetBit(nibble, 2)) + ((stepSize / 2) * GetBit(nibble, 1)) + (stepSize / 4 * GetBit(nibble, 0)) + (stepSize / 8));
            if (GetBit(nibble, 3) == 1)
            {
                difference = -difference;
            }
            return difference;
        }

        static byte GetBit(byte b, int zeroBasedBitNumber)
        {
            // Shift the bits to the right by the number of the bit you want to get and then logic AND it with 1 to clear bits trailing to the left of your desired bit. 
            return (byte)((b >> zeroBasedBitNumber) & 1);
        }

        static int GetNextStepSize(byte nibble)
        {
            if (!computedNextStepSizeOnce)
            {
                computedNextStepSizeOnce = true;
                return possibleStepSizes[0];
            }
            else
            {
                int magnitude = GetMagnitude(nibble);
                if (previousStepSizeIndex + magnitude > 48)
                {
                    previousStepSizeIndex = previousStepSizeIndex + magnitude;
                    return possibleStepSizes[48];
                }
                else if (previousStepSizeIndex + magnitude > 0)
                {
                    previousStepSizeIndex = previousStepSizeIndex + magnitude;
                    return possibleStepSizes[previousStepSizeIndex];
                }
                else
                {
                    return possibleStepSizes[0];
                }
            }
        }

        static int GetMagnitude(byte nibble)
        {
            if (nibble == 15 || nibble == 7)
                return 8;
            else if (nibble == 14 || nibble == 6)
                return 6;
            else if (nibble == 13 || nibble == 5)
                return 4;
            else if (nibble == 12 || nibble == 4)
                return 2;
            else
                return -1;
        }
    }
}
