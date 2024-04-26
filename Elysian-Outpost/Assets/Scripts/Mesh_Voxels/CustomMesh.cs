using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Rendering;

[BurstCompile]
public struct CustomMesh
{

    private readonly Material terrainMaterial;
    private readonly int width;
    private readonly int height;
    private readonly int pixelSize;

    private readonly Transform parent;
    private readonly Vector3 localPosition;
    private readonly Vector3 localScale;
    private readonly string name;
    private readonly PixelDirection orientation;

    public CustomMesh(Material terrainMaterial, int width, int height, int pixelSize, Transform parent, Vector3 localPosition, Vector3 localScale, string name, PixelDirection orientation) { 
        if (width < 1 || height < 1) {
            throw new System.ArgumentException("Width and height must be greater than 0");
        }

        this.terrainMaterial = terrainMaterial;
        this.width = width + 1;
        this.height = height + 1;
        this.pixelSize = pixelSize;
        this.parent = parent;
        this.localPosition = localPosition;
        this.localScale = localScale;
        this.name = name;
        this.orientation = orientation;


    }


    [BurstCompile]
    public GameObject GenerateGameObject() {
        GameObject display = Object.Instantiate(new GameObject(name));

        // Set the rotation pivot to the center of the object
        //display.transform.localPosition = new Vector3(-width * pixelSize / 2, 0, -height * pixelSize / 2);


        display.transform.SetParent(parent);

        display.transform.localPosition = localPosition;

        display.transform.localScale = localScale;

        display.transform.localRotation = orientation switch {
            PixelDirection.Top => Quaternion.Euler(0, 0, 0),
            PixelDirection.Down => Quaternion.Euler(0, 0, 0),
            PixelDirection.Left => Quaternion.Euler(0, 0, 90),
            PixelDirection.Right => Quaternion.Euler(0, 0, 90),
            PixelDirection.Front => Quaternion.Euler(-90, 0, 0),
            PixelDirection.Back => Quaternion.Euler(-90, 0, 0),
            _ => throw new System.Exception("Invalid orientation")
        };

        display.AddComponent<MeshRenderer>().material = terrainMaterial;
        display.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

        Mesh mesh = display.AddComponent<MeshFilter>().mesh;
        Vector3[] vertices = new Vector3[width * height];
        for (int i = 0; i < width; i++) {
            for (int j = 0; j < height; j++) {
                vertices[i * height + j] = new Vector3(i * pixelSize, 0, j * pixelSize);
            }
        }

        int[] triangles = new int[(width - 1) * (height - 1) * 6];
        for (int i = 0; i < width - 1; i++) {
            for (int j = 0; j < height - 1; j++) {
                int index = (i * (height - 1) + j) * 6;
                triangles[index] = i * height + j;
                triangles[index + 1] = i * height + j + 1;
                triangles[index + 2] = (i + 1) * height + j;
                triangles[index + 3] = i * height + j + 1;
                triangles[index + 4] = (i + 1) * height + j + 1;
                triangles[index + 5] = (i + 1) * height + j;
            }
        }

        Vector2[] uvs = new Vector2[width * height];
        for (int i = 0; i < width; i++) {
            for (int j = 0; j < height; j++) {
                uvs[i * height + j] = new Vector2(i / (float)width, j / (float)height);
            }
        }


        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        //mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return display;

    }










}
