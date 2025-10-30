using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ScriptableObjectsDefinition;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Libs.VoxelMeshOptimizer.Toolkit{


/// <summary>
/// Utility class to export a <see cref="Mesh"/> to the Wavefront OBJ format.
/// </summary>
public static class ObjExporter
{


    private static void AddVertex(Vector3 v, Dictionary<Vector3, int> vertexIndices, List<Vector3> vertices)
    {
        if (vertexIndices.ContainsKey(v)) return;
        vertices.Add(v);
        vertexIndices[v] = vertices.Count; // 1-based
    }

    public static UnityEngine.Mesh ToUnityMesh(
        Mesh mesh,
        TextureAtlas atlas)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));
        if (atlas is null) throw new ArgumentNullException(nameof(atlas));

        UnityEngine.Mesh unityMesh = new();
        List<UnityEngine.Vector3> vertices = new();
        List<int> triangles = new();
        List<UnityEngine.Vector3> normals = new();
        List<Vector2> uvs = new();
        
        foreach (MeshQuad quad in mesh.Quads)
        {
            uint voxelId = quad.VoxelID;
            (float u0, float v0, float u1, float v1) = atlas.GetTextureUv(voxelId);
            
            // Convert to Unity coordinates (right-handed to left-handed)
            var vert0 = new UnityEngine.Vector3(quad.Vertex0.X, quad.Vertex0.Y, quad.Vertex0.Z);
            var vert1 = new UnityEngine.Vector3(quad.Vertex1.X, quad.Vertex1.Y, quad.Vertex1.Z);
            var vert2 = new UnityEngine.Vector3(quad.Vertex2.X, quad.Vertex2.Y, quad.Vertex2.Z);
            var vert3 = new UnityEngine.Vector3(quad.Vertex3.X, quad.Vertex3.Y, quad.Vertex3.Z);
            
            var normal = new UnityEngine.Vector3(quad.Normal.X, quad.Normal.Y, quad.Normal.Z);

            int baseIndex = vertices.Count;
            
            // Add vertices
            vertices.Add(vert0);
            vertices.Add(vert1);
            vertices.Add(vert2);
            vertices.Add(vert3);
            
            // Add UVs (Unity's UV coordinate system has origin at bottom-left)
            uvs.Add(new Vector2(u0, v0));
            uvs.Add(new Vector2(u1, v0));
            uvs.Add(new Vector2(u1, v1));
            uvs.Add(new Vector2(u0, v1));
            
            // Add normals
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            
            // Add triangles (clockwise winding order for Unity)
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex);
            
        }

        unityMesh.vertices = vertices.ToArray();
        unityMesh.normals = normals.ToArray();
        unityMesh.uv = uvs.ToArray();
        unityMesh.triangles = triangles.ToArray();
        
        unityMesh.RecalculateBounds();
        unityMesh.RecalculateNormals(); // Ensure normals are correct
        unityMesh.Optimize(); // Optimize the mesh for better performance
        
        return unityMesh;
    }



    
    
    /// <summary>
    /// Exports the provided <paramref name="mesh"/> into OBJ and MTL strings
    /// using the provided <paramref name="atlas"/> to generate texture
    /// coordinates. Each <see cref="MeshQuad.VoxelID"/> is looked up in
    /// <paramref name="voxelTextureMap"/> to determine which texture cell to
    /// use in the atlas.
    /// </summary>
    /// <param name="mesh">Mesh to export.</param>
    /// <param name="atlas">Texture atlas describing UV layout.</param>
    /// <param name="voxelTextureMap">Mapping from voxel identifier to atlas index.</param>
    /// <param name="textureFileName">Name of the texture file referenced by the MTL.</param>
    /// <param name="materialName">Optional material name. The corresponding MTL file will be named &lt;materialName&gt;.mtl.</param>
    /// <returns>A tuple containing the OBJ and MTL contents.</returns>
    public static (string obj, string mtl) MeshToObjString(
        Mesh mesh,
        TextureAtlas atlas
        )
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));
        if (atlas is null) throw new ArgumentNullException(nameof(atlas));

        List<Vector3> vertices = new();
        Dictionary<Vector3, int> vertexIndices = new();
        List<Vector2> uvs = new();
        Dictionary<Vector2, int> uvIndices = new();
        List<Vector3> normals = new();
        Dictionary<Vector3, int> normalIndices = new();
        List<(int v0, int v1, int v2, int v3, int t0, int t1, int t2, int t3, int n)> faces = new();

        foreach (MeshQuad quad in mesh.Quads)
        {
            AddVertex(quad.Vertex0, vertexIndices, vertices);
            AddVertex(quad.Vertex1, vertexIndices, vertices);
            AddVertex(quad.Vertex2, vertexIndices, vertices);
            AddVertex(quad.Vertex3, vertexIndices, vertices);


            (float u0, float v0, float u1, float v1) = atlas.GetTextureUv(quad.VoxelID);

            Vector2 uv0 = new(u0, v0);
            Vector2 uv1 = new(u1, v0);
            Vector2 uv2 = new(u1, v1);
            Vector2 uv3 = new(u0, v1);

            AddUv(uv0, uvIndices, uvs);
            AddUv(uv1, uvIndices, uvs);
            AddUv(uv2, uvIndices, uvs);
            AddUv(uv3, uvIndices, uvs);

            AddNormal(quad.Normal, normalIndices, normals);

            int i0 = vertexIndices[quad.Vertex0];
            int i1 = vertexIndices[quad.Vertex1];
            int i2 = vertexIndices[quad.Vertex2];
            int i3 = vertexIndices[quad.Vertex3];
            int t0 = uvIndices[uv0];
            int t1 = uvIndices[uv1];
            int t2 = uvIndices[uv2];
            int t3 = uvIndices[uv3];
            int n = normalIndices[quad.Normal];

            faces.Add((i0, i1, i2, i3, t0, t1, t2, t3, n));
        }

        Console.WriteLine(uvs.Count());

        StringBuilder sbObj = new();
        string mtlFileName = atlas.MaterialName + ".mtl";
        sbObj.AppendLine($"mtllib {mtlFileName}");

        foreach (Vector3 v in vertices)
        {
            sbObj.AppendLine(string.Format("v {0} {1} {2}", v.X, v.Y, v.Z));
        }

        foreach (Vector2 uv in uvs)
        {
            Console.WriteLine(uv);
            sbObj.AppendLine(string.Format("vt {0} {1}", uv.x, uv.y));
        }

        foreach (Vector3 n in normals)
        {
            sbObj.AppendLine(string.Format(CultureInfo.InvariantCulture, "vn {0} {1} {2}", n.X, n.Y, n.Z));
        }

        sbObj.AppendLine($"usemtl {atlas.MaterialName}");

        foreach ((int v0, int v1, int v2, int v3, int t0, int t1, int t2, int t3, int n) f in faces)
        {
            sbObj.AppendLine($"f {f.v0}/{f.t0}/{f.n} {f.v1}/{f.t1}/{f.n} {f.v2}/{f.t2}/{f.n} {f.v3}/{f.t3}/{f.n}");
        }

        StringBuilder sbMtl = new();
        sbMtl.AppendLine($"newmtl {atlas.MaterialName}");
        sbMtl.AppendLine("Kd 1 1 1");
        sbMtl.AppendLine($"map_Kd {atlas.TextureFilePath}");

        return (sbObj.ToString(), sbMtl.ToString());
    }

    private static void AddUv(Vector2 uv, Dictionary<Vector2, int> uvIndices, List<Vector2> uvs)
    {
        if (uvIndices.ContainsKey(uv)) return;
        uvs.Add(uv);
        uvIndices[uv] = uvs.Count; // 1-based
    }

    private static void AddNormal(Vector3 n, Dictionary<Vector3, int> normalIndices, List<Vector3> normals)
    {
        if (normalIndices.ContainsKey(n)) return;
        normals.Add(n);
        normalIndices[n] = normals.Count; // 1-based
    }
    
    
    
    
    
    
    
    
}
}