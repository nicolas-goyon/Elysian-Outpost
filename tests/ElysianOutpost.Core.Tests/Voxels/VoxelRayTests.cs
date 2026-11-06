// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Tests.Voxels;

public sealed class VoxelRayTests
{
    // Chunks of 4 x 4 x 4 voxels from -8 to 8 on each axis: 64 chunks. Thus many rays cross chunk borders and negative coordinates.
    private static readonly int3 _chunkSize = new(4, 4, 4);
    private const int WorldMin = -8;
    private const int WorldMax = 8;

    public static TheoryData<int, int, int> AxisDirections => new()
    {
        { 1, 0, 0 },
        { -1, 0, 0 },
        { 0, 1, 0 },
        { 0, -1, 0 },
        { 0, 0, 1 },
        { 0, 0, -1 },
    };

    [Theory]
    [MemberData(nameof(AxisDirections))]
    public void TryCast_AlongAnAxis_HitsTheVoxelOnTheAxis(int x, int y, int z)
    {
        var direction = new int3(x, y, z);
        int3 target = direction * 3;
        VoxelWorld world = MakeWorld(target);

        Assert.True(VoxelRay.TryCast(world, new float3(0.5f, 0.5f, 0.5f), new float3(x, y, z), out VoxelRayHit hit));

        Assert.Equal(target, hit.Voxel);
        Assert.Equal(-direction, hit.Normal);
        Assert.Equal(direction * 2, hit.EmptyVoxel);
        Assert.True(hit.HasEmptyVoxel);
        Assert.Equal(2.5f, hit.Distance, 1e-5f);
        Assert.Equal(7, hit.BlockType);
    }

    [Theory]
    [InlineData(1, 0, 0, VoxelFace.Xpos)]
    [InlineData(-1, 0, 0, VoxelFace.Xneg)]
    [InlineData(0, 1, 0, VoxelFace.Ypos)]
    [InlineData(0, -1, 0, VoxelFace.Yneg)]
    [InlineData(0, 0, 1, VoxelFace.Zpos)]
    [InlineData(0, 0, -1, VoxelFace.Zneg)]
    public void Face_IsTheFaceOfTheNormal(int x, int y, int z, VoxelFace face)
    {
        // A ray in the opposite direction of the normal crosses the face that has this normal.
        int3 normal = new(x, y, z);
        VoxelWorld world = MakeWorld(new int3(0, 0, 0));
        float3 origin = new float3(0.5f, 0.5f, 0.5f) + (new float3(x, y, z) * 3);

        Assert.True(VoxelRay.TryCast(world, origin, new float3(-x, -y, -z), out VoxelRayHit hit));

        Assert.Equal(normal, hit.Normal);
        Assert.Equal(face, hit.Face);
    }

    [Fact]
    public void TryCast_AlongAnAxis_IgnoresTheVoxelsBesideTheRay()
    {
        VoxelWorld world = MakeWorld(new int3(2, 1, 0), new int3(2, -1, 0), new int3(2, 0, 1), new int3(2, 0, -1), new int3(5, 0, 0));

        Assert.True(VoxelRay.TryCast(world, new float3(0.5f, 0.5f, 0.5f), new float3(1, 0, 0), out VoxelRayHit hit));

        Assert.Equal(new int3(5, 0, 0), hit.Voxel);
    }

    // The ray (0.5, 0.5, 0.5) + s × (2, 1, 0) crosses x = 1 at s = 0.25, y = 1 at s = 0.5 and x = 2 at s = 0.75.
    // Thus it goes through the voxels (0, 0, 0), (1, 0, 0), (1, 1, 0) and (2, 1, 0).
    [Fact]
    public void TryCast_Diagonal_GoesThroughTheVoxelsInTheirOrder()
    {
        VoxelWorld world = MakeWorld(new int3(2, 1, 0));
        float3 origin = new(0.5f, 0.5f, 0.5f);
        float3 direction = new(2, 1, 0);

        Assert.True(VoxelRay.TryCast(world, origin, direction, out VoxelRayHit hit));
        Assert.Equal(new int3(2, 1, 0), hit.Voxel);
        Assert.Equal(new int3(-1, 0, 0), hit.Normal);
        Assert.Equal(new int3(1, 1, 0), hit.EmptyVoxel);
        Assert.Equal(0.75f * MathF.Sqrt(5), hit.Distance, 1e-5f);

        // The voxel (1, 1, 0) is on the ray before (2, 1, 0). The ray enters it through its bottom face.
        world = MakeWorld(new int3(2, 1, 0), new int3(1, 1, 0));
        Assert.True(VoxelRay.TryCast(world, origin, direction, out hit));
        Assert.Equal(new int3(1, 1, 0), hit.Voxel);
        Assert.Equal(new int3(0, -1, 0), hit.Normal);
        Assert.Equal(0.5f * MathF.Sqrt(5), hit.Distance, 1e-5f);

        // The voxel (0, 1, 0) is beside the ray: the ray goes from (0, 0, 0) to (1, 0, 0), not through (0, 1, 0).
        world = MakeWorld(new int3(0, 1, 0), new int3(2, 1, 0));
        Assert.True(VoxelRay.TryCast(world, origin, direction, out hit));
        Assert.Equal(new int3(2, 1, 0), hit.Voxel);
    }

