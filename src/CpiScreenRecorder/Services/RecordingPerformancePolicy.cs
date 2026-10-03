namespace CpiScreenRecorder.Services;

public sealed record RecordingPerformanceProfile(
    int FrameRate,
    int Bitrate,
    int Quality,
    bool HardwareEncodingEnabled,
    bool FixedFrameRate,
    bool LowLatencyEnabled,
    bool ThrottlingDisabled);

public static class RecordingPerformancePolicy
{
    public static RecordingPerformanceProfile Create(
        int width,
        int height,
        int requestedFrameRate,
        bool forceSoftwareEncoding = false)
    {
        var frameRate = requestedFrameRate >= 60 ? 60 : 30;
        var pixels = Math.Max(1L, (long)Math.Max(1, width) * Math.Max(1, height));

        var bitrate = pixels switch
        {
            <= 2_500_000 => frameRate >= 60 ? 12_000_000 : 8_000_000,
            <= 4_500_000 => frameRate >= 60 ? 18_000_000 : 12_000_000,
            _ => frameRate >= 60 ? 28_000_000 : 18_000_000
        };

        if (forceSoftwareEncoding)
        {
            // Compatibility fallback: keep the requested frame rate, but reduce
            // encoder pressure so older/unsupported GPUs can still record.
            bitrate = Math.Min(
                bitrate,
                frameRate >= 60 ? 10_000_000 : 7_000_000);
        }

        return new RecordingPerformanceProfile(
            FrameRate: frameRate,
            Bitrate: bitrate,
            Quality: 80,
            HardwareEncodingEnabled: !forceSoftwareEncoding,
            FixedFrameRate: true,
            LowLatencyEnabled: true,
            ThrottlingDisabled: false);
    }
}
