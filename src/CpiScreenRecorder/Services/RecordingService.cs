using System.IO;
using CpiScreenRecorder.Models;
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

    public IReadOnlyList<DisplayOption> GetDisplays()
    {
        var result = new List<DisplayOption>();

        foreach (var display in Recorder.GetDisplays())
        {
            if (string.IsNullOrWhiteSpace(display.DeviceName))
                continue;

            var width = 0;
            var height = 0;

            try
            {
                var source = new DisplayRecordingSource(display.DeviceName);
                var dimensions = Recorder.GetOutputDimensionsForRecordingSources(
                    new RecordingSourceBase[] { source });

                width = (int)Math.Round(dimensions.CombinedOutputSize.Width);
                height = (int)Math.Round(dimensions.CombinedOutputSize.Height);
            }
            catch
            {
            }

            result.Add(new DisplayOption(
                display.FriendlyName ?? display.DeviceName,
                display.DeviceName,
                width,
                height));
        }

        return result;
    }

    public IReadOnlyList<WindowOption> GetWindows()
    {
        return Recorder.GetWindows()
            .Where(w => w.IsValidWindow() && !string.IsNullOrWhiteSpace(w.Title))
            .Select(w => new WindowOption(w.Title.Trim(), w.Handle, w.Pid.HasValue ? w.Pid.Value : null))
            .OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<AudioDeviceOption> GetMicrophones()
    {
        return Recorder.GetSystemAudioCaptureDevices()
            .Where(d => !string.IsNullOrWhiteSpace(d.DeviceName))
            .Select(d => new AudioDeviceOption(
                d.FriendlyName ?? "Microphone",
                d.DeviceName,
                d.IsDefaultDevice))
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<AudioDeviceOption> GetSystemAudioDevices()
    {
        return Recorder.GetSystemAudioLoopbackDevices()
            .Where(d => !string.IsNullOrWhiteSpace(d.DeviceName))
            .Select(d => new AudioDeviceOption(
                d.FriendlyName ?? "System audio",
                d.DeviceName,
                d.IsDefaultDevice))
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void Start(RecordingRequest request)
    {
        StopAndDisposeRecorder();

        if (string.IsNullOrWhiteSpace(request.OutputFile))
            throw new ArgumentException("Output file is required.", nameof(request));

        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputFile)!);
        _currentFile = request.OutputFile;

        var videoSource = CreateVideoSource(request);
        var outputOptions = CreateOutputOptions(request);
        var audioOptions = CreateAudioOptions(request);

        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions
            {
                RecordingSources = new List<RecordingSourceBase> { videoSource }
            },
            OutputOptions = outputOptions,
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Framerate = request.FrameRate,
                IsFixedFramerate = true,
                Quality = 94,
                Bitrate = request.FrameRate >= 60 ? 42_000_000 : 28_000_000,
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
                IsMousePointerEnabled = request.ShowCursor,
                IsMouseClicksDetected = request.HighlightClicks,
                MouseClickDetectionMode = MouseDetectionMode.Polling,
                MouseLeftClickDetectionColor = "#22B7FF",
                MouseRightClickDetectionColor = "#126CFF",
                MouseClickDetectionRadius = 18,
                MouseClickDetectionDuration = 180
            },
            AudioOptions = audioOptions,
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
        _recorder.Record(request.OutputFile);
    }

    private static RecordingSourceBase CreateVideoSource(RecordingRequest request)
    {
        if (request.CaptureMode == CaptureMode.Window)
        {
            if (request.WindowHandle == IntPtr.Zero)
                throw new InvalidOperationException("กรุณาเลือกหน้าต่างโปรแกรมที่ต้องการบันทึก");

            return new WindowRecordingSource(request.WindowHandle)
            {
                IsCursorCaptureEnabled = request.ShowCursor,
                IsBorderRequired = false,
                Stretch = StretchMode.None
            };
        }

        if (string.IsNullOrWhiteSpace(request.DisplayDeviceName))
            throw new InvalidOperationException("กรุณาเลือกหน้าจอที่ต้องการบันทึก");

        var source = new DisplayRecordingSource(request.DisplayDeviceName)
        {
            RecorderApi = RecorderApi.WindowsGraphicsCapture,
            IsBorderRequired = false,
            IsCursorCaptureEnabled = request.ShowCursor,
            Stretch = StretchMode.None
        };

        if (request.CaptureMode == CaptureMode.Region)
        {
            var region = request.Region;
            if (region is null || !region.IsValid)
                throw new InvalidOperationException("กรุณาเลือกพื้นที่หน้าจอที่ต้องการบันทึก");

            var width = MakeEven(region.Width);
            var height = MakeEven(region.Height);
            source.SourceRect = new ScreenRect(region.X, region.Y, width, height);
            source.OutputSize = new ScreenSize(width, height);
        }

        return source;
    }

    private static OutputOptions CreateOutputOptions(RecordingRequest request)
    {
        var output = new OutputOptions
        {
            RecorderMode = RecorderMode.Video,
            Stretch = StretchMode.None
        };

        if (request.CaptureMode == CaptureMode.Region && request.Region is { IsValid: true } region)
        {
            var width = MakeEven(region.Width);
            var height = MakeEven(region.Height);
            output.SourceRect = new ScreenRect(0, 0, width, height);
            output.OutputFrameSize = new ScreenSize(width, height);
        }

        return output;
    }

    private static AudioOptions CreateAudioOptions(RecordingRequest request)
    {
        var sources = new List<AudioSourceBase>();

        if (request.RecordMicrophone && !string.IsNullOrWhiteSpace(request.MicrophoneDeviceName))
        {
            sources.Add(new CaptureAudioSource(request.MicrophoneDeviceName)
            {
                Volume = ClampVolume(request.MicrophoneVolume),
                ForceMono = false
            });
        }

        if (request.RecordSystemAudio && !string.IsNullOrWhiteSpace(request.SystemAudioDeviceName))
        {
            sources.Add(new LoopbackAudioSource(request.SystemAudioDeviceName)
            {
                Volume = ClampVolume(request.SystemAudioVolume)
            });
        }

        return new AudioOptions
        {
            IsAudioEnabled = sources.Count > 0,
            Bitrate = AudioBitrate.bitrate_192kbps,
            Channels = AudioChannels.Stereo,
            AudioSources = sources
        };
    }

    private static float ClampVolume(int value)
        => Math.Clamp(value, 0, 100) / 100f;

    private static int MakeEven(int value)
    {
        value = Math.Max(32, value);
        return value % 2 == 0 ? value : value - 1;
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
