using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Libs.VoxelMeshOptimizer.Toolkit{


/// <summary>
/// Utility class to export a <see cref="Mesh"/> to the Wavefront OBJ format.
/// </summary>
public static class ObjExporter
{
    /// <summary>
    /// Exports the provided <paramref name="mesh"/> into an OBJ file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="mesh">Mesh to export.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> or <paramref name="filePath"/> is null.</exception>
    public static string MeshToObjString(Mesh mesh)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));

        List<Vector3> vertices = new List<Vector3>();
        Dictionary<Vector3, int> vertexIndices = new Dictionary<Vector3, int>();

        StringBuilder sb = new StringBuilder();

        // Collect unique vertices and assign indices (1-based as per OBJ spec)
        foreach (MeshQuad quad in mesh.Quads)
        {
            AddVertex(quad.Vertex0, vertexIndices, vertices);
            AddVertex(quad.Vertex1, vertexIndices, vertices);
            AddVertex(quad.Vertex2, vertexIndices, vertices);
            AddVertex(quad.Vertex3, vertexIndices, vertices);
        }

        // Write vertex positions
        foreach (Vector3 v in vertices)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}", v.X, v.Y, v.Z));
        }

        // Write faces using quad indices
        foreach (MeshQuad quad in mesh.Quads)
        {
            int i0 = vertexIndices[quad.Vertex0];
            int i1 = vertexIndices[quad.Vertex1];
            int i2 = vertexIndices[quad.Vertex2];
            int i3 = vertexIndices[quad.Vertex3];
            sb.AppendLine($"f {i0} {i1} {i2} {i3}");
        }

        return sb.ToString();

    }


    private static void AddVertex(Vector3 v, Dictionary<Vector3, int> vertexIndices, List<Vector3> vertices)
    {
        if (!vertexIndices.ContainsKey(v))
        {
            vertices.Add(v);
            vertexIndices[v] = vertices.Count; // 1-based
        }
    }

    public static UnityEngine.Mesh ToUnityMesh(Mesh mesh)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));
    
        UnityEngine.Mesh unityMesh = new UnityEngine.Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        Dictionary<Vector3, int> vertexIndices = new Dictionary<Vector3, int>();
        Dictionary<Vector3, Vector3> vertexNormals = new Dictionary<Vector3, Vector3>();

        foreach (MeshQuad quad in mesh.Quads)
        {
            AddVertexWithNormal(quad.Vertex0, quad.Normal);
            AddVertexWithNormal(quad.Vertex1, quad.Normal);
            AddVertexWithNormal(quad.Vertex2, quad.Normal);
            AddVertexWithNormal(quad.Vertex3, quad.Normal);
    
            int i0 = vertexIndices[quad.Vertex0] - 1;
            int i1 = vertexIndices[quad.Vertex1] - 1;
            int i2 = vertexIndices[quad.Vertex2] - 1;
            int i3 = vertexIndices[quad.Vertex3] - 1;
    
            triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
            triangles.Add(i2); triangles.Add(i3); triangles.Add(i0);
        }
    
        // Convert to Unity types
        UnityEngine.Vector3[] unityVertices = vertices.Select(v => new UnityEngine.Vector3(v.X, v.Y, v.Z)).ToArray();
        UnityEngine.Vector3[] unityNormals = vertices.Select(v => {
            Vector3 n = vertexNormals[v];
            return new UnityEngine.Vector3(n.X, n.Y, n.Z);
        }).ToArray();
    
        unityMesh.vertices = unityVertices;
        unityMesh.normals = unityNormals;
        unityMesh.triangles = triangles.ToArray();
        unityMesh.RecalculateBounds();
    
        return unityMesh;

        // Collect unique vertices and normals
        void AddVertexWithNormal(Vector3 v, Vector3 n)
        {
            if (vertexIndices.ContainsKey(v)) return;
            vertices.Add(v);
            vertexIndices[v] = vertices.Count; // 1-based
            vertexNormals[v] = n;
        }
    }
}
}