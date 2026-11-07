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
/// The four vertices of a quad get the normal of the quad. Since #46, the mesher writes the normal of the face direction.
/// Before #46, this class calculated a flat normal from the vertices, as <c>RecalculateNormals</c> did in the legacy project.
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
            if (quad.VoxelID > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(quads), quad.VoxelID, $"Quad {i} has a block type that is not a 16-bit value.");
            }

            byte4 color = palette[(ushort)quad.VoxelID];
            float3 normal = ToFloat3(quad.Normal);
            int vertex = i * VerticesPerQuad;
            vertices[vertex] = new ChunkVertex { Position = ToFloat3(quad.Vertex0), Normal = normal, Color = color };
            vertices[vertex + 1] = new ChunkVertex { Position = ToFloat3(quad.Vertex1), Normal = normal, Color = color };
            vertices[vertex + 2] = new ChunkVertex { Position = ToFloat3(quad.Vertex2), Normal = normal, Color = color };
            vertices[vertex + 3] = new ChunkVertex { Position = ToFloat3(quad.Vertex3), Normal = normal, Color = color };

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

    private static float3 ToFloat3(Vector3 vector) => new(vector.X, vector.Y, vector.Z);
}
