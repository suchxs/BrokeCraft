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

    // UI styling (Valve-style)
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle infoStyle;
    private GUIStyle labelStyle;
    private GUIStyle separatorStyle;
    private GUIStyle backgroundStyle;
    private Rect consoleRect;
    private bool stylesInitialized = false;
    
    // Colors
    private Color valveOrange = new Color(1f, 0.6f, 0f);
    private Color valveYellow = new Color(1f, 0.9f, 0.3f);
    private Color consoleGreen = new Color(0.6f, 1f, 0.6f);

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

        // Valve-style dark background with border
        GUI.Box(consoleRect, "", backgroundStyle);

        // Draw console content
        GUILayout.BeginArea(new Rect(consoleRect.x + 15, consoleRect.y + 12, consoleRect.width - 30, consoleRect.height - 24));
        
        // Title Header (Valve-style orange)
        GUILayout.Label("PaxCraft v.1 BETA", titleStyle);
        GUILayout.Label("Debug Console", subtitleStyle);
        
        // Separator line
        GUILayout.Label("═══════════════════════════════════════════════════════", separatorStyle);
        GUILayout.Space(5);

        // Get player position and stats
        PlayerController player = FindObjectOfType<PlayerController>();
        Vector3 playerPos = player != null ? player.transform.position : Camera.main.transform.position;
        
        // FPS with color coding (green > 60, yellow > 30, red < 30)
        Color fpsColor = fps >= 60 ? consoleGreen : (fps >= 30 ? valveYellow : Color.red);
        GUIStyle fpsStyle = new GUIStyle(infoStyle);
        fpsStyle.normal.textColor = fpsColor;
        GUILayout.BeginHorizontal();
        GUILayout.Label("fps", labelStyle);
        GUILayout.Space(10);
        GUILayout.Label($"{Mathf.Ceil(fps)}", fpsStyle);
        GUILayout.EndHorizontal();
        
        // Speed (if player exists)
        if (player != null)
        {
            float speed = player.GetCurrentSpeed();
            string sprintStatus = player.IsSprinting() ? " [SPRINT]" : "";
            string groundedStatus = player.IsGrounded() ? " [GROUND]" : " [AIR]";
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("speed", labelStyle);
            GUILayout.Space(10);
            GUILayout.Label($"{speed:F2} m/s{sprintStatus}{groundedStatus}", infoStyle);
            GUILayout.EndHorizontal();
        }
        
        GUILayout.Space(3);
        
        // Coordinates
        GUILayout.BeginHorizontal();
        GUILayout.Label("pos", labelStyle);
        GUILayout.Space(10);
        GUILayout.Label($"X: {playerPos.x:F2}  Y: {playerPos.y:F2}  Z: {playerPos.z:F2}", infoStyle);
        GUILayout.EndHorizontal();
        
        // Chunk coordinates
        int chunkX = Mathf.FloorToInt(playerPos.x / VoxelData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt(playerPos.z / VoxelData.ChunkWidth);
        GUILayout.BeginHorizontal();
        GUILayout.Label("chunk", labelStyle);
        GUILayout.Space(10);
        GUILayout.Label($"X: {chunkX}  Z: {chunkZ}", infoStyle);
        GUILayout.EndHorizontal();
        
        GUILayout.Space(3);
        
        // Biome (get actual biome from world)
        World world = FindObjectOfType<World>();
        string biomeName = "Unknown";
        if (world != null)
        {
            BiomeAttributes biome = world.GetBiome(Mathf.FloorToInt(playerPos.x), Mathf.FloorToInt(playerPos.z));
            biomeName = biome != null ? biome.biomeName : "None";
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("biome", labelStyle);
        GUILayout.Space(10);
        GUILayout.Label(biomeName, infoStyle);
        GUILayout.EndHorizontal();
        
        // Ping (placeholder)
        GUILayout.BeginHorizontal();
        GUILayout.Label("ping", labelStyle);
        GUILayout.Space(10);
        GUILayout.Label("N/A [Singleplayer]", infoStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        // Bottom separator
        GUILayout.Label("───────────────────────────────────────────────────────", separatorStyle);
        GUILayout.Label("Press [F1] to close console", subtitleStyle);

        GUILayout.EndArea();
    }

    void InitializeStyles()
    {
        // Console position and size (Valve-style, larger)
        consoleRect = new Rect(15, 15, 620, 320);

        // Background style (dark with subtle border)
        backgroundStyle = new GUIStyle(GUI.skin.box);
        Texture2D bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.05f, 0.92f)); // Very dark, semi-transparent
        bgTexture.Apply();
        backgroundStyle.normal.background = bgTexture;
        backgroundStyle.border = new RectOffset(2, 2, 2, 2);
        
        // Add border texture
        Texture2D borderTexture = new Texture2D(1, 1);
        borderTexture.SetPixel(0, 0, new Color(0.3f, 0.3f, 0.3f, 1f));
        borderTexture.Apply();
        backgroundStyle.border = new RectOffset(1, 1, 1, 1);

        // Title style (Valve orange/yellow)
        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 20;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = valveOrange;
        titleStyle.alignment = TextAnchor.UpperLeft;

        // Subtitle style
        subtitleStyle = new GUIStyle(GUI.skin.label);
        subtitleStyle.fontSize = 13;
        subtitleStyle.fontStyle = FontStyle.Normal;
        subtitleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        subtitleStyle.alignment = TextAnchor.UpperLeft;

        // Label style (for parameter names - dimmer)
        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        labelStyle.fontStyle = FontStyle.Normal;
        labelStyle.alignment = TextAnchor.MiddleLeft;
        labelStyle.fixedWidth = 65;

        // Info style (for values - brighter)
        infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 14;
        infoStyle.normal.textColor = consoleGreen;
        infoStyle.fontStyle = FontStyle.Normal;
        infoStyle.alignment = TextAnchor.MiddleLeft;

        // Separator style
        separatorStyle = new GUIStyle(GUI.skin.label);
        separatorStyle.fontSize = 10;
        separatorStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
        separatorStyle.alignment = TextAnchor.UpperLeft;

        stylesInitialized = true;
    }
}

