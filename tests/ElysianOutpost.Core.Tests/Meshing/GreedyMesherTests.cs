// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// Tests of <see cref="GreedyMesher"/> that <see cref="MesherTests"/> does not cover: the two inputs and the allocations (#45).
/// </summary>
public sealed class GreedyMesherTests
{
    [Fact]
    public void Mesh_OfAnArrayAndOfAChunk_GivesTheSameQuads()
    {
        ushort[,,] voxels = new MainGeneration(new int3(16, 64, 16), 123).GenerateChunkAt(default);
        var mesher = new GreedyMesher();

        List<MeshQuad> fromArray = mesher.Mesh(voxels);
        List<MeshQuad> fromChunk = mesher.Mesh(new Chunk(voxels, default));

        Assert.NotEmpty(fromArray);
        Assert.Equal(fromArray, fromChunk);
    }

    // The renderer lights each face with the normal of its quad (#46). Before #46, it calculated the normal from the vertices.
    // Thus, for the same lighting, the normal of each quad must be the normal of its triangle (0, 1, 2).
    [Fact]
    public void Mesh_OfTheTerrain_GivesTheNormalOfTheWindingOrderToEachQuad()
    {
        var generation = new MainGeneration(new int3(16, 64, 16), 123);
        var mesher = new GreedyMesher();
        int quadCount = 0;
        foreach (int3 position in WorldSettings.SampleScene.GetChunkPositions(default).Take(64))
        {
            foreach (MeshQuad quad in mesher.Mesh(generation.GenerateChunkAt(position)))
            {
                Vector3 winding = Vector3.Normalize(Vector3.Cross(quad.Vertex1 - quad.Vertex0, quad.Vertex2 - quad.Vertex0));
                Assert.Equal(winding, quad.Normal);
                quadCount++;
            }
        }

        Assert.True(quadCount > 1000, $"Only {quadCount} quads were checked.");
    }

    [Fact]
    public void Mesh_OfAnEmptyArray_GivesNoQuads()
    {
        Assert.Empty(new GreedyMesher().Mesh(new ushort[0, 4, 4]));
    }

    [Fact]
    public void Mesh_AfterTheFirstChunk_AllocatesOnlyTheResult()
    {
        // A checkerboard: 8,192 solid voxels with six visible faces each, thus 49,152 quads.
        var voxels = new ushort[16, 64, 16];
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    voxels[x, y, z] = (x + y + z) % 2 == 0 ? (ushort)22 : (ushort)0;
                }
            }
        }

        var mesher = new GreedyMesher();
        mesher.Mesh(voxels);

        long before = GC.GetAllocatedBytesForCurrentThread();
        List<MeshQuad> quads = mesher.Mesh(voxels);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The result: one MeshQuad object (about 88 bytes) for each quad, and the array of the list (8 bytes for each reference, up to two times the count).
        Assert.Equal(49_152, quads.Count);
        Assert.InRange(allocated, 1, quads.Count * (88 + 16) + 4096);
    }
}
