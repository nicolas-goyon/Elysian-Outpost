using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class VoxelControlToMesh : MonoBehaviour {
    public Material[] terrainMaterials;

    // Update is called once per frame
    void Update() {
        if (VoxelControl.Instance.TryDequeue(out VoxelChunkData chunkData)) {
            Debug.Log("Dequeued : position: " + chunkData.GetChunkPosition());
            ChunkMesh chunkMesh = new(transform, chunkData, terrainMaterials);
            chunkMesh.RenderChunk();
        }
    }

}

