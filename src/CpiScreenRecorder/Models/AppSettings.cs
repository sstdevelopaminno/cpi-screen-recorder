using System.IO;

namespace CpiScreenRecorder.Models;

public sealed class AppSettings
{
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "CPI Screen Recorder");

    public CaptureMode CaptureMode { get; set; } = CaptureMode.Display;
    public string? DisplayDeviceName { get; set; }
    public string? WindowTitle { get; set; }

    public int RegionX { get; set; }
    public int RegionY { get; set; }
    public int RegionWidth { get; set; }
    public int RegionHeight { get; set; }

    public int FrameRate { get; set; } = 30;
    public bool ShowCursor { get; set; } = true;
    public bool HighlightClicks { get; set; } = true;

    public bool RecordMicrophone { get; set; }
    public string? MicrophoneDeviceName { get; set; }
    public int MicrophoneVolume { get; set; } = 100;

    public bool RecordSystemAudio { get; set; }
    public string? SystemAudioDeviceName { get; set; }
    public int SystemAudioVolume { get; set; } = 100;
}
