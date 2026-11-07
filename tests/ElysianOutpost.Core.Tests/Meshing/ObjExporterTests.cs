// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;
using Vector3 = System.Numerics.Vector3;

namespace ElysianOutpost.Core.Tests.Meshing;

public sealed class ObjExporterTests
{
    // Black, half-transparent gray (sRGB 128 / 255 gives the linear value 0.215861), and white with a name.
    private static readonly BlockPalette _palette = BlockPalette.Parse("""
        {"blockTypes": [
            {"id": 0, "color": "#000000FF"},
            {"id": 1, "color": "#80808080"},
            {"id": 2, "color": "#FFFFFFFF", "name": "Snow Top"}
        ]}
        """);

    // The test of task 6 of #97: a mesh with one quad gives the expected OBJ and MTL text.
    [Fact]
    public void Export_OneQuad_GivesTheExpectedObjAndMtlText()
    {
        MeshQuad quad = Quad(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 0, -1), 1);

        (string obj, string mtl) = ObjExporter.Export([quad], _palette, "chunk.mtl");

        Assert.Equal(
            """
            mtllib chunk.mtl
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            vn 0 0 -1
            usemtl block_1
            f 1//1 2//1 3//1 4//1

            """.ReplaceLineEndings(),
            obj);
        Assert.Equal(
            """
            newmtl block_1
            Kd 0.215861 0.215861 0.215861
            d 0.501961

            """.ReplaceLineEndings(),
            mtl);
    }

    // Two quads share an edge and a normal: the OBJ text has 6 positions and 1 normal, not 8 and 2.
    // A change of block type starts a new usemtl line. The MTL text has each material one time.
    [Fact]
    public void Export_QuadsWithSharedVertices_SharesThePositionsAndTheNormals()
    {
        Vector3 up = new(0, 1, 0);
        MeshQuad first = Quad(new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0), up, 2);
        MeshQuad second = Quad(new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(2, 1, 1), new Vector3(2, 1, 0), up, 1);
        MeshQuad third = Quad(new Vector3(2, 1, 0), new Vector3(2, 1, 1), new Vector3(3, 1, 1), new Vector3(3, 1, 0), up, 2);

        (string obj, string mtl) = ObjExporter.Export([first, second, third], _palette, "a.mtl");

        string[] lines = obj.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(8, lines.Count(line => line.StartsWith("v ", StringComparison.Ordinal)));
        Assert.Single(lines, line => line.StartsWith("vn ", StringComparison.Ordinal));
        Assert.Equal(
            ["usemtl block_2_snow_top", "f 1//1 2//1 3//1 4//1", "usemtl block_1", "f 4//1 3//1 5//1 6//1", "usemtl block_2_snow_top", "f 6//1 5//1 7//1 8//1"],
            lines.SkipWhile(line => !line.StartsWith("usemtl", StringComparison.Ordinal)));
        Assert.Equal(["newmtl block_1", "Kd 0.215861 0.215861 0.215861", "d 0.501961", "newmtl block_2_snow_top", "Kd 1 1 1", "d 1"], mtl.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    // The legacy code wrote the positions with the culture of the computer. In French, 0.5 became "0,5", and the OBJ file was wrong.
    [Fact]
    public void Export_WithAFrenchCulture_WritesAPeriodAsTheDecimalSeparator()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            MeshQuad quad = Quad(new Vector3(0.5f, 0, 0), new Vector3(1.5f, 0, 0), new Vector3(1.5f, 1, 0), new Vector3(0.5f, 1, 0), new Vector3(0, 0, -1), 1);

            (string obj, string mtl) = ObjExporter.Export([quad], _palette, "chunk.mtl");

            Assert.Contains("v 0.5 0 0", obj, StringComparison.Ordinal);
            Assert.DoesNotContain(",", obj, StringComparison.Ordinal);
            Assert.DoesNotContain(",", mtl, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // A terrain chunk of the game: each face has 4 valid indices, and each usemtl has a material in the MTL text.
    [Fact]
    public void Export_TerrainChunk_GivesValidIndicesAndMaterials()
    {
        var generation = new MainGeneration(new int3(16, 64, 16), 123);
        List<MeshQuad> quads = new GreedyMesher().Mesh(generation.GenerateChunkAt(default));

        (string obj, string mtl) = ObjExporter.Export(quads, BlockPalette.Game, "chunk.mtl");

        string[] lines = obj.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        int positions = lines.Count(line => line.StartsWith("v ", StringComparison.Ordinal));
        int normals = lines.Count(line => line.StartsWith("vn ", StringComparison.Ordinal));
        string[] faces = lines.Where(line => line.StartsWith("f ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(quads.Count, faces.Length);
        Assert.InRange(normals, 1, 6);
        foreach (string face in faces)
        {
            string[] corners = face.Split(' ')[1..];
            Assert.Equal(4, corners.Length);
            Assert.All(corners, corner =>
            {
                string[] parts = corner.Split("//");
                Assert.InRange(int.Parse(parts[0], CultureInfo.InvariantCulture), 1, positions);
                Assert.InRange(int.Parse(parts[1], CultureInfo.InvariantCulture), 1, normals);
            });
        }

        HashSet<string> materials = mtl.Split(Environment.NewLine).Where(line => line.StartsWith("newmtl ", StringComparison.Ordinal)).Select(line => line[7..]).ToHashSet();
        Assert.All(lines.Where(line => line.StartsWith("usemtl ", StringComparison.Ordinal)), line => Assert.Contains(line[7..], materials));
        Assert.Contains("block_16_grass", materials);
    }

    [Fact]
    public void Export_InvalidArguments_Throws()
    {
        MeshQuad unknown = Quad(default, default, default, default, new Vector3(0, 1, 0), 3);

        Assert.Throws<ArgumentNullException>(() => ObjExporter.Export(null!, _palette, "a.mtl"));
        Assert.Throws<ArgumentNullException>(() => ObjExporter.Export([], null!, "a.mtl"));
        Assert.Throws<ArgumentException>(() => ObjExporter.Export([], _palette, " "));
        Assert.Throws<ArgumentException>(() => ObjExporter.Export([], _palette, "my chunk.mtl"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ObjExporter.Export([unknown], _palette, "a.mtl"));
    }

    private static MeshQuad Quad(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, Vector3 normal, uint blockType) => new()
    {
        Vertex0 = vertex0,
        Vertex1 = vertex1,
        Vertex2 = vertex2,
        Vertex3 = vertex3,
        Normal = normal,
        VoxelID = blockType,
    };
}
