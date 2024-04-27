using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ChunkMesh : MonoBehaviour
{
    Transform parent;
    int3 position;
    //int3 dimensions { get { return VoxelChunkData.CHUNK_SIZE; }  }
    
}

public struct MeshRectangle {
    int width;
    int height;
    PixelDirection orientation; 


    public MeshRectangle(int width, int height, PixelDirection orientation) {
        this.width = width;
        this.height = height;
        this.orientation = orientation;
    }


    public MeshRectangleData GenerateMesh() {
        int[] triangles = new int[6];
        Vector3[] vertices = new Vector3[4];
        Vector2[] uv = new Vector2[4];

        switch (orientation) {
            case PixelDirection.Top:
                vertices[0] = new Vector3(-width / 2, 0, -height / 2);
                vertices[1] = new Vector3(width / 2, 0, -height / 2);
                vertices[2] = new Vector3(width / 2, 0, height / 2);
                vertices[3] = new Vector3(-width / 2, 0, height / 2);

                triangles[0] = 0;
                triangles[1] = 1;
                triangles[2] = 2;
                triangles[3] = 0;
                triangles[4] = 2;
                triangles[5] = 3;

                uv[0] = new Vector2(0, 0);
                uv[1] = new Vector2(1, 0);
                uv[2] = new Vector2(1, 1);
                uv[3] = new Vector2(0, 1);
                break;
            case PixelDirection.Down:
                vertices[0] = new Vector3(-width / 2, 0, -height / 2);
                vertices[1] = new Vector3(width / 2, 0, -height / 2);
                vertices[2] = new Vector3(width / 2, 0, height / 2);
                vertices[3] = new Vector3(-width / 2, 0, height / 2);

                triangles[0] = 0;
                triangles[1] = 2;
                triangles[2] = 1;
                triangles[3] = 0;
                triangles[4] = 3;
                triangles[5] = 2;

                uv[0] = new Vector2(0, 0);
                uv[1] = new Vector2(1, 0);
                uv[2] = new Vector2(1, 1);
                uv[3] = new Vector2(0, 1);
                break;
            case PixelDirection.Left:
                vertices[0] = new Vector3(0, -width / 2, -height / 2);
                vertices[1] = new Vector3(0, width / 2, -height / 2);
                vertices[2] = new Vector3(0, width / 2, height / 2);
                vertices[3] = new Vector3(0, -width / 2, height / 2);
                triangles[0] = 0;
                triangles[1] = 1;
                triangles[2] = 2;
                triangles[3] = 0;
                triangles[4] = 2;
                triangles[5] = 3;

                uv[0] = new Vector2(0, 0);
                uv[1] = new Vector2(1, 0);
                uv[2] = new Vector2(1, 1);
                uv[3] = new Vector2(0, 1);
                break;
            case PixelDirection.Right:
                vertices[0] = new Vector3(0, -width / 2, -height / 2);
                vertices[1] = new Vector3(0, width / 2, -height / 2);
                vertices[2] = new Vector3(0, width / 2, height / 2);
                vertices[3] = new Vector3(0, -width / 2, height / 2);
                triangles[0] = 0;
                triangles[1] = 2;
                triangles[2] = 1;
                triangles[3] = 0;
                triangles[4] = 3;
                triangles[5] = 2;

                uv[0] = new Vector2(0, 0);
                uv[1] = new Vector2(1, 0);
                uv[2] = new Vector2(1, 1);
                uv[3] = new Vector2(0, 1);
                break;
            case PixelDirection.Front:
                vertices[0] = new Vector3(-width / 2, -height / 2, 0);
                vertices[1] = new Vector3(width / 2, -height / 2, 0);
                vertices[2] = new Vector3(width / 2, height / 2, 0);
                vertices[3] = new Vector3(-width / 2, height / 2, 0);
                triangles[0] = 0;
                triangles[1] = 1;
                triangles[2] = 2;
                triangles[3] = 0;
                triangles[4] = 2;
                triangles[5] = 3;

                uv[0] = new Vector2(0, 0);
                uv[1] = new Vector2(1, 0);
                uv[2] = new Vector2(1, 1);
                uv[3] = new Vector2(0, 1);
                break;
            case PixelDirection.Back:
                vertices[0] = new Vector3(-width / 2, -height / 2, 0);
                vertices[1] = new Vector3(width / 2, -height / 2, 0);
                vertices[2] = new Vector3(width / 2, height / 2, 0);
                vertices[3] = new Vector3(-width / 2, height / 2, 0);
                triangles[0] = 0;
                triangles[1] = 2;
                triangles[2] = 1;
                triangles[3] = 0;
                triangles[4] = 3;
                triangles[5] = 2;

                uv[0] = new Vector2(0, 0);
                uv[1] = new Vector2(1, 0);
                uv[2] = new Vector2(1, 1);
                uv[3] = new Vector2(0, 1);
                break;
            default:
                throw new System.Exception("Invalid orientation");
        }

        return new MeshRectangleData(triangles, vertices, uv);
    }

}

public struct MeshRectangleData {
    public int[] triangles;
    public Vector3[] vertices;
    public Vector2[] uv;

    public MeshRectangleData(int[] triangles, Vector3[] vertices, Vector2[] uv) {
        this.triangles = triangles;
        this.vertices = vertices;
        this.uv = uv;
    }

    public void Deconstruct(out int[] triangles, out Vector3[] vertices, out Vector2[] uv) {
        triangles = this.triangles;
        vertices = this.vertices;
        uv = this.uv;
    }
}