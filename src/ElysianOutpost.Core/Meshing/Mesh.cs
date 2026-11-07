// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// A list of quads: the mesh of a chunk before the conversion to GPU data. <see cref="Chunk.ToMesh"/> gives this type.
/// </summary>
public class Mesh
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mesh"/> class with quads.
    /// </summary>
    /// <param name="quads">The quads. The mesh keeps this list, not a copy.</param>
    public Mesh(List<MeshQuad> quads)
    {
        Quads = quads;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Mesh"/> class with no quad.
    /// </summary>
    public Mesh()
    {
        Quads = new List<MeshQuad>();
    }

    /// <summary>
    /// Gets or sets the quads of the mesh.
    /// </summary>
    public List<MeshQuad> Quads { get; set; }
}
