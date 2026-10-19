// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Meshing;

public class Mesh
{
    public List<MeshQuad> Quads { get; set; }

    public Mesh(List<MeshQuad> quads)
    {
        Quads = quads;
    }



    public Mesh()
    {
        Quads = new List<MeshQuad>();
    }


}
