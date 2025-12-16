using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple creative inventory selector for a small palette of blocks.
/// Uses number keys or mouse scroll to choose the active block type.
/// </summary>
[DisallowMultipleComponent]
public class BlockInventory : MonoBehaviour
{
    [Header("Palette")]
    [SerializeField] private BlockType[] palette = new[]
    {
        BlockType.Grass,
        BlockType.Dirt,
        BlockType.Stone,
        BlockType.Sand,
        BlockType.Bedrock
    };

    [Header("UI")]
    [SerializeField] private bool showHotbar = true;
    [SerializeField] private int hotbarSlotSize = 60;
    [SerializeField] private int hotbarPadding = 8;
    [SerializeField] private Color slotColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color selectedColor = new Color(1f, 1f, 1f, 0.65f);
    [SerializeField] private Color textColor = Color.white;

    private int selectedIndex;

    public BlockType SelectedBlock => palette != null && palette.Length > 0
        ? palette[Mathf.Clamp(selectedIndex, 0, palette.Length - 1)]
        : BlockType.Air;

    private void Update()
    {
        if (palette == null || palette.Length == 0)
        {
            return;
        }

        HandleScrollInput();
        HandleNumberInput();
    }

    private void HandleScrollInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                int dir = scroll > 0f ? -1 : 1;
                selectedIndex = WrapIndex(selectedIndex + dir);
            }
        }
#endif
    }

    private void HandleNumberInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return;
        }

        for (int i = 0; i < palette.Length; i++)
        {
            int keyNumber = i + 1;
            Key key = (Key)((int)Key.Digit1 + i);
            if (Keyboard.current[key].wasPressedThisFrame)
            {
                selectedIndex = i;
                return;
            }
        }
#endif
    }

    private int WrapIndex(int value)
    {
        if (palette == null || palette.Length == 0)
        {
            return 0;
        }

        int len = palette.Length;
        if (len == 0)
        {
            return 0;
        }
        value %= len;
        if (value < 0)
        {
            value += len;
        }
        return value;
    }

    private void OnGUI()
    {
        if (!showHotbar || palette == null || palette.Length == 0)
        {
            return;
        }

        int count = palette.Length;
        int width = count * hotbarSlotSize + (count - 1) * hotbarPadding;
        int height = hotbarSlotSize;
        int x = (Screen.width - width) / 2;
        int y = Screen.height - height - 24;

        for (int i = 0; i < count; i++)
        {
            Rect slotRect = new Rect(
                x + i * (hotbarSlotSize + hotbarPadding),
                y,
                hotbarSlotSize,
                hotbarSlotSize
            );

            Color bg = i == selectedIndex ? selectedColor : slotColor;
            DrawRect(slotRect, bg);

            string label = palette[i].ToString();
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = textColor }
            };
            GUI.Label(slotRect, $"{i + 1}\n{label}", style);
        }
    }

    private static Texture2D _whiteTex;
    private static Texture2D WhiteTex
    {
        get
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            return _whiteTex;
        }
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, WhiteTex);
        GUI.color = prev;
    }
}
