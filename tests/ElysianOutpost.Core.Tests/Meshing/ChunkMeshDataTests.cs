// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;
using Vector3 = System.Numerics.Vector3;

namespace ElysianOutpost.Core.Tests.Meshing;

public sealed class ChunkMeshDataTests
{
    private const ushort Stone = 22;
    private const ushort Grass = 16;

    // The vertex shader reads the vertex buffer with these offsets. A change of the layout must also change the renderer.
    [Fact]
    public void ChunkVertex_HasTheLayoutOfTheVertexBuffer()
    {
        Assert.Equal(ChunkVertex.SizeInBytes, Unsafe.SizeOf<ChunkVertex>());
        Assert.Equal(ChunkVertex.SizeInBytes, Marshal.SizeOf<ChunkVertex>());
        Assert.Equal(0, Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Position)));
        Assert.Equal(12, Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Normal)));
        Assert.Equal(24, Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Color)));
    }

    [Fact]
    public void Build_NoQuad_GivesNoVertexAndNoIndex()
    {
        ChunkMeshData mesh = ChunkMeshData.Build([], BlockPalette.Game);

        Assert.Empty(mesh.Vertices);
        Assert.Empty(mesh.Indices);
        Assert.Equal(0, mesh.QuadCount);
    }

    // The legacy project draws each quad as the triangles (0, 1, 2) and (2, 3, 0).
    [Fact]
    public void Build_OneQuad_GivesItsFourVerticesAndTwoTriangles()
    {
        MeshQuad quad = Quad(new Vector3(1, 2, 3), new Vector3(1, 3, 3), new Vector3(2, 3, 3), new Vector3(2, 2, 3), Stone);

        ChunkMeshData mesh = ChunkMeshData.Build([quad], BlockPalette.Game);

        Assert.Equal(1, mesh.QuadCount);
        Assert.Equal(
            [new float3(1, 2, 3), new float3(1, 3, 3), new float3(2, 3, 3), new float3(2, 2, 3)],
            mesh.Vertices.Select(vertex => vertex.Position));
        Assert.Equal([0u, 1u, 2u, 2u, 3u, 0u], mesh.Indices);
    }

    [Fact]
    public void Build_TwoQuads_GivesTheIndicesOfTheSecondQuadAfterItsFirstVertex()
    {
        MeshQuad first = Quad(new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0), Stone);
        MeshQuad second = Quad(new Vector3(0, 0, 5), new Vector3(1, 0, 5), new Vector3(1, 1, 5), new Vector3(0, 1, 5), Grass);

        ChunkMeshData mesh = ChunkMeshData.Build([first, second], BlockPalette.Game);

        Assert.Equal(8, mesh.Vertices.Length);
        Assert.Equal([0u, 1u, 2u, 2u, 3u, 0u, 4u, 5u, 6u, 6u, 7u, 4u], mesh.Indices);
    }

    // The mesher writes the normal (1, 0, 0) for all the quads (#46). The mesh data must not use it.
    // The flat normal of each face must point out of the voxel: from the center of the voxel to the center of the face.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_SixFacesOfOneVoxel_GivesTheNormalThatPointsOutOfTheVoxel(bool optimizedMesher)
    {
        var voxels = new ushort[3, 3, 3];
        voxels[1, 1, 1] = Stone;
        var chunk = new Chunk(voxels, default);
        List<MeshQuad> quads = optimizedMesher ? new DisjointSetMeshOptimizer(new Mesh()).Optimize(chunk).Quads : chunk.ToMesh().Quads;

        ChunkMeshData mesh = ChunkMeshData.Build(quads, BlockPalette.Game);

        Assert.Equal(6, mesh.QuadCount);
        var voxelCenter = new float3(1.5f);
        var normals = new HashSet<float3>();
        for (int quad = 0; quad < mesh.QuadCount; quad++)
        {
            ReadOnlySpan<ChunkVertex> vertices = mesh.Vertices.AsSpan(quad * 4, 4);
            float3 faceCenter = (vertices[0].Position + vertices[1].Position + vertices[2].Position + vertices[3].Position) / 4f;
            float3 expected = (faceCenter - voxelCenter) * 2f;
            foreach (ChunkVertex vertex in vertices)
            {
                Assert.Equal(expected, vertex.Normal);
            }

            normals.Add(expected);
        }

        Assert.Equal(6, normals.Count);
    }

    [Fact]
    public void Build_GivesThePaletteColorOfTheBlockTypeToEachVertexOfTheQuad()
    {
        BlockPalette palette = BlockPalette.Game;
        MeshQuad stone = Quad(new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0), Stone);
        MeshQuad grass = Quad(new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0), Grass);

        ChunkMeshData mesh = ChunkMeshData.Build([stone, grass], palette);

        Assert.All(mesh.Vertices.Take(4), vertex => Assert.Equal(palette[Stone], vertex.Color));
        Assert.All(mesh.Vertices.Skip(4), vertex => Assert.Equal(palette[Grass], vertex.Color));
        Assert.NotEqual(palette[Stone], palette[Grass]);
    }

    // The game palette has the block types 0 to 63. A block type of more than 16 bits cannot be a palette index.
    // Without the check of 16 bits, 65,558 changes into 22 (stone), which the palette has.
    [Theory]
    [InlineData(64u)]
    [InlineData(65_558u)]
    public void Build_BlockTypeThatThePaletteDoesNotHave_ThrowsArgumentOutOfRangeException(uint blockType)
    {
        MeshQuad quad = Quad(new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0), blockType);

        Assert.Equal(64, BlockPalette.Game.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChunkMeshData.Build([quad], BlockPalette.Game));
    }

    [Fact]
    public void Build_QuadWithNoArea_GivesTheZeroNormal()
    {
        var point = new Vector3(2, 2, 2);
        MeshQuad quad = Quad(point, point, point, point, Stone);

        ChunkMeshData mesh = ChunkMeshData.Build([quad], BlockPalette.Game);

        Assert.All(mesh.Vertices, vertex => Assert.Equal(float3.Zero, vertex.Normal));
    }

    // 20,000 quads have 80,000 vertices. A 16-bit index cannot point to the vertices after the first 65,536.
    [Fact]
    public void Build_MoreThan65536Vertices_GivesTheCorrectIndicesForTheLastQuad()
    {
        const int quadCount = 20_000;
        var quads = new List<MeshQuad>(quadCount);
        for (int i = 0; i < quadCount; i++)
        {
            quads.Add(Quad(new Vector3(i, 0, 0), new Vector3(i, 1, 0), new Vector3(i + 1, 1, 0), new Vector3(i + 1, 0, 0), Stone));
        }

        ChunkMeshData mesh = ChunkMeshData.Build(quads, BlockPalette.Game);

        Assert.Equal(quadCount * 4, mesh.Vertices.Length);
        Assert.Equal([79_996u, 79_997u, 79_998u, 79_998u, 79_999u, 79_996u], mesh.Indices[^6..]);
        Assert.Equal(new float3(19_999, 0, 0), mesh.Vertices[79_996].Position);
    }

    [Fact]
    public void Build_NullArgument_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ChunkMeshData.Build(null!, BlockPalette.Game));
        Assert.Throws<ArgumentNullException>(() => ChunkMeshData.Build([], null!));
    }

    private static MeshQuad Quad(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, uint blockType) => new()
    {
        Vertex0 = vertex0,
        Vertex1 = vertex1,
        Vertex2 = vertex2,
        Vertex3 = vertex3,
        Normal = new Vector3(1, 0, 0),
        VoxelID = blockType,
    };
}
