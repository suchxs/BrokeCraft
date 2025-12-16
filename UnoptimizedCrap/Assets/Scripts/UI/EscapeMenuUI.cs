using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// In-game escape menu with Settings, Return to Main Menu, and Resume.
/// </summary>
[DisallowMultipleComponent]
public class EscapeMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MainMenuUI settingsProvider; // reuse settings panel/layout
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Layout")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
    [SerializeField] private Font font;

    private Canvas canvas;
    private GameObject panel;
    private GameObject settingsPanelInstance;
    private InputAction escapeAction;
    private bool isOpen;

    private void Awake()
    {
        AppSettings.Load();
        BuildUI();
        BuildInput();
        HideMenu();
    }

    private void BuildInput()
    {
        escapeAction = new InputAction("Escape", binding: "<Keyboard>/escape");
        escapeAction.performed += _ => ToggleMenu();
        escapeAction.Enable();
    }

    private void OnDestroy()
    {
        if (escapeAction != null)
        {
            escapeAction.Disable();
            escapeAction.Dispose();
        }
    }

    private void BuildUI()
    {
        canvas = CreateCanvas();
        panel = CreatePanel(canvas.transform);

        var settingsButton = CreateButton(panel.transform, "Settings", new Vector2(0f, 40f), OnSettings);
        var menuButton = CreateButton(panel.transform, "Return to Main Menu", new Vector2(0f, -20f), OnReturnToMenu);
        var resumeButton = CreateButton(panel.transform, "Resume", new Vector2(0f, -80f), HideMenu);
    }

    private Canvas CreateCanvas()
    {
        var go = new GameObject("EscapeMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 999;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return c;
    }

    private GameObject CreatePanel(Transform parent)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520, 360);

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.7f);
        return go;
    }

    private Button CreateButton(Transform parent, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 50);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);
        var colors = btn.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0.55f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.65f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
        btn.colors = colors;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = rt.sizeDelta;

        var txt = textGo.GetComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 22;
        txt.color = Color.white;
        txt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.raycastTarget = false;

        return btn;
    }

    private void OnSettings()
    {
        if (settingsProvider == null)
        {
            Debug.LogWarning("EscapeMenuUI: No settings provider assigned.");
            return;
        }

        if (settingsPanelInstance == null)
        {
            settingsPanelInstance = settingsProvider.CreateSettingsPanel(canvas.transform);
        }

        settingsPanelInstance.SetActive(true);
    }

    private void OnReturnToMenu()
    {
        AppSettings.Save();
        SceneManager.LoadScene(mainMenuScene, LoadSceneMode.Single);
    }

    private void ToggleMenu()
    {
        if (isOpen)
        {
            HideMenu();
        }
        else
        {
            ShowMenu();
        }
    }

    private void ShowMenu()
    {
        isOpen = true;
        canvas.enabled = true;
        if (settingsPanelInstance != null)
        {
            settingsPanelInstance.SetActive(false);
        }
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void HideMenu()
    {
        isOpen = false;
        canvas.enabled = false;
        if (settingsPanelInstance != null)
        {
            settingsPanelInstance.SetActive(false);
        }
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
