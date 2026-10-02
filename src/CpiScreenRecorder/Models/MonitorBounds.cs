namespace CpiScreenRecorder.Models;

public sealed record MonitorBounds(
    string DeviceName,
    int Left,
    int Top,
    int Width,
    int Height);
