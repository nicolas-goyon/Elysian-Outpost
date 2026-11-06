// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Voxels;

/// <summary>
/// The voxel edits of the player: remove the voxel that the ray hits, or place a voxel in front of it (#41).
/// These methods replace <c>TerrainHolder.PickupVoxel</c> and <c>TerrainHolder.PlaceVoxel</c> of the legacy project.
/// </summary>
/// <remarks>
/// A method changes only the voxel world. The caller must then mesh the changed chunk again.
/// </remarks>
public static class VoxelEdits
{
    /// <summary>
    /// Removes the voxel that a ray hits.
    /// </summary>
    /// <param name="world">The voxels of the world.</param>
    /// <param name="hit">The result of <see cref="VoxelRay.TryCast"/> in this world.</param>
    /// <param name="chunkPosition">The position of the changed chunk, if the method returns <c>true</c>.</param>
    /// <returns><c>true</c> if the method removed the voxel. <c>false</c> if the voxel is already empty or no chunk contains it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <c>null</c>.</exception>
    public static bool TryRemove(VoxelWorld world, VoxelRayHit hit, out int3 chunkPosition)
    {
        ArgumentNullException.ThrowIfNull(world);
        chunkPosition = default;
        return world.IsSolid(hit.Voxel) && world.TrySetVoxel(hit.Voxel, 0, out chunkPosition);
    }

    /// <summary>
    /// Places a voxel at the empty position before the voxel that a ray hits (<see cref="VoxelRayHit.EmptyVoxel"/>).
    /// </summary>
    /// <param name="world">The voxels of the world.</param>
    /// <param name="hit">The result of <see cref="VoxelRay.TryCast"/> in this world.</param>
    /// <param name="blockType">The block type of the new voxel. It must not be 0.</param>
    /// <param name="cameraPosition">The position of the camera, in world units. The method does not place a voxel that contains this position.</param>
    /// <param name="chunkPosition">The position of the changed chunk, if the method returns <c>true</c>.</param>
    /// <returns>
    /// <c>true</c> if the method placed the voxel. <c>false</c> in these cases:
    /// the ray started in the hit voxel (no empty position), no chunk contains the position, the position is not empty,
    /// or the voxel contains the camera.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="blockType"/> is 0.</exception>
    public static bool TryPlace(VoxelWorld world, VoxelRayHit hit, ushort blockType, float3 cameraPosition, out int3 chunkPosition)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfZero(blockType);
        chunkPosition = default;
        int3 position = hit.EmptyVoxel;

        // If the camera is on a voxel boundary, the empty position of the hit can be the solid voxel on the other side of the camera.
        // A voxel that contains the camera puts the camera in a solid voxel. Then the ray cannot find an empty position.
        return hit.HasEmptyVoxel
            && !world.IsSolid(position)
            && !Contains(position, cameraPosition)
            && world.TrySetVoxel(position, blockType, out chunkPosition);
    }

    // The voxel fills the cube from its position to its position + (1, 1, 1). A point on a face of the voxel is in the voxel.
    private static bool Contains(int3 voxel, float3 point) =>
        point.X >= voxel.X && point.X <= voxel.X + 1
        && point.Y >= voxel.Y && point.Y <= voxel.Y + 1
        && point.Z >= voxel.Z && point.Z <= voxel.Z + 1;
}
