using System.Collections.Generic;
using Libs.VoxelMeshOptimizer;
using UnityEngine;

namespace Base
{
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
}
