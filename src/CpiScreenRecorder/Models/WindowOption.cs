namespace CpiScreenRecorder.Models;

public sealed record WindowOption(string Title, IntPtr Handle, int? ProcessId)
{
    public string Label => ProcessId.HasValue
        ? $"{Title}  ·  PID {ProcessId.Value}"
        : Title;
}
