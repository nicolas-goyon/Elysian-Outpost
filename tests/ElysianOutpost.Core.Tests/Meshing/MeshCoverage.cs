// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// Compares the quads of a mesh with the visible faces of the voxels. This is the test oracle of the mesher.
/// </summary>
/// <remarks>
/// This class does not use the code of the mesher. It finds the visible faces directly from the block types.
/// A face of a solid voxel is visible if its neighbor voxel is air or is outside the chunk.
/// The mesher uses the same rule (<c>VoxelVisibilityMap</c>): it makes quads on all the borders of the chunk.
/// Issue #48 changes this rule.
/// </remarks>
internal static class MeshCoverage
{
    private const int MaxProblemsInMessage = 10;

    /// <summary>
    /// Finds the visible faces of the voxels of a chunk.
    /// </summary>
    /// <param name="voxels">The block types of the chunk, with the indices [x, y, z]. The block type 0 is air.</param>
    /// <returns>The block type of the voxel of each visible face.</returns>
    public static Dictionary<Face, ushort> FindVisibleFaces(ushort[,,] voxels)
    {
        var faces = new Dictionary<Face, ushort>();
        foreach ((int x, int y, int z) in VoxelGrid.Positions(voxels))
        {
            ushort blockType = voxels[x, y, z];
            if (blockType == 0)
            {
                continue;
            }

            foreach (FaceDirection direction in FaceDirections.All)
            {
                var face = new Face(x, y, z, direction);
                if (BlockTypeAt(voxels, face.Neighbor) == 0)
                {
                    faces.Add(face, blockType);
                }
            }
        }

        return faces;
    }

    /// <summary>
    /// Finds the problems of the quads of a mesh.
    /// </summary>
    /// <param name="voxels">The block types of the chunk, with the indices [x, y, z]. The block type 0 is air.</param>
    /// <param name="quads">The quads that the mesher made for the chunk.</param>
    /// <param name="chunkOrigin">
    /// The expected position of the chunk corner in the coordinates of the quads.
    /// It is (0, 0, 0) for a mesh in chunk coordinates. It is the world position of the chunk for a mesh in world coordinates.
    /// </param>
    /// <returns>
    /// A description of each problem. The list is empty if the quads cover each visible face exactly once, with the block type of its voxel.
    /// </returns>
    /// <remarks>
    /// The method finds these problems:
    /// <list type="bullet">
    /// <item>A quad that is not a rectangle on the voxel grid.</item>
    /// <item>A quad on a face that is not visible.</item>
    /// <item>A quad with an incorrect block type.</item>
    /// <item>A face in two quads.</item>
    /// <item>A visible face in no quad.</item>
    /// </list>
    /// </remarks>
    public static List<string> FindProblems(ushort[,,] voxels, IReadOnlyList<MeshQuad> quads, int3 chunkOrigin)
    {
        ArgumentNullException.ThrowIfNull(quads);
        Dictionary<Face, ushort> visibleFaces = FindVisibleFaces(voxels);
        var quadOfFace = new Dictionary<Face, int>();
        var problems = new List<string>();
        for (int i = 0; i < quads.Count; i++)
        {
            if (!QuadShape.TryCreate(quads[i], out QuadShape shape, out string problem))
            {
                problems.Add($"Quad {i}: {problem}");
                continue;
            }

            foreach (Face faceInQuadCoordinates in shape.Faces())
            {
                Face face = faceInQuadCoordinates with
                {
                    X = faceInQuadCoordinates.X - chunkOrigin.X,
                    Y = faceInQuadCoordinates.Y - chunkOrigin.Y,
                    Z = faceInQuadCoordinates.Z - chunkOrigin.Z,
                };
                if (!visibleFaces.TryGetValue(face, out ushort blockType))
                {
                    problems.Add($"Quad {i} ({shape}) covers the {face}. {WhyNotVisible(voxels, face)}");
                }
                else if (blockType != shape.BlockType)
                {
                    problems.Add($"Quad {i} ({shape}) covers the {face}. The block type of this voxel is {blockType}.");
                }

                if (!quadOfFace.TryAdd(face, i))
                {
                    problems.Add($"Quads {quadOfFace[face]} and {i} both cover the {face}.");
                }
            }
        }

        foreach (Face face in visibleFaces.Keys)
        {
            if (!quadOfFace.ContainsKey(face))
            {
                problems.Add($"No quad covers the visible {face}.");
            }
        }

        return problems;
    }

    /// <summary>
    /// Checks that the quads cover each visible face of the voxels exactly once, with the block type of its voxel.
    /// </summary>
    /// <param name="voxels">The block types of the chunk, with the indices [x, y, z]. The block type 0 is air.</param>
    /// <param name="quads">The quads that the mesher made for the chunk.</param>
    /// <param name="chunkOrigin">The expected position of the chunk corner in the coordinates of the quads. See <see cref="FindProblems"/>.</param>
    public static void AssertCoversEachVisibleFaceOnce(ushort[,,] voxels, IReadOnlyList<MeshQuad> quads, int3 chunkOrigin)
    {
        List<string> problems = FindProblems(voxels, quads, chunkOrigin);
        Assert.True(
            problems.Count == 0,
            $"The mesh has {problems.Count} problems. The first problems:{Environment.NewLine}{string.Join(Environment.NewLine, problems.Take(MaxProblemsInMessage))}");
    }

    // Gives the block type at a position, or 0 (air) for a position outside the chunk.
    private static ushort BlockTypeAt(ushort[,,] voxels, (int X, int Y, int Z) position) =>
        VoxelGrid.IsInside(voxels, position) ? voxels[position.X, position.Y, position.Z] : (ushort)0;

    private static string WhyNotVisible(ushort[,,] voxels, Face face)
    {
        if (!VoxelGrid.IsInside(voxels, (face.X, face.Y, face.Z)))
        {
            return "This voxel is outside the chunk.";
        }

        return voxels[face.X, face.Y, face.Z] == 0
            ? "This voxel is air."
            : "The neighbor voxel is solid, thus the face is not visible.";
    }
}
