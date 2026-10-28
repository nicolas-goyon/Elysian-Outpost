// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Tests.Voxels;

public sealed class VoxelWorldTests
{
    private static readonly int3 _chunkSize = new(4, 8, 2);

    [Fact]
    public void GetVoxel_ReadsTheVoxelOfTheChunkThatContainsThePosition()
    {
        var world = new VoxelWorld(_chunkSize);
        ushort[,,] voxels = new ushort[4, 8, 2];
        voxels[1, 7, 0] = 5;
        world.AddChunk(new int3(-4, -8, 2), voxels);

        Assert.Equal(5, world.GetVoxel(new int3(-3, -1, 2)));
        Assert.True(world.IsSolid(new int3(-3, -1, 2)));
        Assert.Equal(0, world.GetVoxel(new int3(-3, -1, 3)));
        Assert.False(world.IsSolid(new int3(-3, -1, 3)));
    }

    // The chunk of a negative position must use the floor of the division, not the division that rounds toward zero.
    [Theory]
    [InlineData(-1, -1, -1)]
    [InlineData(-4, -8, -2)]
    [InlineData(0, 0, 0)]
    [InlineData(3, 7, 1)]
    public void GetVoxel_FindsTheChunkOfEachPosition(int x, int y, int z)
    {
        var world = new VoxelWorld(_chunkSize);
        var position = new int3(x, y, z);
        foreach (int3 chunk in new[] { new int3(-4, -8, -2), new int3(0, 0, 0), new int3(-4, 0, 0), new int3(0, -8, -2) })
        {
            ushort[,,] voxels = new ushort[4, 8, 2];
            if (x >= chunk.X && x < chunk.X + 4 && y >= chunk.Y && y < chunk.Y + 8 && z >= chunk.Z && z < chunk.Z + 2)
            {
                voxels[x - chunk.X, y - chunk.Y, z - chunk.Z] = 9;
            }

            world.AddChunk(chunk, voxels);
        }

        Assert.Equal(9, world.GetVoxel(position));
    }

    [Fact]
    public void GetVoxel_OutsideAllTheChunks_ReturnsZero()
    {
        var world = new VoxelWorld(_chunkSize);
        world.AddChunk(default, Filled(1));

        Assert.Equal(0, world.GetVoxel(new int3(4, 0, 0)));
        Assert.Equal(0, world.GetVoxel(new int3(-1, 0, 0)));
        Assert.Equal(0, world.GetVoxel(new int3(0, 8, 0)));
        Assert.Equal(0, world.GetVoxel(new int3(0, 0, -1)));
        Assert.Equal(0, world.GetVoxel(new int3(int.MinValue, int.MaxValue, int.MinValue)));
    }

    [Fact]
    public void MinAndMax_AreTheBoxAroundAllTheChunks()
    {
        var world = new VoxelWorld(_chunkSize);
        Assert.Equal(0, world.ChunkCount);
        Assert.Equal(default, world.Min);
        Assert.Equal(default, world.Max);

        world.AddChunk(new int3(4, 0, 0), Filled(0));
        Assert.Equal(new int3(4, 0, 0), world.Min);
        Assert.Equal(new int3(8, 8, 2), world.Max);

        world.AddChunk(new int3(-8, 16, -2), Filled(0));
        Assert.Equal(2, world.ChunkCount);
        Assert.Equal(new int3(-8, 0, -2), world.Min);
        Assert.Equal(new int3(8, 24, 2), world.Max);
    }

    [Fact]
    public void AddChunk_WithAnArrayOfAnotherSize_Throws()
    {
        var world = new VoxelWorld(_chunkSize);
        Assert.Throws<ArgumentException>(() => world.AddChunk(default, new ushort[4, 8, 3]));
        Assert.Throws<ArgumentNullException>(() => world.AddChunk(default, null!));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, -4, 0)]
    [InlineData(0, 0, 3)]
    public void AddChunk_AtAPositionThatIsNotAMultipleOfTheChunkSize_Throws(int x, int y, int z)
    {
        var world = new VoxelWorld(_chunkSize);
        Assert.Throws<ArgumentException>(() => world.AddChunk(new int3(x, y, z), Filled(0)));
    }

    [Fact]
    public void AddChunk_TwoTimesAtTheSamePosition_Throws()
    {
        var world = new VoxelWorld(_chunkSize);
        world.AddChunk(new int3(-4, 8, 2), Filled(1));
        Assert.Throws<ArgumentException>(() => world.AddChunk(new int3(-4, 8, 2), Filled(2)));
        Assert.Equal(1, world.GetVoxel(new int3(-4, 8, 2)));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, 0)]
    public void Constructor_WithASizeOfZeroOrLess_Throws(int x, int y, int z) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new VoxelWorld(new int3(x, y, z)));

    private static ushort[,,] Filled(ushort block)
    {
        ushort[,,] voxels = new ushort[4, 8, 2];
        foreach (int x in Enumerable.Range(0, 4))
        {
            foreach (int y in Enumerable.Range(0, 8))
            {
                foreach (int z in Enumerable.Range(0, 2))
                {
                    voxels[x, y, z] = block;
                }
            }
        }

        return voxels;
    }
}
