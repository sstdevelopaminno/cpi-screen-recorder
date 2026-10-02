namespace CpiScreenRecorder.Models;

public sealed record DisplayOption(string FriendlyName, string DeviceName)
{
    public string Label => string.IsNullOrWhiteSpace(FriendlyName)
        ? DeviceName
        : $"{FriendlyName}  ·  {DeviceName}";
}
