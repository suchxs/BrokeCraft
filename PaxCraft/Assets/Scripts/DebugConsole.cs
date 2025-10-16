using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugConsole : MonoBehaviour
{
    // Toggle visibility
    private bool isVisible = false;

    // FPS calculation
    private float deltaTime = 0.0f;
    private float fps = 0.0f;

    // UI styling
    private GUIStyle headerStyle;
    private GUIStyle infoStyle;
    private Rect consoleRect;
    private bool stylesInitialized = false;

    void Update()
    {
        // Toggle console with F1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            isVisible = !isVisible;
        }

        // Calculate FPS
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        fps = 1.0f / deltaTime;
    }

    void OnGUI()
    {
        if (!isVisible) return;

        // Initialize styles once
        if (!stylesInitialized)
        {
            InitializeStyles();
        }

        // Semi-transparent black background
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.Box(consoleRect, "");
        GUI.color = Color.white;

        // Draw console content
        GUILayout.BeginArea(consoleRect);
        
        // Header
        GUILayout.Label("PaxCraft v0.1 BETA Unity Debug Console Made by Suchxs", headerStyle);
        GUILayout.Space(10);

        // Get player position (from camera for now, will use player later)
        Vector3 playerPos = Camera.main.transform.position;
        
        // FPS
        GUILayout.Label($"FPS: {Mathf.Ceil(fps)}", infoStyle);
        
        // Coordinates
        GUILayout.Label($"Position: X={playerPos.x:F2}, Y={playerPos.y:F2}, Z={playerPos.z:F2}", infoStyle);
        GUILayout.Label($"Chunk: ({Mathf.FloorToInt(playerPos.x / VoxelData.ChunkWidth)}, {Mathf.FloorToInt(playerPos.z / VoxelData.ChunkWidth)})", infoStyle);
        
        // Biome (get actual biome from world)
        World world = FindObjectOfType<World>();
        string biomeName = "Unknown";
        if (world != null)
        {
            BiomeAttributes biome = world.GetBiome(Mathf.FloorToInt(playerPos.x), Mathf.FloorToInt(playerPos.z));
            biomeName = biome != null ? biome.biomeName : "None";
        }
        GUILayout.Label($"Biome: {biomeName}", infoStyle);
        
        // Ping (placeholder)
        GUILayout.Label($"Ping: N/A (Singleplayer)", infoStyle);

        GUILayout.Space(10);
        GUILayout.Label("Press F1 to close", infoStyle);

        GUILayout.EndArea();
    }

    void InitializeStyles()
    {
        // Console position and size (top of screen)
        consoleRect = new Rect(10, 10, 500, 200);

        // Header style
        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 16;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = new Color(0.2f, 1f, 0.2f); // Bright green
        headerStyle.wordWrap = true;

        // Info style
        infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 14;
        infoStyle.normal.textColor = Color.white;
        infoStyle.fontStyle = FontStyle.Normal;

        stylesInitialized = true;
    }
}

