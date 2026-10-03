using CpiScreenRecorder.Models;
using CpiScreenRecorder.Services;

namespace CpiScreenRecorder.Tests;

public sealed class RecordingPerformancePolicyTests
{
    [Theory]
    [InlineData(1920, 1080, 30, 8_000_000)]
    [InlineData(1920, 1080, 60, 12_000_000)]
    [InlineData(2560, 1440, 30, 12_000_000)]
    [InlineData(2560, 1440, 60, 18_000_000)]
    [InlineData(3840, 2160, 30, 18_000_000)]
    [InlineData(3840, 2160, 60, 28_000_000)]
    public void Create_SelectsBalancedBitrate(
        int width,
        int height,
        int fps,
        int expectedBitrate)
    {
        var profile = RecordingPerformancePolicy.Create(width, height, fps);

        Assert.Equal(fps, profile.FrameRate);
        Assert.Equal(expectedBitrate, profile.Bitrate);
        Assert.True(profile.HardwareEncodingEnabled);
        Assert.True(profile.FixedFrameRate);
        Assert.True(profile.LowLatencyEnabled);
        Assert.False(profile.ThrottlingDisabled);
        Assert.Equal(80, profile.Quality);
    }

    [Theory]
    [InlineData(1920, 1080, 30, 7_000_000)]
    [InlineData(1920, 1080, 60, 10_000_000)]
    [InlineData(3840, 2160, 60, 10_000_000)]
    public void Create_SoftwareFallbackCapsEncoderPressure(
        int width,
        int height,
        int fps,
        int expectedBitrate)
    {
        var profile = RecordingPerformancePolicy.Create(
            width,
            height,
            fps,
            forceSoftwareEncoding: true);

        Assert.False(profile.HardwareEncodingEnabled);
        Assert.Equal(expectedBitrate, profile.Bitrate);
    }

    [Fact]
    public void Create_NormalizesUnsupportedFrameRates()
    {
        Assert.Equal(30, RecordingPerformancePolicy.Create(1920, 1080, 24).FrameRate);
        Assert.Equal(60, RecordingPerformancePolicy.Create(1920, 1080, 120).FrameRate);
    }

    [Fact]
    public void CaptureRegion_RequiresMinimumUsableDimensions()
    {
        Assert.False(new CaptureRegion(0, 0, 31, 1080).IsValid);
        Assert.False(new CaptureRegion(0, 0, 1920, 31).IsValid);
        Assert.True(new CaptureRegion(10, 20, 32, 32).IsValid);
    }
}
