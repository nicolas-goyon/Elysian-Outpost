using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


public struct Voxel : IComponentData {
    public VoxelKind kind;
    public int voxelId;

}


public enum VoxelKind {
    Air,
    Liquid,
    Solid,
    Entity
}

public struct VoxelType {
    public float4 floatColor;

}

public class VoxelChunkBuilder {

    private NativeArray<Voxel> voxels;
    private int index;
    private int3 position;

    public VoxelChunkBuilder(int3 chunkPosition, Allocator allocator) { 
        voxels = new(VoxelChunkData.CHUNK_SIZE.x * VoxelChunkData.CHUNK_SIZE.y * VoxelChunkData.CHUNK_SIZE.z, allocator);
        index = 0;
        position = chunkPosition;
    }

    public void AddVoxelDataPosition(Voxel newVoxel) {
        voxels[index] = newVoxel;
        index++;
    }

    public Voxel GetVoxelDataPosition(int index) {
        return voxels[index];
    }

    public void Dispose() {
        voxels.Dispose();
    }

    public int GetIndex() {
        return index;
    }


    public VoxelChunkData Build() {
        return new VoxelChunkData(voxels, position);
    }


}



public struct VoxelChunkData {
    public readonly static int3 CHUNK_SIZE = new(16, 16, 32);

    public static int CHUNK_VOLUME { get { return CHUNK_SIZE.x * CHUNK_SIZE.y * CHUNK_SIZE.z; } }

    private NativeArray<Voxel> voxels;

    private readonly int3 chunkPosition;


    public VoxelChunkData(NativeArray<Voxel> voxels, int3 chunkPosition) { 
        this.voxels = voxels;
        this.chunkPosition = chunkPosition;
    }

    public Voxel GetVoxel(int index) {
        if (index < 0 || index >= CHUNK_SIZE.x * CHUNK_SIZE.y * CHUNK_SIZE.z) {
            throw new System.Exception("Voxel out of bounds");
        }
        return voxels[index];
    }

    public Voxel GetVoxel(int x, int y, int z) {
        if (x < 0 || x >= CHUNK_SIZE.x || y < 0 || y >= CHUNK_SIZE.y || z < 0 || z >= CHUNK_SIZE.z) {
            throw new System.Exception("Voxel out of bounds");
        }

        return voxels[x + y * CHUNK_SIZE.x + z * CHUNK_SIZE.x * CHUNK_SIZE.y];
    }

    public void Dispose() {
        voxels.Dispose();
    }

    public readonly int3 GetChunkPosition() {
        return chunkPosition;
    }

    public float3 GetVoxelPosition(int index) {
        if (index < 0 || index >= CHUNK_SIZE.x * CHUNK_SIZE.y * CHUNK_SIZE.z) {
            throw new System.Exception("Voxel out of bounds");
        }
        float3 xyz = GetVoxelPositionInChunk(index);
        (float x, float y, float z) = (xyz.x, xyz.y, xyz.z);

        int xOffset = chunkPosition.x * CHUNK_SIZE.x;
        int yOffset = chunkPosition.y * CHUNK_SIZE.y;
        int zOffset = chunkPosition.z * CHUNK_SIZE.z;

        return new(x + xOffset, y + yOffset, z + zOffset);
    }
    public static float3 GetVoxelPositionInChunk(int index) {
        if (index < 0 || index >= CHUNK_SIZE.x * CHUNK_SIZE.y * CHUNK_SIZE.z) {
            throw new System.Exception("Voxel out of bounds");
        }
        int x = index % CHUNK_SIZE.x;
        int y = (index / CHUNK_SIZE.x) % CHUNK_SIZE.z;
        int z = index / (CHUNK_SIZE.x * CHUNK_SIZE.z);

        return new float3(x, y, z);
    }


    public Voxel GetVoxel(int posX, int posY, int depth, PixelDirection direction) {
        return direction switch {
            PixelDirection.Top => GetVoxelTOP(posX, posY, depth),
            PixelDirection.Down => GetVoxelDOWN(posX, posY, depth),
            PixelDirection.Left => GetVoxelLEFT(posX, posY, depth),
            PixelDirection.Right => GetVoxelRIGHT(posX, posY, depth),
            PixelDirection.Front => GetVoxelFRONT(posX, posY, depth),
            PixelDirection.Back => GetVoxelBACK(posX, posY, depth),
            _ => throw new System.Exception("Invalid orientation")
        };
    }

    public Voxel GetVoxelTOP(int posX, int posY, int depth) {
        int newDepth = CHUNK_SIZE.z - depth - 1;
        return GetVoxel(posX, posY, newDepth);
    }

    public Voxel GetVoxelDOWN(int posX, int posY, int depth) {
        return GetVoxel(posX, posY, depth);    
    }

    public Voxel GetVoxelLEFT(int posX, int posY, int depth) {
        return GetVoxel(depth, posX, posY);
    }

    public Voxel GetVoxelRIGHT(int posX, int posY, int depth) {
        return GetVoxel(CHUNK_SIZE.x - depth - 1, posX, posY);
    }

    public Voxel GetVoxelFRONT(int posX, int posY, int depth) {
        return GetVoxel(posX, depth, posY);
    }

    public Voxel GetVoxelBACK(int posX, int posY, int depth) {
        return GetVoxel(posX, CHUNK_SIZE.y - depth - 1, posY);
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