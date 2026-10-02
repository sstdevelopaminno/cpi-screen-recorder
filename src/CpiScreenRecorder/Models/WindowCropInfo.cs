namespace CpiScreenRecorder.Models;

public sealed record WindowCropInfo(
    string DisplayDeviceName,
    int X,
    int Y,
    int Width,
    int Height);