    [Fact]
    public void TryCast_ThroughAVoxelCorner_CrossesXThenYThenZ()
    {
        // The ray goes through the corner (1, 1, 1) of the voxel (0, 0, 0). The next voxel is (1, 0, 0), then (1, 1, 0), then (1, 1, 1).
        float3 origin = new(0.5f, 0.5f, 0.5f);
        float3 direction = new(1, 1, 1);

        Assert.True(VoxelRay.TryCast(MakeWorld(new int3(1, 0, 0)), origin, direction, out VoxelRayHit hit));
        Assert.Equal(new int3(-1, 0, 0), hit.Normal);
        Assert.True(VoxelRay.TryCast(MakeWorld(new int3(1, 1, 0)), origin, direction, out hit));
        Assert.Equal(new int3(0, -1, 0), hit.Normal);
        Assert.True(VoxelRay.TryCast(MakeWorld(new int3(1, 1, 1)), origin, direction, out hit));
        Assert.Equal(new int3(0, 0, -1), hit.Normal);
        Assert.Equal(0.5f * MathF.Sqrt(3), hit.Distance, 1e-5f);
    }

    // Random rays in a random world, against a reference that samples each ray every 0.0001 voxels.
    [Fact]
    public void TryCast_DiagonalRays_AgreeWithASampledRay()
    {
        var random = new Random(40);
        int checkedRays = 0;
        for (int i = 0; i < 300; i++)
        {
            var solid = new List<int3>();
            for (int n = 0; n < 200; n++)
            {
                solid.Add(new int3(random.Next(WorldMin, WorldMax), random.Next(WorldMin, WorldMax), random.Next(WorldMin, WorldMax)));
            }

            VoxelWorld world = MakeWorld([.. solid]);
            float3 origin = new(RandomCoordinate(random), RandomCoordinate(random), RandomCoordinate(random));
            float3 direction = new((float)((random.NextDouble() * 2) - 1), (float)((random.NextDouble() * 2) - 1), (float)((random.NextDouble() * 2) - 1));
            if (world.IsSolid(Floor(origin)) || float3.Dot(direction, direction) < 0.01f)
            {
                continue;
            }

            const float MaxDistance = 10f;
            if (!TrySample(world, origin, direction, MaxDistance, out VoxelRayHit expected))
            {
                // The sampled ray went through an edge or a corner. There, the order of the voxels is not certain.
                continue;
            }

            bool found = VoxelRay.TryCast(world, origin, direction, out VoxelRayHit hit, MaxDistance);
            Assert.Equal(expected.BlockType != 0, found);
            if (found)
            {
                Assert.Equal(expected.Voxel, hit.Voxel);
                Assert.Equal(expected.Normal, hit.Normal);
                Assert.Equal(expected.Distance, hit.Distance, 2e-4f);
            }

            checkedRays++;
        }

        // Most rays must not go near an edge or a corner. Otherwise the test checks almost nothing.
        Assert.True(checkedRays > 200, $"Only {checkedRays} rays were checked.");
    }

