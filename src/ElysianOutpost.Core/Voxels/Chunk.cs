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

    /// <summary>
    /// Gets the number of voxels on the X axis.
    /// </summary>
    public uint XDepth { get; }

    /// <summary>
    /// Gets the number of voxels on the Y axis.
    /// </summary>
    public uint YDepth { get; }

    /// <summary>
    /// Gets the number of voxels on the Z axis.
    /// </summary>
    public uint ZDepth { get; }

    /// <summary>
    /// Gets the world position of the first voxel of the chunk. A chunk read from a file has the position (0, 0, 0).
    /// </summary>
    public int3 WorldPosition { get; }

    /// <summary>
    /// Gets the block types of the chunk, in the order of the index of the chunk. The mesher reads them with no copy.
    /// </summary>
    internal ReadOnlySpan<ushort> VoxelData => _voxels;

    private static Voxel EmptyVoxel => default;

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Chunk"/> class with a copy of generated voxels.
    /// </summary>
    /// <param name="voxelArray">The block types, for example the result of <see cref="WorldGeneration.MainGeneration.GenerateChunkAt"/>.</param>
    /// <param name="position">The world position of the first voxel of the chunk.</param>
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
    /// Initializes a new instance of the <see cref="Chunk"/> class from a file of <see cref="Save"/>.
    /// The first line contains the sizes X, Y and Z, with commas between them.
    /// The second line contains all the block types in the order X, Y, Z, with commas between them.
    /// The file does not contain the world position of the chunk. TODO(#70): the save format of the world gives the chunk positions.
    /// </summary>
    /// <param name="fileName">The path of the file.</param>
    /// <exception cref="ArgumentException">The file has less than two lines.</exception>
    /// <exception cref="FormatException">The first line does not have three values, or the number of block types is not X × Y × Z.</exception>
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
    /// Gets the voxel at a position in the chunk.
    /// </summary>
    /// <param name="x">The X coordinate in the chunk.</param>
    /// <param name="y">The Y coordinate in the chunk.</param>
    /// <param name="z">The Z coordinate in the chunk.</param>
    /// <returns>The voxel.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the chunk.</exception>
    public Voxel Get(uint x, uint y, uint z)
    {
        if (x >= XDepth || y >= YDepth || z >= ZDepth)
        {
            throw new ArgumentOutOfRangeException("Requested voxel coordinates are out of bounds.");
        }

        return new Voxel(_voxels[Index(x, y, z)]);
    }

    /// <summary>
    /// Gets the voxel at a world position.
    /// </summary>
    /// <param name="worldPosition">The world position of the voxel.</param>
    /// <returns>The voxel.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the chunk.</exception>
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
    /// Sets the voxel at a position in the chunk.
    /// </summary>
    /// <param name="x">The X coordinate in the chunk.</param>
    /// <param name="y">The Y coordinate in the chunk.</param>
    /// <param name="z">The Z coordinate in the chunk.</param>
    /// <param name="voxel">The new voxel.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the chunk.</exception>
    public void Set(uint x, uint y, uint z, Voxel voxel)
    {
        if (x >= XDepth || y >= YDepth || z >= ZDepth)
        {
            throw new ArgumentOutOfRangeException("Requested voxel coordinates are out of bounds.");
        }

        _voxels[Index(x, y, z)] = voxel.ID;
    }

    /// <summary>
    /// Sets the voxel at a world position.
    /// </summary>
    /// <param name="worldPosition">The world position of the voxel.</param>
    /// <param name="voxel">The new voxel.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the chunk.</exception>
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

    /// <summary>
    /// Gets a value that tells if a position is outside the chunk.
    /// </summary>
    /// <param name="x">The X coordinate in the chunk.</param>
    /// <param name="y">The Y coordinate in the chunk.</param>
    /// <param name="z">The Z coordinate in the chunk.</param>
    /// <returns><c>true</c> if the position is outside the chunk.</returns>
    public bool IsOutOfBound(uint x, uint y, uint z)
    {
        return x >= XDepth || y >= YDepth || z >= ZDepth;
    }

    #endregion

    #region Mesh Generation

    /// <summary>
    /// Makes a mesh with all six faces of each solid voxel of the chunk, also the hidden faces.
    /// This simple mesh is the reference of the tests of <see cref="GreedyMesher"/>.
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
    /// Writes the chunk to a file. The first line contains the sizes X, Y and Z, with commas between them.
    /// The second line contains all the block types in the order X, Y, Z, with commas between them.
    /// The file does not contain the world position of the chunk. TODO(#70): the save format of the world gives the chunk positions.
    /// </summary>
    /// <param name="fileName">The path of the file. The method replaces a file that exists.</param>
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

    /// <summary>
    /// Removes a voxel: its block type becomes air (0).
    /// </summary>
    /// <param name="position">The position in the chunk.</param>
    /// <returns>The voxel before the change.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the chunk.</exception>
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

    /// <summary>
    /// Sets the voxel at a position in the chunk.
    /// </summary>
    /// <param name="position">The position in the chunk.</param>
    /// <param name="voxel">The new voxel.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the chunk.</exception>
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
