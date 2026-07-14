using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using LibMpv.Thumbnailing;
using Xunit;

namespace LibMpv.Thumbnailing.Tests;

/// <summary>
/// Initializes Avalonia (Skia platform) once for all tests in the assembly.
/// WriteableBitmap requires IPlatformRenderInterface which is provided by Avalonia.Skia.
/// </summary>
public class AvaloniaFixture
{
    public AvaloniaFixture()
    {
        AppBuilder.Configure<Application>()
            .UsePlatformDetect()
            .SetupWithoutStarting();
    }
}

[CollectionDefinition("MpvThumbnail")]
public class MpvThumbnailCollection : ICollectionFixture<AvaloniaFixture> { }

[Collection("MpvThumbnail")]
public class MpvThumbnailExtractorTests
{
    private static readonly string TestVideoPath =
        Path.Combine(Path.GetDirectoryName(typeof(MpvThumbnailExtractorTests).Assembly.Location)!,
            "TestFixtures", "test-video.mkv");

    [Fact]
    public async Task ExtractAsync_ValidVideo_ReturnsNonNullBitmap()
    {
        var result = await MpvThumbnailExtractor.ExtractAsync(TestVideoPath);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExtractAsync_ValidVideo_ReturnsCorrectDimensions()
    {
        var result = await MpvThumbnailExtractor.ExtractAsync(
            TestVideoPath, maxWidth: 320, maxHeight: 240);

        Assert.NotNull(result);
        Assert.True(result!.PixelSize.Width <= 320);
        Assert.True(result!.PixelSize.Height <= 240);
        Assert.True(result!.PixelSize.Width > 0);
        Assert.True(result!.PixelSize.Height > 0);
    }

    [Fact]
    public async Task ExtractAsync_NonExistentFile_ReturnsNull()
    {
        var result = await MpvThumbnailExtractor.ExtractAsync("/does/not/exist.mp4");

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractAsync_ValidVideo_CanBeCalledConcurrently()
    {
        var tasks = new[]
        {
            MpvThumbnailExtractor.ExtractAsync(TestVideoPath),
            MpvThumbnailExtractor.ExtractAsync(TestVideoPath),
        };

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.NotNull(r));
    }
}
