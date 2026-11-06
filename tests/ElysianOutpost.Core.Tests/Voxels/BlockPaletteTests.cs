// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using Brutal.Numerics;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Tests.Voxels;

public sealed class BlockPaletteTests
{
    [Fact]
    public void Game_HasOneColorForEachTileOfTheLegacyAtlas()
    {
        Assert.Equal(64, BlockPalette.Game.Count);
        Assert.Equal(64, BlockPalette.Game.Colors.Length);
    }

    // The expected colors come from the legacy atlas. The pull request of #2 gives the script that read them.
    [Theory]
    [InlineData(8, 0x34, 0x00, 0xCF)]
    [InlineData(11, 0x63, 0x54, 0x00)]
    [InlineData(13, 0xDB, 0xBB, 0x00)]
    [InlineData(16, 0x00, 0x72, 0x0D)]
    [InlineData(22, 0x82, 0x82, 0x82)]
    [InlineData(24, 0x00, 0x00, 0x00)]
    [InlineData(26, 0xF2, 0x00, 0xFF)]
    public void Game_GivesTheExpectedColorOfEachTerrainBlockType(ushort blockType, byte red, byte green, byte blue)
    {
        Assert.Equal(new byte4(red, green, blue, 0xFF), BlockPalette.Game[blockType]);
    }

    [Fact]
    public void Indexer_BlockTypeOutsideThePalette_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BlockPalette.Game[64]);
    }

    [Fact]
    public void Game_GivesTheNamesOfTheTerrainBlockTypes()
    {
        Assert.Equal("air", BlockPalette.Game.GetName(0));
        Assert.Equal("grass", BlockPalette.Game.GetName(16));
        Assert.Null(BlockPalette.Game.GetName(1));
        Assert.Equal(new ushort[] { 8, 11, 13, 16, 22, 24, 26 }, BlockPalette.Game.GetNamedSolidBlockTypes());
        Assert.Throws<ArgumentOutOfRangeException>(() => BlockPalette.Game.GetName(64));
    }

    [Fact]
    public void Parse_IdsInAnyOrder_GivesTheColorsInTheOrderOfTheIds()
    {
        const string Json = """
            {
              "blockTypes": [
                {"id": 1, "name": "grass", "color": "#00720DFF"},
                {"id": 0, "color": "#01020304"}
              ]
            }
            """;

        var palette = BlockPalette.Parse(Json);

        Assert.Equal(2, palette.Count);
        Assert.Equal(new byte4(0x01, 0x02, 0x03, 0x04), palette[0]);
        Assert.Equal(new byte4(0x00, 0x72, 0x0D, 0xFF), palette[1]);
        Assert.Null(palette.GetName(0));
        Assert.Equal("grass", palette.GetName(1));
        Assert.Equal(new ushort[] { 1 }, palette.GetNamedSolidBlockTypes());
    }

    [Theory]
    [InlineData("""{"colors": []}""")]
    [InlineData("""{"blockTypes": [{"color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": "0", "color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": null, "color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0.5, "color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 1, "color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": -1, "color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0, "color": "#000000FF"}, {"id": 0, "color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0, "color": "#000000"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0, "color": "000000FF0"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0, "color": "#00000GFF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0, "name": "", "color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0, "name": " ", "color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0, "name": 3, "color": "#000000FF"}]}""")]
    [InlineData("""{"blockTypes": [{"id": 0, "name": null, "color": "#000000FF"}]}""")]
    public void Parse_InvalidPalette_ThrowsInvalidDataException(string json)
    {
        Assert.Throws<InvalidDataException>(() => BlockPalette.Parse(json));
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => BlockPalette.Parse("{"));
    }
}
