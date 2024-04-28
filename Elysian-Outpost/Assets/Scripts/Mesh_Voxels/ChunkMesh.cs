using System.Collections;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Global_Voxels;

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


        RenderUntilNoAir(PixelDirection.Top);
        RenderUntilNoAir(PixelDirection.Down);
        RenderUntilNoAir(PixelDirection.Left);
        RenderUntilNoAir(PixelDirection.Right);
        RenderUntilNoAir(PixelDirection.Front);
        RenderUntilNoAir(PixelDirection.Back);
    }

    private void RenderUntilNoAir(PixelDirection orientation) {
        bool hasAir = true;
        int depth = 0;
        while (hasAir) {
            hasAir = RenderOne(depth, orientation);
            depth++;
        }
    }

    private bool RenderOne(int depth, PixelDirection orientation) {
        Voxel[,] voxels = Slice(depth, orientation);
        int[,] screen = VoxelScreenToIntScreen(voxels);
        bool hasAir = OptimizeAndBuild(screen, depth, orientation);
        return hasAir;
    }

    private Voxel[,] Slice(int depth, PixelDirection orientation) {
        int3 dimensions = VoxelChunkBuilder.CHUNK_SIZE;
        int2 sliceDimensions = orientation switch {
            PixelDirection.Top or PixelDirection.Down => new int2(dimensions.x, dimensions.z),
            PixelDirection.Left or PixelDirection.Right => new int2(dimensions.y, dimensions.z),
            PixelDirection.Front or PixelDirection.Back => new int2(dimensions.x, dimensions.y),
            _ => throw new System.Exception("Invalid orientation") // Should never happen, all orientations are covered
        };


        Voxel[,] voxels = new Voxel[sliceDimensions.x, sliceDimensions.y];
        for (int i = 0; i < sliceDimensions.x; i++) {
            for (int k = 0; k < sliceDimensions.y; k++) {
                voxels[i, k] = orientation switch {
                    PixelDirection.Top => chunkData.GetVoxel(i, dimensions.y - depth - 1, k),
                    PixelDirection.Down => chunkData.GetVoxel(i, depth, k),
                    PixelDirection.Left => chunkData.GetVoxel(depth, i, k),
                    PixelDirection.Right => chunkData.GetVoxel(dimensions.x - depth - 1, i, k),
                    PixelDirection.Front => chunkData.GetVoxel(i, k, depth),
                    PixelDirection.Back => chunkData.GetVoxel(i, k, dimensions.z - depth - 1),
                    _ => throw new System.Exception("Invalid orientation"),// Should never happen, all orientations are covered
                };
            }
        }

        return voxels;
    }


    private int[,] VoxelScreenToIntScreen(Voxel[,] voxels) {

        int[,] screen = new int[voxels.GetLength(0), voxels.GetLength(1)];
        for (int i = 0; i < voxels.GetLength(0); i++) {
            for (int k = 0; k < voxels.GetLength(1); k++) {
                if (voxels[i, k].kind == VoxelKind.Air) screen[i, k] = -1;
                else screen[i, k] = (int)voxels[i, k].voxelId;
            }
        }

        return screen;
    }

    private bool OptimizeAndBuild(int[,] data, int depth, PixelDirection orientation) {
        MeshOptimisation meshOptimisation = new(data);
        meshOptimisation.Optimize();
        List<MeshBuildData> meshBuildDatas = meshOptimisation.ToMeshData();
        bool hasAir = false;
        foreach (MeshBuildData meshBuildData in meshBuildDatas) {
            hasAir = hasAir || BuildMeshData(meshBuildData, depth, orientation);
        }

        return hasAir;
    }



    private bool BuildMeshData(MeshBuildData meshBuildData, int depth, PixelDirection orientation) {

        switch (orientation) {
            case PixelDirection.Top:
                return BuildMeshDataTop(meshBuildData, depth);
            case PixelDirection.Down:
                return BuildMeshDataDown(meshBuildData, depth);
            case PixelDirection.Left:
                return BuildMeshDataLeft(meshBuildData, depth);
            case PixelDirection.Right:
                return BuildMeshDataRight(meshBuildData, depth);
            case PixelDirection.Front:
                return BuildMeshDataFront(meshBuildData, depth);
            case PixelDirection.Back:
                return BuildMeshDataBack(meshBuildData, depth);
            default:
                throw new System.Exception("Invalid orientation");
        }
    }

    private bool BuildMeshDataRight(MeshBuildData meshBuildData, int depth) {
        Vector3 voxelPosition = new(depth, meshBuildData.offsetX, meshBuildData.offsetY);

        Voxel voxel = chunkData.GetVoxel((int)voxelPosition.x, (int)voxelPosition.y, (int)voxelPosition.z);

        if (voxel.kind == VoxelKind.Air) return true;

        Material voxelMaterial = terrainMaterials[voxel.voxelId];
        Vector3 position = new(voxelPosition.x, voxelPosition.y, voxelPosition.z);
        Vector3 scale = new(meshBuildData.width, 1, meshBuildData.height);
        BuildOne(voxelMaterial, position, scale, "RIGHT", PixelDirection.Left);
        return false;
    }

    private bool BuildMeshDataLeft(MeshBuildData meshBuildData, int depth) {
        Vector3 voxelPosition = new(VoxelChunkBuilder.CHUNK_SIZE.x - depth - 1, meshBuildData.offsetX, meshBuildData.offsetY);

        Voxel voxel = chunkData.GetVoxel((int)voxelPosition.x, (int)voxelPosition.y, (int)voxelPosition.z);

        if (voxel.kind == VoxelKind.Air) return true;

        Material voxelMaterial = terrainMaterials[voxel.voxelId];
        Vector3 position = new(voxelPosition.x + 1, voxelPosition.y, voxelPosition.z);
        Vector3 scale = new(meshBuildData.width, -1, meshBuildData.height);
        BuildOne(voxelMaterial, position, scale, "LEFT", PixelDirection.Right);
        return false;
    }

    private bool BuildMeshDataFront(MeshBuildData meshBuildData, int depth) {
        Vector3 voxelPosition = new(meshBuildData.offsetX, meshBuildData.offsetY, depth);

        Voxel voxel = chunkData.GetVoxel((int)voxelPosition.x, (int)voxelPosition.y, (int)voxelPosition.z);

        if (voxel.kind == VoxelKind.Air) return true;

        Material voxelMaterial = terrainMaterials[voxel.voxelId];
        Vector3 position = new(voxelPosition.x, voxelPosition.y, voxelPosition.z);
        Vector3 scale = new(meshBuildData.width, 1, meshBuildData.height);
        BuildOne(voxelMaterial, position, scale, "FRONT", PixelDirection.Front);
        return false;
    }

    private bool BuildMeshDataBack(MeshBuildData meshBuildData, int depth) {
        Vector3 voxelPosition = new(meshBuildData.offsetX, meshBuildData.offsetY, VoxelChunkBuilder.CHUNK_SIZE.z - depth - 1);

        Voxel voxel = chunkData.GetVoxel((int)voxelPosition.x, (int)voxelPosition.y, (int)voxelPosition.z);

        if (voxel.kind == VoxelKind.Air) return true;

        Material voxelMaterial = terrainMaterials[voxel.voxelId];
        Vector3 position = new(voxelPosition.x, voxelPosition.y, voxelPosition.z + 1);
        Vector3 scale = new(meshBuildData.width, -1, meshBuildData.height);
        BuildOne(voxelMaterial, position, scale, "BACK", PixelDirection.Back);
        return false;
    }

    private bool BuildMeshDataTop(MeshBuildData meshBuildData, int depth) {
        Vector3 voxelPosition = new(meshBuildData.offsetX, VoxelChunkBuilder.CHUNK_SIZE.y - depth - 1, meshBuildData.offsetY);

        Voxel voxel = chunkData.GetVoxel((int)voxelPosition.x, (int)voxelPosition.y, (int)voxelPosition.z);

        if (voxel.kind == VoxelKind.Air) return true;

        Material voxelMaterial = terrainMaterials[voxel.voxelId];
        Vector3 position = new(voxelPosition.x, voxelPosition.y + 1, voxelPosition.z);
        Vector3 scale = new(meshBuildData.width, 1, meshBuildData.height);
        BuildOne(voxelMaterial, position, scale, "TOP", PixelDirection.Top);
        return false;
    }

    private bool BuildMeshDataDown(MeshBuildData meshBuildData, int depth) {
        Vector3 voxelPosition = new(meshBuildData.offsetX, depth, meshBuildData.offsetY);

        Voxel voxel = chunkData.GetVoxel((int)voxelPosition.x, (int)voxelPosition.y, (int)voxelPosition.z);

        if (voxel.kind == VoxelKind.Air) return true;
        Material voxelMaterial = terrainMaterials[voxel.voxelId];
        Vector3 position = new(voxelPosition.x, voxelPosition.y, voxelPosition.z);
        Vector3 scale = new(meshBuildData.width, -1, meshBuildData.height);
        BuildOne(voxelMaterial, position, scale, "DOWN", PixelDirection.Down);
        return false;
    }

    private void BuildOne(Material voxelMaterial, Vector3 pixelPosition, Vector3 scale, string name, PixelDirection facingDirection) { 
        CustomMesh cm = new(voxelMaterial, 1, 1, 1, chunkObject, pixelPosition, scale,name, facingDirection);
        cm.GenerateGameObject();
    }
}
