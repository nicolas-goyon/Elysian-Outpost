using System.Collections.Generic;
using Libs.VoxelMeshOptimizer;
using UnityEngine;
using Mesh = Libs.VoxelMeshOptimizer.Mesh;

namespace Base
{
    public class ExampleMesh : Mesh
    {
        public List<MeshQuad> Quads { get; set; } 

        public ExampleMesh(List<MeshQuad> quads)
        {
            Quads = quads;
        }
        

        public ExampleMesh()
        {
            Quads = new List<MeshQuad>();
        }

      
    }
}
