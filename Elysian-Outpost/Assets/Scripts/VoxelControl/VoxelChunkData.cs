using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public struct VoxelChunkData
{
    NativeArray<VoxelDataPosition> voxelDataPositions;

    public readonly int arraySize;
    private int index;

    public VoxelChunkData(int size) {
        voxelDataPositions = new NativeArray<VoxelDataPosition>(size, Allocator.Persistent);
        arraySize = size;
        index = 0;
    }

    public void AddVoxelDataPosition(VoxelDataPosition voxelDataPosition) {
        voxelDataPositions[index] = voxelDataPosition;
        index++;
    }

    public VoxelDataPosition GetVoxelDataPosition(int index) {
        return voxelDataPositions[index];
    }

    public void Dispose() {
        voxelDataPositions.Dispose();
    }

    public readonly int GetIndex() {
        return index;
    }
}
