// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;

namespace ElysianOutpost.Core.WorldGeneration;

/// <summary>
/// The mesh data of one chunk and its position in the world.
/// </summary>
/// <param name="Position">The position of the first voxel of the chunk, in voxel coordinates.</param>
/// <param name="Mesh">The vertices and the indices of the chunk, in chunk coordinates.</param>
public readonly record struct BuiltChunk(int3 Position, ChunkMeshData Mesh);
