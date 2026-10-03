namespace CpiScreenRecorder.Models;

public sealed class RecordingRequest
{
    public CaptureMode CaptureMode { get; init; } = CaptureMode.Display;
    public string? DisplayDeviceName { get; init; }

    public IntPtr WindowHandle { get; init; }
    public string? WindowTitle { get; init; }
    public int? WindowProcessId { get; init; }

    public CaptureRegion? Region { get; init; }

    public string OutputFile { get; init; } = string.Empty;
    public int FrameRate { get; init; } = 30;
    public RecordingQualityPreset QualityPreset { get; init; } = RecordingQualityPreset.Smooth;
    public bool ShowCursor { get; init; } = true;
    public bool HighlightClicks { get; init; } = true;

    public bool RecordMicrophone { get; init; }
    public string? MicrophoneDeviceName { get; init; }
    public int MicrophoneVolume { get; init; } = 100;

    public bool RecordSystemAudio { get; init; }
    public string? SystemAudioDeviceName { get; init; }
    public int SystemAudioVolume { get; init; } = 100;

    public bool WebcamEnabled { get; init; }
    public string? WebcamDeviceName { get; init; }
    public WebcamPosition WebcamPosition { get; init; } = WebcamPosition.BottomRight;
    public WebcamSizePreset WebcamSize { get; init; } = WebcamSizePreset.Medium;
}
