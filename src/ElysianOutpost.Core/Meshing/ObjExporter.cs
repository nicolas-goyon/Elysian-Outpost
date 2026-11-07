// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using System.Numerics;
using System.Text;
using Brutal.Numerics;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// Writes the quads of a mesh in the Wavefront OBJ format, with one material for each block type (#97).
/// A developer can open the OBJ file of a chunk in a 3D tool, for example Blender.
/// </summary>
/// <remarks>
/// <para>
/// The legacy code (<c>ObjExporter.MeshToObjString</c>) used the texture coordinates of a texture atlas.
/// This port uses the block color palette (#2): the MTL file has one material for each block type, with the palette color.
/// The OBJ file has no texture coordinates.
/// </para>
/// <para>
/// The OBJ file shares the identical positions and normals. The indices start at 1, as the format specifies.
/// All numbers use the invariant culture: the decimal separator is always a period.
/// </para>
/// <para>
/// The palette gives sRGB colors. The <c>Kd</c> value of the MTL file is the linear color, because Blender and most 3D tools read it
/// as a linear color. The <c>d</c> value is the alpha value of the palette color.
/// </para>
/// </remarks>
public static class ObjExporter
{
    /// <summary>
    /// Writes the OBJ text and the MTL text of a mesh.
    /// </summary>
    /// <param name="quads">The quads of the mesh, for example the result of <see cref="GreedyMesher"/>.</param>
    /// <param name="palette">The palette that gives the color of each block type.</param>
    /// <param name="materialLibrary">The file name of the MTL file. The OBJ text refers to it with <c>mtllib</c>. Example: <c>chunk.mtl</c>.</param>
    /// <returns>The OBJ text and the MTL text.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="materialLibrary"/> is empty or contains a white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A quad has a block type that the palette does not have.</exception>
    public static (string Obj, string Mtl) Export(IReadOnlyList<MeshQuad> quads, BlockPalette palette, string materialLibrary)
    {
        ArgumentNullException.ThrowIfNull(quads);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentException.ThrowIfNullOrWhiteSpace(materialLibrary);
        if (materialLibrary.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("The file name of the MTL file must not contain a white space.", nameof(materialLibrary));
        }

        var positions = new List<Vector3>();
        var positionIndices = new Dictionary<Vector3, int>();
        var normals = new List<Vector3>();
        var normalIndices = new Dictionary<Vector3, int>();
        var faces = new StringBuilder();
        var blockTypes = new SortedSet<ushort>();
        int currentBlockType = -1;
        for (int i = 0; i < quads.Count; i++)
        {
            MeshQuad quad = quads[i];
            if (quad.VoxelID > ushort.MaxValue || quad.VoxelID >= palette.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(quads), quad.VoxelID, $"Quad {i} has a block type that the palette does not have.");
            }

            var blockType = (ushort)quad.VoxelID;
            blockTypes.Add(blockType);
            if (blockType != currentBlockType)
            {
                faces.Append("usemtl ").AppendLine(MaterialName(palette, blockType));
                currentBlockType = blockType;
            }

            int n = Index(quad.Normal, normalIndices, normals);
            faces.Append(CultureInfo.InvariantCulture, $"f {Index(quad.Vertex0, positionIndices, positions)}//{n}");
            faces.Append(CultureInfo.InvariantCulture, $" {Index(quad.Vertex1, positionIndices, positions)}//{n}");
            faces.Append(CultureInfo.InvariantCulture, $" {Index(quad.Vertex2, positionIndices, positions)}//{n}");
            faces.Append(CultureInfo.InvariantCulture, $" {Index(quad.Vertex3, positionIndices, positions)}//{n}").AppendLine();
        }

        var obj = new StringBuilder();
        obj.Append("mtllib ").AppendLine(materialLibrary);
        foreach (Vector3 position in positions)
        {
            obj.AppendLine(CultureInfo.InvariantCulture, $"v {position.X} {position.Y} {position.Z}");
        }

        foreach (Vector3 normal in normals)
        {
            obj.AppendLine(CultureInfo.InvariantCulture, $"vn {normal.X} {normal.Y} {normal.Z}");
        }

        obj.Append(faces);

        var mtl = new StringBuilder();
        foreach (ushort blockType in blockTypes)
        {
            byte4 color = palette[blockType];
            mtl.Append("newmtl ").AppendLine(MaterialName(palette, blockType));
            mtl.AppendLine(CultureInfo.InvariantCulture, $"Kd {ToLinear(color.X):0.######} {ToLinear(color.Y):0.######} {ToLinear(color.Z):0.######}");
            mtl.AppendLine(CultureInfo.InvariantCulture, $"d {color.W / 255.0:0.######}");
        }

        return (obj.ToString(), mtl.ToString());
    }

    // The name of the material of a block type: "block_22", or "block_16_grass" for a block type with a name.
    // A material name in the OBJ format cannot contain a white space. Thus the name keeps only letters, digits and underscores.
    private static string MaterialName(BlockPalette palette, ushort blockType)
    {
        string? name = palette.GetName(blockType);
        if (string.IsNullOrEmpty(name))
        {
            return $"block_{blockType}";
        }

        var builder = new StringBuilder($"block_{blockType}_");
        foreach (char character in name)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_');
        }

        return builder.ToString();
    }

    // Gives the index of a value, from 1. A new value goes to the end of the list.
    private static int Index(Vector3 value, Dictionary<Vector3, int> indices, List<Vector3> values)
    {
        if (!indices.TryGetValue(value, out int index))
        {
            values.Add(value);
            index = values.Count;
            indices.Add(value, index);
        }

        return index;
    }

    // Converts an sRGB value to a linear value (IEC 61966-2-1), as the chunk shader does.
    private static double ToLinear(byte value)
    {
        double color = value / 255.0;
        return color <= 0.04045 ? color / 12.92 : Math.Pow((color + 0.055) / 1.055, 2.4);
    }
}
