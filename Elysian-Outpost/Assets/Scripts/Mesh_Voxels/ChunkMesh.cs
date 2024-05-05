using System.Collections;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class ChunkMesh
{
    Transform chunkObject;
    Transform parent;
    VoxelChunkData chunkData;
    Material[] terrainMaterials;
    
    public ChunkMesh(Transform parent, VoxelChunkData chunk, Material[] terrainMaterials) {
        this.parent = parent;
        this.chunkData = chunk;
        this.terrainMaterials = terrainMaterials;

    }

    public void RenderChunk() {
        chunkObject = new GameObject("Chunk").transform;
        chunkObject.SetParent(parent);
        chunkObject.transform.localPosition = new Vector3(chunkData.GetChunkPosition().x, chunkData.GetChunkPosition().y, chunkData.GetChunkPosition().z);


    }

    //public bool RenderOne(int depth, PixelDirection orientation) {
    //    Voxel[,] voxels = Slice(depth, orientation);
    //    int[,] screen = VoxelScreenToIntScreen(voxels);
    //    bool hasAir = OptimizeAndBuild(screen, depth, orientation);
    //    return hasAir;
    //}

    //public Voxel[,] Slice(int depth, PixelDirection orientation) {
    //    int3 dimensions = VoxelChunkData.CHUNK_SIZE;
    //    int2 sliceDimensions = orientation switch {
    //        PixelDirection.Top or PixelDirection.Down => new int2(dimensions.x, dimensions.z),
    //        PixelDirection.Left or PixelDirection.Right => new int2(dimensions.y, dimensions.z),
    //        PixelDirection.Front or PixelDirection.Back => new int2(dimensions.x, dimensions.y),
    //        _ => throw new System.Exception("Invalid orientation") // Should never happen, all orientations are covered
    //    };


    //    Voxel[,] voxels = new Voxel[sliceDimensions.x, sliceDimensions.y];
    //    for (int i = 0; i < sliceDimensions.x; i++) {
    //        for (int k = 0; k < sliceDimensions.y; k++) {
    //            voxels[i, k] = chunkData.GetVoxel(i, k, depth, orientation);
    //        }
    //    }

    //    return voxels;
    //}


    //public int[,] VoxelScreenToIntScreen(Voxel[,] voxels) {

    //    int[,] screen = new int[voxels.GetLength(0), voxels.GetLength(1)];
    //    for (int i = 0; i < voxels.GetLength(0); i++) {
    //        for (int k = 0; k < voxels.GetLength(1); k++) {
    //            if (voxels[i, k].kind == VoxelKind.Air) screen[i, k] = -1;
    //            else screen[i, k] = (int)voxels[i, k].voxelId;
    //        }
    //    }

    //    return screen;
    //}

    //public bool OptimizeAndBuild(int[,] data, int depth, PixelDirection orientation) {
    //    MeshOptimisation meshOptimisation = new(data);
    //    meshOptimisation.Optimize();
    //    List<MeshBuildData> meshBuildDatas = meshOptimisation.ToMeshData();
    //    bool hasAir = false;
    //    foreach (MeshBuildData meshBuildData in meshBuildDatas) {
    //        hasAir = hasAir || BuildMeshData(meshBuildData, depth, orientation);
    //    }

    //    return hasAir;
    //}



    //public bool BuildMeshData(MeshBuildData meshBuildData, int depth, PixelDirection orientation) {
    //    Debug.Log("Building mesh data");
    //    Debug.Log("Orientation: " + orientation);
    //    Debug.Log("Depth: " + depth);
    //    Debug.Log("OffsetX: " + meshBuildData.offsetX);
    //    Debug.Log("OffsetY: " + meshBuildData.offsetY);
    //    Debug.Log("Width: " + meshBuildData.width);
    //    Debug.Log("Height: " + meshBuildData.height);
    //    return true;

    //    //switch (orientation) {
    //    //    case PixelDirection.Top:
    //    //        return BuildMeshDataTop(meshBuildData, depth);
    //    //    case PixelDirection.Down:
    //    //        return BuildMeshDataDown(meshBuildData, depth);
    //    //    case PixelDirection.Left:
    //    //        return BuildMeshDataLeft(meshBuildData, depth);
    //    //    case PixelDirection.Right:
    //    //        return BuildMeshDataRight(meshBuildData, depth);
    //    //    case PixelDirection.Front:
    //    //        return BuildMeshDataFront(meshBuildData, depth);
    //    //    case PixelDirection.Back:
    //    //        return BuildMeshDataBack(meshBuildData, depth);
    //    //    default:
    //    //        throw new System.Exception("Invalid orientation");
    //    //}
    //}

    //public void BuildOne(Material voxelMaterial, Vector3 pixelPosition, Vector3 scale, string name, PixelDirection facingDirection) { 
    //    CustomMesh cm = new(voxelMaterial, 1, 1, 1, chunkObject, pixelPosition, scale,name, facingDirection);
    //    cm.GenerateGameObject();
    //}
}
