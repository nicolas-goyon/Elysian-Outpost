// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// Tests of the faces between chunks (#48): the mesher reads the border voxels of the neighbor chunks in the voxel world.
/// </summary>
public sealed class ChunkBorderTests
{
    private const ushort Stone = 22;
    private const ushort Dirt = 11;

    // Chunks of 4 × 4 × 4 voxels.
    private static readonly int3 _size = new(4, 4, 4);

    [Fact]
    public void Mesh_TwoSolidChunksSideBySide_HidesTheFacesBetweenThem()
    {
        var world = new VoxelWorld(_size);
        world.AddChunk(new int3(0, 0, 0), Filled(Stone));
        world.AddChunk(new int3(4, 0, 0), Filled(Dirt));
        var mesher = new GreedyMesher();

        List<MeshQuad> left = mesher.Mesh(Voxels(world, new int3(0, 0, 0)), world, new int3(0, 0, 0));
        List<MeshQuad> right = mesher.Mesh(Voxels(world, new int3(4, 0, 0)), world, new int3(4, 0, 0));

        // Each chunk shows five sides: one quad for each side. The side between the chunks has no quad.
        Assert.Equal(5, left.Count);
        Assert.Equal(5, right.Count);
        Assert.DoesNotContain(left, quad => quad.Normal == System.Numerics.Vector3.UnitX);
        Assert.DoesNotContain(right, quad => quad.Normal == -System.Numerics.Vector3.UnitX);
    }

    [Fact]
    public void Mesh_WithAMissingNeighbor_ShowsTheBorderFaces()
    {
        var world = new VoxelWorld(_size);
        world.AddChunk(new int3(0, 0, 0), Filled(Stone));
        var mesher = new GreedyMesher();

        List<MeshQuad> withWorld = mesher.Mesh(Voxels(world, default), world, default);
        List<MeshQuad> alone = mesher.Mesh(Voxels(world, default));

        Assert.Equal(6, withWorld.Count);
        Assert.Equal(alone, withWorld);
    }

    [Fact]
    public void Mesh_WithAWorldOfAnotherChunkSize_Throws()
    {
        var world = new VoxelWorld(new int3(8, 4, 4));
        Assert.Throws<ArgumentException>(() => new GreedyMesher().Mesh(new ushort[4, 4, 4], world, default));
    }

    [Theory]
    [InlineData(1, 1, 1, 1)] // Inside the chunk: no neighbor.
    [InlineData(0, 1, 1, 2)] // On the -X border: the chunk at x = -4.
    [InlineData(3, 1, 1, 2)] // On the +X border: the chunk at x = 4.
    [InlineData(0, 0, 3, 4)] // On a corner: three neighbors.
    [InlineData(3, 3, 3, 4)] // On the opposite corner.
    public void GetChunksToMesh_GivesTheChunkAndTheNeighborsThatTouchTheVoxel(int x, int y, int z, int expectedCount)
    {
        VoxelWorld world = MakeWorld(new Random(1), 0.5);
        Span<int3> chunks = stackalloc int3[4];
        var voxel = new int3(x, y, z);

        int count = world.GetChunksToMesh(voxel, chunks);

        Assert.Equal(expectedCount, count);
        Assert.Equal(default, chunks[0]);
        for (int i = 1; i < count; i++)
        {
            int3 offset = chunks[i] - chunks[0];
            Assert.Equal(4, Math.Abs(offset.X) + Math.Abs(offset.Y) + Math.Abs(offset.Z));
        }
    }

    [Fact]
    public void GetChunksToMesh_OutsideTheChunks_GivesNoChunk()
    {
        VoxelWorld world = MakeWorld(new Random(1), 0.5);
        Span<int3> chunks = stackalloc int3[4];
        Assert.Equal(0, world.GetChunksToMesh(new int3(100, 0, 0), chunks));
        Assert.Throws<ArgumentException>(() => world.GetChunksToMesh(default, new int3[3]));
    }

