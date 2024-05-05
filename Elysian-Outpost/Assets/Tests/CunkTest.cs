using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

public class CunkTest
{
    private VoxelChunkData chunkData;
    private VoxelChunkBuilder chunkBuilder;

    [SetUp]
    public void Setup() {
        chunkBuilder = new(new int3(0, 0, 0), Allocator.Temp);
        for (int i = 1; i <= VoxelChunkData.CHUNK_SIZE.x * VoxelChunkData.CHUNK_SIZE.y * VoxelChunkData.CHUNK_SIZE.z; i++) {
            chunkBuilder.AddVoxelDataPosition(new Voxel {
                kind = VoxelKind.Solid,
                voxelId = i
            });
        }

        chunkData = chunkBuilder.Build();
    }

    [Test]
    public void TestVoxel() {
        Voxel testVoxel = new() {
            kind = VoxelKind.Solid,
            voxelId = 1
        };
        Assert.AreEqual(testVoxel.kind, VoxelKind.Solid);
        Assert.AreEqual(testVoxel.voxelId, 1);
    }


    [Test]
    public void TestChunkSize() {
        Assert.AreEqual(new int3(16, 16, 32), VoxelChunkData.CHUNK_SIZE);
        Assert.AreEqual(8192, VoxelChunkData.CHUNK_VOLUME);
    }

