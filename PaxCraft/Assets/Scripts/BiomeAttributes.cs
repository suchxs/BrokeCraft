using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BiomeAttributes
{
    public string biomeName;
    
    [Header("Terrain Properties")]
    public int terrainHeight = 64;
    public float terrainScale = 0.005f;
    
    [Header("Grass Color")]
    public Color grassColor = Color.green;
    
    [Header("Surface Blocks")]
    public int surfaceBlock = 3;  // Grass, Sand, Snow, etc.
    public int subSurfaceBlock = 4;  // Dirt, Sand, Stone, etc.
    
    [Header("Grass Coloring (only for grass blocks)")]
    public bool useGrassColoring = true;  // Enable/disable grass color tinting
}

public enum BiomeType
{
    Plains,
    Desert,
    Forest,
    Mountains,
    SnowyTundra
}

