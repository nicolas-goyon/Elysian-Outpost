using System;
using System.Collections.Generic;
using System.IO;
using Base;
using Unity.Mathematics;
using Mesh = Libs.VoxelMeshOptimizer.Mesh;
using Vector3 = System.Numerics.Vector3;

namespace Libs.VoxelMeshOptimizer
{
    public class Chunk
    {
        private readonly Voxel[,,] _voxels;
        

        public uint XDepth { get; }
        public uint YDepth { get; }
        public uint ZDepth { get; }
        
        public int3 WorldPosition { get; }

        public Chunk(ushort[,,] voxelArray, int3 position)
        {
            XDepth = (uint)voxelArray.GetLength(0);
            YDepth = (uint)voxelArray.GetLength(1);
            ZDepth = (uint)voxelArray.GetLength(2);
            WorldPosition = position;

            _voxels = new Voxel[XDepth, YDepth, ZDepth];

            // Initialize from the ushort array
            for (uint x = 0; x < XDepth; x++)
            {
                for (uint y = 0; y < YDepth; y++)
                {
                    for (uint z = 0; z < ZDepth; z++)
                    {
                        ushort value = voxelArray[x, y, z];
                        _voxels[x, y, z] = new Voxel(value);
                    }
                }
            }
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

            _voxels = new Voxel[XDepth, YDepth, ZDepth];

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
                        ushort id = ushort.Parse(voxelIds[index++]);
                        _voxels[x, y, z] = new Voxel(id);
                    }
                }
            }
        }
        

        /// <summary>
        /// Returns all voxels in this chunk in no guaranteed order.
        /// </summary>
        public IEnumerable<Voxel> GetVoxels()
        {
            for (uint x = 0; x < XDepth; x++)
            {
                for (uint y = 0; y < YDepth; y++)
                {
                    for (uint z = 0; z < ZDepth; z++)
                    {
                        yield return _voxels[x, y, z];
                    }
                }
            }
        }

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

            return _voxels[x, y, z];
        }
        
        public Voxel GetAtWorldPosition(int3 worldPosition)
        {
            int localX = worldPosition.x - WorldPosition.x;
            int localY = worldPosition.y - WorldPosition.y;
            int localZ = worldPosition.z - WorldPosition.z;

            if (localX < 0 || localY < 0 || localZ < 0 ||
                localX >= XDepth || localY >= YDepth || localZ >= ZDepth)
            {
                throw new ArgumentOutOfRangeException("Requested world position is out of chunk bounds.");
            }

            return _voxels[localX, localY, localZ];
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

            _voxels[x, y, z] = voxel;
        }
        
        public void SetAtWorldPosition(int3 worldPosition, Voxel voxel)
        {
            int localX = worldPosition.x - WorldPosition.x;
            int localY = worldPosition.y - WorldPosition.y;
            int localZ = worldPosition.z - WorldPosition.z;

            if (localX < 0 || localY < 0 || localZ < 0 ||
                localX >= XDepth || localY >= YDepth || localZ >= ZDepth)
            {
                throw new ArgumentOutOfRangeException("Requested world position is out of chunk bounds.");
            }

            _voxels[localX, localY, localZ] = voxel;
        }

        /// <summary>
        /// Iterates over every (X,Y,Z) in the chunk in the order of three distinct axes 
        /// (Major, Middle, Minor). The callback receives a VoxelPosition that includes 
        /// the coordinate plus the iteration axes for debugging or advanced logic.
        /// </summary>
        public void ForEachCoordinate(
            Axis majorA, AxisOrder majorAsc,
            Axis middleA, AxisOrder middleAsc,
            Axis minorA, AxisOrder minorAsc,
            Action<uint, uint, uint> action
        )
        {
            // 2) Ensure all axes are distinct
            if (majorA == middleA || middleA == minorA || majorA == minorA)
            {
                throw new ArgumentException("All three HumanAxis values must target different axes (X/Y/Z).");
            }

            // 4) Triple-nested loop in the order: major -> middle -> minor
            //    We find which axis is major, middle, minor, then nest them accordingly.
            foreach (uint majorVal in BuildRange(GetDepth(majorA), majorAsc))
            {
                foreach (uint midVal in BuildRange(GetDepth(middleA), middleAsc))
                {
                    foreach (uint minVal in BuildRange(GetDepth(minorA), minorAsc))
                    {
                        uint x = 0, y = 0, z = 0;

                        switch (majorA)
                        {
                            case Axis.X:
                                x = majorVal;
                                break;
                            case Axis.Y:
                                y = majorVal;
                                break;
                            case Axis.Z:
                                z = majorVal;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(majorA), majorA, null);
                        }

                        switch (middleA)
                        {
                            case Axis.X:
                                x = midVal;
                                break;
                            case Axis.Y:
                                y = midVal;
                                break;
                            case Axis.Z:
                                z = midVal;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(middleA), middleA, null);
                        }

                        switch (minorA)
                        {
                            case Axis.X:
                                x = minVal;
                                break;
                            case Axis.Y:
                                y = minVal;
                                break;
                            case Axis.Z:
                                z = minVal;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(minorA), minorA, null);
                        }

                        action(x, y, z);
                    }
                }
            }
        }


        private IEnumerable<uint> BuildRange(uint size, AxisOrder order)
        {
            if (order == AxisOrder.ASCENDING)
            {
                for (uint i = 0; i < size; i++)
                    yield return i;
            }
            else
            {
                for (int i = (int)size - 1; i >= 0; i--)
                    yield return (uint)i;
            }
        }


        /// <summary>
        /// Simple helper to pick the chunk’s dimension (depth) by axis.
        /// </summary>
        public uint GetDepth(Axis axis)
        {
            return axis switch
            {
                Axis.X => XDepth,
                Axis.Y => YDepth,
                Axis.Z => ZDepth,
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unknown axis.")
            };
        }

        public bool IsOutOfBound(uint x, uint y, uint z)
        {
            return x >= GetDepth(Axis.X)
                || y >= GetDepth(Axis.Y)
                || z >= GetDepth(Axis.Z);
        }

        public bool AreDifferentAxis(
            Axis major,
            Axis middle,
            Axis minor
        )
        {
            return major != middle && middle != minor && minor != major;
        }

        public (uint planeWidth, uint planeHeight) GetPlaneDimensions(
            Axis major,
            Axis middle,
            Axis minor
        )
        {
            // "Plane dimensions" = minor dimension (x-axis of the plane),
            //                      middle dimension (y-axis of the plane).
            uint planeWidth = GetDepth(middle);
            uint planeHeight = GetDepth(minor);

            return (planeWidth, planeHeight);
        }
        
        
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
                        Voxel voxel = _voxels[x, y, z];
                        if (!voxel.IsSolid)
                            continue;

                        list.AddRange(CreateVoxelQuads(x, y, z, voxel.ID));
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
                        ids.Add(_voxels[x, y, z].ID.ToString());
                    }
                }
            }

            writer.WriteLine(string.Join(',', ids));
        }

        public void RemoveVoxel(uint3 position)
        {
            if (position.x >= XDepth || position.y >= YDepth || position.z >= ZDepth)
            {
                throw new ArgumentOutOfRangeException("Requested voxel coordinates are out of bounds.");
            }

            _voxels[position.x, position.y, position.z] =
                new Voxel(0); // TODO : Assuming ID 0 represents an empty voxel
        }
    }
}
