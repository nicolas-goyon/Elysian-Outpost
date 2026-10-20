// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// Tests of the mesher: the quads that <see cref="DisjointSetMeshOptimizer"/> makes for a chunk.
/// </summary>
/// <remarks>
/// <para>
/// These tests give the current result of the mesher. The changes of M2 (#5, #45, #46, #48) must keep these results,
/// except the known failure of #46 and the border rule of #48.
/// </para>
/// <para>
/// The exact numbers of quads in these tests are minimums. A correct mesher cannot make fewer quads for these chunks.
/// </para>
/// </remarks>
public sealed class MesherTests
{
    // Block types of the terrain generation. See data/palette.json.
    private const ushort Dirt = 11;
    private const ushort Grass = 16;
    private const ushort Stone = 22;

    // The mesher writes the quads in chunk coordinates: the chunk corner is at (0, 0, 0) at all world positions.
    // The legacy project puts the chunk object at the world position of the chunk. The renderer gives it to the shader (#39).
    private static readonly int3 _chunkOrigin = new(0, 0, 0);

    [Fact]
    public void Mesher_EmptyChunk_GivesNoQuads()
    {
        Assert.Empty(MakeQuads(new ushort[3, 4, 5]));
    }

    // All the neighbors of the voxel are air. Its six faces are in six different planes, thus each face is one quad.
    [Fact]
    public void Mesher_OneSolidVoxel_GivesSixQuadsOfOneFace()
    {
        ushort[,,] voxels = ChunkText.Parse(
            """
            ...
            ...
            ...

            ...
            .S.
            ...

            ...
            ...
            ...
            """,
            ('S', Stone));

        AssertShapes(
            [
                new(FaceDirection.NegativeX, Plane: 1, MinU: 1, MaxU: 2, MinV: 1, MaxV: 2, Stone),
                new(FaceDirection.PositiveX, Plane: 2, MinU: 1, MaxU: 2, MinV: 1, MaxV: 2, Stone),
                new(FaceDirection.NegativeY, Plane: 1, MinU: 1, MaxU: 2, MinV: 1, MaxV: 2, Stone),
                new(FaceDirection.PositiveY, Plane: 2, MinU: 1, MaxU: 2, MinV: 1, MaxV: 2, Stone),
                new(FaceDirection.NegativeZ, Plane: 1, MinU: 1, MaxU: 2, MinV: 1, MaxV: 2, Stone),
                new(FaceDirection.PositiveZ, Plane: 2, MinU: 1, MaxU: 2, MinV: 1, MaxV: 2, Stone),
            ],
            MakeQuads(voxels));
    }

    // Only the faces on the chunk border are visible. All the faces of one side of the chunk are in one plane,
    // and they have the same block type. Thus one quad covers each side.
    // The three sizes are different: an exchange of two axes in the mesher changes the result.
    [Fact]
    public void Mesher_FullChunkOfOneBlockType_GivesOneQuadForEachSide()
    {
        var voxels = new ushort[3, 4, 5];
        foreach ((int x, int y, int z) in VoxelGrid.Positions(voxels))
        {
            voxels[x, y, z] = Stone;
        }

        AssertShapes(
            [
                new(FaceDirection.NegativeX, Plane: 0, MinU: 0, MaxU: 4, MinV: 0, MaxV: 5, Stone),
                new(FaceDirection.PositiveX, Plane: 3, MinU: 0, MaxU: 4, MinV: 0, MaxV: 5, Stone),
                new(FaceDirection.NegativeY, Plane: 0, MinU: 0, MaxU: 3, MinV: 0, MaxV: 5, Stone),
                new(FaceDirection.PositiveY, Plane: 4, MinU: 0, MaxU: 3, MinV: 0, MaxV: 5, Stone),
                new(FaceDirection.NegativeZ, Plane: 0, MinU: 0, MaxU: 3, MinV: 0, MaxV: 4, Stone),
                new(FaceDirection.PositiveZ, Plane: 5, MinU: 0, MaxU: 3, MinV: 0, MaxV: 4, Stone),
            ],
            MakeQuads(voxels));
    }

