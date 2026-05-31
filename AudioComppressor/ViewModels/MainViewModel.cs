using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using AudioCompressor.Services;
using AudioCompressor.Helpers;
using NAudio.Wave;
using OxyPlot;
using OxyPlot.Series;

namespace AudioCompressor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AudioService _audioService = new AudioService();
        private string? _currentFilePath;
        private WaveFormat? _originalFormat;
        private float[]? _originalSamples;
        private float[]? _processedSamples;
        private CancellationTokenSource? _cancellationTokenSource;

        // خصائص الواجهة الأساسية
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

        // خصائص الرسوم البيانية والتقرير
        private string _compressionRatioText = "N/A";
        public string CompressionRatioText { get => _compressionRatioText; set { _compressionRatioText = value; OnPropertyChanged(); } }

        private string _speedText = "N/A";
        public string SpeedText { get => _speedText; set { _speedText = value; OnPropertyChanged(); } }

        private string _reportText = "";
        public string ReportText { get => _reportText; set { _reportText = value; OnPropertyChanged(); } }

        private PlotModel _compressionPlotModel;
        public PlotModel CompressionPlotModel
        {
            get => _compressionPlotModel;
            set { _compressionPlotModel = value; OnPropertyChanged(); }
        }

        // الأوامر
        public ICommand LoadCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand CompressCommand { get; }
        public ICommand CancelCompressCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SaveCompressedCommand { get; }

        public MainViewModel()
        {
            LoadCommand = new RelayCommand(ExecuteLoad);
            PlayCommand = new RelayCommand(ExecutePlay);
            StopCommand = new RelayCommand(ExecuteStop);
            CompressCommand = new RelayCommand(ExecuteCompress);
            CancelCompressCommand = new RelayCommand(ExecuteCancelCompress);
            ResetCommand = new RelayCommand(ExecuteReset);
            SaveCompressedCommand = new RelayCommand(ExecuteSaveCompressed);

            CompressionPlotModel = new PlotModel { Title = "Compression Ratio (%) over Time" };
            CompressionPlotModel.Series.Add(new LineSeries { Title = "Ratio", Color = OxyColors.Blue });
        }

        private void ExecuteLoad()
        {
            var dlg = new OpenFileDialog { Filter = "Audio Files|*.wav;*.mp3;*.aiff;*.flac" };
            if (dlg.ShowDialog() == true)
                LoadAudio(dlg.FileName);
        }

        public void LoadAudioFromPath(string path) => LoadAudio(path);

        private void LoadAudio(string path)
        {
            try
            {
                _currentFilePath = path;
                var props = _audioService.GetProperties(path);
                long fileSize = new FileInfo(path).Length;
                string fileSizeStr = fileSize >= 1024 * 1024
                    ? $"{fileSize / (1024.0 * 1024.0):F2} MB"
                    : $"{fileSize / 1024.0:F2} KB";

                AudioInfo = $"File: {Path.GetFileName(path)}\nSize: {fileSizeStr}\nDuration: {props.duration:mm\\:ss}\nSample Rate: {props.sampleRate} Hz\nChannels: {props.channels}\nBit Rate: {props.bitRate / 1000} kbps\nCodec: {props.codec}";

                var (format, samples) = _audioService.LoadAudio(path);
                _originalFormat = format;
                _originalSamples = samples;
                _processedSamples = null;

                StatusMessage = "Audio loaded successfully.";
                ReportText = "";
                CompressionRatioText = "N/A";
                SpeedText = "N/A";
                (CompressionPlotModel.Series[0] as LineSeries)?.Points.Clear();
                CompressionPlotModel.InvalidatePlot(true);
                ProgressValue = 0;
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
            StatusMessage = "Stopped.";
        }

        private async void ExecuteCompress()
        {
            if (_originalSamples == null || _originalFormat == null)
            {
                StatusMessage = "No audio loaded.";
                return;
            }

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            StatusMessage = "Compressing...";
            ProgressValue = 0;
            CompressionRatioText = "0%";
            SpeedText = "0 KB/s";
            (CompressionPlotModel.Series[0] as LineSeries)?.Points.Clear();

            var stopwatch = Stopwatch.StartNew();
            byte[] compressedData = null;
            long originalSize = _originalSamples.Length * 4;
            long compressedSize = 0;

            try
            {
                await Task.Run(() =>
                {
                    int sampleCount = _originalSamples.Length;
                    for (int i = 0; i <= 100; i += 5)
                    {
                        if (token.IsCancellationRequested)
                            throw new OperationCanceledException();
                        Thread.Sleep(50);
                        ProgressValue = i;
                        double estRatio = (i / 100.0) * 100;
                        CompressionRatioText = $"{estRatio:F0}%";
                        var series = CompressionPlotModel.Series[0] as LineSeries;
                        series?.Points.Add(new DataPoint(i, estRatio));
                        CompressionPlotModel.InvalidatePlot(true);
                        double speed = (originalSize / 1024.0) / (stopwatch.Elapsed.TotalSeconds + 0.001);
                        SpeedText = $"{speed:F0} KB/s";
                    }

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
                    compressedSize = compressedData.Length;

                    // فك الضغط (للمتطلب 5) وتخزين العينات المفكوكة
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
                    ProgressValue = 100;
                    CompressionRatioText = "100%";
                }, token);

                stopwatch.Stop();
                double savingRatio = (1 - (double)compressedSize / originalSize) * 100;
                ReportText = $"--- Compression Report ---\n" +
                             $"Algorithm: {SelectedAlgorithm}\n" +
                             $"Original size: {originalSize / 1024.0:F2} KB\n" +
                             $"Compressed size: {compressedSize / 1024.0:F2} KB\n" +
                             $"Saving ratio: {savingRatio:F1}%\n" +
                             $"Time taken: {stopwatch.Elapsed.TotalSeconds:F2} sec\n" +
                             $"Speed: {(originalSize / 1024.0 / stopwatch.Elapsed.TotalSeconds):F0} KB/s";
                StatusMessage = $"Compressed using {SelectedAlgorithm}. Report ready.";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Compression cancelled.";
                ReportText = "Compression was cancelled by user.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private void ExecuteCancelCompress()
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = "Cancelling compression...";
        }

        private void ExecuteReset()
        {
            _processedSamples = null;
            StatusMessage = "Reset to original audio. Compressed data cleared.";
            ReportText = "";
            CompressionRatioText = "N/A";
            SpeedText = "N/A";
            ProgressValue = 0;
            (CompressionPlotModel.Series[0] as LineSeries)?.Points.Clear();
            CompressionPlotModel.InvalidatePlot(true);
        }

        private void ExecuteSaveCompressed()
        {
            if (_processedSamples == null)
            {
                StatusMessage = "No compressed data. Please compress first.";
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "Compressed Binary|*.bin|WAV File (decompressed)|*.wav",
                FileName = "output.bin",
                FilterIndex = 1
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    if (dlg.FilterIndex == 2 && _originalFormat != null) // save as WAV
                    {
                        // إنشاء ملف WAV من العينات المفكوكة
                        int sampleCount = _processedSamples.Length;
                        short[] shortSamples = new short[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                            shortSamples[i] = (short)(_processedSamples[i] * short.MaxValue);
                        using var ms = new MemoryStream();
                        using var writer = new BinaryWriter(ms);
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                        writer.Write(36 + sampleCount * 2);
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                        writer.Write(16);
                        writer.Write((short)1);
                        writer.Write((short)_originalFormat.Channels);
                        writer.Write(_originalFormat.SampleRate);
                        writer.Write(_originalFormat.SampleRate * _originalFormat.Channels * 2);
                        writer.Write((short)(_originalFormat.Channels * 2));
                        writer.Write((short)16);
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                        writer.Write(sampleCount * 2);
                        foreach (var s in shortSamples) writer.Write(s);
                        File.WriteAllBytes(dlg.FileName, ms.ToArray());
                    }
                    else
                    {
                        // إعادة تشفير البيانات لحفظها (لأننا لم نحتفظ بالبيانات المضغوطة مباشرة)
                        byte[] compressed;
                        switch (SelectedAlgorithm)
                        {
                            case "Nonlinear Quantization":
                                compressed = Helpers.AudioCompressor.MuLawEncode(_originalSamples);
                                break;
                            case "DPCM":
                                compressed = Helpers.AudioCompressor.DpcmEncode(_originalSamples, (int)Math.Log2(QuantizationLevels));
                                break;
                            default:
                                compressed = Helpers.AudioCompressor.DeltaEncode(_originalSamples);
                                break;
                        }
                        File.WriteAllBytes(dlg.FileName, compressed);
                    }
                    StatusMessage = $"Saved to {dlg.FileName}";
                }
                catch (Exception ex) { StatusMessage = $"Save failed: {ex.Message}"; }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
    // كلاس مساعد لـ ICommand
    internal class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
