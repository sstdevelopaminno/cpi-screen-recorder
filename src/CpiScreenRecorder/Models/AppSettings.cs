using System.IO;
namespace CpiScreenRecorder.Models;

public sealed class AppSettings
{
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "CPI Screen Recorder");

    public string? DisplayDeviceName { get; set; }
    public int FrameRate { get; set; } = 30;
    public bool ShowCursor { get; set; } = true;
    public bool HighlightClicks { get; set; } = true;
}
