using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

/// <summary>
/// Minecraft-style F3 debug overlay showing game information.
/// Press F3 to toggle visibility.
/// </summary>
public class DebugOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private World world;
    [SerializeField] private Transform player;

    [Header("Settings")]
    // F3 key toggle - using new Input System's Keyboard.current.f3Key
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color shadowColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    private bool isVisible = false;
    private GUIStyle leftTextStyle;
    private GUIStyle rightTextStyle;
    private GUIStyle titleStyle;

    private float fps;
    private float deltaTime;
    private const float FPS_UPDATE_INTERVAL = 0.25f;
    private float fpsTimer;

    private void Awake()
    {
        // Try to find references if not assigned
        if (world == null)
        {
            world = FindFirstObjectByType<World>();
        }

        if (player == null)
        {
            var playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
        }
    }

    private void Update()
    {
        // Toggle visibility with F3 (new Input System only)
        bool f3Pressed = false;
        
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
        {
            f3Pressed = true;
        }
#else
        // Fallback for old Input System (if enabled)
        if (Input.GetKeyDown(toggleKey))
        {
            f3Pressed = true;
        }
#endif

        if (f3Pressed)
        {
            isVisible = !isVisible;
            
            // Keep cursor locked so player can still move and look around
            // Just show the overlay on top
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            Debug.Log($"Debug Overlay: {(isVisible ? "ENABLED" : "DISABLED")}");
        }

        // Update FPS counter
        fpsTimer += Time.unscaledDeltaTime;
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        if (fpsTimer >= FPS_UPDATE_INTERVAL)
        {
            fps = 1.0f / deltaTime;
            fpsTimer = 0f;
        }
    }

    private void OnGUI()
    {
        if (!isVisible)
        {
            return;
        }

        InitializeStyles();

        // No background - transparent overlay like Minecraft
        // Text is drawn directly on the screen with text shadow for readability
        
        DrawLeftPanel();
        DrawRightPanel();
    }

    private void DrawLeftPanel()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height - 20));

        // Title
        GUILayout.Label("BrokeCraft V1.0 BETA", titleStyle);
        GUILayout.Space(10);

        // Player position
        if (player != null)
        {
            Vector3 pos = player.position;
            GUILayout.Label($"XYZ: {pos.x:F2} / {pos.y:F2} / {pos.z:F2}", leftTextStyle);
            
            int3 chunkPos = World.WorldPosToChunkPos(new float3(pos.x, pos.y, pos.z));
            GUILayout.Label($"Chunk: {chunkPos.x} {chunkPos.y} {chunkPos.z}", leftTextStyle);
            
            int3 blockPos = new int3(
                Mathf.FloorToInt(pos.x),
                Mathf.FloorToInt(pos.y),
                Mathf.FloorToInt(pos.z)
            );
            GUILayout.Label($"Block: {blockPos.x} {blockPos.y} {blockPos.z}", leftTextStyle);
        }

        GUILayout.Space(10);

        // Biome (placeholder)
        GUILayout.Label("Biome: Plains (WIP)", leftTextStyle);

        GUILayout.Space(10);

        // FPS
        GUILayout.Label($"FPS: {Mathf.RoundToInt(fps)}", leftTextStyle);
        GUILayout.Label($"Frame Time: {deltaTime * 1000f:F1} ms", leftTextStyle);

        GUILayout.Space(10);

        // World info
        if (world != null)
        {
            GUILayout.Label($"Loaded Chunks: {world.GetLoadedChunkCount()}", leftTextStyle);
            GUILayout.Label($"View Distance: H:{world.horizontalViewDistance} V:{world.verticalViewDistance}", leftTextStyle);
        }

        GUILayout.Space(10);

        // Controls hint
        GUILayout.Label("Press F3 to close", leftTextStyle);

        GUILayout.EndArea();
    }

    private void DrawRightPanel()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 410, 10, 400, Screen.height - 20));

        GUILayout.BeginVertical();

        // System Info - Right aligned
        GUILayout.Label($"CPU: {SystemInfo.processorType}", rightTextStyle);
        GUILayout.Label($"Cores: {SystemInfo.processorCount}", rightTextStyle);
        GUILayout.Label($"Frequency: {SystemInfo.processorFrequency} MHz", rightTextStyle);

        GUILayout.Space(10);

        GUILayout.Label($"GPU: {SystemInfo.graphicsDeviceName}", rightTextStyle);
        GUILayout.Label($"VRAM: {SystemInfo.graphicsMemorySize} MB", rightTextStyle);
        GUILayout.Label($"API: {SystemInfo.graphicsDeviceType}", rightTextStyle);

        GUILayout.Space(10);

        // Memory usage
        long totalMemory = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        long reservedMemory = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
        long monoMemory = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);

        GUILayout.Label($"System RAM: {SystemInfo.systemMemorySize} MB", rightTextStyle);
        GUILayout.Label($"Allocated: {totalMemory} MB", rightTextStyle);
        GUILayout.Label($"Reserved: {reservedMemory} MB", rightTextStyle);
        GUILayout.Label($"Mono: {monoMemory} MB", rightTextStyle);

        GUILayout.Space(10);

        GUILayout.Label($"Unity: {Application.unityVersion}", rightTextStyle);
        GUILayout.Label($"Platform: {Application.platform}", rightTextStyle);

        GUILayout.EndVertical();

        GUILayout.EndArea();
    }

    private void InitializeStyles()
    {
        if (leftTextStyle != null)
        {
            return;
        }

        // Title style (larger, bold) with text shadow like Minecraft
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow },
            alignment = TextAnchor.UpperLeft,
            // Add shadow for better readability
            richText = true
        };

        // Left-aligned text with shadow
        leftTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = textColor },
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(0, 0, 2, 2),
            richText = true
        };

        // Right-aligned text with shadow
        rightTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = textColor },
            alignment = TextAnchor.UpperRight,
            padding = new RectOffset(0, 0, 2, 2),
            richText = true
        };
    }
}
