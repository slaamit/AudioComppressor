using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using System;
using Microsoft.VisualBasic;

namespace AudioCompressor.Services
{
    public class AudioService
    {
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioReader;

        // قراءة ملف صوتي واستخراج WaveFormat وعينات
        public (WaveFormat format, float[] samples) LoadAudio(string filePath)
        {
            var reader = new AudioFileReader(filePath);
            var format = reader.WaveFormat;
            int sampleCount = (int)(reader.Length / 4); // float = 4 bytes
            float[] samples = new float[sampleCount];
            reader.Read(samples, 0, sampleCount);
            reader.Dispose();
            return (format, samples);
        }

        // تشغيل الملف
        public void Play(string filePath)
        {
            Stop();
            _audioReader = new AudioFileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.Play();
        }

        public void Stop()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _audioReader?.Dispose();
        }

        // استخراج خصائص الملف بدون تحميل العينات كلها
        public (TimeSpan duration, int sampleRate, int channels, int bitRate, string codec) GetProperties(string filePath)
        {
            using var reader = new AudioFileReader(filePath);
            var duration = reader.TotalTime;
            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;
            int bitRate = reader.WaveFormat.AverageBytesPerSecond * 8;
            string codec = reader.WaveFormat.Encoding.ToString();
            return (duration, sampleRate, channels, bitRate, codec);
        }


    }
}