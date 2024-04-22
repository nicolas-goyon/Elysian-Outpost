using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public struct VoxelDataPosition
{
    public byte type;
    public int4 color;
    public int x;
    public int y;
    public int z;

    public VoxelDataPosition(byte type, int4 color, int x, int y, int z) { 
        this.type = type;
        this.color = color;
        this.x = x;
        this.y = y;
        this.z = z;
    }

}
