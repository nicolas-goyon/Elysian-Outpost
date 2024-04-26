using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public class VoxelControl : MonoBehaviour
{
    public static VoxelControl Instance { get; private set; }

    private void Awake() {
        if (Instance != null && Instance != this) {
            throw new System.Exception("Multiple instances of VoxelControl found");
        } else {
            Instance = this;
        }
    }


    // private VoxelData[] voxels; // Maybe keep track of the voxels TODO

    //private NativeQueue<VoxelDataPosition> voxelDataQueue;
    private NativeQueue<VoxelChunkData> chunkVoxelQueue;


    private readonly int width = 5; // 5 chunks

    private int index = 0;
    private int spawnedAmount;


    private bool isSpaceUp = true;
    //Unity.Mathematics.Random random = new(123);


    private void Start() {
        //voxelDataQueue = new NativeQueue<VoxelDataPosition>(Allocator.Persistent);
        chunkVoxelQueue = new NativeQueue<VoxelChunkData>(Allocator.Persistent);

    }


    private void OnDestroy() {
        //voxelDataQueue.Dispose();
        chunkVoxelQueue.Dispose();

    }





    [BurstCompile]
    private void Update() {
        if (isSpaceUp && Input.GetKeyDown(KeyCode.Space)) {
            isSpaceUp = false;
            CreateChunkToQueue(index);

        } else {
            isSpaceUp = true;
        }   
    }


    [BurstCompile]
    private void CreateChunkToQueue(int startIndex) {
        int2 chunkPosition = new(startIndex % width, startIndex / width);
        //Debug.Log(chunkPosition + " : " + startIndex + " => " + startIndex % width + " , " + startIndex / width);

        VoxelChunkBuilder voxelChunkBuilder = new(chunkPosition);

        int amount = VoxelChunkBuilder.CHUNK_SIZE.x * VoxelChunkBuilder.CHUNK_SIZE.y * VoxelChunkBuilder.CHUNK_SIZE.z;

        for (int i = 0; i < amount; i++) {
            int voxelId = 3;
            voxelChunkBuilder.AddVoxelDataPosition(new Voxel {
                kind = VoxelKind.Solid,
                voxelId = voxelId
            });

        }

        chunkVoxelQueue.Enqueue(voxelChunkBuilder.Build());
        index ++;
        spawnedAmount += amount;
    }



    public bool TryDequeue(out VoxelChunkData voxelData) {
        return chunkVoxelQueue.TryDequeue(out voxelData);
    }

    public bool IsQueueEmpty() {
        return chunkVoxelQueue.IsEmpty();
    }



    public int GetWidth() {
        return width;
    }


    public int GetIndex() {
        return index;
    }

    public int GetAmountOfSpawnedVoxels() {
        return spawnedAmount;
    }


}
