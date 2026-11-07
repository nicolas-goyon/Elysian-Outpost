// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Voxels;
using Vector3 = System.Numerics.Vector3;

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// The vertices and the triangle indices of one chunk, for the GPU buffers of the chunk renderer.
/// </summary>
/// <remarks>
/// <para>
/// Each quad has four vertices and two triangles: (0, 1, 2) and (2, 3, 0), as in the legacy project (<c>ObjExporter.ToUnityMesh</c>).
/// The faces that point to the camera are then clockwise on the screen. See <c>ScreenWindingTests</c>.
/// </para>
/// <para>
/// The four vertices of a quad get the normal of the quad: one of the six unit normals of the faces (#46).
/// The vertices are packed in 8 bytes (#51): see <see cref="ChunkVertex"/>. The shader reads the color of the block type from the palette.
/// Thus this class uses the palette only to check that each block type has a color.
/// </para>
/// <para>
/// The indices have 32 bits. A chunk of 16 × 64 × 16 voxels can have more than 65,536 vertices, for example a 3D checkerboard.
/// </para>
/// </remarks>
public sealed class ChunkMeshData
{
    private const int VerticesPerQuad = 4;
    private const int IndicesPerQuad = 6;

    private ChunkMeshData(ChunkVertex[] vertices, uint[] indices, float3 boundsMin, float3 boundsMax)
    {
        Vertices = vertices;
        Indices = indices;
        BoundsMin = boundsMin;
        BoundsMax = boundsMax;
    }

    /// <summary>
    /// Gets the vertices: four for each quad, in the order of the quads.
    /// </summary>
    public ChunkVertex[] Vertices { get; }

    /// <summary>
    /// Gets the triangle indices: six for each quad.
    /// </summary>
    public uint[] Indices { get; }

    /// <summary>
    /// Gets the lowest corner of the box around all the vertices, in chunk coordinates (#50). The value is (0, 0, 0) for a mesh with no quad.
    /// </summary>
    public float3 BoundsMin { get; }

    /// <summary>
    /// Gets the highest corner of the box around all the vertices, in chunk coordinates (#50). The value is (0, 0, 0) for a mesh with no quad.
    /// </summary>
    public float3 BoundsMax { get; }

    /// <summary>
    /// Gets the number of quads.
    /// </summary>
    public int QuadCount => Vertices.Length / VerticesPerQuad;

    /// <summary>
    /// Makes the vertices and the indices of the quads of a chunk.
    /// </summary>
    /// <param name="quads">The quads of the mesher, in chunk coordinates.</param>
    /// <param name="palette">The palette that gives the color of each block type.</param>
    /// <returns>The mesh data of the quads.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="quads"/> or <paramref name="palette"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The palette has no color for the block type of a quad.</exception>
    public static ChunkMeshData Build(IReadOnlyList<MeshQuad> quads, BlockPalette palette)
    {
        ArgumentNullException.ThrowIfNull(quads);
        ArgumentNullException.ThrowIfNull(palette);

        var vertices = new ChunkVertex[quads.Count * VerticesPerQuad];
        var indices = new uint[quads.Count * IndicesPerQuad];
        for (int i = 0; i < quads.Count; i++)
        {
            MeshQuad quad = quads[i];
            if (quad.VoxelID > ushort.MaxValue || quad.VoxelID >= palette.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(quads), quad.VoxelID, $"Quad {i} has a block type that the palette does not have.");
            }

            var blockType = (ushort)quad.VoxelID;
            int normal = NormalIndex(quad.Normal, i);
            int vertex = i * VerticesPerQuad;
            vertices[vertex] = Vertex(quad.Vertex0, normal, blockType, i);
            vertices[vertex + 1] = Vertex(quad.Vertex1, normal, blockType, i);
            vertices[vertex + 2] = Vertex(quad.Vertex2, normal, blockType, i);
            vertices[vertex + 3] = Vertex(quad.Vertex3, normal, blockType, i);

            uint first = (uint)vertex;
            int index = i * IndicesPerQuad;
            indices[index] = first;
            indices[index + 1] = first + 1;
            indices[index + 2] = first + 2;
            indices[index + 3] = first + 2;
            indices[index + 4] = first + 3;
            indices[index + 5] = first;
        }

        float3 min = vertices.Length > 0 ? vertices[0].Position : default;
        float3 max = min;
        foreach (ChunkVertex vertex in vertices)
        {
            min = float3.Min(min, vertex.Position);
            max = float3.Max(max, vertex.Position);
        }

        return new ChunkMeshData(vertices, indices, min, max);
    }

    // The mesher writes integer positions in the chunk. The packed vertex keeps them exactly (#51).
    private static ChunkVertex Vertex(Vector3 position, int normal, ushort blockType, int quad)
    {
        int x = (int)position.X, y = (int)position.Y, z = (int)position.Z;
        if (x != position.X || y != position.Y || z != position.Z
            || x < 0 || y < 0 || z < 0 || x > ChunkVertex.MaxCoordinate || y > ChunkVertex.MaxCoordinate || z > ChunkVertex.MaxCoordinate)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, $"Quad {quad} has a vertex that is not an integer position from 0 to {ChunkVertex.MaxCoordinate}.");
        }

        return ChunkVertex.Create(x, y, z, normal, blockType);
    }

    // The normal of the quad must be one of the six unit normals of the faces.
    private static int NormalIndex(Vector3 normal, int quad)
    {
        ReadOnlySpan<float3> normals = ChunkVertex.Normals;
        for (int i = 0; i < normals.Length; i++)
        {
            if (normals[i].X == normal.X && normals[i].Y == normal.Y && normals[i].Z == normal.Z)
            {
                return i;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(normal), normal, $"Quad {quad} has a normal that is not the unit normal of a face.");
    }

    private static float3 ToFloat3(Vector3 vector) => new(vector.X, vector.Y, vector.Z);
}
