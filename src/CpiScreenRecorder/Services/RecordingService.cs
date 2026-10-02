using ScreenRecorderLib;

namespace CpiScreenRecorder.Services;

public sealed class RecordingService : IDisposable
{
    private Recorder? _recorder;
    private string? _currentFile;

    public event EventHandler<string>? RecordingCompleted;
    public event EventHandler<string>? RecordingFailed;
    public event EventHandler<RecorderStatus>? StatusChanged;

    public bool IsRecording => _recorder?.Status is RecorderStatus.Recording or RecorderStatus.Paused;

    public IReadOnlyList<(string FriendlyName, string DeviceName)> GetDisplays()
    {
        return Recorder.GetDisplays()
            .Select(d => (d.FriendlyName ?? d.DeviceName, d.DeviceName))
            .Where(d => !string.IsNullOrWhiteSpace(d.DeviceName))
            .ToList();
    }

    public void Start(
        string deviceName,
        string outputFile,
        int frameRate,
        bool showCursor,
        bool highlightClicks)
    {
        StopAndDisposeRecorder();

        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        _currentFile = outputFile;

        var source = new DisplayRecordingSource(deviceName)
        {
            RecorderApi = RecorderApi.DesktopDuplication,
            IsBorderRequired = false,
            IsCursorCaptureEnabled = showCursor
        };

        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions
            {
                RecordingSources = new List<RecordingSourceBase> { source }
            },
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
                Stretch = StretchMode.Uniform
            },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Framerate = frameRate,
                IsFixedFramerate = true,
                Quality = 94,
                Bitrate = frameRate >= 60 ? 42_000_000 : 28_000_000,
                IsHardwareEncodingEnabled = true,
                IsLowLatencyEnabled = false,
                IsThrottlingDisabled = false,
                IsMp4FastStartEnabled = true,
                IsFragmentedMp4Enabled = false,
                Encoder = new H264VideoEncoder
                {
                    EncoderProfile = H264Profile.High,
                    BitrateMode = H264BitrateControlMode.Quality
                }
            },
            MouseOptions = new MouseOptions
            {
                IsMousePointerEnabled = showCursor,
                IsMouseClicksDetected = highlightClicks,
                MouseClickDetectionMode = MouseDetectionMode.Polling,
                MouseLeftClickDetectionColor = "#22B7FF",
                MouseRightClickDetectionColor = "#126CFF",
                MouseClickDetectionRadius = 18,
                MouseClickDetectionDuration = 180
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = false
            },
            LogOptions = new LogOptions
            {
                IsLogEnabled = true,
                LogSeverityLevel = LogLevel.Warn,
                LogFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CPI Screen Recorder",
                    "recorder.log")
            }
        };

        _recorder = Recorder.CreateRecorder(options);
        _recorder.OnRecordingComplete += Recorder_OnRecordingComplete;
        _recorder.OnRecordingFailed += Recorder_OnRecordingFailed;
        _recorder.OnStatusChanged += Recorder_OnStatusChanged;
        _recorder.Record(outputFile);
    }

    public void Stop()
    {
        if (_recorder is { Status: RecorderStatus.Recording or RecorderStatus.Paused })
            _recorder.Stop();
    }

    private void Recorder_OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        RecordingCompleted?.Invoke(this, e.FilePath ?? _currentFile ?? string.Empty);
    }

    private void Recorder_OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        var detail = string.IsNullOrWhiteSpace(e.Error) ? "ไม่สามารถบันทึกวิดีโอได้" : e.Error;
        RecordingFailed?.Invoke(this, detail);
    }

    private void Recorder_OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        StatusChanged?.Invoke(this, e.Status);
    }

    private void StopAndDisposeRecorder()
    {
        if (_recorder is null)
            return;

        try
        {
            if (_recorder.Status is RecorderStatus.Recording or RecorderStatus.Paused)
                _recorder.Stop();
        }
        catch
        {
        }

        _recorder.OnRecordingComplete -= Recorder_OnRecordingComplete;
        _recorder.OnRecordingFailed -= Recorder_OnRecordingFailed;
        _recorder.OnStatusChanged -= Recorder_OnStatusChanged;
        _recorder.Dispose();
        _recorder = null;
    }

    public void Dispose() => StopAndDisposeRecorder();
}
