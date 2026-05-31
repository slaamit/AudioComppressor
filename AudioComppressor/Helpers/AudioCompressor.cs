using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace AudioCompressor.Helpers
{
    public static class AudioCompressor
    {
        // 1. Nonlinear quantization (μ-law companding)
        public static byte[] MuLawEncode(float[] samples)
        {
            byte[] compressed = new byte[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                float sample = samples[i];
                // μ-law formula (μ=255) map float [-1,1] to byte [0,255]
                double sign = sample >= 0 ? 1 : -1;
                double absSample = Math.Min(Math.Abs(sample), 1.0);
                double compressedVal = sign * (Math.Log(1 + 255 * absSample) / Math.Log(1 + 255));
                compressed[i] = (byte)((compressedVal + 1) / 2 * 255);
            }
            return compressed;
        }

        public static float[] MuLawDecode(byte[] compressed)
        {
            float[] samples = new float[compressed.Length];
            for (int i = 0; i < compressed.Length; i++)
            {
                double val = compressed[i] / 255.0 * 2 - 1;
                double sign = val >= 0 ? 1 : -1;
                double absVal = Math.Abs(val);
                double sample = sign * (1.0 / 255) * (Math.Pow(1 + 255, absVal) - 1);
                samples[i] = (float)sample;
            }
            return samples;
        }

        // 2. DPCM (simple 4-bit quantized difference)
        public static byte[] DpcmEncode(float[] samples, int bits = 4)
        {
            int maxDiff = (int)Math.Pow(2, bits) - 1;
            List<byte> encoded = new List<byte>();
            float prev = 0;
            foreach (float s in samples)
            {
                float diff = s - prev;
                int quantizedDiff = (int)((diff + 1) / 2 * maxDiff);
                quantizedDiff = Math.Clamp(quantizedDiff, 0, maxDiff);
                encoded.Add((byte)quantizedDiff);
                // Dequantize and update predictor for next step (using same decoder logic)
                float dequantDiff = (float)quantizedDiff / maxDiff * 2 - 1;
                prev = prev + dequantDiff;
                // clamp prev to [-1,1]
                prev = Math.Clamp(prev, -1, 1);
            }
            return encoded.ToArray();
        }

        public static float[] DpcmDecode(byte[] encoded, int bits = 4)
        {
            int maxDiff = (int)Math.Pow(2, bits) - 1;
            float[] samples = new float[encoded.Length];
            float prev = 0;
            for (int i = 0; i < encoded.Length; i++)
            {
                float dequantDiff = (float)encoded[i] / maxDiff * 2 - 1;
                float sample = prev + dequantDiff;
                sample = Math.Clamp(sample, -1, 1);
                samples[i] = sample;
                prev = sample;
            }
            return samples;
        }

        // 3. Delta Modulation (1 bit per sample)
        public static byte[] DeltaEncode(float[] samples, float stepSize = 0.05f)
        {
            byte[] encoded = new byte[(samples.Length + 7) / 8]; // pack bits
            float prev = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                int bit = (samples[i] > prev) ? 1 : 0;
                // set bit in byte array
                int byteIdx = i / 8;
                int bitIdx = i % 8;
                if (bit == 1)
                    encoded[byteIdx] |= (byte)(1 << bitIdx);
                // update prev: if bit=1, increase by step, else decrease
                prev += (bit == 1 ? stepSize : -stepSize);
                prev = Math.Clamp(prev, -1, 1);
            }
            return encoded;
        }

        public static float[] DeltaDecode(byte[] encoded, int sampleCount, float stepSize = 0.05f)
        {
            float[] samples = new float[sampleCount];
            float prev = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                int byteIdx = i / 8;
                int bitIdx = i % 8;
                int bit = (encoded[byteIdx] >> bitIdx) & 1;
                prev += (bit == 1 ? stepSize : -stepSize);
                prev = Math.Clamp(prev, -1, 1);
                samples[i] = prev;
            }
            return samples;
        }
    }
}