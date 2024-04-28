using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Global_Voxels {

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

        public VoxelType(float4 color) {
            this.floatColor = color;
        }


    }

    public class VoxelChunkBuilder {
        public static int3 CHUNK_SIZE {get { return VoxelChunkData.CHUNK_SIZE; } }

        private NativeArray<Voxel> voxels;
        private int index;
        private int3 position;

        public VoxelChunkBuilder(int3 chunkPosition, Allocator allocator) { 
            voxels = new(CHUNK_SIZE.x * CHUNK_SIZE.y * CHUNK_SIZE.z, allocator);
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
            int y = index / (CHUNK_SIZE.x * CHUNK_SIZE.z);
            int z = (index / CHUNK_SIZE.x) % CHUNK_SIZE.z;

            return new float3(x, y, z);
        }

        public VoxelChunkData Build() {
            return new VoxelChunkData(voxels, index, position);
        }


    }



    public struct VoxelChunkData {
        public readonly static int3 CHUNK_SIZE = new(16, 20, 10);

        private NativeArray<Voxel> voxels;

        private readonly int index;
        private readonly int3 chunkPosition;


        public VoxelChunkData(NativeArray<Voxel> voxels, int index, int3 chunkPosition) { 
            this.voxels = voxels;
            this.index = index;
            this.chunkPosition = chunkPosition;
        }

        public Voxel GetVoxel(int index) {
            return voxels[index];
        }

        public Voxel GetVoxel(int x, int y, int z) {
            return voxels[x + z * CHUNK_SIZE.x + y * CHUNK_SIZE.x * CHUNK_SIZE.z];
        }

        public void Dispose() {
            voxels.Dispose();
        }

        public readonly int GetIndex() {
            return index;
        }

        public readonly int3 GetChunkPosition() {
            return chunkPosition;
        }

        public float3 GetVoxelPosition(int index) {
            int x = index % CHUNK_SIZE.x;
            int y = index / (CHUNK_SIZE.x * CHUNK_SIZE.z);
            int z = (index / CHUNK_SIZE.x) % CHUNK_SIZE.z;

            int xOffset = chunkPosition.x * CHUNK_SIZE.x;
            int yOffset = 0 * CHUNK_SIZE.y;
            int zOffset = chunkPosition.y * CHUNK_SIZE.z;

            return new(x + xOffset, y + yOffset, z + zOffset);


        }
    }
}