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

        public UnityEngine.Mesh ToUnityObject()
        {
            UnityEngine.Mesh mesh = new UnityEngine.Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            foreach (var quad in Quads)
            {
                int baseIndex = vertices.Count;
                
                // Add vertices for the quad
                vertices.Add(new Vector3(quad.Vertex0.X, quad.Vertex0.Y, quad.Vertex0.Z));
                vertices.Add(new Vector3(quad.Vertex1.X, quad.Vertex1.Y, quad.Vertex1.Z));
                vertices.Add(new Vector3(quad.Vertex2.X, quad.Vertex2.Y, quad.Vertex2.Z));
                vertices.Add(new Vector3(quad.Vertex3.X, quad.Vertex3.Y, quad.Vertex3.Z));

                // Add triangles (quad is made of two triangles)
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
