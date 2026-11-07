// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using Vector3 = System.Numerics.Vector3;

namespace ElysianOutpost.Core.Voxels;

/// <summary>
/// The voxels of one chunk, for the mesher.
/// </summary>
/// <remarks>
/// The chunk stores the block types in one <c>ushort</c> array (#5). The index of the voxel (x, y, z) is
/// <c>(x × YDepth + y) × ZDepth + z</c>. This is the order of a <c>ushort[x, y, z]</c> array.
/// Thus the constructor copies the generated voxels with one block copy.
/// Before #5, the chunk made one <see cref="Voxel"/> object for each voxel: about 16.8 million objects for the default world.
/// </remarks>
public class Chunk
{
    private readonly ushort[] _voxels;


    public uint XDepth { get; }
    public uint YDepth { get; }
    public uint ZDepth { get; }

    public int3 WorldPosition { get; }

    /// <summary>
    /// Gets the block types of the chunk, in the order of the index of the chunk. The mesher reads them with no copy.
    /// </summary>
    internal ReadOnlySpan<ushort> VoxelData => _voxels;

    private static Voxel EmptyVoxel => default;


    #region Constructors

    public Chunk(ushort[,,] voxelArray, int3 position)
    {
        XDepth = (uint)voxelArray.GetLength(0);
        YDepth = (uint)voxelArray.GetLength(1);
        ZDepth = (uint)voxelArray.GetLength(2);
        WorldPosition = position;

        _voxels = new ushort[voxelArray.Length];

        // A ushort[,,] array is one block of memory in the order [x, y, z]: the same order as the index of the chunk.
        Buffer.BlockCopy(voxelArray, 0, _voxels, 0, voxelArray.Length * sizeof(ushort));
    }

