// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal.Numerics;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// Makes the quads of the visible faces of a chunk, and merges the faces of the same block type into rectangles (greedy meshing, #45).
/// </summary>
/// <remarks>
/// <para>
/// For each of the six face directions, the mesher goes through the slices of the chunk. In each slice, it writes a mask:
/// the block type of each visible face, or 0 for no face. A face is visible if the neighbor voxel in the face direction is air.
/// </para>
/// <para>
/// At a chunk border, the neighbor voxel is in the neighbor chunk (#48). With a <see cref="VoxelWorld"/>, the mesher reads the border voxels
/// of the neighbor chunks in the world. A neighbor chunk that the world does not have is air. Thus, when the neighbor chunk arrives
/// or changes, the caller must mesh the chunk again. Without a world, all the voxels outside the chunk are air, as before #48.
/// </para>
/// <para>
/// The mesher then reads the mask row by row. At the first face that is not in a rectangle, it makes the rectangle as wide as possible,
/// then as high as possible, with faces of the same block type. It clears the faces of the rectangle in the mask, and writes one quad.
/// </para>
/// <para>
/// The quads are the same as the quads of the legacy mesher (union-find, before #45), in the same order: the directions
/// −X, +X, −Y, +Y, −Z, +Z, the slices in their order, and the rectangles in the order of their first face.
/// The vertex order of each direction is also the same. The normal of each quad is the unit vector of its face direction (#46).
/// It agrees with the winding order: the cross product (vertex 1 − vertex 0) × (vertex 2 − vertex 0) has the same direction.
/// </para>
/// <para>
/// The mesher keeps its mask between chunks. Thus, after the first chunk, it allocates only the quads of the result.
/// An instance is not safe for many threads: use one instance for each thread.
/// </para>
/// </remarks>
public sealed class GreedyMesher
{
    private ushort[] _mask = [];

    /// <summary>
    /// Makes the quads of a chunk. All the voxels outside the chunk are air.
    /// </summary>
    /// <param name="chunk">The chunk.</param>
    /// <returns>A new list with the quads, in chunk coordinates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chunk"/> is <c>null</c>.</exception>
    public List<MeshQuad> Mesh(Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        var quads = new List<MeshQuad>();
        Mesh(chunk.VoxelData, (int)chunk.XDepth, (int)chunk.YDepth, (int)chunk.ZDepth, null, default, quads);
        return quads;
    }

    /// <summary>
    /// Makes the quads of the voxels of a chunk, with no copy of the voxels. All the voxels outside the chunk are air.
    /// </summary>
    /// <param name="voxels">The block types of the chunk, with the indices [x, y, z].</param>
    /// <returns>A new list with the quads, in chunk coordinates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="voxels"/> is <c>null</c>.</exception>
    public List<MeshQuad> Mesh(ushort[,,] voxels) => Mesh(voxels, null, default);

    /// <summary>
    /// Makes the quads of the voxels of a chunk, with no copy of the voxels. The neighbor chunks in the world hide the faces at the borders (#48).
    /// </summary>
    /// <param name="voxels">The block types of the chunk, with the indices [x, y, z].</param>
    /// <param name="world">
    /// The world that has the neighbor chunks, or <c>null</c>. The chunk size of the world must be the size of <paramref name="voxels"/>.
    /// A neighbor chunk that the world does not have is air.
    /// </param>
    /// <param name="chunkPosition">The world position of the first voxel of the chunk.</param>
    /// <returns>A new list with the quads, in chunk coordinates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="voxels"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The size of <paramref name="voxels"/> is not the chunk size of <paramref name="world"/>.</exception>
    public List<MeshQuad> Mesh(ushort[,,] voxels, VoxelWorld? world, int3 chunkPosition)
    {
        ArgumentNullException.ThrowIfNull(voxels);
        if (world is not null
            && (voxels.GetLength(0) != world.ChunkSize.X || voxels.GetLength(1) != world.ChunkSize.Y || voxels.GetLength(2) != world.ChunkSize.Z))
        {
            throw new ArgumentException("The size of the voxels is not the chunk size of the world.", nameof(voxels));
        }

        var quads = new List<MeshQuad>();
        Mesh(AsSpan(voxels), voxels.GetLength(0), voxels.GetLength(1), voxels.GetLength(2), world, chunkPosition, quads);
        return quads;
    }

