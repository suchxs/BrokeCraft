using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// F3 Debug overlay - Minecraft-style detailed information
public class ChunkDebugOverlay : MonoBehaviour
{
    private bool isVisible = false;
    
    // FPS calculation
    private float deltaTime = 0.0f;
    private float fps = 0.0f;
    
    // UI Styles
    private GUIStyle textStyle;
    private GUIStyle headerStyle;
    private bool stylesInitialized = false;
    
    void Update()
    {
        // Toggle with F3 (Minecraft style)
        if (Input.GetKeyDown(KeyCode.F3))
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
        
        // Draw black shadow layer first (Minecraft style text shadow)
        GUI.color = new Color(0, 0, 0, 0.5f);
        DrawLeftPanelShadow();
        DrawRightPanelShadow();
        
        // Draw main text on top
        GUI.color = Color.white;
        DrawLeftPanel();
        DrawRightPanel();
    }
    
    void DrawLeftPanelShadow()
    {
        float startX = 11f; // 1px offset for shadow
        float startY = 11f;
        float lineHeight = 18f;
        int line = 0;
        
        PlayerController player = FindObjectOfType<PlayerController>();
        World world = FindObjectOfType<World>();
        ChunkOptimizer optimizer = FindObjectOfType<ChunkOptimizer>();
        
        if (player == null || world == null) return;
        
        Vector3 playerPos = player.transform.position;
        Vector3 blockPos = new Vector3(
            Mathf.FloorToInt(playerPos.x),
            Mathf.FloorToInt(playerPos.y),
            Mathf.FloorToInt(playerPos.z)
        );
        
        GUIStyle shadowStyle = new GUIStyle(textStyle);
        shadowStyle.normal.textColor = Color.black;
        
        int chunkX = Mathf.FloorToInt(playerPos.x / VoxelData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt(playerPos.z / VoxelData.ChunkWidth);
        int localX = Mathf.FloorToInt(playerPos.x) - (chunkX * VoxelData.ChunkWidth);
        int localZ = Mathf.FloorToInt(playerPos.z) - (chunkZ * VoxelData.ChunkWidth);
        
        Vector3 forward = player.transform.forward;
        string facing = GetCardinalDirection(forward);
        float yaw = player.transform.eulerAngles.y;
        
        BiomeAttributes biome = world.GetBiome(Mathf.FloorToInt(playerPos.x), Mathf.FloorToInt(playerPos.z));
        string biomeName = biome != null ? biome.biomeName : "None";
        
        string state = player.IsGrounded() ? "On Ground" : "In Air";
        if (player.IsSprinting()) state += " (Sprinting)";
        float speed = player.GetCurrentSpeed();
        
        // Draw all text as shadows
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"PaxCraft v.1 BETA", shadowStyle);
        line++;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"FPS: {Mathf.Ceil(fps)}", shadowStyle);
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), "Ping: N/A (Singleplayer)", shadowStyle);
        line++;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"XYZ: {playerPos.x:F3} / {playerPos.y:F3} / {playerPos.z:F3}", shadowStyle);
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"Block: {blockPos.x} {blockPos.y} {blockPos.z}", shadowStyle);
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"Chunk: {chunkX} {chunkZ} in {localX} {Mathf.FloorToInt(playerPos.y)} {localZ}", shadowStyle);
        line++;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"Facing: {facing} ({yaw:F1}°)", shadowStyle);
        line++;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"Biome: {biomeName}", shadowStyle);
        line++;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"State: {state}", shadowStyle);
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"Speed: {speed:F2} m/s", shadowStyle);
        line++;
        if (optimizer != null)
            GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), optimizer.GetStats(), shadowStyle);
        line++;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), "Press F3 to close", shadowStyle);
    }
    
    void DrawRightPanelShadow()
    {
        float startX = Screen.width - 399f; // 1px offset
        float startY = 11f;
        float lineHeight = 18f;
        int line = 0;
        
        GUIStyle shadowStyle = new GUIStyle(textStyle);
        shadowStyle.normal.textColor = Color.black;
        
        long totalMemory = System.GC.GetTotalMemory(false) / 1024 / 1024;
        int systemMemory = SystemInfo.systemMemorySize;
        
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"CPU: {SystemInfo.processorType}", shadowStyle);
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"GPU: {SystemInfo.graphicsDeviceName}", shadowStyle);
        line++;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"Memory: {totalMemory} MB / {systemMemory} MB", shadowStyle);
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"GPU Memory: {SystemInfo.graphicsMemorySize} MB", shadowStyle);
        line++;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"Unity {Application.unityVersion}", shadowStyle);
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"{Screen.width}x{Screen.height}", shadowStyle);
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), $"VSync: {QualitySettings.vSyncCount}", shadowStyle);
    }
    
    void DrawLeftPanel()
    {
        float startX = 10f;
        float startY = 10f;
        float lineHeight = 18f;
        int line = 0;
        
        // Get references
        PlayerController player = FindObjectOfType<PlayerController>();
        World world = FindObjectOfType<World>();
        ChunkOptimizer optimizer = FindObjectOfType<ChunkOptimizer>();
        
        if (player == null || world == null) return;
        
        Vector3 playerPos = player.transform.position;
        Vector3 blockPos = new Vector3(
            Mathf.FloorToInt(playerPos.x),
            Mathf.FloorToInt(playerPos.y),
            Mathf.FloorToInt(playerPos.z)
        );
        
        // HEADER
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"PaxCraft v.1 BETA", headerStyle);
        line++; // Blank line
        
        // FPS with color coding
        string fpsText = $"FPS: {Mathf.Ceil(fps)}";
        Color fpsColor = fps >= 60 ? Color.green : (fps >= 30 ? Color.yellow : Color.red);
        GUIStyle fpsStyle = new GUIStyle(textStyle);
        fpsStyle.normal.textColor = fpsColor;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), fpsText, fpsStyle);
        
        // Ping (moved from F1)
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  "Ping: N/A (Singleplayer)", textStyle);
        
        line++; // Blank line
        
        // XYZ POSITION (Minecraft format)
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"XYZ: {playerPos.x:F3} / {playerPos.y:F3} / {playerPos.z:F3}", textStyle);
        
        // BLOCK POSITION
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"Block: {blockPos.x} {blockPos.y} {blockPos.z}", textStyle);
        
        // CHUNK POSITION (Minecraft format: chunk X, Z in chunk [local X Z])
        int chunkX = Mathf.FloorToInt(playerPos.x / VoxelData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt(playerPos.z / VoxelData.ChunkWidth);
        int localX = Mathf.FloorToInt(playerPos.x) - (chunkX * VoxelData.ChunkWidth);
        int localZ = Mathf.FloorToInt(playerPos.z) - (chunkZ * VoxelData.ChunkWidth);
        
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"Chunk: {chunkX} {chunkZ} in {localX} {Mathf.FloorToInt(playerPos.y)} {localZ}", textStyle);
        
        line++; // Blank line
        
        // FACING DIRECTION (Minecraft style)
        Vector3 forward = player.transform.forward;
        string facing = GetCardinalDirection(forward);
        float yaw = player.transform.eulerAngles.y;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"Facing: {facing} ({yaw:F1}°)", textStyle);
        
        line++; // Blank line
        
        // BIOME
        BiomeAttributes biome = world.GetBiome(Mathf.FloorToInt(playerPos.x), Mathf.FloorToInt(playerPos.z));
        string biomeName = biome != null ? biome.biomeName : "None";
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"Biome: {biomeName}", textStyle);
        
        line++; // Blank line
        
        // PLAYER STATE
        string state = player.IsGrounded() ? "On Ground" : "In Air";
        if (player.IsSprinting()) state += " (Sprinting)";
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"State: {state}", textStyle);
        
        // SPEED
        float speed = player.GetCurrentSpeed();
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"Speed: {speed:F2} m/s", textStyle);
        
        line++; // Blank line
        
        // CHUNK STATS
        if (optimizer != null)
        {
            GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                      optimizer.GetStats(), textStyle);
        }
        
        line++; // Blank line
        
        // FOOTER
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  "Press F3 to close", textStyle);
    }
    
    void DrawRightPanel()
    {
        float startX = Screen.width - 400f;
        float startY = 10f;
        float lineHeight = 18f;
        int line = 0;
        
        // SYSTEM INFO (like Minecraft)
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"CPU: {SystemInfo.processorType}", textStyle);
        
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"GPU: {SystemInfo.graphicsDeviceName}", textStyle);
        
        line++; // Blank line
        
        // MEMORY INFO
        long totalMemory = System.GC.GetTotalMemory(false) / 1024 / 1024;
        int systemMemory = SystemInfo.systemMemorySize;
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"Memory: {totalMemory} MB / {systemMemory} MB", textStyle);
        
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"GPU Memory: {SystemInfo.graphicsMemorySize} MB", textStyle);
        
        line++; // Blank line
        
        // RENDER INFO
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"Unity {Application.unityVersion}", textStyle);
        
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"{Screen.width}x{Screen.height}", textStyle);
        
        GUI.Label(new Rect(startX, startY + (line++ * lineHeight), 400, 20), 
                  $"VSync: {QualitySettings.vSyncCount}", textStyle);
    }
    
    string GetCardinalDirection(Vector3 forward)
    {
        forward.y = 0;
        forward.Normalize();
        
        float angle = Vector3.SignedAngle(Vector3.forward, forward, Vector3.up);
        if (angle < 0) angle += 360f;
        
        // 8 directions like Minecraft
        if (angle >= 337.5f || angle < 22.5f) return "north";
        if (angle >= 22.5f && angle < 67.5f) return "northeast";
        if (angle >= 67.5f && angle < 112.5f) return "east";
        if (angle >= 112.5f && angle < 157.5f) return "southeast";
        if (angle >= 157.5f && angle < 202.5f) return "south";
        if (angle >= 202.5f && angle < 247.5f) return "southwest";
        if (angle >= 247.5f && angle < 292.5f) return "west";
        if (angle >= 292.5f && angle < 337.5f) return "northwest";
        
        return "north";
    }
    
    void InitializeStyles()
    {
        // Text style (white, like Minecraft F3)
        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 12;
        textStyle.normal.textColor = Color.white;
        textStyle.fontStyle = FontStyle.Normal;
        
        // CRITICAL: Add text shadow/outline for readability (Minecraft style)
        // No background box - just shadow under text
        textStyle.contentOffset = new Vector2(1, 1); // Offset for shadow effect
        
        // Header style (yellow like Minecraft)
        headerStyle = new GUIStyle(textStyle);
        headerStyle.normal.textColor = Color.yellow;
        headerStyle.fontStyle = FontStyle.Bold;
        
        stylesInitialized = true;
    }
    
    // Helper to create colored texture for background
    Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}

