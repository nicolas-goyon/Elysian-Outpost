using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Libs.VoxelMeshOptimizer;
using Libs.VoxelMeshOptimizer.OcclusionAlgorithms;
using Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet;
using Libs.VoxelMeshOptimizer.Toolkit;
using ScriptableObjectsDefinition;
using TerrainGeneration;
using Unity.Mathematics;
using UnityEngine;

namespace Base
{
    public class SingleObject : MonoBehaviour
    {
        [SerializeField] private GameObject _templateObject;
        [SerializeField] private TextureAtlas _textureAtlas;
        
        private MainGeneration gen = new MainGeneration(new int3(50,50,50),123);

        
        private void Start()
        {
            int3 chunkPosition = new int3(0,0,0);
            int3 position = new int3(0,0,0);
            _ = GenerateTerrainAt(chunkPosition, position);
            
            int3 chunkPosition2 = new int3(50,0,0);
            int3 position2 = new int3(50,0,0);
            _ = GenerateTerrainAt(chunkPosition2, position2);
            
            
            gen = new MainGeneration(new int3(150,50,150),123);
            
            int3 chunkPosition3 = new int3(0,0,0);
            int3 position3 = new int3(0,0,-300);
            _ = GenerateTerrainAt(chunkPosition3, position3);
        }

        private async Task<InstanciatedChunk> GenerateTerrainAt(int3 chunkPosition, int3 position)
        {
            Debug.Log($"Generating chunk at {position}");
        
            // Initialize a default mesh here.
            ExampleChunk exampleChunk = new( gen.GenerateChunkAt(chunkPosition), chunkPosition);
            
            DisjointSetMeshOptimizer<ExampleMesh> optimizer = new (new ExampleMesh(new List<MeshQuad>()));
            ExampleMesh baseMesh = optimizer.Optimize(exampleChunk);
            return await Create(baseMesh, position);
        }
    
    
        private async Task<InstanciatedChunk> Create(ExampleMesh mesh, int3 position)
        {
            AsyncInstantiateOperation<GameObject> asyncObj = InstantiateAsync(_templateObject);
            await asyncObj;
            GameObject[] obj = asyncObj.Result;
            Debug.Log($"Chunk at {position} instantiated.");
            InstanciatedChunk singleObject = obj[0].GetComponent<InstanciatedChunk>();
            obj[0].transform.position = new Vector3(position.x, position.y, position.z);
            
            singleObject.SetMesh(mesh, _textureAtlas);
            
            return singleObject;
        }
    }
}
