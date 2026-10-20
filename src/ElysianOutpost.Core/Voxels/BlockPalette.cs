// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using System.Text;
using System.Text.Json;
using Brutal.Numerics;

namespace ElysianOutpost.Core.Voxels;

/// <summary>
/// The colors of the block types. The index of a color is the ID of the block type.
/// </summary>
/// <remarks>
/// Each color is an sRGB color with alpha. <see cref="byte4.X"/> is the red value, <see cref="byte4.Y"/> is the green value,
/// <see cref="byte4.Z"/> is the blue value and <see cref="byte4.W"/> is the alpha value.
/// The file <c>data/palette.json</c> gives the colors of the game. The build puts this file into the core library.
/// </remarks>
public sealed class BlockPalette
{
    private const string GamePaletteResource = "ElysianOutpost.Core.Voxels.palette.json";

    private static readonly Lazy<BlockPalette> _game = new(LoadGamePalette);

    private readonly byte4[] _colors;

    private BlockPalette(byte4[] colors)
    {
        _colors = colors;
    }

    /// <summary>
    /// Gets the palette of the game, from the file <c>data/palette.json</c>.
    /// </summary>
    public static BlockPalette Game => _game.Value;

    /// <summary>
    /// Gets the number of block types in the palette.
    /// </summary>
    public int Count => _colors.Length;

    /// <summary>
    /// Gets the colors of all the block types, in the order of their IDs.
    /// </summary>
    public ReadOnlySpan<byte4> Colors => _colors;

    /// <summary>
    /// Gets the color of a block type.
    /// </summary>
    /// <param name="blockType">The ID of the block type.</param>
    /// <exception cref="ArgumentOutOfRangeException">The palette has no color for this block type.</exception>
    public byte4 this[ushort blockType]
    {
        get
        {
            if (blockType >= _colors.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(blockType), blockType, $"The palette has {_colors.Length} colors.");
            }

            return _colors[blockType];
        }
    }

    /// <summary>
    /// Reads a palette in the format of <c>data/palette.json</c>.
    /// </summary>
    /// <param name="json">The content of the palette file.</param>
    /// <returns>The palette.</returns>
    /// <exception cref="JsonException">The content is not valid JSON.</exception>
    /// <exception cref="InvalidDataException">The content does not have the expected format.</exception>
    public static BlockPalette Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("blockTypes"u8, out JsonElement blockTypes)
            || blockTypes.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The palette has no array \"blockTypes\".");
        }

        // The IDs must be the numbers from 0 to the number of entries minus 1, in any order.
        var colors = new byte4?[blockTypes.GetArrayLength()];
        foreach (JsonElement entry in blockTypes.EnumerateArray())
        {
            int id = ReadId(entry, colors.Length);
            if (colors[id] is not null)
            {
                throw new InvalidDataException($"The palette has two entries with the id {id}.");
            }

            colors[id] = ReadColor(entry, id);
        }

        return new BlockPalette(Array.ConvertAll(colors, color => color!.Value));
    }

    private static int ReadId(JsonElement entry, int count)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty("id"u8, out JsonElement id)
            || id.ValueKind != JsonValueKind.Number
            || !id.TryGetInt32(out int value))
        {
            throw new InvalidDataException("An entry of the palette has no integer \"id\".");
        }

        if (value < 0 || value >= count)
        {
            throw new InvalidDataException(
                $"The id {value} is not in the range from 0 to {count - 1}. The ids must be the numbers from 0 to the number of entries minus 1.");
        }

        return value;
    }

    // The format of a color is #RRGGBBAA, with hexadecimal digits.
    private static byte4 ReadColor(JsonElement entry, int id)
    {
        string? text = entry.TryGetProperty("color"u8, out JsonElement color) && color.ValueKind == JsonValueKind.String
            ? color.GetString()
            : null;
        if (text is null
            || text.Length != 9
            || text[0] != '#'
            || !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint rgba))
        {
            throw new InvalidDataException($"The entry with the id {id} has no color in the format #RRGGBBAA.");
        }

        return new byte4((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
    }

    private static BlockPalette LoadGamePalette()
    {
        using Stream stream = typeof(BlockPalette).Assembly.GetManifestResourceStream(GamePaletteResource)
            ?? throw new InvalidOperationException($"The core library has no resource {GamePaletteResource}.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }
}