    [Fact]
    public void GetChunksToMesh_AtTheEdgeOfTheWorld_GivesOnlyTheChunksOfTheWorld()
    {
        VoxelWorld world = MakeWorld(new Random(1), 0.5);
        Span<int3> chunks = stackalloc int3[4];

        // (7, 7, 7) is the last voxel of the world from -8 to 8: its neighbors on the + sides are outside the world.
        Assert.Equal(1, world.GetChunksToMesh(new int3(7, 7, 7), chunks));
    }

    // The acceptance test of #48: random edits on the chunk borders, then a new mesh of the chunks of GetChunksToMesh.
    // After each group of edits, all the meshes must cover each visible face of the world exactly once, with no face between chunks.
    [Fact]
    public void Edits_OnTheChunkBorders_LeaveNoHoleAndNoHiddenFace()
    {
        var random = new Random(48);
        VoxelWorld world = MakeWorld(random, 0.6);
        var mesher = new GreedyMesher();
        var meshes = new Dictionary<int3, List<MeshQuad>>();
        foreach (int3 chunk in ChunkPositions())
        {
            meshes[chunk] = mesher.Mesh(Voxels(world, chunk), world, chunk);
        }

        AssertAllMeshesAreCorrect(world, meshes);
        Span<int3> changed = stackalloc int3[4];
        for (int round = 0; round < 20; round++)
        {
            var toMesh = new HashSet<int3>();
            for (int edit = 0; edit < 10; edit++)
            {
                // A voxel on a border: one coordinate is the first or the last of its chunk.
                int3 voxel = RandomBorderVoxel(random);
                Assert.True(world.TrySetVoxel(voxel, world.IsSolid(voxel) ? (ushort)0 : Stone, out _));
                int count = world.GetChunksToMesh(voxel, changed);
                for (int i = 0; i < count; i++)
                {
                    toMesh.Add(changed[i]);
                }
            }

            foreach (int3 chunk in toMesh)
            {
                meshes[chunk] = mesher.Mesh(Voxels(world, chunk), world, chunk);
            }

            AssertAllMeshesAreCorrect(world, meshes);
        }
    }

    private static void AssertAllMeshesAreCorrect(VoxelWorld world, Dictionary<int3, List<MeshQuad>> meshes)
    {
        foreach ((int3 chunk, List<MeshQuad> quads) in meshes)
        {
            MeshCoverage.AssertCoversEachVisibleFaceOnce(
                Voxels(world, chunk),
                quads,
                default,
                position => world.GetVoxel(chunk + new int3(position.X, position.Y, position.Z)));
        }
    }

    // The world from -8 to 8 on each axis: 4 × 4 × 4 chunks, with random stone voxels.
    private static VoxelWorld MakeWorld(Random random, double fill)
    {
        var world = new VoxelWorld(_size);
        foreach (int3 chunk in ChunkPositions())
        {
            var voxels = new ushort[4, 4, 4];
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    for (int z = 0; z < 4; z++)
                    {
                        voxels[x, y, z] = random.NextDouble() < fill ? Stone : (ushort)0;
                    }
                }
            }

            world.AddChunk(chunk, voxels);
        }

        return world;
    }

    private static IEnumerable<int3> ChunkPositions()
    {
        for (int x = -8; x < 8; x += 4)
        {
            for (int y = -8; y < 8; y += 4)
            {
                for (int z = -8; z < 8; z += 4)
                {
                    yield return new int3(x, y, z);
                }
            }
        }
    }

    private static int3 RandomBorderVoxel(Random random)
    {
        Span<int> coordinates = [random.Next(-8, 8), random.Next(-8, 8), random.Next(-8, 8)];
        int axis = random.Next(3);
        int chunkStart = (int)Math.Floor(coordinates[axis] / 4.0) * 4;
        coordinates[axis] = chunkStart + (random.Next(2) == 0 ? 0 : 3);
        return new int3(coordinates[0], coordinates[1], coordinates[2]);
    }

    private static ushort[,,] Voxels(VoxelWorld world, int3 chunk)
    {
        Assert.True(world.TryGetChunk(chunk, out ushort[,,]? voxels));
        return voxels;
    }

    private static ushort[,,] Filled(ushort block)
    {
        var voxels = new ushort[4, 4, 4];
        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                for (int z = 0; z < 4; z++)
                {
                    voxels[x, y, z] = block;
                }
            }
        }

        return voxels;
    }
}
