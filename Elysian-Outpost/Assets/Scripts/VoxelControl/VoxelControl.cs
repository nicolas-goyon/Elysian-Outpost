using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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

    private NativeQueue<VoxelDataPosition> voxelDataQueue;


    private readonly int width = 160;

    private int index = 0;


    private bool isSpaceUp = true;
    Unity.Mathematics.Random random = new(123);


    private void Start() {
        voxelDataQueue = new NativeQueue<VoxelDataPosition>(Allocator.Persistent);

    }


    private void OnDestroy() {
        voxelDataQueue.Dispose();
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
        for (int i = 0; i < amount; i++) {
            CreateVoxelToQueue(startIndex + i);
        }
    }


    [BurstCompile]
    private void CreateVoxelToQueue(int indexInBuffer) {
        int4 randomColor = new((byte) random.NextInt(0, 255), (byte) random.NextInt(0, 255), (byte) random.NextInt(0, 255), 255);
        voxelDataQueue.Enqueue(new VoxelDataPosition(1, randomColor, indexInBuffer % width, 0, indexInBuffer / width));
        index++;
    }

    public bool TryDequeue(out VoxelDataPosition voxelData) {
        return voxelDataQueue.TryDequeue(out voxelData);
    }



    public int GetWidth() {
        return width;
    }

    public bool IsQueueEmpty() {
        return voxelDataQueue.IsEmpty();
    }


}
