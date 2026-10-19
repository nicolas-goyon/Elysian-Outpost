using System.Collections.Generic;

namespace Libs.VoxelMeshOptimizer
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
