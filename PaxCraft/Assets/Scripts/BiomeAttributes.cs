using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BiomeAttributes
{
    public string biomeName;
    public BiomeType biomeType;
    
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

// MINECRAFT-STYLE BIOME GRID (Temperature x Humidity)
public enum BiomeType
{
    // COLD ROW (Temperature 0-0.33)
    SnowyTundra,      // Cold + Dry
    SnowyTaiga,       // Cold + Medium
    IceSpikes,        // Cold + Wet
    
    // MEDIUM ROW (Temperature 0.33-0.66)
    Plains,           // Medium + Dry
    Forest,           // Medium + Medium
    Swamp,            // Medium + Wet
    
    // HOT ROW (Temperature 0.66-1.0)
    Desert,           // Hot + Dry
    Savanna,          // Hot + Medium
    Jungle            // Hot + Wet
}

