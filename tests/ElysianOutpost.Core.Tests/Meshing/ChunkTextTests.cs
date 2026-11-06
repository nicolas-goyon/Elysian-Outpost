// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Tests.Meshing;

public sealed class ChunkTextTests
{
    private const ushort Dirt = 11;
    private const ushort Stone = 22;

    // The size on the X axis is the length of a row. The size on the Y axis is the number of layers.
    // The size on the Z axis is the number of rows in a layer.
    [Fact]
    public void Parse_TwoLayersOfTwoRowsOfThreeCharacters_GivesThreeByTwoByTwoVoxels()
    {
        ushort[,,] voxels = ChunkText.Parse(
            """
            SSS
            SSS

            D..
            ...
            """,
            ('S', Stone), ('D', Dirt));

        Assert.Equal((3, 2, 2), Size(voxels));
    }

    [Fact]
    public void Parse_EachCharacter_GoesToItsPositionWithItsBlockType()
    {
        // The first layer is Y = 0. The first row of a layer is Z = 0. The first character of a row is X = 0.
        ushort[,,] voxels = ChunkText.Parse(
            """
            S..
            ...

            ...
            ..D
            """,
            ('S', Stone), ('D', Dirt));

        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    ushort expected = (x, y, z) switch
                    {
                        (0, 0, 0) => Stone,
                        (2, 1, 1) => Dirt,
                        _ => 0,
                    };
                    Assert.Equal(expected, voxels[x, y, z]);
                }
            }
        }
    }

    [Fact]
    public void Parse_IndentedLinesAndWindowsLineEnds_GivesTheSameVoxels()
    {
        ushort[,,] expected = ChunkText.Parse(string.Join('\n', "S.", ".S", "", "..", "SS"), ('S', Stone));

        // Spaces around the rows, two empty lines between the layers and Windows line ends.
        ushort[,,] voxels = ChunkText.Parse(string.Join("\r\n", "  S.  ", "  .S", "", "", "  ..", "  SS  ", ""), ('S', Stone));

        Assert.Equal(Size(expected), Size(voxels));
        Assert.Equal(expected.Cast<ushort>(), voxels.Cast<ushort>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n  \n")]
    public void Parse_NoLayer_ThrowsFormatException(string text)
    {
        Assert.Throws<FormatException>(() => ChunkText.Parse(text));
    }

    [Fact]
    public void Parse_CharacterNotInTheLegend_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ChunkText.Parse("S.D", ('S', Stone)));
    }

    [Fact]
    public void Parse_RowsOfDifferentLengths_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ChunkText.Parse("SS\nS", ('S', Stone)));
    }

    [Fact]
    public void Parse_LayersWithDifferentRowCounts_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ChunkText.Parse("SS\nSS\n\nSS", ('S', Stone)));
    }

    [Theory]
    [InlineData('.', Stone)]
    [InlineData(' ', Stone)]
    [InlineData('A', (ushort)0)]
    public void Parse_IncorrectLegend_ThrowsArgumentException(char symbol, ushort blockType)
    {
        Assert.Throws<ArgumentException>(() => ChunkText.Parse("..", (symbol, blockType)));
    }

    [Fact]
    public void Parse_CharacterTwoTimesInTheLegend_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ChunkText.Parse("S", ('S', Stone), ('S', Dirt)));
    }

    private static (int X, int Y, int Z) Size(ushort[,,] voxels) => (voxels.GetLength(0), voxels.GetLength(1), voxels.GetLength(2));
}
