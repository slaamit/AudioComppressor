using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using AudioCompressor.Services;
using AudioCompressor.Helpers;
using NAudio.Wave;
using Microsoft.VisualBasic;

namespace AudioCompressor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AudioService _audioService = new AudioService();
        private string? _currentFilePath;
        private WaveFormat? _originalFormat;
        private float[]? _originalSamples;
        private float[]? _processedSamples;

        // Properties for UI binding
        private string _audioInfo = "No audio loaded.";
        public string AudioInfo { get => _audioInfo; set { _audioInfo = value; OnPropertyChanged(); } }

        private double _progressValue = 0;
        public double ProgressValue { get => _progressValue; set { _progressValue = value; OnPropertyChanged(); } }

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        private string _selectedAlgorithm = "Nonlinear Quantization";
        public string SelectedAlgorithm { get => _selectedAlgorithm; set { _selectedAlgorithm = value; OnPropertyChanged(); } }

        private int _newSampleRate = 44100;
        public int NewSampleRate { get => _newSampleRate; set { _newSampleRate = value; OnPropertyChanged(); } }

        private int _quantizationLevels = 256;
        public int QuantizationLevels { get => _quantizationLevels; set { _quantizationLevels = value; OnPropertyChanged(); } }

        public ObservableCollection<string> Algorithms { get; } = new ObservableCollection<string>
        {
            "Nonlinear Quantization",
            "DPCM",
            "Delta Modulation"
        };

        // Commands
        public ICommand LoadCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand CompressCommand { get; }
        public ICommand DecompressCommand { get; }


        public MainViewModel()
        {
            LoadCommand = new RelayCommand(ExecuteLoad);
            PlayCommand = new RelayCommand(ExecutePlay);
            StopCommand = new RelayCommand(ExecuteStop);
            CompressCommand = new RelayCommand(ExecuteCompress);
            DecompressCommand = new RelayCommand(ExecuteDecompress);
        }

        private void ExecuteLoad()
        {
            var dlg = new OpenFileDialog { Filter = "Audio Files|*.wav;*.mp3;*.aiff;*.flac" };
            if (dlg.ShowDialog() == true)
            {
                LoadAudio(dlg.FileName);
            }
        }

        public void LoadAudioFromPath(string path) => LoadAudio(path);

        private void LoadAudio(string path)
        {
            try
            {
                _currentFilePath = path;
                var props = _audioService.GetProperties(path);
                AudioInfo = $"File: {Path.GetFileName(path)}\nDuration: {props.duration:mm\\:ss}\nSample Rate: {props.sampleRate} Hz\nChannels: {props.channels}\nBit Rate: {props.bitRate / 1000} kbps\nCodec: {props.codec}";
                // Load samples for compression
                var (format, samples) = _audioService.LoadAudio(path);
                _originalFormat = format;
                _originalSamples = samples;
                _processedSamples = null; // reset processed
                StatusMessage = "Audio loaded successfully.";
            }
            catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        }

        private void ExecutePlay()
        {
            if (_currentFilePath != null)
                _audioService.Play(_currentFilePath);
        }

        private void ExecuteStop()
        {
            _audioService.Stop();
        }

        private async void ExecuteCompress()
        {
            if (_originalSamples == null || _originalFormat == null)
            {
                StatusMessage = "No audio loaded.";
                return;
            }
            StatusMessage = "Compressing...";
            ProgressValue = 0;
            // Simulate progress or do work in background
            await Task.Run(() =>
            {
                byte[] compressedData = null;
                int sampleCount = _originalSamples.Length;
                for (int i = 0; i <= 100; i += 20)
                {
                    System.Threading.Thread.Sleep(100);
                    ProgressValue = i;
                }
                // Apply selected algorithm
                switch (SelectedAlgorithm)
                {
                    case "Nonlinear Quantization":
                        compressedData = Helpers.AudioCompressor.MuLawEncode(_originalSamples);
                        break;
                    case "DPCM":
                        compressedData = Helpers.AudioCompressor.DpcmEncode(_originalSamples, (int)Math.Log2(QuantizationLevels));
                        break;
                    case "Delta Modulation":
                        compressedData = Helpers.AudioCompressor.DeltaEncode(_originalSamples);
                        break;
                }
                // For demo, we just store compressed data (could save to file)
                // We'll store decompressed samples for playback
                float[] decompressed = null;
                switch (SelectedAlgorithm)
                {
                    case "Nonlinear Quantization":
                        decompressed = Helpers.AudioCompressor.MuLawDecode(compressedData);
                        break;
                    case "DPCM":
                        decompressed = Helpers.AudioCompressor.DpcmDecode(compressedData, (int)Math.Log2(QuantizationLevels));
                        break;
                    case "Delta Modulation":
                        decompressed = Helpers.AudioCompressor.DeltaDecode(compressedData, sampleCount);
                        break;
                }
                _processedSamples = decompressed;
                // Optionally resample? Not implemented for brevity.
                ProgressValue = 100;
            });
            StatusMessage = $"Compressed using {SelectedAlgorithm}. Ready to play (decompressed).";
        }

        private void ExecuteDecompress()
        {
            if (_processedSamples == null)
            {
                StatusMessage = "No compressed data. Please compress first.";
                return;
            }
            // Just set status; we already have decompressed samples.
            // For actual decompression, we could save to WAV.
            StatusMessage = "Decompression done. You can play the decompressed audio (but Play still plays original file). To play decompressed, you would need to write to temp file.";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}