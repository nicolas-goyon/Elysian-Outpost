// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Tests.Voxels;

public sealed class VoxelEditsTests
{
    // Two chunks of 4 x 4 x 4 voxels on the X axis: from x = -4 to x = 4. The floor is the layer y = 0.
    private static VoxelWorld MakeWorld()
    {
        var world = new VoxelWorld(new int3(4, 4, 4));
        foreach (int x in new[] { -4, 0 })
        {
            ushort[,,] voxels = new ushort[4, 4, 4];
            for (int i = 0; i < 4; i++)
            {
                for (int k = 0; k < 4; k++)
                {
                    voxels[i, 0, k] = 22;
                }
            }

            world.AddChunk(new int3(x, 0, 0), voxels);
        }

        return world;
    }

    private static VoxelRayHit CastDown(VoxelWorld world, float x, float z, float height = 3.5f)
    {
        Assert.True(VoxelRay.TryCast(world, new float3(x, height, z), new float3(0, -1, 0), out VoxelRayHit hit));
        return hit;
    }

    [Fact]
    public void TryRemove_RemovesTheHitVoxel_AndGivesItsChunk()
    {
        VoxelWorld world = MakeWorld();
        VoxelRayHit hit = CastDown(world, -0.5f, 1.5f);

        Assert.True(VoxelEdits.TryRemove(world, hit, out int3 chunk));

        Assert.Equal(new int3(-4, 0, 0), chunk);
        Assert.False(world.IsSolid(new int3(-1, 0, 1)));
        Assert.True(world.IsSolid(new int3(0, 0, 1)));
        Assert.False(VoxelRay.TryCast(world, new float3(-0.5f, 3.5f, 1.5f), new float3(0, -1, 0), out _));
    }

    [Fact]
    public void TryRemove_AVoxelThatIsAlreadyEmpty_ReturnsFalse()
    {
        VoxelWorld world = MakeWorld();
        VoxelRayHit hit = CastDown(world, 0.5f, 0.5f);
        Assert.True(VoxelEdits.TryRemove(world, hit, out _));

        Assert.False(VoxelEdits.TryRemove(world, hit, out _));
    }

    [Fact]
    public void TryPlace_PlacesTheBlockTypeAtTheEmptyPosition()
    {
        VoxelWorld world = MakeWorld();
        VoxelRayHit hit = CastDown(world, 2.5f, 3.5f);

        Assert.True(VoxelEdits.TryPlace(world, hit, 16, new float3(2.5f, 3.5f, 3.5f), out int3 chunk));

        Assert.Equal(new int3(0, 0, 0), chunk);
        Assert.Equal(16, world.GetVoxel(new int3(2, 1, 3)));
        Assert.Equal(new int3(2, 1, 3), CastDown(world, 2.5f, 3.5f).Voxel);
    }

    [Fact]
    public void TryPlace_AcrossAChunkBorder_ChangesTheChunkOfTheEmptyPosition()
    {
        // The ray goes to -X along the layer y = 1 and hits a wall at x = -1, in the chunk at x = -4. The empty position is in the chunk at x = 0.
        VoxelWorld world = MakeWorld();
        Assert.True(world.TrySetVoxel(new int3(-1, 1, 2), 22, out _));
        Assert.True(VoxelRay.TryCast(world, new float3(3.5f, 1.5f, 2.5f), new float3(-1, 0, 0), out VoxelRayHit hit));

        Assert.True(VoxelEdits.TryPlace(world, hit, 11, new float3(3.5f, 1.5f, 2.5f), out int3 chunk));

        Assert.Equal(new int3(0, 0, 0), chunk);
        Assert.Equal(11, world.GetVoxel(new int3(0, 1, 2)));
    }

    [Fact]
    public void TryPlace_OutsideTheChunks_ReturnsFalse()
    {
        // The hit is on the top layer of the world. The empty position (y = 4) is above all the chunks.
        VoxelWorld world = MakeWorld();
        Assert.True(world.TrySetVoxel(new int3(1, 3, 1), 22, out _));
        VoxelRayHit hit = CastDown(world, 1.5f, 1.5f, 10f);
        Assert.Equal(new int3(1, 4, 1), hit.EmptyVoxel);

        Assert.False(VoxelEdits.TryPlace(world, hit, 16, new float3(1.5f, 10f, 1.5f), out _));
    }

    [Theory]
    [InlineData(2.5f, 1.5f, 2.5f)] // In the voxel.
    [InlineData(2.5f, 2f, 2.5f)] // On the top face of the voxel.
    [InlineData(3f, 1f, 3f)] // On a corner of the voxel.
    public void TryPlace_InTheVoxelOfTheCamera_ReturnsFalse(float x, float y, float z)
    {
        VoxelWorld world = MakeWorld();
        VoxelRayHit hit = CastDown(world, 2.5f, 2.5f);

        Assert.False(VoxelEdits.TryPlace(world, hit, 16, new float3(x, y, z), out _));

        Assert.False(world.IsSolid(new int3(2, 1, 2)));
    }

    [Fact]
    public void TryPlace_FromInsideASolidVoxel_ReturnsFalse()
    {
        VoxelWorld world = MakeWorld();
        Assert.True(VoxelRay.TryCast(world, new float3(0.5f, 0.5f, 0.5f), new float3(0, 1, 0), out VoxelRayHit hit));
        Assert.False(hit.HasEmptyVoxel);

        Assert.False(VoxelEdits.TryPlace(world, hit, 16, new float3(0.5f, 0.5f, 0.5f), out _));
    }

    [Fact]
    public void TryPlace_AtAPositionThatIsNotEmpty_ReturnsFalse()
    {
        // The camera is on the boundary y = 2 between two solid voxels. The ray down enters the lower voxel at the distance 0,
        // and its empty position is the upper voxel.
        VoxelWorld world = MakeWorld();
        Assert.True(world.TrySetVoxel(new int3(1, 1, 1), 22, out _));
        Assert.True(world.TrySetVoxel(new int3(1, 2, 1), 22, out _));
        VoxelRayHit hit = CastDown(world, 1.5f, 1.5f, 2f);
        Assert.Equal(new int3(1, 2, 1), hit.EmptyVoxel);

        Assert.False(VoxelEdits.TryPlace(world, hit, 16, new float3(9f, 9f, 9f), out _));

        Assert.Equal(22, world.GetVoxel(new int3(1, 2, 1)));
    }

    [Fact]
    public void TryPlace_WithInvalidArguments_Throws()
    {
        VoxelWorld world = MakeWorld();
        VoxelRayHit hit = CastDown(world, 1.5f, 1.5f);

        Assert.Throws<ArgumentNullException>(() => VoxelEdits.TryPlace(null!, hit, 16, default, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => VoxelEdits.TryPlace(world, hit, 0, default, out _));
        Assert.Throws<ArgumentNullException>(() => VoxelEdits.TryRemove(null!, hit, out _));
    }
}
