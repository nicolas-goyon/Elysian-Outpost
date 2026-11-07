// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// Makes the quads of the visible faces of a chunk, and merges the faces of the same block type into rectangles (greedy meshing, #45).
/// </summary>
/// <remarks>
/// <para>
/// For each of the six face directions, the mesher goes through the slices of the chunk. In each slice, it writes a mask:
/// the block type of each visible face, or 0 for no face. A face is visible if the neighbor voxel in the face direction is air
/// or outside the chunk. Thus each chunk mesh has the faces at the chunk borders (#48 changes this).
/// </para>
/// <para>
/// The mesher then reads the mask row by row. At the first face that is not in a rectangle, it makes the rectangle as wide as possible,
/// then as high as possible, with faces of the same block type. It clears the faces of the rectangle in the mask, and writes one quad.
/// </para>
/// <para>
/// The quads are the same as the quads of the legacy mesher (union-find, before #45), in the same order: the directions
/// −X, +X, −Y, +Y, −Z, +Z, the slices in their order, and the rectangles in the order of their first face.
/// The vertex order of each direction is also the same. All the normals are (1, 0, 0), as before (#46).
/// </para>
/// <para>
/// The mesher keeps its mask between chunks. Thus, after the first chunk, it allocates only the quads of the result.
/// An instance is not safe for many threads: use one instance for each thread.
/// </para>
/// </remarks>
public sealed class GreedyMesher
{
    // The legacy normal of all the quads. #46 writes the correct normal of each direction.
    private static readonly Vector3 _legacyNormal = new(1, 0, 0);

    private ushort[] _mask = [];

    /// <summary>
    /// Makes the quads of a chunk.
    /// </summary>
    /// <param name="chunk">The chunk.</param>
    /// <returns>A new list with the quads, in chunk coordinates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chunk"/> is <c>null</c>.</exception>
    public List<MeshQuad> Mesh(Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        var quads = new List<MeshQuad>();
        Mesh(chunk.VoxelData, (int)chunk.XDepth, (int)chunk.YDepth, (int)chunk.ZDepth, quads);
        return quads;
    }

