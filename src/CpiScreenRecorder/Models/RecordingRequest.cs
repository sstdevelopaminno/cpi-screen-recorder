namespace CpiScreenRecorder.Models;

public sealed class RecordingRequest
{
    public CaptureMode CaptureMode { get; init; } = CaptureMode.Display;
    public string? DisplayDeviceName { get; init; }
    public IntPtr WindowHandle { get; init; }
    public CaptureRegion? Region { get; init; }

    public string OutputFile { get; init; } = string.Empty;
    public int FrameRate { get; init; } = 30;
    public bool ShowCursor { get; init; } = true;
    public bool HighlightClicks { get; init; } = true;

    public bool RecordMicrophone { get; init; }
    public string? MicrophoneDeviceName { get; init; }
    public int MicrophoneVolume { get; init; } = 100;

    public bool RecordSystemAudio { get; init; }
    public string? SystemAudioDeviceName { get; init; }
    public int SystemAudioVolume { get; init; } = 100;
}
