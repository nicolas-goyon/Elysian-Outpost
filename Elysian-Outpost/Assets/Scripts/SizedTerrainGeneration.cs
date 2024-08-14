using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using VoxelMaster;

public class SizedTerrainGeneration : MonoBehaviour
{
    /// <summary>
    /// Size of the terrain in chunks.
    /// </summary>
    [SerializeField] private int3 terrainSize = new int3(16, 16, 5);
    
    
    private VoxelTerrain terrain;
    
    private int chunkSize
    {
        get {
            return terrain.ChunkSize;
        }
    }
    
    
    private BaseGeneration generator;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        terrain = GetComponent<VoxelTerrain>();
        generator = GetComponent<BaseGeneration>(); 
        GenerateTerrain();

    }
    
    private void GenerateTerrain()
    {
        for (int x = 0; x < terrainSize.x; x++)
        {
            for (int y = 0; y < terrainSize.y; y++)
            {
                for (int z = 0; z < terrainSize.z; z++)
                {
                    // Task.Run(() => GenerateChunk(x, y, z));
                    GenerateChunk(x, y, z);
                }
            }
        }
    }
    
    
    private void GenerateChunk(int posX, int posY, int posZ)
    {
        Block[,,] blocks = new Block[chunkSize, chunkSize, chunkSize];
        bool isEmpty = true; // Important variable to determine if the whole chunk we are generating is empty, if it is, don't bother filling & refreshing the chunk.
        Chunk c = terrain.CreateChunk( new Vector3(posX * chunkSize, posY * chunkSize, posZ * chunkSize));

        // TODO - Implement a way to generate chunks in a more efficient way. Maybe using a thread pool.
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    short id = generator.Generation(x + c.x, y + c.y, z + c.z);

                    if (id == -1) continue;
                    
                    isEmpty = false;
                    blocks[x, y, z] = new Block(c, id);
                }
            }
        }


        if (!isEmpty)
            c.SetBlocks(blocks);
    }


#if UNITY_EDITOR
#region Editor

private void OnValidate()
{
    if (!terrain) terrain = GetComponent<VoxelTerrain>();
    
}

private void OnDrawGizmos()
{
    if (!terrain) return;
    
    // Draw a wireframe of the terrain
    Gizmos.color = Color.green;
    for (int x = 0; x < terrainSize.x; x++)
    {
        for (int y = 0; y < terrainSize.y; y++)
        {
            for (int z = 0; z < terrainSize.z; z++)
            {
                Gizmos.DrawWireCube(new Vector3(x * chunkSize, y * chunkSize, z * chunkSize), new Vector3(chunkSize, chunkSize, chunkSize));
            }
        }
    }
}

#endregion
#endif
}