    [Fact]
    public void TryCast_AcrossChunkBorders_FindsTheVoxelInTheNextChunk()
    {
        // The chunks have 4 voxels on each axis. The voxel (5, 1, 2) is in the chunk at (4, 0, 0), the voxel (-3, 1, 2) in the chunk at (-4, 0, 0).
        VoxelWorld world = MakeWorld(new int3(5, 1, 2), new int3(-3, 1, 2), new int3(1, 6, 2));
        float3 origin = new(1.5f, 1.5f, 2.5f);

        Assert.True(VoxelRay.TryCast(world, origin, new float3(1, 0, 0), out VoxelRayHit hit));
        Assert.Equal(new int3(5, 1, 2), hit.Voxel);
        Assert.Equal(3.5f, hit.Distance, 1e-5f);

        Assert.True(VoxelRay.TryCast(world, origin, new float3(-1, 0, 0), out hit));
        Assert.Equal(new int3(-3, 1, 2), hit.Voxel);
        Assert.Equal(new int3(1, 0, 0), hit.Normal);
        Assert.Equal(new int3(-2, 1, 2), hit.EmptyVoxel);
        Assert.Equal(3.5f, hit.Distance, 1e-5f);

        Assert.True(VoxelRay.TryCast(world, origin, new float3(0, 1, 0), out hit));
        Assert.Equal(new int3(1, 6, 2), hit.Voxel);
    }

    [Fact]
    public void TryCast_WithNegativeCoordinates_UsesTheFloorOfThePosition()
    {
        // The origin (-5.5, -2.25, -4.5) is in the voxel (-6, -3, -5). The ray goes to -Z and enters (-6, -3, -7) at z = -6.
        VoxelWorld world = MakeWorld(new int3(-6, -3, -7), new int3(-5, -2, -7));

        Assert.True(VoxelRay.TryCast(world, new float3(-5.5f, -2.25f, -4.5f), new float3(0, 0, -1), out VoxelRayHit hit));

        Assert.Equal(new int3(-6, -3, -7), hit.Voxel);
        Assert.Equal(new int3(0, 0, 1), hit.Normal);
        Assert.Equal(new int3(-6, -3, -6), hit.EmptyVoxel);
        Assert.Equal(1.5f, hit.Distance, 1e-5f);
    }

