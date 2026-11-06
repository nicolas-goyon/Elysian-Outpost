// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace ElysianOutpost.Core.Meshing;

public record MeshQuad
{
    public Vector3 Vertex0; // bottom-left
    public Vector3 Vertex1; // bottom-right
    public Vector3 Vertex2; // top-right
    public Vector3 Vertex3; // top-left

    public Vector3 Normal;
    public uint VoxelID;

}