    // The faces between the two layers are hidden. The bottom is stone and the top is dirt: one quad each.
    // Each vertical side has a row of stone faces and a row of dirt faces: one quad for each block type.
    [Fact]
    public void Mesher_TwoBlockTypesInTwoLayers_GivesTenQuads()
    {
        ushort[,,] voxels = ChunkText.Parse(
            """
            SSSS
            SSSS

            DDDD
            DDDD

            ....
            ....
            """,
            ('S', Stone), ('D', Dirt));

        AssertShapes(
            [
                new(FaceDirection.NegativeY, Plane: 0, MinU: 0, MaxU: 4, MinV: 0, MaxV: 2, Stone),
                new(FaceDirection.PositiveY, Plane: 2, MinU: 0, MaxU: 4, MinV: 0, MaxV: 2, Dirt),
                new(FaceDirection.NegativeX, Plane: 0, MinU: 0, MaxU: 1, MinV: 0, MaxV: 2, Stone),
                new(FaceDirection.NegativeX, Plane: 0, MinU: 1, MaxU: 2, MinV: 0, MaxV: 2, Dirt),
                new(FaceDirection.PositiveX, Plane: 4, MinU: 0, MaxU: 1, MinV: 0, MaxV: 2, Stone),
                new(FaceDirection.PositiveX, Plane: 4, MinU: 1, MaxU: 2, MinV: 0, MaxV: 2, Dirt),
                new(FaceDirection.NegativeZ, Plane: 0, MinU: 0, MaxU: 4, MinV: 0, MaxV: 1, Stone),
                new(FaceDirection.NegativeZ, Plane: 0, MinU: 0, MaxU: 4, MinV: 1, MaxV: 2, Dirt),
                new(FaceDirection.PositiveZ, Plane: 2, MinU: 0, MaxU: 4, MinV: 0, MaxV: 1, Stone),
                new(FaceDirection.PositiveZ, Plane: 2, MinU: 0, MaxU: 4, MinV: 1, MaxV: 2, Dirt),
            ],
            MakeQuads(voxels));
    }

    // In a 3D checkerboard, all the neighbors of a solid voxel are air. Thus each solid voxel has six visible faces.
    // In each plane, no two faces have a common edge. Thus the mesher cannot join faces, and each face is one quad.
    [Fact]
    public void Mesher_CheckerboardOfOneBlockTypeAndAir_GivesOneQuadForEachVisibleFace()
    {
        var voxels = new ushort[5, 4, 3];
        foreach ((int x, int y, int z) in VoxelGrid.Positions(voxels))
        {
            voxels[x, y, z] = (x + y + z) % 2 == 0 ? Stone : (ushort)0;
        }

        List<MeshQuad> quads = MakeQuads(voxels);

        // 30 of the 60 voxels are solid.
        Assert.Equal(6 * 30, quads.Count);
        Assert.All(quads, quad => Assert.Equal(1, QuadShape.From(quad).Area));
        MeshCoverage.AssertCoversEachVisibleFaceOnce(voxels, quads, _chunkOrigin);
    }

    // All the voxels are solid. Two neighbor voxels in the layer have different block types,
    // thus the mesher cannot join two faces. The visible faces are the 12 top faces, the 12 bottom faces and 14 side faces.
    [Fact]
    public void Mesher_CheckerboardOfTwoBlockTypes_GivesOneQuadForEachVisibleFace()
    {
        ushort[,,] voxels = ChunkText.Parse(
            """
            SDSD
            DSDS
            SDSD
            """,
            ('S', Stone), ('D', Dirt));

        List<MeshQuad> quads = MakeQuads(voxels);

        Assert.Equal(38, quads.Count);
        Assert.All(quads, quad => Assert.Equal(1, QuadShape.From(quad).Area));
        MeshCoverage.AssertCoversEachVisibleFaceOnce(voxels, quads, _chunkOrigin);
    }

    // Random chunks of 1 to 8 voxels on each axis, with air and one to three block types, at random world positions.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Mesher_RandomChunks_CoverEachVisibleFaceOnce(int seed)
    {
        const int chunkCount = 40;
        var random = new Random(seed);
        for (int i = 0; i < chunkCount; i++)
        {
            ushort[,,] voxels = RandomChunk(random);
            var worldPosition = new int3(random.Next(-1024, 1024), random.Next(-128, 128), random.Next(-1024, 1024));

            List<string> problems = MeshCoverage.FindProblems(voxels, MakeQuads(voxels, worldPosition), _chunkOrigin);

            Assert.True(problems.Count == 0, $"Chunk {i} of the seed {seed} ({voxels.GetLength(0)} x {voxels.GetLength(1)} x {voxels.GetLength(2)}): {problems.Count} problems. The first problem: {problems.FirstOrDefault()}");
        }
    }

