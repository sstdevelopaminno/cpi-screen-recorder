using System.IO;
using CpiScreenRecorder.Models;
using ScreenRecorderLib;

namespace CpiScreenRecorder.Services;

public sealed class RecordingService : IDisposable
{
    private readonly object _sync = new();

    private Recorder? _recorder;
    private string? _currentFile;
    private TaskCompletionSource<bool>? _recordingFinished;
    private bool _isStopping;

    public event EventHandler<string>? RecordingCompleted;
    public event EventHandler<string>? RecordingFailed;
    public event EventHandler<RecorderStatus>? StatusChanged;

    public bool IsRecording
    {
        get
        {
            lock (_sync)
            {
                return _recorder?.Status is RecorderStatus.Recording or RecorderStatus.Paused;
            }
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _recorder?.Status == RecorderStatus.Paused;
            }
        }
    }

    public bool IsBusy
    {
        get
        {
            lock (_sync)
            {
                return _isStopping
                       || _recorder?.Status is RecorderStatus.Recording or RecorderStatus.Paused;
            }
        }
    }

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
                var source = new DisplayRecordingSource(display.DeviceName)
                {
                    RecorderApi = RecorderApi.DesktopDuplication
                };

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
            .Where(w => w.IsValidWindow()
                        && !w.IsMinmimized()
                        && !string.IsNullOrWhiteSpace(w.Title))
            .Select(w => new WindowOption(
                w.Title.Trim(),
                w.Handle,
                w.Pid.HasValue ? w.Pid.Value : null))
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

    public IReadOnlyList<CameraDeviceOption> GetCameras()
    {
        return Recorder.GetSystemVideoCaptureDevices()
            .Where(c => !string.IsNullOrWhiteSpace(c.DeviceName))
            .Select(c => new CameraDeviceOption(
                c.FriendlyName ?? "Camera",
                c.DeviceName))
            .OrderBy(c => c.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void Start(RecordingRequest request)
    {
        ReleaseIdleRecorder();

        if (string.IsNullOrWhiteSpace(request.OutputFile))
            throw new ArgumentException("Output file is required.", nameof(request));

        if (IsBusy)
            throw new InvalidOperationException(
                "ตัวบันทึกกำลังทำงานหรือกำลังปิดไฟล์ กรุณารอสักครู่แล้วลองอีกครั้ง");

        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputFile)!);
        _currentFile = request.OutputFile;

        var videoSource = CreateAndValidateVideoSource(request);
        var outputOptions = CreateOutputOptions(request);
        var audioOptions = CreateAudioOptions(request);
        var overlayOptions = CreateOverlayOptions(request);

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
                Quality = 92,
                Bitrate = request.FrameRate >= 60 ? 36_000_000 : 24_000_000,

                // Software encoding is intentionally used in the stability build.
                // It avoids GPU/driver encoder conflicts that can leave a capture
                // session stuck while finalizing on some Windows 10 PCs.
                IsHardwareEncodingEnabled = false,

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
            OverlayOptions = overlayOptions,
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

        var recorder = Recorder.CreateRecorder(options);
        recorder.OnRecordingComplete += Recorder_OnRecordingComplete;
        recorder.OnRecordingFailed += Recorder_OnRecordingFailed;
        recorder.OnStatusChanged += Recorder_OnStatusChanged;

        lock (_sync)
        {
            _recorder = recorder;
            _isStopping = false;
            _recordingFinished = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        try
        {
            recorder.Record(request.OutputFile);
        }
        catch
        {
            ForceDetachRecorder(recorder);
            throw;
        }
    }

    private RecordingSourceBase CreateAndValidateVideoSource(RecordingRequest request)
    {
        RecordingSourceBase source = request.CaptureMode switch
        {
            CaptureMode.Window => CreateStableWindowSource(request),
            CaptureMode.Display => CreateDisplaySource(request),
            CaptureMode.Region => CreateRegionSource(request),
            _ => throw new InvalidOperationException("ไม่รู้จักโหมดการบันทึกที่เลือก")
        };

        if (!IsSourceUsable(source))
        {
            throw new InvalidOperationException(
                "ไม่สามารถเตรียมแหล่งภาพสำหรับบันทึกได้ กรุณากดรีเฟรชแล้วเลือกหน้าจอหรือโปรแกรมใหม่");
        }

        return source;
    }

    private RecordingSourceBase CreateStableWindowSource(RecordingRequest request)
    {
        var currentWindow = ResolveCurrentWindow(request);

        if (currentWindow is null)
        {
            throw new InvalidOperationException(
                "หน้าต่างโปรแกรมที่เลือกเปลี่ยนหรือปิดไปแล้ว กรุณากด “รีเฟรช” และเลือกโปรแกรมใหม่");
        }

        // Windows 11 handles isolated Window Graphics Capture reliably.
        // Windows 10 can return a stale/unsupported HWND for some desktop apps.
        // On Windows 10 we therefore capture the exact on-screen window rectangle
        // from Desktop Duplication. This avoids the 'No valid recording sources'
        // failure seen on the production test PC.
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            var directWindow = new WindowRecordingSource(currentWindow)
            {
                IsCursorCaptureEnabled = request.ShowCursor,
                IsBorderRequired = false,
                Stretch = StretchMode.None
            };

            if (IsSourceUsable(directWindow))
                return directWindow;
        }

        if (!WindowCaptureInfoService.TryGetWindowCrop(
                currentWindow.Handle,
                out var crop)
            || crop is null)
        {
            throw new InvalidOperationException(
                "ไม่สามารถอ่านขอบเขตของหน้าต่างโปรแกรมนี้ได้ กรุณาเปิดหน้าต่างให้แสดงบนจอและอย่าย่อโปรแกรมก่อนเริ่มบันทึก");
        }

        return new DisplayRecordingSource(crop.DisplayDeviceName)
        {
            RecorderApi = RecorderApi.DesktopDuplication,
            IsBorderRequired = false,
            IsCursorCaptureEnabled = request.ShowCursor,
            Stretch = StretchMode.None,
            SourceRect = new ScreenRect(crop.X, crop.Y, crop.Width, crop.Height),
            OutputSize = new ScreenSize(crop.Width, crop.Height)
        };
    }

    private static RecordableWindow? ResolveCurrentWindow(RecordingRequest request)
    {
        var windows = Recorder.GetWindows()
            .Where(w => w.IsValidWindow()
                        && !w.IsMinmimized()
                        && !string.IsNullOrWhiteSpace(w.Title))
            .ToList();

        var exactHandle = windows.FirstOrDefault(
            w => request.WindowHandle != IntPtr.Zero
                 && w.Handle == request.WindowHandle);

        if (exactHandle is not null)
            return exactHandle;

        if (request.WindowProcessId.HasValue)
        {
            var sameProcessAndTitle = windows.FirstOrDefault(
                w => w.Pid.HasValue
                     && w.Pid.Value == request.WindowProcessId.Value
                     && !string.IsNullOrWhiteSpace(request.WindowTitle)
                     && string.Equals(
                         w.Title?.Trim(),
                         request.WindowTitle.Trim(),
                         StringComparison.CurrentCultureIgnoreCase));

            if (sameProcessAndTitle is not null)
                return sameProcessAndTitle;

            var sameProcess = windows.FirstOrDefault(
                w => w.Pid.HasValue
                     && w.Pid.Value == request.WindowProcessId.Value);

            if (sameProcess is not null)
                return sameProcess;
        }

        if (!string.IsNullOrWhiteSpace(request.WindowTitle))
        {
            return windows.FirstOrDefault(
                w => string.Equals(
                    w.Title?.Trim(),
                    request.WindowTitle.Trim(),
                    StringComparison.CurrentCultureIgnoreCase));
        }

        return null;
    }

    private static RecordingSourceBase CreateDisplaySource(RecordingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayDeviceName))
            throw new InvalidOperationException("กรุณาเลือกหน้าจอที่ต้องการบันทึก");

        return new DisplayRecordingSource(request.DisplayDeviceName)
        {
            RecorderApi = RecorderApi.DesktopDuplication,
            IsBorderRequired = false,
            IsCursorCaptureEnabled = request.ShowCursor,
            Stretch = StretchMode.None
        };
    }

    private static RecordingSourceBase CreateRegionSource(RecordingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayDeviceName))
            throw new InvalidOperationException("กรุณาเลือกหน้าจอที่ต้องการบันทึก");

        var region = request.Region;
        if (region is null || !region.IsValid)
            throw new InvalidOperationException("กรุณาเลือกพื้นที่หน้าจอที่ต้องการบันทึก");

        var width = MakeEven(region.Width);
        var height = MakeEven(region.Height);

        return new DisplayRecordingSource(request.DisplayDeviceName)
        {
            RecorderApi = RecorderApi.DesktopDuplication,
            IsBorderRequired = false,
            IsCursorCaptureEnabled = request.ShowCursor,
            Stretch = StretchMode.None,
            SourceRect = new ScreenRect(region.X, region.Y, width, height),
            OutputSize = new ScreenSize(width, height)
        };
    }

    private static bool IsSourceUsable(RecordingSourceBase source)
    {
        try
        {
            var dimensions = Recorder.GetOutputDimensionsForRecordingSources(
                new[] { source });

            return dimensions.CombinedOutputSize.Width >= 32
                   && dimensions.CombinedOutputSize.Height >= 32;
        }
        catch
        {
            return false;
        }
    }

    private static OutputOptions CreateOutputOptions(RecordingRequest request)
    {
        var output = new OutputOptions
        {
            RecorderMode = RecorderMode.Video,
            Stretch = StretchMode.None
        };

        if (request.CaptureMode == CaptureMode.Region
            && request.Region is { IsValid: true } region)
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

        if (request.RecordMicrophone
            && !string.IsNullOrWhiteSpace(request.MicrophoneDeviceName))
        {
            sources.Add(new CaptureAudioSource(request.MicrophoneDeviceName)
            {
                Volume = ClampVolume(request.MicrophoneVolume),
                ForceMono = false
            });
        }

        if (request.RecordSystemAudio
            && !string.IsNullOrWhiteSpace(request.SystemAudioDeviceName))
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

    private static OverLayOptions CreateOverlayOptions(RecordingRequest request)
    {
        var overlays = new List<RecordingOverlayBase>();

        if (!request.WebcamEnabled)
            return new OverLayOptions { Overlays = overlays };

        if (string.IsNullOrWhiteSpace(request.WebcamDeviceName))
        {
            throw new InvalidOperationException(
                "เปิด Webcam Overlay ไว้ แต่ยังไม่ได้เลือกกล้อง");
        }

        var available = Recorder.GetSystemVideoCaptureDevices()
            .Any(c => string.Equals(
                c.DeviceName,
                request.WebcamDeviceName,
                StringComparison.OrdinalIgnoreCase));

        if (!available)
        {
            throw new InvalidOperationException(
                "ไม่พบ Webcam ที่เลือก กรุณากดรีเฟรชกล้องแล้วเลือกใหม่");
        }

        var (width, height) = request.WebcamSize switch
        {
            WebcamSizePreset.Small => (240, 135),
            WebcamSizePreset.Large => (420, 236),
            _ => (320, 180)
        };

        var anchor = request.WebcamPosition switch
        {
            WebcamPosition.TopLeft => Anchor.TopLeft,
            WebcamPosition.TopRight => Anchor.TopRight,
            WebcamPosition.BottomLeft => Anchor.BottomLeft,
            _ => Anchor.BottomRight
        };

        overlays.Add(new VideoCaptureOverlay(request.WebcamDeviceName)
        {
            Size = new ScreenSize(width, height),
            Offset = new ScreenSize(20, 20),
            AnchorPoint = anchor,
            Stretch = StretchMode.UniformToFill
        });

        return new OverLayOptions
        {
            Overlays = overlays
        };
    }

    private static float ClampVolume(int value)
        => Math.Clamp(value, 0, 100) / 100f;

    private static int MakeEven(int value)
    {
        value = Math.Max(32, value);
        return value % 2 == 0 ? value : value - 1;
    }

    public bool Pause()
    {
        Recorder? recorder;

        lock (_sync)
        {
            recorder = _recorder;

            if (_isStopping
                || recorder is null
                || recorder.Status != RecorderStatus.Recording)
            {
                return false;
            }
        }

        try
        {
            recorder.Pause();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Resume()
    {
        Recorder? recorder;

        lock (_sync)
        {
            recorder = _recorder;

            if (_isStopping
                || recorder is null
                || recorder.Status != RecorderStatus.Paused)
            {
                return false;
            }
        }

        try
        {
            recorder.Resume();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StopAsync(TimeSpan? timeout = null)
    {
        Recorder? recorder;
        TaskCompletionSource<bool>? finished;
        var shouldRequestStop = false;

        lock (_sync)
        {
            recorder = _recorder;
            finished = _recordingFinished;

            if (recorder is null)
                return true;

            if (recorder.Status == RecorderStatus.Idle)
                return true;

            if (!_isStopping)
            {
                _isStopping = true;
                shouldRequestStop = true;
            }
        }

        if (shouldRequestStop)
        {
            try
            {
                var stopTask = Task.Run(() => recorder.Stop());
                await stopTask.WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch (TimeoutException)
            {
                ForceDetachRecorder(recorder);
                return false;
            }
            catch
            {
                ForceDetachRecorder(recorder);
                return false;
            }
        }

        if (finished is null)
            return true;

        var finalizeTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var completed = await Task.WhenAny(
            finished.Task,
            Task.Delay(finalizeTimeout));

        if (completed != finished.Task)
        {
            ForceDetachRecorder(recorder);
            return false;
        }

        return true;
    }

    private void Recorder_OnRecordingComplete(
        object? sender,
        RecordingCompleteEventArgs e)
    {
        lock (_sync)
        {
            _isStopping = false;
            _recordingFinished?.TrySetResult(true);
        }

        RecordingCompleted?.Invoke(
            this,
            e.FilePath ?? _currentFile ?? string.Empty);
    }

    private void Recorder_OnRecordingFailed(
        object? sender,
        RecordingFailedEventArgs e)
    {
        lock (_sync)
        {
            _isStopping = false;
            _recordingFinished?.TrySetResult(true);
        }

        var detail = string.IsNullOrWhiteSpace(e.Error)
            ? "ไม่สามารถบันทึกวิดีโอได้"
            : e.Error;

        RecordingFailed?.Invoke(this, detail);
    }

    private void Recorder_OnStatusChanged(
        object? sender,
        RecordingStatusEventArgs e)
    {
        if (e.Status == RecorderStatus.Idle)
        {
            lock (_sync)
            {
                _isStopping = false;
                _recordingFinished?.TrySetResult(true);
            }
        }

        StatusChanged?.Invoke(this, e.Status);
    }

    private void ReleaseIdleRecorder()
    {
        Recorder? oldRecorder = null;

        lock (_sync)
        {
            if (_recorder is null)
                return;

            if (_recorder.Status is RecorderStatus.Recording or RecorderStatus.Paused
                || _isStopping)
            {
                return;
            }

            oldRecorder = _recorder;
            _recorder = null;
            _recordingFinished = null;
        }

        if (oldRecorder is null)
            return;

        DetachEvents(oldRecorder);

        try
        {
            oldRecorder.Dispose();
        }
        catch
        {
        }
    }

    private void ForceDetachRecorder(Recorder recorder)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_recorder, recorder))
            {
                _recorder = null;
                _isStopping = false;
                _recordingFinished?.TrySetResult(false);
                _recordingFinished = null;
            }
        }

        DetachEvents(recorder);

        _ = Task.Run(() =>
        {
            try
            {
                recorder.Stop();
            }
            catch
            {
            }

            try
            {
                recorder.Dispose();
            }
            catch
            {
            }
        });
    }

    private void DetachEvents(Recorder recorder)
    {
        recorder.OnRecordingComplete -= Recorder_OnRecordingComplete;
        recorder.OnRecordingFailed -= Recorder_OnRecordingFailed;
        recorder.OnStatusChanged -= Recorder_OnStatusChanged;
    }

    public void Dispose()
    {
        Recorder? recorder;

        lock (_sync)
        {
            recorder = _recorder;
            _recorder = null;
            _isStopping = false;
            _recordingFinished?.TrySetResult(false);
            _recordingFinished = null;
        }

        if (recorder is null)
            return;

        DetachEvents(recorder);

        _ = Task.Run(() =>
        {
            try
            {
                if (recorder.Status is RecorderStatus.Recording or RecorderStatus.Paused)
                    recorder.Stop();
            }
            catch
            {
            }

            try
            {
                recorder.Dispose();
            }
            catch
            {
            }
        });
    }
}
