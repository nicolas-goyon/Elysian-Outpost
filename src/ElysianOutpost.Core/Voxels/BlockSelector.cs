// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Voxels;

/// <summary>
/// The current block type of the player: the block type of a placed voxel (#41). The mouse wheel changes it.
/// </summary>
/// <remarks>
/// The selector goes through a fixed list of block types. After the last block type, it goes back to the first block type.
/// A touchpad can give scroll offsets less than one step. The selector adds these offsets until they make a full step.
/// </remarks>
public sealed class BlockSelector
{
    private readonly ushort[] _blockTypes;
    private double _scroll;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockSelector"/> class.
    /// </summary>
    /// <param name="blockTypes">The block types to select, in their order. None of them is 0 (air).</param>
    /// <param name="initial">The first current block type. It must be in the list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="blockTypes"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The list is empty, contains 0, or does not contain <paramref name="initial"/>.</exception>
    public BlockSelector(IReadOnlyList<ushort> blockTypes, ushort initial)
    {
        ArgumentNullException.ThrowIfNull(blockTypes);
        if (blockTypes.Count == 0 || blockTypes.Contains((ushort)0))
        {
            throw new ArgumentException("The list must contain at least one block type, and it must not contain 0 (air).", nameof(blockTypes));
        }

        _blockTypes = [.. blockTypes];
        Index = Array.IndexOf(_blockTypes, initial);
        if (Index < 0)
        {
            throw new ArgumentException($"The block type {initial} is not in the list.", nameof(initial));
        }
    }

    /// <summary>
    /// Gets the index of the current block type in the list.
    /// </summary>
    public int Index { get; private set; }

    /// <summary>
    /// Gets the current block type.
    /// </summary>
    public ushort Current => _blockTypes[Index];

    /// <summary>
    /// Changes the current block type for a movement of the mouse wheel.
    /// </summary>
    /// <param name="offset">
    /// The vertical scroll offset of GLFW. One step of a mouse wheel is 1 or -1.
    /// A positive offset (the wheel turns away from the player) selects the next block types.
    /// A value that is not finite does nothing.
    /// </param>
    /// <returns><c>true</c> if the current block type changed.</returns>
    public bool Scroll(double offset)
    {
        if (!double.IsFinite(offset))
        {
            return false;
        }

        _scroll += offset;
        double steps = Math.Truncate(_scroll);
        if (steps == 0)
        {
            return false;
        }

        // The remainder is less than the number of block types. Thus a very large offset cannot overflow the index.
        _scroll -= steps;
        int previous = Index;
        int count = _blockTypes.Length;
        int move = (int)(steps % count);
        Index = (((Index + move) % count) + count) % count;
        return Index != previous;
    }
}