    // The test uses 64 chunks of the sample scene, at their world positions: one chunk in four on the X axis and on the Z axis.
    // These chunks contain the seven block types of the scene: air, water, dirt, sand, grass, stone and bedrock.
    // The current mesher makes 1,448 quads for these chunks. A change of the mesher must not make more quads (#45).
    [Fact]
    public void Mesher_TerrainChunksOfTheSampleScene_CoverEachVisibleFaceOnce()
    {
        const int maxQuadCount = 1448;
        WorldSettings scene = WorldSettings.SampleScene;
        var generation = new MainGeneration(scene.ChunkSize, scene.Seed);
        int3[] positions = SampleChunkPositions(scene);
        var blockTypes = new HashSet<ushort>();
        int quadCount = 0;

        foreach (int3 position in positions)
        {
            ushort[,,] voxels = generation.GenerateChunkAt(position);
            List<MeshQuad> quads = MakeQuads(voxels, position);

            List<string> problems = MeshCoverage.FindProblems(voxels, quads, _chunkOrigin);

            Assert.True(problems.Count == 0, $"Chunk at ({position.X}, {position.Y}, {position.Z}): {problems.Count} problems. The first problem: {problems.FirstOrDefault()}");
            blockTypes.UnionWith(voxels.Cast<ushort>());
            quadCount += quads.Count;
        }

        Assert.Equal(64, positions.Length);
        Assert.Equal(new ushort[] { 0, 8, 11, 13, 16, 22, 24 }, blockTypes.Order());
        Assert.InRange(quadCount, 1, maxQuadCount);
    }

    [Fact]
    public void Mesher_ChunkAwayFromTheOrigin_GivesTheQuadsOfTheChunkAtTheOrigin()
    {
        ushort[,,] voxels = ChunkText.Parse(
            """
            SD.
            S..

            D..
            ...
            """,
            ('S', Stone), ('D', Dirt));

        List<MeshQuad> quads = MakeQuads(voxels, new int3(-256, 64, 240));

        Assert.Equal(MakeQuads(voxels), quads);
        MeshCoverage.AssertCoversEachVisibleFaceOnce(voxels, quads, _chunkOrigin);
    }

    [Fact]
    public void Mesher_SameChunkTwice_GivesTheSameQuads()
    {
        WorldSettings scene = WorldSettings.SampleScene;
        ushort[,,] voxels = new MainGeneration(scene.ChunkSize, scene.Seed).GenerateChunkAt(new int3(0, 0, 0));
        var chunk = new Chunk(voxels, new int3(0, 0, 0));

        List<MeshQuad> first = MakeQuads(chunk);
        List<MeshQuad> second = MakeQuads(chunk);
        List<MeshQuad> copy = MakeQuads((ushort[,,])voxels.Clone());

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.Equal(first, copy);
    }

    // The normal of a quad must be the unit vector of its face direction. The direction comes from the winding order.
    // For a correct mesh, the normal of each quad also agrees with its winding order.
    [Fact(Skip = "Known failure, #46: DisjointSetVisiblePlaneOptimizer.ToMeshQuads writes the normal (1, 0, 0) for all the face directions.")]
    public void Mesher_EachQuad_HasTheNormalOfItsFaceDirection()
    {
        ushort[,,] voxels = ChunkText.Parse(
            """
            SS.
            S..

            S..
            ...
            """,
            ('S', Stone));

        List<MeshQuad> quads = MakeQuads(voxels);

        Assert.Equal(FaceDirections.All.ToHashSet(), quads.Select(quad => QuadShape.From(quad).Direction).ToHashSet());
        Assert.All(quads, quad => Assert.Equal(QuadShape.From(quad).Direction.UnitVector(), quad.Normal));
    }

    // The entry points of the mesher. The tests use only these two methods. Thus a change of the mesher API changes only these methods.
    private static List<MeshQuad> MakeQuads(ushort[,,] voxels, int3 worldPosition = default) => MakeQuads(new Chunk(voxels, worldPosition));

    private static List<MeshQuad> MakeQuads(Chunk chunk) => new DisjointSetMeshOptimizer(new Mesh()).Optimize(chunk).Quads;

    // Compares the shapes of the quads with the expected shapes, in any order.
    private static void AssertShapes(QuadShape[] expected, List<MeshQuad> quads)
    {
        Assert.Equal(Sorted(expected), Sorted(quads.Select(QuadShape.From)));
    }

    private static List<QuadShape> Sorted(IEnumerable<QuadShape> shapes) =>
        [.. shapes.OrderBy(shape => shape.Direction).ThenBy(shape => shape.Plane).ThenBy(shape => shape.MinU).ThenBy(shape => shape.MinV)];

    private static int3[] SampleChunkPositions(WorldSettings scene)
    {
        int stride = 4 * scene.ChunkSize.X;
        return [.. scene.GetChunkPositions(new int3(0, 0, 0)).Where(position => position.X % stride == 0 && position.Z % stride == 0)];
    }

    private static ushort[,,] RandomChunk(Random random)
    {
        ushort[] blockTypes = [Stone, Dirt, Grass];
        int blockTypeCount = random.Next(1, blockTypes.Length + 1);
        double solidRatio = random.NextDouble();
        var voxels = new ushort[random.Next(1, 9), random.Next(1, 9), random.Next(1, 9)];
        foreach ((int x, int y, int z) in VoxelGrid.Positions(voxels))
        {
            if (random.NextDouble() < solidRatio)
            {
                voxels[x, y, z] = blockTypes[random.Next(blockTypeCount)];
            }
        }

        return voxels;
    }
}
