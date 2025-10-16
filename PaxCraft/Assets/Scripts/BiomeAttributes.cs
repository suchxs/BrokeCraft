using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BiomeAttributes
{
    public string biomeName;
    
    [Header("Terrain Properties - Sebastian Lague Style")]
    [Tooltip("Height multiplier (0-1). 0 = low terrain (valleys/ocean), 1 = high terrain (mountains)")]
    [Range(0f, 1f)]
    public float heightWeight = 0.5f;  // Only affects HEIGHT interpretation, not noise scale!
    
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

