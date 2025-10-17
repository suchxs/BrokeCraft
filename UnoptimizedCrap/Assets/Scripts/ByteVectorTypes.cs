using System.Runtime.InteropServices;
using Unity.Mathematics;

/// <summary>
/// Burst-compatible byte vector types for memory-efficient voxel data.
/// These structs use sequential layout for optimal memory packing.
/// </summary>

[StructLayout(LayoutKind.Sequential)]
public struct byte2
{
    public byte x;
    public byte y;
    
    public byte2(byte x, byte y)
    {
        this.x = x;
        this.y = y;
    }
    
    public byte2(int x, int y)
    {
        this.x = (byte)x;
        this.y = (byte)y;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct byte3
{
    public byte x;
    public byte y;
    public byte z;
    
    public byte3(byte x, byte y, byte z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    
    public byte3(int x, int y, int z)
    {
        this.x = (byte)x;
        this.y = (byte)y;
        this.z = (byte)z;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct sbyte3
{
    public sbyte x;
    public sbyte y;
    public sbyte z;
    
    public sbyte3(sbyte x, sbyte y, sbyte z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    
    public sbyte3(int x, int y, int z)
    {
        this.x = (sbyte)x;
        this.y = (sbyte)y;
        this.z = (sbyte)z;
    }
}

