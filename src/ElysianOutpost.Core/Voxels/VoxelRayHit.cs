// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Voxels;

/// <summary>
/// The result of <see cref="VoxelRay.TryCast"/>: the solid voxel that a ray hits.
/// </summary>
/// <param name="Voxel">The world position of the solid voxel.</param>
/// <param name="Normal">
/// The normal of the face that the ray crosses to enter the voxel: a unit vector on one axis, that points back to the origin of the ray.
/// The value is (0, 0, 0) if the origin of the ray is in the solid voxel, and not on one of its faces.
/// </param>
/// <param name="Distance">The distance from the origin of the ray to the hit point on the face, in world units.</param>
/// <param name="BlockType">The block type of the solid voxel. It is not 0.</param>
public readonly record struct VoxelRayHit(int3 Voxel, int3 Normal, float Distance, ushort BlockType)
{
    /// <summary>
    /// Gets the face that the ray crosses to enter the voxel, or <see cref="VoxelFace.NONE"/> if the origin of the ray is in the solid voxel and not on one of its faces.
    /// </summary>
    public VoxelFace Face => (Normal.X, Normal.Y, Normal.Z) switch
    {
        (1, 0, 0) => VoxelFace.Xpos,
        (-1, 0, 0) => VoxelFace.Xneg,
        (0, 1, 0) => VoxelFace.Ypos,
        (0, -1, 0) => VoxelFace.Yneg,
        (0, 0, 1) => VoxelFace.Zpos,
        (0, 0, -1) => VoxelFace.Zneg,
        _ => VoxelFace.NONE,
    };

    /// <summary>
    /// Gets a value that tells if <see cref="EmptyVoxel"/> is valid. It is <c>false</c> only if the origin of the ray is in the solid voxel.
    /// </summary>
    public bool HasEmptyVoxel => Face != VoxelFace.NONE;

    /// <summary>
    /// Gets the world position of the empty voxel before the hit: the neighbor of <see cref="Voxel"/> on the side of <see cref="Normal"/>.
    /// A new block goes at this position. The position can be outside the world limits, for example above the highest chunk layer.
    /// If <see cref="HasEmptyVoxel"/> is <c>false</c>, the value is <see cref="Voxel"/>.
    /// </summary>
    public int3 EmptyVoxel => Voxel + Normal;
}
