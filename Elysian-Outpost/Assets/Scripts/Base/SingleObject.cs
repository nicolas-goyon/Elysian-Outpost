using System;
using System.Collections.Generic;
using System.IO;
using Libs.VoxelMeshOptimizer;
using Libs.VoxelMeshOptimizer.OcclusionAlgorithms;
using Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet;
using Libs.VoxelMeshOptimizer.Toolkit;
using UnityEngine;

namespace Base
{
    public class SingleObject : MonoBehaviour
    {
        [SerializeField] private GameObject _templateObject;

        
        private void Start()
        {
            // Initialize a default mesh here.
            ExampleChunk exampleChunk = new ExampleChunk(PerlinNoiseChunkGen.CreatePerlinLandscape(50, 123));
            Debug.Log("Starting optimization...");
            DisjointSetMeshOptimizer<ExampleMesh> optimizer = new (new ExampleMesh(new List<MeshQuad>()));
            ExampleMesh baseMesh = optimizer.Optimize(exampleChunk);
            string filePath = Path.Combine("C:\\Users\\Nico\\Documents\\github\\Elysian-Outpost\\Elysian-Outpost\\Assets\\Resources", "ChunkBase" + ".obj");
            File.WriteAllText(filePath, ObjExporter.MeshToObjString(baseMesh));
            Create(baseMesh, transform.position + Vector3.right * 2);
            
            
            // var occluder = new VoxelOcclusionOptimizer(exampleChunk);
            // var visibileFaces = occluder.ComputeVisibleFaces();
            // var occludedQuads = VisibleFacesMesher.Build(visibileFaces, exampleChunk);
            // var occludedMesh = new ExampleMesh(occludedQuads);
            // Create(occludedMesh, transform.position + Vector3.left * 2);
            // string filePath2 = Path.Combine("C:\\Users\\Nico\\Documents\\github\\Elysian-Outpost\\Elysian-Outpost\\Assets\\Resources", "ChunkOccluded" + ".obj");
            // File.WriteAllText(filePath2, ObjExporter.MeshToObjString(occludedMesh));
            Debug.Log("Optimization complete.");
        }

        private InstanciatedChunk Create(ExampleMesh mesh, Vector3 position)
        {
            GameObject obj = Instantiate(_templateObject);
            InstanciatedChunk singleObject = obj.GetComponent<InstanciatedChunk>();
            obj.transform.position = position;
            
            singleObject.SetMesh(mesh);
            
            return singleObject;
        }
    }
}
