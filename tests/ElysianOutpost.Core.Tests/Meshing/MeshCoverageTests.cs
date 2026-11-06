// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// Tests of the test oracle of the mesher: <see cref="MeshCoverage"/> and <see cref="QuadShape"/>.
/// </summary>
public sealed class MeshCoverageTests
{
    private const ushort Dirt = 11;
    private const ushort Stone = 22;

    // The simple mesh of Chunk.ToMesh is in chunk coordinates: the chunk corner is at (0, 0, 0).
    private static readonly int3 _chunkCoordinates = new(0, 0, 0);

    // A voxel in the center of a 3 x 3 x 3 chunk.
    private static readonly ushort[,,] _oneVoxel = ChunkText.Parse(
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

    [Fact]
    public void FindVisibleFaces_OneVoxel_GivesItsSixFaces()
    {
        Dictionary<Face, ushort> faces = MeshCoverage.FindVisibleFaces(_oneVoxel);

        Assert.Equal(FaceDirections.All.Select(direction => new Face(1, 1, 1, direction)).ToHashSet(), faces.Keys.ToHashSet());
        Assert.All(faces.Values, blockType => Assert.Equal(Stone, blockType));
    }

    [Fact]
    public void FindVisibleFaces_TwoNeighborVoxels_HidesTheFacesBetweenThem()
    {
        ushort[,,] voxels = ChunkText.Parse("SD", ('S', Stone), ('D', Dirt));

        Dictionary<Face, ushort> faces = MeshCoverage.FindVisibleFaces(voxels);

        Assert.Equal(10, faces.Count);
        Assert.DoesNotContain(new Face(0, 0, 0, FaceDirection.PositiveX), faces.Keys);
        Assert.DoesNotContain(new Face(1, 0, 0, FaceDirection.NegativeX), faces.Keys);
        Assert.Equal(Dirt, faces[new Face(1, 0, 0, FaceDirection.PositiveX)]);
    }

    // Only the faces on the border are visible: 2 x (3 x 4 + 4 x 5 + 3 x 5) faces.
    [Fact]
    public void FindVisibleFaces_FullChunk_GivesOnlyTheBorderFaces()
    {
        ushort[,,] voxels = Filled(3, 4, 5, Stone);

        Dictionary<Face, ushort> faces = MeshCoverage.FindVisibleFaces(voxels);

        Assert.Equal(94, faces.Count);
        Assert.All(faces.Keys, face => Assert.False(VoxelGrid.IsInside(voxels, face.Neighbor), $"The {face} is not on the border."));
    }

    // In a 3D checkerboard, all the faces are visible. Thus the simple mesh of Chunk.ToMesh, with six quads for each solid voxel, is correct.
    // This test also checks the direction rule of QuadShape: the simple mesh has the correct normals.
    [Fact]
    public void FindProblems_SimpleMeshOfACheckerboard_FindsNoProblem()
    {
        ushort[,,] voxels = Checkerboard(4, 3, 2);
        List<MeshQuad> quads = SimpleQuads(voxels);

        Assert.Empty(MeshCoverage.FindProblems(voxels, quads, _chunkCoordinates));
        Assert.All(quads, quad => Assert.Equal(QuadShape.From(quad).Direction.UnitVector(), quad.Normal));
    }

    [Fact]
    public void FindProblems_MissingQuad_FindsTheFaceInNoQuad()
    {
        List<MeshQuad> quads = SimpleQuads(_oneVoxel);
        QuadShape removed = QuadShape.From(quads[0]);
        quads.RemoveAt(0);

        string problem = Assert.Single(MeshCoverage.FindProblems(_oneVoxel, quads, _chunkCoordinates));

        Assert.Equal($"No quad covers the visible {Assert.Single(removed.Faces())}.", problem);
    }

    [Fact]
    public void FindProblems_QuadTwoTimes_FindsTheFaceInTwoQuads()
    {
        List<MeshQuad> quads = SimpleQuads(_oneVoxel);
        quads.Add(quads[0]);

        string problem = Assert.Single(MeshCoverage.FindProblems(_oneVoxel, quads, _chunkCoordinates));

        Assert.StartsWith("Quads 0 and 6 both cover", problem, StringComparison.Ordinal);
    }

    // The opposite winding order gives the opposite direction. Then the quad covers a face of the air voxel on the other side.
    [Fact]
    public void FindProblems_QuadWithTheOppositeWindingOrder_FindsTheFaceOfAnAirVoxel()
    {
        List<MeshQuad> quads = SimpleQuads(_oneVoxel);
        MeshQuad quad = quads[0];
        quads[0] = quad with { Vertex1 = quad.Vertex3, Vertex3 = quad.Vertex1 };

        List<string> problems = MeshCoverage.FindProblems(_oneVoxel, quads, _chunkCoordinates);

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, problem => problem.EndsWith("This voxel is air.", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("No quad covers", StringComparison.Ordinal));
    }

    [Fact]
    public void FindProblems_QuadWithAnotherBlockType_FindsTheBlockTypeOfTheVoxel()
    {
        List<MeshQuad> quads = SimpleQuads(_oneVoxel);
        quads[0] = quads[0] with { VoxelID = Dirt };

        string problem = Assert.Single(MeshCoverage.FindProblems(_oneVoxel, quads, _chunkCoordinates));

        Assert.EndsWith($"The block type of this voxel is {Stone}.", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void FindProblems_QuadOnAHiddenFace_FindsTheSolidNeighbor()
    {
        ushort[,,] voxels = ChunkText.Parse("SS", ('S', Stone));

        // The simple mesh has all six faces of each voxel, also the two hidden faces between the voxels.
        List<string> problems = MeshCoverage.FindProblems(voxels, SimpleQuads(voxels), _chunkCoordinates);

        Assert.Equal(2, problems.Count);
        Assert.All(problems, problem => Assert.EndsWith("The neighbor voxel is solid, thus the face is not visible.", problem, StringComparison.Ordinal));
    }

    // A mesh in world coordinates has all its vertices moved by the world position of the chunk.
    [Fact]
    public void FindProblems_MeshMovedByTheChunkOrigin_FindsNoProblem()
    {
        var origin = new int3(-32, 64, 16);
        List<MeshQuad> quads = [.. SimpleQuads(_oneVoxel).Select(quad => Moved(quad, origin))];

        Assert.Empty(MeshCoverage.FindProblems(_oneVoxel, quads, origin));
    }

    // The six quads are outside the chunk, thus the six visible faces are in no quad.
    [Fact]
    public void FindProblems_MeshMovedByAnotherOrigin_FindsTheQuadsOutsideTheChunk()
    {
        List<MeshQuad> quads = [.. SimpleQuads(_oneVoxel).Select(quad => Moved(quad, new int3(-32, 64, 16)))];

        List<string> problems = MeshCoverage.FindProblems(_oneVoxel, quads, _chunkCoordinates);

        Assert.Equal(12, problems.Count);
        Assert.Equal(6, problems.Count(problem => problem.EndsWith("This voxel is outside the chunk.", StringComparison.Ordinal)));
    }

    public static TheoryData<Vector3, Vector3, Vector3, Vector3> IncorrectQuads => new()
    {
        // A vertex between two grid points.
        { new(0f, 0f, 0f), new(0f, 1.5f, 0f), new(1f, 1.5f, 0f), new(1f, 0f, 0f) },

        // The corners of a rectangle in an incorrect order.
        { new(0f, 0f, 0f), new(1f, 1f, 0f), new(0f, 1f, 0f), new(1f, 0f, 0f) },

        // An edge of length 0.
        { new(0f, 0f, 0f), new(0f, 0f, 0f), new(1f, 0f, 0f), new(1f, 0f, 0f) },

        // A parallelogram with edges that are not parallel to the axes.
        { new(0f, 0f, 0f), new(1f, 1f, 0f), new(2f, 1f, 0f), new(1f, 0f, 0f) },
    };

    [Theory]
    [MemberData(nameof(IncorrectQuads))]
    public void TryCreate_IncorrectQuad_GivesAProblem(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, Vector3 vertex3)
    {
        var quad = new MeshQuad { Vertex0 = vertex0, Vertex1 = vertex1, Vertex2 = vertex2, Vertex3 = vertex3, VoxelID = Stone };

        bool created = QuadShape.TryCreate(quad, out _, out string problem);

        Assert.False(created);
        Assert.NotEmpty(problem);
    }

    [Fact]
    public void Faces_QuadOfTwoByThreeCells_GivesSixFacesOfTheVoxelsBeforeThePlane()
    {
        var shape = new QuadShape(FaceDirection.PositiveY, Plane: 4, MinU: 1, MaxU: 3, MinV: 5, MaxV: 8, BlockType: Stone);

        Assert.Equal(6, shape.Area);
        Assert.Equal(
            new[] { (1, 5), (1, 6), (1, 7), (2, 5), (2, 6), (2, 7) }.Select(cell => new Face(cell.Item1, 3, cell.Item2, FaceDirection.PositiveY)),
            shape.Faces());
    }

    private static List<MeshQuad> SimpleQuads(ushort[,,] voxels) => new Chunk(voxels, new int3(0, 0, 0)).ToMesh().Quads;

    private static MeshQuad Moved(MeshQuad quad, int3 offset)
    {
        var vector = new Vector3(offset.X, offset.Y, offset.Z);
        return quad with
        {
            Vertex0 = quad.Vertex0 + vector,
            Vertex1 = quad.Vertex1 + vector,
            Vertex2 = quad.Vertex2 + vector,
            Vertex3 = quad.Vertex3 + vector,
        };
    }

    private static ushort[,,] Filled(int sizeX, int sizeY, int sizeZ, ushort blockType)
    {
        var voxels = new ushort[sizeX, sizeY, sizeZ];
        foreach ((int x, int y, int z) in VoxelGrid.Positions(voxels))
        {
            voxels[x, y, z] = blockType;
        }

        return voxels;
    }

    private static ushort[,,] Checkerboard(int sizeX, int sizeY, int sizeZ)
    {
        var voxels = new ushort[sizeX, sizeY, sizeZ];
        foreach ((int x, int y, int z) in VoxelGrid.Positions(voxels))
        {
            voxels[x, y, z] = (x + y + z) % 2 == 0 ? Stone : (ushort)0;
        }

        return voxels;
    }
}
