using CpiScreenRecorder.Models;

namespace CpiScreenRecorder.Services;

public sealed record RecordingPerformanceProfile(
    int FrameRate,
    int Bitrate,
    int Quality,
    bool HardwareEncodingEnabled,
    bool FixedFrameRate,
    bool LowLatencyEnabled,
    bool ThrottlingDisabled,
    int OutputWidth,
    int OutputHeight);

public static class RecordingPerformancePolicy
{
    public static RecordingPerformanceProfile Create(
        int width,
        int height,
        int requestedFrameRate,
        RecordingQualityPreset preset = RecordingQualityPreset.Smooth,
        bool forceSoftwareEncoding = false)
    {
        width = Math.Max(32, width);
        height = Math.Max(32, height);

        var frameRate = requestedFrameRate >= 60 ? 60 : 30;
        var (maxWidth, maxHeight) = preset switch
        {
            RecordingQualityPreset.Smooth => (1280, 720),
            RecordingQualityPreset.Balanced => (1600, 900),
            _ => (width, height)
        };

        var (outputWidth, outputHeight) = FitWithin(width, height, maxWidth, maxHeight);
        var pixels = (long)outputWidth * outputHeight;

        var bitrate = pixels switch
        {
            <= 1_000_000 => frameRate >= 60 ? 8_000_000 : 5_000_000,
            <= 1_500_000 => frameRate >= 60 ? 10_000_000 : 7_000_000,
            <= 2_500_000 => frameRate >= 60 ? 14_000_000 : 10_000_000,
            _ => frameRate >= 60 ? 24_000_000 : 16_000_000
        };

        if (forceSoftwareEncoding)
        {
            // Compatibility mode prioritizes uninterrupted frame delivery over
            // maximum detail. Keep bitrate bounded so CPU-only encoding does not
            // build a long frame queue on older machines.
            bitrate = Math.Min(
                bitrate,
                frameRate >= 60 ? 8_000_000 : 6_000_000);
        }

        return new RecordingPerformanceProfile(
            FrameRate: frameRate,
            Bitrate: bitrate,
            Quality: 72,
            HardwareEncodingEnabled: !forceSoftwareEncoding,
            FixedFrameRate: true,
            LowLatencyEnabled: true,
            ThrottlingDisabled: false,
            OutputWidth: outputWidth,
            OutputHeight: outputHeight);
    }

    private static (int Width, int Height) FitWithin(
        int width,
        int height,
        int maxWidth,
        int maxHeight)
    {
        var scale = Math.Min(
            1d,
            Math.Min(
                maxWidth / (double)width,
                maxHeight / (double)height));

        var targetWidth = MakeEven((int)Math.Round(width * scale));
        var targetHeight = MakeEven((int)Math.Round(height * scale));

        return (
            Math.Max(32, targetWidth),
            Math.Max(32, targetHeight));
    }

    private static int MakeEven(int value)
        => value % 2 == 0 ? value : value - 1;
}
