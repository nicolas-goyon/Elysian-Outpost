using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


public struct Voxel : IComponentData {
    public VoxelKind kind;
    public long voxelId;
}


public enum VoxelKind {
    Air,
    Liquid,
    Solid,
    Entity
}

public struct VoxelType {
    public float4 floatColor;

    public VoxelType(float4 color) {
        this.floatColor = color;
    }


}

public class VoxelChunkBuilder {
    public readonly static int3 CHUNK_SIZE = new(16, 16, 16);

    private NativeArray<Voxel> voxels;
    private int index;
    private int2 position;

    public VoxelChunkBuilder(int2 chunkPosition) { 
        voxels = new(CHUNK_SIZE.x * CHUNK_SIZE.y * CHUNK_SIZE.z, Allocator.Persistent);
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

    public float3 GetVoxelPosition(int index) {
        int x = index % CHUNK_SIZE.x;
        int y = (index / CHUNK_SIZE.x) % CHUNK_SIZE.y;
        int z = index / (CHUNK_SIZE.x * CHUNK_SIZE.y);

        return new float3(x, y, z);
    }

    public VoxelChunkData Build() {
        return new VoxelChunkData(voxels, index, position);
    }


}



public struct VoxelChunkData {
    private static int3 CHUNK_SIZE = new(16, 16, 16);

    private NativeArray<Voxel> voxels;

    private readonly int index;
    private readonly int2 chunkPosition;


    public VoxelChunkData(NativeArray<Voxel> voxels, int index, int2 chunkPosition) { 
        this.voxels = voxels;
        this.index = index;
        this.chunkPosition = chunkPosition;
    }

    public Voxel GetVoxelDataPosition(int index) {
        return voxels[index];
    }

    public void Dispose() {
        voxels.Dispose();
    }

    public readonly int GetIndex() {
        return index;
    }

    public readonly int2 GetChunkPosition() {
        Debug.Log(chunkPosition);
        return chunkPosition;
    }

    public float3 GetVoxelPosition(int index) {
        int x = index % CHUNK_SIZE.x;
        int y = (index / CHUNK_SIZE.x) % CHUNK_SIZE.y;
        int z = index / (CHUNK_SIZE.x * CHUNK_SIZE.y);

        int xOffset = chunkPosition.x * CHUNK_SIZE.x;
        int yOffset = 0 * CHUNK_SIZE.y;
        int zOffset = chunkPosition.y * CHUNK_SIZE.z;

        return new(x + xOffset, y + yOffset, z + zOffset);


    }
}