    [Test]
    public void TestAccessChunkDataAndBuilder() {
        

        Assert.AreEqual(8192, chunkBuilder.GetIndex());

        for (int i = 0; i < VoxelChunkData.CHUNK_SIZE.x * VoxelChunkData.CHUNK_SIZE.y * VoxelChunkData.CHUNK_SIZE.z; i++) {
            Assert.AreEqual(i + 1, chunkBuilder.GetVoxelDataPosition(i).voxelId);
        }

        for (int i = 0; i < VoxelChunkData.CHUNK_SIZE.x * VoxelChunkData.CHUNK_SIZE.y * VoxelChunkData.CHUNK_SIZE.z; i++) {
            Assert.AreEqual(i + 1, chunkData.GetVoxel(i).voxelId);
        }

        Assert.AreEqual(1, chunkData.GetVoxel(0, 0, 0).voxelId);
        Assert.AreEqual(2, chunkData.GetVoxel(1, 0, 0).voxelId);
        Assert.AreEqual(3, chunkData.GetVoxel(2, 0, 0).voxelId);
        Assert.AreEqual(4, chunkData.GetVoxel(3, 0, 0).voxelId);
        Assert.AreEqual(5, chunkData.GetVoxel(4, 0, 0).voxelId);
        Assert.AreEqual(6, chunkData.GetVoxel(5, 0, 0).voxelId);
        Assert.AreEqual(7, chunkData.GetVoxel(6, 0, 0).voxelId);
        Assert.AreEqual(8, chunkData.GetVoxel(7, 0, 0).voxelId);
        Assert.AreEqual(16, chunkData.GetVoxel(15, 0, 0).voxelId);
        Assert.AreEqual(17, chunkData.GetVoxel(0, 1, 0).voxelId);
        Assert.AreEqual(18, chunkData.GetVoxel(1, 1, 0).voxelId);
        Assert.AreEqual(19, chunkData.GetVoxel(2, 1, 0).voxelId);
        Assert.AreEqual(32, chunkData.GetVoxel(15, 1, 0).voxelId);
        Assert.AreEqual(33, chunkData.GetVoxel(0, 2, 0).voxelId);

        Assert.AreEqual(256, chunkData.GetVoxel(15, 15, 0).voxelId);
        Assert.AreEqual(257, chunkData.GetVoxel(0, 0, 1).voxelId);
        Assert.AreEqual(258, chunkData.GetVoxel(1, 0, 1).voxelId);
        Assert.AreEqual(259, chunkData.GetVoxel(2, 0, 1).voxelId);
        Assert.AreEqual(272, chunkData.GetVoxel(15, 0, 1).voxelId);
        Assert.AreEqual(512, chunkData.GetVoxel(15, 15, 1).voxelId);
        Assert.AreEqual(513, chunkData.GetVoxel(0, 0, 2).voxelId);
        Assert.AreEqual(514, chunkData.GetVoxel(1, 0, 2).voxelId);
        Assert.AreEqual(515, chunkData.GetVoxel(2, 0, 2).voxelId);
        Assert.AreEqual(516, chunkData.GetVoxel(3, 0, 2).voxelId);
        Assert.AreEqual(544, chunkData.GetVoxel(15, 1, 2).voxelId);

        // Last 
        Assert.AreEqual(8192, chunkData.GetVoxel(15, 15, 31).voxelId);


        // DOWN to TOP
        Assert.AreEqual(1, chunkData.GetVoxel(0, 0, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(2, chunkData.GetVoxel(1, 0, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(3, chunkData.GetVoxel(2, 0, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(4, chunkData.GetVoxel(3, 0, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(16, chunkData.GetVoxel(15, 0, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(17, chunkData.GetVoxel(0, 1, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(18, chunkData.GetVoxel(1, 1, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(19, chunkData.GetVoxel(2, 1, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(32, chunkData.GetVoxel(15, 1, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(256, chunkData.GetVoxel(15, 15, 0, PixelDirection.Down).voxelId);
        Assert.AreEqual(257, chunkData.GetVoxel(0, 0, 1, PixelDirection.Down).voxelId);
        Assert.AreEqual(8192, chunkData.GetVoxel(15, 15, 31, PixelDirection.Down).voxelId);


        // TOP To DOWN
        Assert.AreEqual(7937, chunkData.GetVoxel(0, 0, 0, PixelDirection.Top).voxelId);
        Assert.AreEqual(7938, chunkData.GetVoxel(1, 0, 0, PixelDirection.Top).voxelId);
        Assert.AreEqual(7939, chunkData.GetVoxel(2, 0, 0, PixelDirection.Top).voxelId);
        Assert.AreEqual(7940, chunkData.GetVoxel(3, 0, 0, PixelDirection.Top).voxelId);
        Assert.AreEqual(7952, chunkData.GetVoxel(15, 0, 0, PixelDirection.Top).voxelId);
        Assert.AreEqual(8192, chunkData.GetVoxel(15, 15, 0, PixelDirection.Top).voxelId);
        Assert.AreEqual(7681, chunkData.GetVoxel(0, 0, 1, PixelDirection.Top).voxelId);
        Assert.AreEqual(7936, chunkData.GetVoxel(15, 15, 1, PixelDirection.Top).voxelId);


        // FRONT To BACK
        Assert.AreEqual(1, chunkData.GetVoxel(0, 0, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(2, chunkData.GetVoxel(1, 0, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(3, chunkData.GetVoxel(2, 0, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(4, chunkData.GetVoxel(3, 0, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(16, chunkData.GetVoxel(15, 0, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(257, chunkData.GetVoxel(0, 1, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(258, chunkData.GetVoxel(1, 1, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(259, chunkData.GetVoxel(2, 1, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(272, chunkData.GetVoxel(15, 1, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(3856, chunkData.GetVoxel(15, 15, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(7952, chunkData.GetVoxel(15, 31, 0, PixelDirection.Front).voxelId);
        Assert.AreEqual(17, chunkData.GetVoxel(0, 0, 1, PixelDirection.Front).voxelId);
        Assert.AreEqual(8192, chunkData.GetVoxel(15, 31, 15, PixelDirection.Front).voxelId);


        // BACK To FRONT
        Assert.AreEqual(241, chunkData.GetVoxel(0, 0, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(242, chunkData.GetVoxel(1, 0, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(243, chunkData.GetVoxel(2, 0, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(244, chunkData.GetVoxel(3, 0, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(256, chunkData.GetVoxel(15, 0, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(497, chunkData.GetVoxel(0, 1, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(498, chunkData.GetVoxel(1, 1, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(499, chunkData.GetVoxel(2, 1, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(512, chunkData.GetVoxel(15, 1, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(4096, chunkData.GetVoxel(15, 15, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(8192, chunkData.GetVoxel(15, 31, 0, PixelDirection.Back).voxelId);
        Assert.AreEqual(225, chunkData.GetVoxel(0, 0, 1, PixelDirection.Back).voxelId);
        Assert.AreEqual(7952, chunkData.GetVoxel(15, 31, 15, PixelDirection.Back).voxelId);

        // LEFT To RIGHT
        Assert.AreEqual(1, chunkData.GetVoxel(0, 0, 0, PixelDirection.Left).voxelId);
        Assert.AreEqual(17, chunkData.GetVoxel(1, 0, 0, PixelDirection.Left).voxelId);
        Assert.AreEqual(33, chunkData.GetVoxel(2, 0, 0, PixelDirection.Left).voxelId);
        Assert.AreEqual(241, chunkData.GetVoxel(15, 0, 0, PixelDirection.Left).voxelId);
        Assert.AreEqual(257, chunkData.GetVoxel(0, 1, 0, PixelDirection.Left).voxelId);
        Assert.AreEqual(273, chunkData.GetVoxel(1, 1, 0, PixelDirection.Left).voxelId);
        Assert.AreEqual(497, chunkData.GetVoxel(15, 1, 0, PixelDirection.Left).voxelId);
        Assert.AreEqual(2, chunkData.GetVoxel(0, 0, 1, PixelDirection.Left).voxelId);
        Assert.AreEqual(8177, chunkData.GetVoxel(15, 31, 0, PixelDirection.Left).voxelId);
        Assert.AreEqual(8192, chunkData.GetVoxel(15, 31, 15, PixelDirection.Left).voxelId);


        // RIGHT To LEFT
        Assert.AreEqual(16, chunkData.GetVoxel(0, 0, 0, PixelDirection.Right).voxelId);
        Assert.AreEqual(32, chunkData.GetVoxel(1, 0, 0, PixelDirection.Right).voxelId);
        Assert.AreEqual(48, chunkData.GetVoxel(2, 0, 0, PixelDirection.Right).voxelId);
        Assert.AreEqual(256, chunkData.GetVoxel(15, 0, 0, PixelDirection.Right).voxelId);
        Assert.AreEqual(272, chunkData.GetVoxel(0, 1, 0, PixelDirection.Right).voxelId);
        Assert.AreEqual(288, chunkData.GetVoxel(1, 1, 0, PixelDirection.Right).voxelId);
        Assert.AreEqual(512, chunkData.GetVoxel(15, 1, 0, PixelDirection.Right).voxelId);
        Assert.AreEqual(15, chunkData.GetVoxel(0, 0, 1, PixelDirection.Right).voxelId);
        Assert.AreEqual(8192, chunkData.GetVoxel(15, 31, 0, PixelDirection.Right).voxelId);
        Assert.AreEqual(8177, chunkData.GetVoxel(15, 31, 15, PixelDirection.Right).voxelId);

        Assert.AreEqual(new int3(0, 0, 0), chunkData.GetChunkPosition());
        

        // Test out of bound
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(-1);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(8192);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(-1, 0, 0, PixelDirection.Top);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, -1, 0, PixelDirection.Top);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, 0, -1, PixelDirection.Top);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(-1, 0, 0, PixelDirection.Down);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, -1, 0, PixelDirection.Down);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, 0, -1, PixelDirection.Down);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(-1, 0, 0, PixelDirection.Left);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, -1, 0, PixelDirection.Left);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, 0, -1, PixelDirection.Left);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(-1, 0, 0, PixelDirection.Right);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, -1, 0, PixelDirection.Right);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, 0, -1, PixelDirection.Right);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(-1, 0, 0, PixelDirection.Front);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, -1, 0, PixelDirection.Front);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, 0, -1, PixelDirection.Front);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(-1, 0, 0, PixelDirection.Back);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, -1, 0, PixelDirection.Back);
        });
        Assert.Throws<System.Exception>(() => {
            chunkData.GetVoxel(0, 0, -1, PixelDirection.Back);
        });


        Assert.DoesNotThrow(() => {
            chunkData.Dispose();
        });
        Assert.DoesNotThrow(() => {
            chunkBuilder.Dispose();
        });
    }

    [Test]
    public void TestChunkPositions() {

        Assert.AreEqual(new int3(0, 0, 0), chunkData.GetChunkPosition());


        VoxelChunkBuilder otherChunkBuilder = new(new int3(1, 2, 3), Allocator.Temp);
        VoxelChunkData otherChunkData = otherChunkBuilder.Build();

        Assert.AreEqual(new float3(16, 32, 96), otherChunkData.GetVoxelPosition(0));
        Assert.AreEqual(new float3(17, 32, 96), otherChunkData.GetVoxelPosition(1));
        Assert.AreEqual(new float3(16, 33, 96), otherChunkData.GetVoxelPosition(16));
        Assert.AreEqual(new float3(0, 0, 0), chunkData.GetVoxelPosition(0));
        Assert.AreEqual(new float3(1, 0, 0), chunkData.GetVoxelPosition(1));
        Assert.AreEqual(new float3(0, 1, 0), chunkData.GetVoxelPosition(16));

        // Test throw
        Assert.Throws<System.Exception>(() => {
            otherChunkData.GetVoxelPosition(-1);
        });

        Assert.Throws<System.Exception>(() => {
            otherChunkData.GetVoxelPosition(8192);
        });

        Assert.Throws<System.Exception>(() => {
            VoxelChunkData.GetVoxelPositionInChunk(-1);
        });
        Assert.Throws<System.Exception>(() => {
            VoxelChunkData.GetVoxelPositionInChunk(8192);
        });


        Assert.DoesNotThrow(() => {
            otherChunkData.Dispose();
        });

    }





    //[UnityTest]
    //public IEnumerator TestRenderUntilNoAir() {
    //    yield return null;
    //}










}
