// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// Makes the voxels of a test chunk from a short text description.
/// </summary>
/// <remarks>
/// <para>
/// The text gives the horizontal layers of the chunk, from the bottom layer (Y = 0) to the top layer.
/// One or more empty lines separate two layers.
/// In a layer, the first row is Z = 0, and the first character of a row is X = 0.
/// </para>
/// <para>
/// A dot is air. The legend gives the block type of each other character.
/// The parser ignores the spaces at the start and at the end of each line.
/// </para>
/// </remarks>
/// <example>
/// A chunk of 3 x 2 x 2 voxels: one layer of stone, and one layer with one dirt voxel.
/// <code>
/// ushort[,,] voxels = ChunkText.Parse(
///     """
///     SSS
///     SSS
///
///     D..
///     ...
///     """,
///     ('S', 22), ('D', 11));
/// </code>
/// </example>
internal static class ChunkText
{
    /// <summary>
    /// The character for air, the block type 0.
    /// </summary>
    public const char Air = '.';

    /// <summary>
    /// Makes the voxels of a chunk from a text description.
    /// </summary>
    /// <param name="text">The layers of the chunk. See the remarks of <see cref="ChunkText"/>.</param>
    /// <param name="legend">The block type of each character, except the dot.</param>
    /// <returns>The block types, with the indices [x, y, z]. This is the format of the <c>Chunk</c> constructor.</returns>
    /// <exception cref="ArgumentException">The legend uses the dot, a space or the block type 0, or it uses a character two times.</exception>
    /// <exception cref="FormatException">The text has no layer, a character that is not in the legend, or rows and layers of different sizes.</exception>
    public static ushort[,,] Parse(string text, params (char Symbol, ushort BlockType)[] legend)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(legend);

        var blockTypes = new Dictionary<char, ushort> { [Air] = 0 };
        foreach ((char symbol, ushort blockType) in legend)
        {
            if (symbol == Air || char.IsWhiteSpace(symbol))
            {
                throw new ArgumentException($"The legend cannot use the character '{symbol}'.", nameof(legend));
            }

            if (blockType == 0)
            {
                throw new ArgumentException($"The character '{symbol}' has the block type 0. Use a dot for air.", nameof(legend));
            }

            if (!blockTypes.TryAdd(symbol, blockType))
            {
                throw new ArgumentException($"The legend uses the character '{symbol}' two times.", nameof(legend));
            }
        }

        List<List<string>> layers = ReadLayers(text);
        int sizeX = layers[0][0].Length;
        int sizeY = layers.Count;
        int sizeZ = layers[0].Count;
        var voxels = new ushort[sizeX, sizeY, sizeZ];
        for (int y = 0; y < sizeY; y++)
        {
            if (layers[y].Count != sizeZ)
            {
                throw new FormatException($"Layer {y} has {layers[y].Count} rows. Layer 0 has {sizeZ} rows.");
            }

            for (int z = 0; z < sizeZ; z++)
            {
                string row = layers[y][z];
                if (row.Length != sizeX)
                {
                    throw new FormatException($"Row {z} of layer {y} has {row.Length} characters. Row 0 of layer 0 has {sizeX} characters.");
                }

                for (int x = 0; x < sizeX; x++)
                {
                    if (!blockTypes.TryGetValue(row[x], out ushort blockType))
                    {
                        throw new FormatException($"The character '{row[x]}' in row {z} of layer {y} is not in the legend.");
                    }

                    voxels[x, y, z] = blockType;
                }
            }
        }

        return voxels;
    }

    // Splits the text into layers of rows. An empty line ends a layer.
    private static List<List<string>> ReadLayers(string text)
    {
        var layers = new List<List<string>>();
        List<string>? layer = null;
        foreach (string line in text.Split('\n'))
        {
            string row = line.Trim();
            if (row.Length == 0)
            {
                layer = null;
                continue;
            }

            if (layer is null)
            {
                layer = [];
                layers.Add(layer);
            }

            layer.Add(row);
        }

        if (layers.Count == 0)
        {
            throw new FormatException("The text has no layer.");
        }

        return layers;
    }
}
