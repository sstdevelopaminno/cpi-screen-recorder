namespace CpiScreenRecorder.Models;

public sealed record AudioDeviceOption(string FriendlyName, string DeviceName, bool IsDefault)
{
    public string Label => IsDefault ? $"{FriendlyName}  ·  ค่าเริ่มต้น" : FriendlyName;
}
