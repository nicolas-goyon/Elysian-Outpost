// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Voxels;

/// <summary>
/// One voxel: its block type. Block type 0 is air.
/// </summary>
/// <remarks>
/// The voxel is a value of 2 bytes, not an object (#5). The chunks store the block types in a <c>ushort</c> array,
/// and make a <see cref="Voxel"/> value only when a caller reads a voxel.
/// </remarks>
/// <param name="id">The block type.</param>
public readonly struct Voxel(ushort id) : IEquatable<Voxel>
{
    /// <summary>
    /// Gets the block type of the voxel.
    /// </summary>
    public ushort ID { get; } = id;

    /// <summary>
    /// Gets a value that tells if the voxel is solid: its block type is not 0.
    /// </summary>
    public bool IsSolid => ID != 0;

    /// <summary>
    /// Compares two voxels.
    /// </summary>
    public static bool operator ==(Voxel left, Voxel right) => left.ID == right.ID;

    /// <summary>
    /// Compares two voxels.
    /// </summary>
    public static bool operator !=(Voxel left, Voxel right) => left.ID != right.ID;

    /// <inheritdoc/>
    public bool Equals(Voxel other) => ID == other.ID;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Voxel other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => ID;
}



/// <summary>
/// The faces of a voxel, as flags. <see cref="VoxelRay"/> gives the face that a ray hits.
/// </summary>
[Flags]
public enum VoxelFace
{
    /// <summary>
    /// No face.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// The face on the +Z side (the back face in the legacy project).
    /// </summary>
    Zpos = 1 << 0,

    /// <summary>
    /// The face on the −Z side (the front face in the legacy project).
    /// </summary>
    Zneg = 1 << 1,

    /// <summary>
    /// The face on the −X side (the left face).
    /// </summary>
    Xneg = 1 << 2,

    /// <summary>
    /// The face on the +X side (the right face).
    /// </summary>
    Xpos = 1 << 3,

    /// <summary>
    /// The face on the +Y side (the top face).
    /// </summary>
    Ypos = 1 << 4,

    /// <summary>
    /// The face on the −Y side (the bottom face).
    /// </summary>
    Yneg = 1 << 5,
}
