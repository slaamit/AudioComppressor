using NAudio.Wave;
using System;
using System.IO;

namespace AudioCompressor.Services
{
    public class AudioService
    {
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioReader;

        // قراءة الملف الصوتي وتحويله إلى مصفوفة عينات float
        public (WaveFormat format, float[] samples) LoadAudio(string filePath)
        {
            using var reader = new AudioFileReader(filePath);
            var format = reader.WaveFormat;
            int sampleCount = (int)(reader.Length / 4); // 4 bytes per float
            float[] samples = new float[sampleCount];
            reader.Read(samples, 0, sampleCount);
            return (format, samples);
        }

        // تشغيل الملف من البداية
        public void Play(string filePath)
        {
            Stop();
            _audioReader = new AudioFileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.Play();
        }

        // إيقاف التشغيل وتحرير الموارد
        public void Stop()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _audioReader?.Dispose();
            _waveOut = null;
            _audioReader = null;
        }

        // استخراج خصائص الملف (بدون تحميل العينات كلها)
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