    /// <summary>
    /// Makes the quads of the voxels of a chunk, with no copy of the voxels.
    /// </summary>
    /// <param name="voxels">The block types of the chunk, with the indices [x, y, z].</param>
    /// <returns>A new list with the quads, in chunk coordinates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="voxels"/> is <c>null</c>.</exception>
    public List<MeshQuad> Mesh(ushort[,,] voxels)
    {
        ArgumentNullException.ThrowIfNull(voxels);

        // A ushort[x, y, z] array is one block of memory, in the same order as the index of Chunk.
        ReadOnlySpan<ushort> data = voxels.Length == 0
            ? []
            : MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetArrayDataReference(voxels)), voxels.Length);
        var quads = new List<MeshQuad>();
        Mesh(data, voxels.GetLength(0), voxels.GetLength(1), voxels.GetLength(2), quads);
        return quads;
    }

    private void Mesh(ReadOnlySpan<ushort> voxels, int sizeX, int sizeY, int sizeZ, List<MeshQuad> quads)
    {
        if (voxels.IsEmpty)
        {
            return;
        }

        var size = new ChunkSize(sizeX, sizeY, sizeZ);
        foreach (Direction direction in _directions)
        {
            (int width, int height, int depth) = size.Plane(direction.Axis);
            if (_mask.Length < width * height)
            {
                _mask = new ushort[width * height];
            }

            Span<ushort> mask = _mask.AsSpan(0, width * height);
            for (int slice = 0; slice < depth; slice++)
            {
                if (FillMask(voxels, size, direction, slice, width, height, mask))
                {
                    MergeFaces(mask, size, direction, slice, width, height, quads);
                }
            }
        }
    }

    // The six directions, in the order of the legacy mesher.
    private static readonly Direction[] _directions =
    [
        new(Axis.X, Descending: false),
        new(Axis.X, Descending: true),
        new(Axis.Y, Descending: false),
        new(Axis.Y, Descending: true),
        new(Axis.Z, Descending: false),
        new(Axis.Z, Descending: true),
    ];

    // Writes the block type of each visible face of the slice in the mask, or 0. Returns true if the slice has a visible face.
    private static bool FillMask(ReadOnlySpan<ushort> voxels, ChunkSize size, Direction direction, int slice, int width, int height, Span<ushort> mask)
    {
        bool any = false;
        for (int planeY = 0; planeY < height; planeY++)
        {
            for (int planeX = 0; planeX < width; planeX++)
            {
                (int x, int y, int z) = size.ToVoxel(direction, slice, planeX, planeY);
                ushort block = voxels[size.Index(x, y, z)];
                ushort face = 0;
                if (block != 0 && IsNeighborTransparent(voxels, size, direction, x, y, z))
                {
                    face = block;
                    any = true;
                }

                mask[planeX + (planeY * width)] = face;
            }
        }

        return any;
    }

    // An ascending direction shows the negative face of a voxel, and a descending direction the positive face (legacy mesher).
    private static bool IsNeighborTransparent(ReadOnlySpan<ushort> voxels, ChunkSize size, Direction direction, int x, int y, int z)
    {
        int step = direction.Descending ? 1 : -1;
        switch (direction.Axis)
        {
            case Axis.X:
                x += step;
                break;
            case Axis.Y:
                y += step;
                break;
            default:
                z += step;
                break;
        }

        return x < 0 || y < 0 || z < 0 || x >= size.X || y >= size.Y || z >= size.Z || voxels[size.Index(x, y, z)] == 0;
    }

    // Merges the faces of the mask into rectangles, and writes one quad for each rectangle.
    private static void MergeFaces(Span<ushort> mask, ChunkSize size, Direction direction, int slice, int width, int height, List<MeshQuad> quads)
    {
        for (int planeY = 0; planeY < height; planeY++)
        {
            for (int planeX = 0; planeX < width; planeX++)
            {
                int start = planeX + (planeY * width);
                ushort block = mask[start];
                if (block == 0)
                {
                    continue;
                }

                int rectangleWidth = 1;
                while (planeX + rectangleWidth < width && mask[start + rectangleWidth] == block)
                {
                    rectangleWidth++;
                }

                int rectangleHeight = 1;
                while (planeY + rectangleHeight < height && IsRowOf(mask.Slice(start + (rectangleHeight * width), rectangleWidth), block))
                {
                    rectangleHeight++;
                }

                for (int row = 0; row < rectangleHeight; row++)
                {
                    mask.Slice(start + (row * width), rectangleWidth).Clear();
                }

                quads.Add(MakeQuad(size, direction, slice, planeX, planeX + rectangleWidth - 1, planeY, planeY + rectangleHeight - 1, block));
            }
        }
    }

    private static bool IsRowOf(ReadOnlySpan<ushort> row, ushort block)
    {
        foreach (ushort face in row)
        {
            if (face != block)
            {
                return false;
            }
        }

        return true;
    }

    // The vertices of the legacy mesher (DisjointSetVisiblePlaneOptimizer.ToMeshQuads before #45), for each direction.
    // minX to maxX and minY to maxY are the cells of the rectangle on the plane.
    private static MeshQuad MakeQuad(ChunkSize size, Direction direction, int slice, int minX, int maxX, int minY, int maxY, ushort block)
    {
        switch (direction.Axis, direction.Descending)
        {
            case (Axis.X, true):
                {
                    int x = size.X - slice;
                    int y1 = size.Y - minX, y2 = size.Y - maxX - 1, z1 = size.Z - minY, z2 = size.Z - maxY - 1;
                    return Quad(new(x, y1, z1), new(x, y2, z1), new(x, y2, z2), new(x, y1, z2), block);
                }

            case (Axis.X, false):
                {
                    int x = slice;
                    int y1 = minX, y2 = maxX + 1, z1 = minY, z2 = maxY + 1;
                    return Quad(new(x, y2, z1), new(x, y1, z1), new(x, y1, z2), new(x, y2, z2), block);
                }

            case (Axis.Y, true):
                {
                    int y = size.Y - slice;
                    int x1 = size.X - minY, x2 = size.X - maxY - 1, z1 = size.Z - minX, z2 = size.Z - maxX - 1;
                    return Quad(new(x1, y, z1), new(x1, y, z2), new(x2, y, z2), new(x2, y, z1), block);
                }

            case (Axis.Y, false):
                {
                    int y = slice;
                    int x1 = minY, x2 = maxY + 1, z1 = minX, z2 = maxX + 1;
                    return Quad(new(x1, y, z2), new(x1, y, z1), new(x2, y, z1), new(x2, y, z2), block);
                }

            case (Axis.Z, true):
                {
                    int z = size.Z - slice;
                    int x1 = size.X - minY, x2 = size.X - maxY - 1, y1 = size.Y - minX, y2 = size.Y - maxX - 1;
                    return Quad(new(x1, y2, z), new(x1, y1, z), new(x2, y1, z), new(x2, y2, z), block);
                }

            default:
                {
                    int z = slice;
                    int x1 = minY, x2 = maxY + 1, y1 = minX, y2 = maxX + 1;
                    return Quad(new(x1, y1, z), new(x1, y2, z), new(x2, y2, z), new(x2, y1, z), block);
                }
        }
    }

    private static MeshQuad Quad(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, ushort block) => new()
    {
        Vertex0 = vertex0,
        Vertex1 = vertex1,
        Vertex2 = vertex2,
        Vertex3 = vertex3,
        Normal = _legacyNormal,
        VoxelID = block,
    };

    // A face direction: the axis of the slices, and the order of the slices on the axis.
    private readonly record struct Direction(Axis Axis, bool Descending);

    // The size of the chunk, and the mapping between a plane cell and a voxel. The planes are the planes of the legacy mesher:
    // - X slices: plane X is Y, plane Y is Z.
    // - Y slices: plane X is Z, plane Y is X.
    // - Z slices: plane X is Y, plane Y is X.
    // In a descending direction, the slice and the two plane coordinates count from the end of their axes.
    private readonly record struct ChunkSize(int X, int Y, int Z)
    {
        public int Index(int x, int y, int z) => (((x * Y) + y) * Z) + z;

        public (int Width, int Height, int Depth) Plane(Axis axis) => axis switch
        {
            Axis.X => (Y, Z, X),
            Axis.Y => (Z, X, Y),
            _ => (Y, X, Z),
        };

        public (int X, int Y, int Z) ToVoxel(Direction direction, int slice, int planeX, int planeY)
        {
            (int width, int height, int depth) = Plane(direction.Axis);
            if (direction.Descending)
            {
                slice = depth - 1 - slice;
                planeX = width - 1 - planeX;
                planeY = height - 1 - planeY;
            }

            return direction.Axis switch
            {
                Axis.X => (slice, planeX, planeY),
                Axis.Y => (planeY, slice, planeX),
                _ => (planeY, planeX, slice),
            };
        }
    }
}
