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


    private readonly int width = 160;

    private int index = 0;


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
        if (isSpaceUp && Input.GetKey(KeyCode.Space)) {
            isSpaceUp = false;
            CreateMultipleToQueue(index, 1000);

        } else {
            isSpaceUp = true;
        }   
    }


    [BurstCompile]
    private void CreateMultipleToQueue(int startIndex, int amount) {

        VoxelChunkData chunkData = new(amount);
        for (int i = 0; i < amount; i++) {
            int3 position = new((startIndex + i) % width, 0, (startIndex + i) / width);
            int voxelId = 3;
            chunkData.AddVoxelDataPosition(new VoxelDataPosition(position, voxelId));
        }

        chunkVoxelQueue.Enqueue(chunkData);
        index += amount;
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


}
