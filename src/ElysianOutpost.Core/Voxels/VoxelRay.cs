// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Voxels;

/// <summary>
/// Finds the first solid voxel on a ray. The legacy project used <c>Physics.Raycast</c> on the mesh colliders of the chunks.
/// This class reads the voxel data directly and needs no collider.
/// </summary>
/// <remarks>
/// <para>
/// The algorithm is the voxel traversal of Amanatides and Woo ("A Fast Voxel Traversal Algorithm for Ray Tracing", 1987).
/// It goes through the voxels on the ray in their order, one voxel boundary at a time.
/// The voxel at the integer position (x, y, z) fills the cube from (x, y, z) to (x + 1, y + 1, z + 1) in world units.
/// </para>
/// <para>
/// When the ray goes through an edge or a corner of a voxel, it crosses two or three boundaries at the same distance.
/// The algorithm then crosses the X boundary first, then the Y boundary, then the Z boundary.
/// </para>
/// <para>
/// An origin exactly on a voxel boundary is on a face of two voxels. The ray starts in the voxel on the side of the direction,
/// and it enters that voxel through that face at the distance 0. If the ray is parallel to the boundary, it starts in the voxel after the boundary.
/// </para>
/// <para>
/// The calculations use double precision. Rounding errors are much smaller than one voxel.
/// </para>
/// </remarks>
public static class VoxelRay
{
    /// <summary>
    /// The default maximum distance of a ray, in voxels.
    /// </summary>
    public const float DefaultMaxDistance = 64f;

    /// <summary>
    /// Finds the first solid voxel on a ray.
    /// </summary>
    /// <param name="world">The voxels of the world.</param>
    /// <param name="origin">The start of the ray, in world units. It can be outside the world.</param>
    /// <param name="direction">The direction of the ray. The length is not important, but it must not be zero.</param>
    /// <param name="hit">The solid voxel, if the method returns <c>true</c>.</param>
    /// <param name="maxDistance">
    /// The maximum distance from the origin to the hit, in world units (one unit is one voxel).
    /// The ray finds a voxel only if it enters the voxel at this distance or nearer.
    /// </param>
    /// <returns>
    /// <c>true</c> if the ray hits a solid voxel. <c>false</c> if the ray gets to the maximum distance,
    /// or goes out of the world limits (<see cref="VoxelWorld.Min"/> and <see cref="VoxelWorld.Max"/>), before a solid voxel.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A coordinate of the origin or of the direction is not finite, or the direction is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum distance is less than zero or not finite.</exception>
    public static bool TryCast(VoxelWorld world, float3 origin, float3 direction, out VoxelRayHit hit, float maxDistance = DefaultMaxDistance)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y) || !float.IsFinite(origin.Z))
        {
            throw new ArgumentException("The coordinates of the origin must be finite.", nameof(origin));
        }

        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
        {
            throw new ArgumentException("The coordinates of the direction must be finite.", nameof(direction));
        }

        if (!(maxDistance >= 0f) || !float.IsFinite(maxDistance))
        {
            throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "The maximum distance must be finite and zero or more.");
        }

        double length = Math.Sqrt(((double)direction.X * direction.X) + ((double)direction.Y * direction.Y) + ((double)direction.Z * direction.Z));
        if (length == 0)
        {
            throw new ArgumentException("The direction must not be zero.", nameof(direction));
        }

        hit = default;
        if (world.ChunkCount == 0)
        {
            return false;
        }

        Span<double> start = [origin.X, origin.Y, origin.Z];
        Span<double> dir = [direction.X / length, direction.Y / length, direction.Z / length];
        Span<int> min = [world.Min.X, world.Min.Y, world.Min.Z];
        Span<int> max = [world.Max.X, world.Max.Y, world.Max.Z];

        // The ray stops at the maximum distance, or where it goes out of the box of the world (the slab method).
        double limit = maxDistance;
        for (int axis = 0; axis < 3; axis++)
        {
            if (dir[axis] > 0)
            {
                limit = Math.Min(limit, (max[axis] - start[axis]) / dir[axis]);
            }
            else if (dir[axis] < 0)
            {
                limit = Math.Min(limit, (min[axis] - start[axis]) / dir[axis]);
            }
            else if (start[axis] < min[axis] || start[axis] >= max[axis])
            {
                // The ray is parallel to the two boundaries of this axis, and it is not between them.
                return false;
            }
        }

        // The first voxel of the ray. On a boundary, a ray in the negative direction starts in the voxel before the boundary.
        // Then the origin is on a face of the first voxel, and the ray enters it through that face (the first such axis in the order X, Y, Z).
        Span<int> cell = stackalloc int[3];
        int entryAxis = -1;
        for (int axis = 0; axis < 3; axis++)
        {
            double floor = Math.Floor(start[axis]);
            cell[axis] = (int)floor;
            if (floor == start[axis] && dir[axis] != 0)
            {
                cell[axis] -= dir[axis] < 0 ? 1 : 0;
                entryAxis = entryAxis < 0 ? axis : entryAxis;
            }
        }

        var first = new int3(cell[0], cell[1], cell[2]);
        ushort firstBlock = world.GetVoxel(first);
        if (firstBlock != 0)
        {
            Span<int> entry = [0, 0, 0];
            if (entryAxis >= 0)
            {
                entry[entryAxis] = dir[entryAxis] < 0 ? 1 : -1;
            }

            hit = new VoxelRayHit(first, new int3(entry[0], entry[1], entry[2]), 0f, firstBlock);
            return true;
        }

        // next[axis]: the distance to the next voxel boundary on this axis. delta[axis]: the distance between two boundaries on this axis.
        Span<int> step = stackalloc int[3];
        Span<double> next = stackalloc double[3];
        Span<double> delta = stackalloc double[3];
        for (int axis = 0; axis < 3; axis++)
        {
            if (dir[axis] > 0)
            {
                step[axis] = 1;
                next[axis] = (cell[axis] + 1 - start[axis]) / dir[axis];
                delta[axis] = 1 / dir[axis];
            }
            else if (dir[axis] < 0)
            {
                step[axis] = -1;
                next[axis] = (cell[axis] - start[axis]) / dir[axis];
                delta[axis] = -1 / dir[axis];
            }
            else
            {
                next[axis] = double.PositiveInfinity;
                delta[axis] = double.PositiveInfinity;
            }
        }

        while (true)
        {
            // The axis with the nearest boundary. The comparisons keep the order X, Y, Z for equal distances.
            int axis = next[0] <= next[1]
                ? (next[0] <= next[2] ? 0 : 2)
                : (next[1] <= next[2] ? 1 : 2);
            double distance = next[axis];
            if (distance > limit)
            {
                return false;
            }

            cell[axis] += step[axis];
            next[axis] += delta[axis];
            var position = new int3(cell[0], cell[1], cell[2]);
            ushort block = world.GetVoxel(position);
            if (block != 0)
            {
                // The ray enters the voxel through the face that points back to the previous voxel.
                Span<int> normal = [0, 0, 0];
                normal[axis] = -step[axis];
                hit = new VoxelRayHit(position, new int3(normal[0], normal[1], normal[2]), (float)distance, block);
                return true;
            }
        }
    }
}
