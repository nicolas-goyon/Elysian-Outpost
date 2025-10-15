using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Base;
using Libs.VoxelMeshOptimizer;
using Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet;
using ScriptableObjectsDefinition;
using Unity.Mathematics;
using UnityEngine;

public class GenerateTerrainAroundCamera : MonoBehaviour
{
    [SerializeField] private GameObject _templateObject;
    [SerializeField] private TextureAtlas _textureAtlas;

    [SerializeField] private int _chunkSize = 50;
    [SerializeField] private int _viewDistanceInChunks = 3;
    
    private Dictionary<int3, InstanciatedChunk> _loadedChunks = new();
    
    private Vector3 lastPosition;

    private MainGeneration gen;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gen = new MainGeneration(_chunkSize,123);
        
    }

    // Update is called once per frame
    async void Update()
    {
        
        List<int3> requiredChunks = GetChunksInViewDistance(PositionToInt());
        foreach (int3 chunkPos in requiredChunks)
        {
            if (_loadedChunks.ContainsKey(chunkPos)) continue;
            LoadChunk(chunkPos);
            break;
        }
        List<(int3, InstanciatedChunk)> chunksToUnload = GetChunksToUnload();
        foreach ((int3 pos, InstanciatedChunk obj) in chunksToUnload)
        {
            _loadedChunks.Remove(pos);
            Destroy(obj);
        }
    }

    #region Get Chunks to Load/Unload

    /// <summary>
    /// Checks which chunks are out of view distance and should be unloaded
    /// </summary>
    /// <returns>List of tuples containing chunk position and instance to be unloaded</returns>
    private List<(int3, InstanciatedChunk)> GetChunksToUnload()
    {
        HashSet<int3> currentViewChunks = new HashSet<int3>(GetChunksInViewDistance(PositionToInt()));

        List<(int3, InstanciatedChunk)> chunksToUnload = new List<(int3, InstanciatedChunk)>();
        foreach ((int3 chunkPos, InstanciatedChunk chunkTask) in _loadedChunks.ToList())
        {
            if (!currentViewChunks.Contains(chunkPos))
            {
                chunksToUnload.Add((chunkPos, chunkTask));
            }
        }
        return chunksToUnload;
    }

    /// <summary>
    /// Returns all chunk positions within the view distance from a center position
    /// </summary>
    /// <param name="centerPosition">The center position to calculate chunks around</param>
    /// <returns>List of chunk positions (in chunk coordinates)</returns>
    private List<int3> GetChunksInViewDistance(int3 centerPosition)
    {
        List<int3> chunks = new List<int3>();
        int centerChunkX = Mathf.FloorToInt(centerPosition.x / (float)_chunkSize);
        int centerChunkZ = Mathf.FloorToInt(centerPosition.z / (float)_chunkSize);

        for (int x = -_viewDistanceInChunks; x <= _viewDistanceInChunks; x++)
        {
            for (int z = -_viewDistanceInChunks; z <= _viewDistanceInChunks; z++)
            {
                // Calculate the chunk position
                int chunkX = centerChunkX + x;
                int chunkZ = centerChunkZ + z;

                // Only include chunks within the circular view distance
                float distance = Mathf.Sqrt(x * x + z * z);
                if (distance <= _viewDistanceInChunks)
                {
                    chunks.Add(new int3(
                        chunkX * _chunkSize,
                        0, // Y is always 0 for chunk positions
                        chunkZ * _chunkSize
                    ));
                }
            }
        }

        return chunks;
    }
    #endregion
    
    
    private void LoadChunk(int3 chunkPos)
    {
        var mesh = GenerateTerrainAt(chunkPos);
        // Create the chunk on the main thread
        InstanciatedChunk chunkInstance = Create(mesh, chunkPos);
        _loadedChunks[chunkPos] = chunkInstance;
    }
    
    private int3 PositionToInt()
    {
        return new int3(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y), Mathf.FloorToInt(transform.position.z));
    }

    
    
    private ExampleMesh GenerateTerrainAt(int3 position)
    {

        ExampleChunk exampleChunk = new(gen.GenerateChunkAt(position.x, position.z));

        DisjointSetMeshOptimizer<ExampleMesh> optimizer = new(new ExampleMesh(new List<MeshQuad>()));
        return optimizer.Optimize(exampleChunk);
    }
    
    
    private InstanciatedChunk Create(ExampleMesh mesh, int3 position)
    {
        GameObject obj = Instantiate(_templateObject);
        InstanciatedChunk singleObject = obj.GetComponent<InstanciatedChunk>();
        obj.transform.position = new Vector3(position.x, position.y, position.z);
            
        singleObject.SetMesh(mesh, _textureAtlas);
            
        return singleObject;
    }
}
