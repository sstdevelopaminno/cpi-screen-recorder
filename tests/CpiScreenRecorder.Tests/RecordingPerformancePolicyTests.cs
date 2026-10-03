using Xunit;
using CpiScreenRecorder.Models;
using CpiScreenRecorder.Services;

namespace CpiScreenRecorder.Tests;

public sealed class RecordingPerformancePolicyTests
{
    [Theory]
    [InlineData(1600, 900, 1280, 720, 5_000_000)]
    [InlineData(1920, 1080, 1280, 720, 5_000_000)]
    [InlineData(2560, 1440, 1280, 720, 5_000_000)]
    [InlineData(3840, 2160, 1280, 720, 5_000_000)]
    public void SmoothPreset_CapsOutputAt720p(
        int width,
        int height,
        int expectedWidth,
        int expectedHeight,
        int expectedBitrate)
    {
        var profile = RecordingPerformancePolicy.Create(
            width,
            height,
            30,
            RecordingQualityPreset.Smooth);

        Assert.Equal(expectedWidth, profile.OutputWidth);
        Assert.Equal(expectedHeight, profile.OutputHeight);
        Assert.Equal(expectedBitrate, profile.Bitrate);
        Assert.Equal(30, profile.FrameRate);
        Assert.True(profile.HardwareEncodingEnabled);
        Assert.True(profile.FixedFrameRate);
        Assert.True(profile.LowLatencyEnabled);
        Assert.False(profile.ThrottlingDisabled);
        Assert.Equal(72, profile.Quality);
    }

    [Fact]
    public void BalancedPreset_Caps1080pSourceAt900p()
    {
        var profile = RecordingPerformancePolicy.Create(
            1920,
            1080,
            30,
            RecordingQualityPreset.Balanced);

        Assert.Equal(1600, profile.OutputWidth);
        Assert.Equal(900, profile.OutputHeight);
        Assert.Equal(7_000_000, profile.Bitrate);
    }

    [Fact]
    public void HighPreset_PreservesSourceResolution()
    {
        var profile = RecordingPerformancePolicy.Create(
            1920,
            1080,
            30,
            RecordingQualityPreset.High);

        Assert.Equal(1920, profile.OutputWidth);
        Assert.Equal(1080, profile.OutputHeight);
        Assert.Equal(10_000_000, profile.Bitrate);
    }

    [Fact]
    public void SmoothPreset_DoesNotUpscaleSmallSources()
    {
        var profile = RecordingPerformancePolicy.Create(
            1024,
            576,
            30,
            RecordingQualityPreset.Smooth);

        Assert.Equal(1024, profile.OutputWidth);
        Assert.Equal(576, profile.OutputHeight);
        Assert.Equal(5_000_000, profile.Bitrate);
    }

    [Theory]
    [InlineData(1920, 1080, 30, RecordingQualityPreset.Smooth, 5_000_000)]
    [InlineData(1920, 1080, 60, RecordingQualityPreset.Smooth, 8_000_000)]
    [InlineData(3840, 2160, 60, RecordingQualityPreset.High, 8_000_000)]
    public void SoftwareFallback_CapsEncoderPressure(
        int width,
        int height,
        int fps,
        RecordingQualityPreset preset,
        int expectedBitrate)
    {
        var profile = RecordingPerformancePolicy.Create(
            width,
            height,
            fps,
            preset,
            forceSoftwareEncoding: true);

        Assert.False(profile.HardwareEncodingEnabled);
        Assert.Equal(expectedBitrate, profile.Bitrate);
    }

    [Fact]
    public void Create_NormalizesUnsupportedFrameRates()
    {
        Assert.Equal(
            30,
            RecordingPerformancePolicy.Create(1920, 1080, 24).FrameRate);
        Assert.Equal(
            60,
            RecordingPerformancePolicy.Create(1920, 1080, 120).FrameRate);
    }

    [Fact]
    public void CaptureRegion_RequiresMinimumUsableDimensions()
    {
        Assert.False(new CaptureRegion(0, 0, 31, 1080).IsValid);
        Assert.False(new CaptureRegion(0, 0, 1920, 31).IsValid);
        Assert.True(new CaptureRegion(10, 20, 32, 32).IsValid);
    }
}
