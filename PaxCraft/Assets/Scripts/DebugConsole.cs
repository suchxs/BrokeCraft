using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// F1 Console - Valve Source Engine style command console
public class DebugConsole : MonoBehaviour
{
    // Toggle visibility
    private bool isVisible = false;

    // Console output
    private List<string> consoleLog = new List<string>();
    private int maxLogLines = 15; // How many lines to display
    
    // Input
    private string currentInput = "";
    private List<string> commandHistory = new List<string>();
    private int historyIndex = -1;
    
    // UI styling
    private GUIStyle backgroundStyle;
    private GUIStyle textStyle;
    private GUIStyle inputStyle;
    private GUIStyle headerStyle;
    private Rect consoleRect;
    private bool stylesInitialized = false;
    private bool focusInput = false;

    void Start()
    {
        // Welcome message
        AddLog("PaxCraft v.1 BETA - Developer Console");
        AddLog("Type 'help' for available commands");
        AddLog("────────────────────────────────────────");
    }

    void Update()
    {
        // Toggle console with F1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            isVisible = !isVisible;
            if (isVisible)
            {
                focusInput = true;
                // Unlock cursor when console opens
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // Re-lock cursor when console closes
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        // Handle input when console is open
        if (isVisible)
        {
            // Submit command with Enter
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!string.IsNullOrEmpty(currentInput))
                {
                    ExecuteCommand(currentInput);
                    commandHistory.Add(currentInput);
                    currentInput = "";
                    historyIndex = commandHistory.Count;
                }
            }
            
            // Navigate command history with Up/Down arrows
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (commandHistory.Count > 0 && historyIndex > 0)
                {
                    historyIndex--;
                    currentInput = commandHistory[historyIndex];
                }
            }
            
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (historyIndex < commandHistory.Count - 1)
                {
                    historyIndex++;
                    currentInput = commandHistory[historyIndex];
                }
                else
                {
                    historyIndex = commandHistory.Count;
                    currentInput = "";
                }
            }
        }
    }

    void OnGUI()
    {
        if (!isVisible) return;

        // Initialize styles once
        if (!stylesInitialized)
        {
            InitializeStyles();
        }

        // Draw console background
        GUI.Box(consoleRect, "", backgroundStyle);

        // Console content area
        GUILayout.BeginArea(new Rect(consoleRect.x + 10, consoleRect.y + 10, consoleRect.width - 20, consoleRect.height - 20));
        
        // Header
        GUILayout.Label("Developer Console", headerStyle);
        GUILayout.Space(5);
        
        // Console output (scrolling log)
        int startIndex = Mathf.Max(0, consoleLog.Count - maxLogLines);
        for (int i = startIndex; i < consoleLog.Count; i++)
        {
            GUILayout.Label(consoleLog[i], textStyle);
        }
        
        GUILayout.FlexibleSpace();
        
        // Input field at bottom
        GUILayout.BeginHorizontal();
        GUILayout.Label(">", textStyle);
        GUI.SetNextControlName("ConsoleInput");
        currentInput = GUILayout.TextField(currentInput, inputStyle, GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        GUILayout.Label("F1 to close | Up/Down arrows for history | Type 'help' for commands", textStyle);
        
        GUILayout.EndArea();
        
        // Focus input field when console opens
        if (focusInput)
        {
            GUI.FocusControl("ConsoleInput");
            focusInput = false;
        }
    }

    void ExecuteCommand(string command)
    {
        // Echo command
        AddLog($"> {command}");
        
        // Parse command (split by spaces)
        string[] parts = command.Trim().Split(' ');
        string cmd = parts[0].ToLower();
        
        // Execute based on command
        switch (cmd)
        {
            case "help":
                AddLog("Available commands:");
                AddLog("  help - Show this help message");
                AddLog("  clear - Clear console output");
                AddLog("  fps - Show current FPS");
                AddLog("  world - Show world generation info");
                AddLog("  pos - Show player position");
                AddLog("  teleport <x> <y> <z> - Teleport to coordinates");
                AddLog("  timescale <value> - Set game speed (0.1-10)");
                AddLog("  quit - Exit game");
                AddLog("Commands coming soon: give, setblock, gamemode, etc.");
                break;
                
            case "clear":
                consoleLog.Clear();
                AddLog("Console cleared");
                break;
                
            case "fps":
                float fps = 1.0f / Time.deltaTime;
                AddLog($"Current FPS: {Mathf.Ceil(fps)}");
                break;
                
            case "world":
                World worldObj = FindObjectOfType<World>();
                if (worldObj != null)
                {
                    AddLog($"World Info:");
                    AddLog($"  {worldObj.GetWorldStats()}");
                    AddLog($"  Seed: {worldObj.seed}");
                }
                else
                {
                    AddLog("Error: World not found");
                }
                break;
                
            case "pos":
                PlayerController player = FindObjectOfType<PlayerController>();
                if (player != null)
                {
                    Vector3 pos = player.transform.position;
                    AddLog($"Player position: X={pos.x:F2}, Y={pos.y:F2}, Z={pos.z:F2}");
                }
                else
                {
                    AddLog("Error: Player not found");
                }
                break;
                
            case "teleport":
            case "tp":
                if (parts.Length < 4)
                {
                    AddLog("Usage: teleport <x> <y> <z>");
                }
                else
                {
                    try
                    {
                        float x = float.Parse(parts[1]);
                        float y = float.Parse(parts[2]);
                        float z = float.Parse(parts[3]);
                        PlayerController playerTP = FindObjectOfType<PlayerController>();
                        if (playerTP != null)
                        {
                            playerTP.transform.position = new Vector3(x, y, z);
                            AddLog($"Teleported to X={x}, Y={y}, Z={z}");
                        }
                    }
                    catch
                    {
                        AddLog("Error: Invalid coordinates");
                    }
                }
                break;
                
            case "timescale":
                if (parts.Length < 2)
                {
                    AddLog($"Current timescale: {Time.timeScale}");
                    AddLog("Usage: timescale <value> (0.1-10)");
                }
                else
                {
                    try
                    {
                        float scale = float.Parse(parts[1]);
                        scale = Mathf.Clamp(scale, 0.1f, 10f);
                        Time.timeScale = scale;
                        AddLog($"Timescale set to {scale}");
                    }
                    catch
                    {
                        AddLog("Error: Invalid value");
                    }
                }
                break;
                
            case "quit":
            case "exit":
                AddLog("Quitting game...");
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #else
                Application.Quit();
                #endif
                break;
                
            default:
                AddLog($"Unknown command: '{cmd}'. Type 'help' for available commands.");
                break;
        }
    }

    void AddLog(string message)
    {
        consoleLog.Add(message);
        
        // Limit log size to prevent memory issues
        if (consoleLog.Count > 100)
        {
            consoleLog.RemoveAt(0);
        }
    }

    void InitializeStyles()
    {
        // Console position and size (half screen, bottom)
        consoleRect = new Rect(10, 10, Screen.width - 20, Screen.height / 2);

        // Background style (dark, semi-transparent)
        backgroundStyle = new GUIStyle(GUI.skin.box);
        Texture2D bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.05f, 0.95f));
        bgTexture.Apply();
        backgroundStyle.normal.background = bgTexture;

        // Header style (orange like Valve)
        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 16;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = new Color(1f, 0.6f, 0f);

        // Text style (white)
        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 12;
        textStyle.normal.textColor = Color.white;
        textStyle.fontStyle = FontStyle.Normal;
        textStyle.wordWrap = true;

        // Input style
        inputStyle = new GUIStyle(GUI.skin.textField);
        inputStyle.fontSize = 14;
        inputStyle.normal.textColor = Color.white;
        inputStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 1f));
        inputStyle.focused.textColor = Color.white;
        inputStyle.focused.background = MakeTex(2, 2, new Color(0.3f, 0.3f, 0.3f, 1f));
        inputStyle.padding = new RectOffset(5, 5, 5, 5);

        stylesInitialized = true;
    }

    // Helper to create colored texture
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