    // The origin is on the boundary x = 1, between the voxels (0, 0, 0) and (1, 0, 0).
    // The ray enters the voxel on the side of its direction through the boundary face, at the distance 0.
    [Theory]
    [InlineData(-1, 0, 1)]
    [InlineData(1, 1, -1)]
    public void TryCast_FromAVoxelBoundary_EntersTheVoxelOnTheSideOfTheDirection(int directionX, int solidX, int normalX)
    {
        VoxelWorld world = MakeWorld(new int3(solidX, 0, 0));

        Assert.True(VoxelRay.TryCast(world, new float3(1f, 0.5f, 0.5f), new float3(directionX, 0, 0), out VoxelRayHit hit));

        Assert.Equal(new int3(solidX, 0, 0), hit.Voxel);
        Assert.Equal(new int3(normalX, 0, 0), hit.Normal);
        Assert.Equal(new int3(solidX + normalX, 0, 0), hit.EmptyVoxel);
        Assert.Equal(0f, hit.Distance);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void TryCast_FromAVoxelBoundary_IgnoresTheVoxelBehindTheOrigin(int directionX)
    {
        // The solid voxel is on the other side of the boundary x = 1. The ray leaves it and does not enter it.
        int behind = directionX < 0 ? 1 : 0;
        VoxelWorld world = MakeWorld(new int3(behind, 0, 0), new int3(behind + (directionX * 4), 0, 0));

        Assert.True(VoxelRay.TryCast(world, new float3(1f, 0.5f, 0.5f), new float3(directionX, 0, 0), out VoxelRayHit hit));

        Assert.Equal(new int3(behind + (directionX * 4), 0, 0), hit.Voxel);
        Assert.Equal(3f, hit.Distance, 1e-5f);
    }

    [Fact]
    public void TryCast_ParallelToAVoxelBoundary_StartsInTheVoxelAfterTheBoundary()
    {
        // The origin is on the boundary y = 1. The ray goes to +X along the boundary, in the voxels with y = 1.
        VoxelWorld world = MakeWorld(new int3(3, 0, 0), new int3(4, 1, 0));

        Assert.True(VoxelRay.TryCast(world, new float3(0.5f, 1f, 0.5f), new float3(1, 0, 0), out VoxelRayHit hit));

        Assert.Equal(new int3(4, 1, 0), hit.Voxel);
    }

    [Fact]
    public void TryCast_FromInsideASolidVoxel_HitsThatVoxelWithNoFace()
    {
        VoxelWorld world = MakeWorld(new int3(-1, 2, 0));

        Assert.True(VoxelRay.TryCast(world, new float3(-0.5f, 2.5f, 0.5f), new float3(0, 1, 0), out VoxelRayHit hit));

        Assert.Equal(new int3(-1, 2, 0), hit.Voxel);
        Assert.Equal(default, hit.Normal);
        Assert.Equal(VoxelFace.NONE, hit.Face);
        Assert.False(hit.HasEmptyVoxel);
        Assert.Equal(hit.Voxel, hit.EmptyVoxel);
        Assert.Equal(0f, hit.Distance);
    }

    [Theory]
    [InlineData(4.4f, false)]
    [InlineData(4.5f, true)]
    public void TryCast_StopsAtTheMaximumDistance(float maxDistance, bool expected)
    {
        // The ray enters the voxel (5, 0, 0) at the distance 4.5.
        VoxelWorld world = MakeWorld(new int3(5, 0, 0));

        Assert.Equal(expected, VoxelRay.TryCast(world, new float3(0.5f, 0.5f, 0.5f), new float3(1, 0, 0), out _, maxDistance));
    }

    [Fact]
    public void TryCast_HasADefaultMaximumDistanceOf64Voxels()
    {
        // One row of chunks of 16 voxels from x = 0 to x = 96.
        var world = new VoxelWorld(new int3(16, 1, 1));
        var voxels = new ushort[6][,,];
        for (int i = 0; i < voxels.Length; i++)
        {
            voxels[i] = new ushort[16, 1, 1];
            world.AddChunk(new int3(i * 16, 0, 0), voxels[i]);
        }

        // From x = 0.5, the ray enters the voxel 65 at the distance 64.5, and the voxel 64 at the distance 63.5.
        voxels[4][1, 0, 0] = 1;
        Assert.False(VoxelRay.TryCast(world, new float3(0.5f, 0.5f, 0.5f), new float3(1, 0, 0), out _));
        voxels[4][0, 0, 0] = 1;
        Assert.True(VoxelRay.TryCast(world, new float3(0.5f, 0.5f, 0.5f), new float3(1, 0, 0), out VoxelRayHit hit));
        Assert.Equal(new int3(64, 0, 0), hit.Voxel);
        Assert.Equal(64f, VoxelRay.DefaultMaxDistance);
    }

    [Fact]
    public void TryCast_FromAboveTheWorld_HitsTheTopFace()
    {
        // The world ends at y = 8. The origin is above it, as the camera usually is above the terrain.
        VoxelWorld world = MakeWorld(new int3(2, 7, -3));

        Assert.True(VoxelRay.TryCast(world, new float3(2.5f, 20f, -2.5f), new float3(0, -1, 0), out VoxelRayHit hit));

        Assert.Equal(new int3(2, 7, -3), hit.Voxel);
        Assert.Equal(VoxelFace.Ypos, hit.Face);
        Assert.Equal(new int3(2, 8, -3), hit.EmptyVoxel);
        Assert.Equal(12f, hit.Distance, 1e-5f);
    }

    // Outside the chunks, all the voxels are empty. Thus these tests cannot see where the ray stops: they check only the result.
    // The stop at the world limits only makes the ray faster.
    [Theory]
    [InlineData(20f, 0.5f, 1f, 0f)] // Outside the world on the X axis, to +X: away from the world.
    [InlineData(0.5f, 20f, 1f, 0f)] // Above the world and parallel to its top: it never enters the world.
    [InlineData(0.5f, 0.5f, 1f, 0f)] // In the world: it goes out at x = 8.
    [InlineData(0.5f, 0.5f, 1f, 0.5f)] // In the world: it goes out at y = 8.
    public void TryCast_ThatLeavesTheWorld_FindsNoVoxel(float x, float y, float directionX, float directionY)
    {
        // The solid voxels are not on these rays.
        VoxelWorld world = MakeWorld(new int3(-8, 0, 0), new int3(0, 0, -8));

        Assert.False(VoxelRay.TryCast(world, new float3(x, y, 0.5f), new float3(directionX, directionY, 0), out VoxelRayHit hit, 1000f));
        Assert.Equal(default, hit);
    }

    [Fact]
    public void TryCast_InAWorldWithNoChunk_FindsNoVoxel()
    {
        var world = new VoxelWorld(_chunkSize);
        Assert.False(VoxelRay.TryCast(world, default, new float3(0, -1, 0), out _));
    }

    [Fact]
    public void TryCast_DoesNotNeedAUnitDirection()
    {
        VoxelWorld world = MakeWorld(new int3(0, -3, 0));

        Assert.True(VoxelRay.TryCast(world, new float3(0.5f, 0.5f, 0.5f), new float3(0, -0.001f, 0), out VoxelRayHit small));
        Assert.True(VoxelRay.TryCast(world, new float3(0.5f, 0.5f, 0.5f), new float3(0, -1000f, 0), out VoxelRayHit large));
        Assert.Equal(2.5f, small.Distance, 1e-5f);
        Assert.Equal(small, large);
    }

    [Fact]
    public void TryCast_WithInvalidArguments_Throws()
    {
        VoxelWorld world = MakeWorld();
        float3 origin = new(0.5f, 0.5f, 0.5f);
        float3 direction = new(0, 0, 1);

        Assert.Throws<ArgumentNullException>(() => VoxelRay.TryCast(null!, origin, direction, out _));
        Assert.Throws<ArgumentException>(() => VoxelRay.TryCast(world, origin, default, out _));
        Assert.Throws<ArgumentException>(() => VoxelRay.TryCast(world, origin, new float3(float.NaN, 0, 1), out _));
        Assert.Throws<ArgumentException>(() => VoxelRay.TryCast(world, new float3(0, float.PositiveInfinity, 0), direction, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => VoxelRay.TryCast(world, origin, direction, out _, -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => VoxelRay.TryCast(world, origin, direction, out _, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => VoxelRay.TryCast(world, origin, direction, out _, float.PositiveInfinity));
    }

    // Makes the world from -8 to 8 on each axis, with the block type 7 at the given positions.
    private static VoxelWorld MakeWorld(params int3[] solid)
    {
        var world = new VoxelWorld(_chunkSize);
        var chunks = new Dictionary<int3, ushort[,,]>();
        for (int x = WorldMin; x < WorldMax; x += 4)
        {
            for (int y = WorldMin; y < WorldMax; y += 4)
            {
                for (int z = WorldMin; z < WorldMax; z += 4)
                {
                    chunks[new int3(x, y, z)] = new ushort[4, 4, 4];
                }
            }
        }

        foreach (int3 p in solid)
        {
            int3 chunk = new(FloorTo4(p.X), FloorTo4(p.Y), FloorTo4(p.Z));
            chunks[chunk][p.X - chunk.X, p.Y - chunk.Y, p.Z - chunk.Z] = 7;
        }

        foreach ((int3 position, ushort[,,] voxels) in chunks)
        {
            world.AddChunk(position, voxels);
        }

        return world;
    }

    private static int FloorTo4(int value) => (int)Math.Floor(value / 4.0) * 4;

    private static int3 Floor(float3 p) => new((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y), (int)MathF.Floor(p.Z));

    private static float RandomCoordinate(Random random) => (float)((random.NextDouble() * 12) - 6);

    // The reference: it samples the ray every 0.0001 voxels. It returns false if the ray goes through an edge or a corner,
    // because then two samples in a row can be in voxels that are not neighbors. Otherwise BlockType is 0 if no solid voxel is on the ray.
    private static bool TrySample(VoxelWorld world, float3 origin, float3 direction, float maxDistance, out VoxelRayHit hit)
    {
        const double Step = 1e-4;
        double length = Math.Sqrt(((double)direction.X * direction.X) + ((double)direction.Y * direction.Y) + ((double)direction.Z * direction.Z));
        double dx = direction.X / length, dy = direction.Y / length, dz = direction.Z / length;
        int3 previous = Floor(origin);
        hit = default;
        for (double t = Step; t <= maxDistance + Step; t += Step)
        {
            var cell = new int3(
                (int)Math.Floor(origin.X + (dx * t)),
                (int)Math.Floor(origin.Y + (dy * t)),
                (int)Math.Floor(origin.Z + (dz * t)));
            if (cell == previous)
            {
                continue;
            }

            int3 change = cell - previous;
            if (Math.Abs(change.X) + Math.Abs(change.Y) + Math.Abs(change.Z) != 1)
            {
                return false;
            }

            if (world.IsSolid(cell))
            {
                // The hit is between this sample and the previous sample. Near the maximum distance, the result is not certain.
                double distance = t - (Step / 2);
                if (Math.Abs(distance - maxDistance) < Step)
                {
                    return false;
                }

                if (distance <= maxDistance)
                {
                    hit = new VoxelRayHit(cell, -change, (float)distance, world.GetVoxel(cell));
                }

                return true;
            }

            previous = cell;
        }

        return true;
    }
}