    // A ushort[x, y, z] array is one block of memory, in the same order as the index of Chunk.
    private static ReadOnlySpan<ushort> AsSpan(ushort[,,] voxels) => voxels.Length == 0
        ? []
        : MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetArrayDataReference(voxels)), voxels.Length);

    private void Mesh(ReadOnlySpan<ushort> voxels, int sizeX, int sizeY, int sizeZ, VoxelWorld? world, int3 chunkPosition, List<MeshQuad> quads)
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

            // The neighbor chunk on the side of the faces of this direction. Its voxels hide the faces of the border slice.
            ReadOnlySpan<ushort> neighbor = world is not null && world.TryGetChunk(chunkPosition + size.NeighborOffset(direction), out ushort[,,]? neighborVoxels)
                ? AsSpan(neighborVoxels)
                : [];
            Span<ushort> mask = _mask.AsSpan(0, width * height);
            for (int slice = 0; slice < depth; slice++)
            {
                if (FillMask(voxels, neighbor, size, direction, slice, width, height, mask))
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
    private static bool FillMask(ReadOnlySpan<ushort> voxels, ReadOnlySpan<ushort> neighbor, ChunkSize size, Direction direction, int slice, int width, int height, Span<ushort> mask)
    {
        bool any = false;
        for (int planeY = 0; planeY < height; planeY++)
        {
            for (int planeX = 0; planeX < width; planeX++)
            {
                (int x, int y, int z) = size.ToVoxel(direction, slice, planeX, planeY);
                ushort block = voxels[size.Index(x, y, z)];
                ushort face = 0;
                if (block != 0 && IsNeighborTransparent(voxels, neighbor, size, direction, x, y, z))
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
    // Outside the chunk, the neighbor voxel is in the neighbor chunk: at the same position, minus or plus one chunk size on the axis.
    // An empty span is a neighbor chunk of air.
    private static bool IsNeighborTransparent(ReadOnlySpan<ushort> voxels, ReadOnlySpan<ushort> neighbor, ChunkSize size, Direction direction, int x, int y, int z)
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

        if (x >= 0 && y >= 0 && z >= 0 && x < size.X && y < size.Y && z < size.Z)
        {
            return voxels[size.Index(x, y, z)] == 0;
        }

        return neighbor.IsEmpty || neighbor[size.Index((x + size.X) % size.X, (y + size.Y) % size.Y, (z + size.Z) % size.Z)] == 0;
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

    // The vertices of the legacy mesher (DisjointSetVisiblePlaneOptimizer.ToMeshQuads before #45), and the normal, for each direction.
    // An ascending direction shows the negative face of a voxel, and a descending direction the positive face.
    // minX to maxX and minY to maxY are the cells of the rectangle on the plane.
    private static MeshQuad MakeQuad(ChunkSize size, Direction direction, int slice, int minX, int maxX, int minY, int maxY, ushort block)
    {
        switch (direction.Axis, direction.Descending)
        {
            case (Axis.X, true):
                {
                    int x = size.X - slice;
                    int y1 = size.Y - minX, y2 = size.Y - maxX - 1, z1 = size.Z - minY, z2 = size.Z - maxY - 1;
                    return Quad(new(x, y1, z1), new(x, y2, z1), new(x, y2, z2), new(x, y1, z2), Vector3.UnitX, block);
                }

            case (Axis.X, false):
                {
                    int x = slice;
                    int y1 = minX, y2 = maxX + 1, z1 = minY, z2 = maxY + 1;
                    return Quad(new(x, y2, z1), new(x, y1, z1), new(x, y1, z2), new(x, y2, z2), -Vector3.UnitX, block);
                }

            case (Axis.Y, true):
                {
                    int y = size.Y - slice;
                    int x1 = size.X - minY, x2 = size.X - maxY - 1, z1 = size.Z - minX, z2 = size.Z - maxX - 1;
                    return Quad(new(x1, y, z1), new(x1, y, z2), new(x2, y, z2), new(x2, y, z1), Vector3.UnitY, block);
                }

            case (Axis.Y, false):
                {
                    int y = slice;
                    int x1 = minY, x2 = maxY + 1, z1 = minX, z2 = maxX + 1;
                    return Quad(new(x1, y, z2), new(x1, y, z1), new(x2, y, z1), new(x2, y, z2), -Vector3.UnitY, block);
                }

            case (Axis.Z, true):
                {
                    int z = size.Z - slice;
                    int x1 = size.X - minY, x2 = size.X - maxY - 1, y1 = size.Y - minX, y2 = size.Y - maxX - 1;
                    return Quad(new(x1, y2, z), new(x1, y1, z), new(x2, y1, z), new(x2, y2, z), Vector3.UnitZ, block);
                }

            default:
                {
                    int z = slice;
                    int x1 = minY, x2 = maxY + 1, y1 = minX, y2 = maxX + 1;
                    return Quad(new(x1, y1, z), new(x1, y2, z), new(x2, y2, z), new(x2, y1, z), -Vector3.UnitZ, block);
                }
        }
    }

    private static MeshQuad Quad(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, Vector3 normal, ushort block) => new()
    {
        Vertex0 = vertex0,
        Vertex1 = vertex1,
        Vertex2 = vertex2,
        Vertex3 = vertex3,
        Normal = normal,
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

        // The offset from the chunk to its neighbor chunk on the side of the faces of a direction.
        public int3 NeighborOffset(Direction direction)
        {
            int sign = direction.Descending ? 1 : -1;
            return direction.Axis switch
            {
                Axis.X => new int3(sign * X, 0, 0),
                Axis.Y => new int3(0, sign * Y, 0),
                _ => new int3(0, 0, sign * Z),
            };
        }

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