    /// <summary>
    /// Initializes a new chunk from a file on disk.
    /// The first line contains the comma separated dimensions (X,Y,Z)
    /// and the second line contains all voxel IDs in X-Y-Z order, separated by commas.
    /// TODO : Need to hold the world position of the chunk as well.
    /// </summary>
    public Chunk(string fileName)
    {
        string[] lines = File.ReadAllLines(fileName);
        if (lines.Length < 2)
        {
            throw new ArgumentException("File must contain at least two lines", nameof(fileName));
        }

        string[] sizes = lines[0].Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (sizes.Length != 3)
        {
            throw new FormatException("First line must contain three comma-separated values.");
        }

        XDepth = uint.Parse(sizes[0]);
        YDepth = uint.Parse(sizes[1]);
        ZDepth = uint.Parse(sizes[2]);

        _voxels = new ushort[XDepth * YDepth * ZDepth];

        string[] voxelIds = lines[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (voxelIds.Length != XDepth * YDepth * ZDepth)
        {
            throw new FormatException("Voxel count does not match chunk dimensions.");
        }

        int index = 0;
        for (uint x = 0; x < XDepth; x++)
        {
            for (uint y = 0; y < YDepth; y++)
            {
                for (uint z = 0; z < ZDepth; z++)
                {
                    _voxels[Index(x, y, z)] = ushort.Parse(voxelIds[index++]);
                }
            }
        }
    }

    #endregion

    #region Getters and Setters

    /// <summary>
    /// Retrieves the voxel at the given position (X,Y,Z), ignoring the axis fields for now.
    /// Throws if out of range, or you could choose to return null.
    /// </summary>
    public Voxel Get(uint x, uint y, uint z)
    {
        if (x >= XDepth || y >= YDepth || z >= ZDepth)
        {
            throw new ArgumentOutOfRangeException("Requested voxel coordinates are out of bounds.");
        }

        return new Voxel(_voxels[Index(x, y, z)]);
    }

    public Voxel GetAtWorldPosition(int3 worldPosition)
    {
        int localX = worldPosition.X - WorldPosition.X;
        int localY = worldPosition.Y - WorldPosition.Y;
        int localZ = worldPosition.Z - WorldPosition.Z;

        if (localX < 0 || localY < 0 || localZ < 0 ||
            localX >= XDepth || localY >= YDepth || localZ >= ZDepth)
        {
            throw new ArgumentOutOfRangeException("Requested world position is out of chunk bounds.");
        }

        return new Voxel(_voxels[Index((uint)localX, (uint)localY, (uint)localZ)]);
    }

    /// <summary>
    /// Sets a voxel at the given position (X,Y,Z). 
    /// </summary>
    public void Set(uint x, uint y, uint z, Voxel voxel)
    {
        if (x >= XDepth || y >= YDepth || z >= ZDepth)
        {
            throw new ArgumentOutOfRangeException("Requested voxel coordinates are out of bounds.");
        }

        _voxels[Index(x, y, z)] = voxel.ID;
    }

    public void SetAtWorldPosition(int3 worldPosition, Voxel voxel)
    {
        int localX = worldPosition.X - WorldPosition.X;
        int localY = worldPosition.Y - WorldPosition.Y;
        int localZ = worldPosition.Z - WorldPosition.Z;

        if (localX < 0 || localY < 0 || localZ < 0 ||
            localX >= XDepth || localY >= YDepth || localZ >= ZDepth)
        {
            throw new ArgumentOutOfRangeException("Requested world position is out of chunk bounds.");
        }

        _voxels[Index((uint)localX, (uint)localY, (uint)localZ)] = voxel.ID;
    }

    #endregion

    #region Bounds

    // The index of the voxel (x, y, z) in the array. The caller checks the bounds.
    private int Index(uint x, uint y, uint z) => (int)((((x * YDepth) + y) * ZDepth) + z);

    public bool IsOutOfBound(uint x, uint y, uint z)
    {
        return x >= XDepth || y >= YDepth || z >= ZDepth;
    }

    #endregion

    #region Mesh Generation

    /// <summary>
    /// Builds a mesh that contains every face for each solid voxel in the chunk.
    /// This is a naïve implementation without any form of optimization.
    /// </summary>
    public Mesh ToMesh()
    {
        List<MeshQuad> list = new();

        for (uint x = 0; x < XDepth; x++)
        {
            for (uint y = 0; y < YDepth; y++)
            {
                for (uint z = 0; z < ZDepth; z++)
                {
                    ushort id = _voxels[Index(x, y, z)];
                    if (id == 0)
                        continue;

                    list.AddRange(CreateVoxelQuads(x, y, z, id));
                }
            }
        }

        Mesh mesh = new(list);

        return mesh;
    }

    private static IEnumerable<MeshQuad> CreateVoxelQuads(uint x, uint y, uint z, ushort voxelId)
    {
        yield return new MeshQuad
        {
            Vertex0 = new Vector3(x, y, z),
            Vertex1 = new Vector3(x, y + 1, z),
            Vertex2 = new Vector3(x + 1, y + 1, z),
            Vertex3 = new Vector3(x + 1, y, z),
            Normal = new Vector3(0, 0, -1),
            VoxelID = voxelId
        };

        yield return new MeshQuad
        {
            Vertex0 = new Vector3(x, y, z + 1),
            Vertex1 = new Vector3(x + 1, y, z + 1),
            Vertex2 = new Vector3(x + 1, y + 1, z + 1),
            Vertex3 = new Vector3(x, y + 1, z + 1),
            Normal = new Vector3(0, 0, 1),
            VoxelID = voxelId
        };

        yield return new MeshQuad
        {
            Vertex0 = new Vector3(x, y, z),
            Vertex1 = new Vector3(x, y, z + 1),
            Vertex2 = new Vector3(x, y + 1, z + 1),
            Vertex3 = new Vector3(x, y + 1, z),
            Normal = new Vector3(-1, 0, 0),
            VoxelID = voxelId
        };

        yield return new MeshQuad
        {
            Vertex0 = new Vector3(x + 1, y, z + 1),
            Vertex1 = new Vector3(x + 1, y, z),
            Vertex2 = new Vector3(x + 1, y + 1, z),
            Vertex3 = new Vector3(x + 1, y + 1, z + 1),
            Normal = new Vector3(1, 0, 0),
            VoxelID = voxelId
        };

        yield return new MeshQuad
        {
            Vertex0 = new Vector3(x, y, z),
            Vertex1 = new Vector3(x + 1, y, z),
            Vertex2 = new Vector3(x + 1, y, z + 1),
            Vertex3 = new Vector3(x, y, z + 1),
            Normal = new Vector3(0, -1, 0),
            VoxelID = voxelId
        };

        yield return new MeshQuad
        {
            Vertex0 = new Vector3(x, y + 1, z + 1),
            Vertex1 = new Vector3(x + 1, y + 1, z + 1),
            Vertex2 = new Vector3(x + 1, y + 1, z),
            Vertex3 = new Vector3(x, y + 1, z),
            Normal = new Vector3(0, 1, 0),
            VoxelID = voxelId
        };
    }

    #endregion

    #region Saving and Loading

    /// <summary>
    /// Saves this chunk to a file. The first line contains the dimensions
    /// (X,Y,Z) separated by commas. The second line contains all voxel IDs
    /// separated by commas in X-Y-Z order.
    /// TODO : Need to save the world position of the chunk as well.
    /// </summary>
    public void Save(string fileName)
    {
        using StreamWriter writer = new(fileName);
        writer.WriteLine($"{XDepth},{YDepth},{ZDepth}");

        List<string> ids = new();
        for (uint x = 0; x < XDepth; x++)
        {
            for (uint y = 0; y < YDepth; y++)
            {
                for (uint z = 0; z < ZDepth; z++)
                {
                    ids.Add(_voxels[Index(x, y, z)].ToString());
                }
            }
        }

        writer.WriteLine(string.Join(',', ids));
    }

    #endregion

    #region Chunk interactions

    public Voxel RemoveVoxel(uint3 position)
    {
        if (IsOutOfBound(position.X, position.Y, position.Z))
        {
            throw new ArgumentOutOfRangeException("Requested voxel coordinates are out of bounds.");
        }

        int index = Index(position.X, position.Y, position.Z);
        var removedVoxel = new Voxel(_voxels[index]);
        _voxels[index] = EmptyVoxel.ID;
        return removedVoxel;
    }

    public void PlaceVoxel(uint3 position, Voxel voxel)
    {
        if (IsOutOfBound(position.X, position.Y, position.Z))
        {
            throw new ArgumentOutOfRangeException("Requested voxel coordinates are out of bounds.");
        }

        _voxels[Index(position.X, position.Y, position.Z)] = voxel.ID;
    }

    #endregion
}
