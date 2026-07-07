using System.Linq;
using HyperVConsoleKit;
using Xunit;

namespace HyperVConsoleKit.Tests;

public sealed class PixelCodecTests
{
    [Fact]
    public void ConvertRgb565ToRgb332CompressesExpectedColors()
    {
        var rgb565 = new byte[]
        {
            0x00, 0xF8,
            0xE0, 0x07,
            0x1F, 0x00,
            0xFF, 0xFF
        };

        var converted = PixelCodec.ConvertRgb565(rgb565, ConsoleFramePixelFormat.Rgb332);

        Assert.Equal(new byte[] { 0xE0, 0x1C, 0x03, 0xFF }, converted);
    }

    [Fact]
    public void ConvertRgb565ToMonoPacksEightPixelsPerByte()
    {
        var rgb565 = Enumerable.Range(0, 8)
            .SelectMany(i => i < 4 ? new byte[] { 0x00, 0x00 } : new byte[] { 0xFF, 0xFF })
            .ToArray();

        var converted = PixelCodec.ConvertRgb565(rgb565, ConsoleFramePixelFormat.Mono1);

        Assert.Equal(new byte[] { 0x0F }, converted);
    }

    [Fact]
    public void GetChangedTilesReturnsOnlyChangedTiles()
    {
        var previous = new byte[16];
        var current = new byte[16];
        current[5] = 0x7F;

        var tiles = PixelCodec.GetChangedTiles(
            previous,
            current,
            width: 4,
            height: 4,
            format: ConsoleFramePixelFormat.Gray8,
            tileWidth: 2,
            tileHeight: 2);

        var tile = Assert.Single(tiles);
        Assert.Equal(0, tile.X);
        Assert.Equal(0, tile.Y);
        Assert.Equal(2, tile.Width);
        Assert.Equal(2, tile.Height);
        Assert.Equal(new byte[] { 0, 0, 0, 0x7F }, tile.RawBytes);
    }
}
