using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Global_Voxels;

public class VoxelControlToMesh : MonoBehaviour {
    public Material[] terrainMaterials;

    // Update is called once per frame
    void Update() {
        if (VoxelControl.Instance.TryDequeue(out VoxelChunkData chunkData)) {
            Debug.Log("Dequeued : position: " + chunkData.GetChunkPosition());
            ChunkMesh chunkMesh = new ChunkMesh(this.transform, chunkData, terrainMaterials);
            chunkMesh.RenderChunk();
        }
    }

}

public enum PixelDirection {
    Top,
    Down,
    Left,
    Right,
    Front,
    Back
}
