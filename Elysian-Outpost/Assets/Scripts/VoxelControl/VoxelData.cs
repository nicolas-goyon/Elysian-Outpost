using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public struct VoxelData
{
    public byte type;
    public int4 color;
    
    public VoxelData(byte type, int4 color) {
        this.type = type;
        this.color = color;
    }
}
