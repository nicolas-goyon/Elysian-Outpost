using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class ChunkCulling
{
    private VoxelChunkData chunkData;

    private Dictionary<PixelDirection, Faces> faces;

    public ChunkCulling(VoxelChunkData chunkData) {
        this.chunkData = chunkData;
        faces = new Dictionary<PixelDirection, Faces>();
    }


}

public class Faces {
    private FacePlane[] faces;
    private int maxDepth;
    private int currentDepth;

    public Faces(int maxDepth) {
        faces = new FacePlane[maxDepth];
        this.maxDepth = maxDepth;
    }

    public void AddFace(FacePlane face) {
        if (currentDepth >= maxDepth) { 
            throw new System.Exception("Max depth reached");
        }

        faces[currentDepth] = face;
        currentDepth++;
    }

    public FacePlane GetFace(int depth) {
        if (depth < 0 || depth >= maxDepth) {
            throw new System.Exception("Depth out of bounds");
        }

        return faces[depth];
    }


}

public class FacePlane {
    public NativeArray<Voxel> voxels;
    public int depth;
    public PixelDirection orientation;
}

