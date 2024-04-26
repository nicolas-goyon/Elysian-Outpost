using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test_Meshes : MonoBehaviour
{
    public Material[] terrainMaterials;

    //private int width = 10;
    //private int height = 10;
    private int pixelSize = 10;


    private int[,] pixels = new int[,] { // 11x10
        {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        {0, 1, 1, 1, 1, 2, 1, 1, 1, 1, 0 },
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
    };
    

    // Start is called before the first frame update
    void Start()
    {

        //int width = 10;
        //int height = 10;
        //Material terrainMaterial = terrainMaterials[0];
        //CustomMesh cm = new(terrainMaterial, 2, 2, pixelSize, transform, Vector3.zero, Vector3.one, "Test");
        //cm.GenerateMesh();


        //for (int i = 0; i < pixels.GetLength(0); i++) {
        //    for (int j = 0; j < pixels.GetLength(1); j++) {
        //        CustomMesh cm = new(terrainMaterials[pixels[i, j]], 1, 1, pixelSize, transform, new Vector3(i * pixelSize, 0, j * pixelSize), Vector3.one, "Test");
        //        cm.GenerateMesh();
        //    }
        //}


        pixels = new int[20, 20];
        for (int i = 0; i < pixels.GetLength(0); i++) {
            for (int j = 0; j < pixels.GetLength(1); j++) {
                pixels[i, j] = UnityEngine.Random.Range(0, 3);
            }
        }

    }

    private void Update() {
        // Only display a plane with the pixels
        if (Input.GetKeyDown(KeyCode.Space)) {

            //int[,] pixels = new int[,]{ // 11x10
            //        {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            //        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
            //        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
            //        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
            //        {0, 1, 1, 1, 1, 2, 1, 1, 1, 1, 0 },
            //        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
            //        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
            //        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
            //        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
            //        {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
            //    };

            //int[,] pixels = new int[,] {
            //    {1, 1, 1, 1},
            //    {0, 1, 1, 0},
            //    {0, 0, 2, 0},
            //    {1, 1, 1, 1},
            //};

            
            Debug.Log("Before optimisation");
            MeshOptimisation meshOptimisation = new MeshOptimisation(pixels);
            meshOptimisation.Optimize();
            Debug.Log("Flag ");
            //Debug.Log("Time taken: " + timeTaken + "ms");
            //Debug.Log("Start time: " + startTime + "s");
            //Debug.Log("End time: " + endTime + "s");

            List<MeshBuildData> res = meshOptimisation.ToMeshData();
            foreach (var (offsetX, offsetY, width, height, value) in res) {
                //CustomMesh cm = new(terrainMaterials[r.value], r.width, r.height, pixelSize, transform, new Vector3(r.offsetX * pixelSize + offsetX, 0, r.offsetY * pixelSize), Vector3.one, "Test");
                //CustomMesh cm = new(terrainMaterials[value], 1, 1, pixelSize, transform, new Vector3(offsetX * pixelSize, 0, offsetY * pixelSize), new Vector3(width, 1, height), "Test");
                //cm.GenerateGameObject();
            }

            Debug.Log("After optimisation");
        }
    }
}
