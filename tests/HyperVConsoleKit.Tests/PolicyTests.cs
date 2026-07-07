using HyperVConsoleKit;
using Xunit;

namespace HyperVConsoleKit.Tests;

public sealed class PolicyTests
{
    [Fact]
    public void ApplyToFrameOptionsClampsResolution()
    {
        var policy = new HyperVConsolePolicy { MaxWidth = 800, MaxHeight = 600 };
        var options = new ConsoleFrameOptions { Width = 1920, Height = 1080 };

        policy.ApplyTo(options);

        Assert.Equal(800, options.Width);
        Assert.Equal(600, options.Height);
    }

    [Fact]
    public void ApplyToStreamOptionsClampsPerformanceControls()
    {
        var policy = new HyperVConsolePolicy
        {
            MaxWidth = 800,
            MaxHeight = 600,
            MaxFramesPerSecond = 5,
            MaxBytesPerSecond = 250_000,
            MaxColorDepth = ConsoleFramePixelFormat.Rgb332
        };
        var options = new ConsoleFrameStreamOptions
        {
            Width = 1920,
            Height = 1080,
            FramesPerSecond = 30,
            ActiveFramesPerSecond = 15,
            IdleFramesPerSecond = 10,
            MaxBytesPerSecond = 500_000,
            PixelFormat = ConsoleFramePixelFormat.Rgb565
        };

        policy.ApplyTo(options);

        Assert.Equal(800, options.Width);
        Assert.Equal(600, options.Height);
        Assert.Equal(5, options.FramesPerSecond);
        Assert.Equal(5, options.ActiveFramesPerSecond);
        Assert.Equal(5, options.IdleFramesPerSecond);
        Assert.Equal(250_000, options.MaxBytesPerSecond);
        Assert.Equal(ConsoleFramePixelFormat.Rgb332, options.PixelFormat);
    }

    [Fact]
    public void ApplyToStreamOptionsKeepsStricterExistingBandwidthLimit()
    {
        var policy = new HyperVConsolePolicy { MaxBytesPerSecond = 500_000 };
        var options = new ConsoleFrameStreamOptions { MaxBytesPerSecond = 100_000 };

        policy.ApplyTo(options);

        Assert.Equal(100_000, options.MaxBytesPerSecond);
    }
}
