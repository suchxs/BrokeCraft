using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds a simple main menu UI at runtime.
/// Play -> async load of the main scene with a small "Optimizing Chunks" overlay.
/// Settings -> placeholder.
/// Exit -> quits the app.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuUI : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private Sprite backgroundSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
    [SerializeField] private int titleFontSize = 64;
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private int buttonWidth = 280;
    [SerializeField] private int buttonHeight = 60;
    [SerializeField] private int buttonSpacing = 16;
    [SerializeField] private Font font;

    [Header("Audio")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private float musicVolume = 0.45f;

    private Canvas canvas;
    private GameObject loadingOverlay;
    private GameObject buttonContainer;
    private GameObject titleObject;
    private GameObject settingsPanel;

    private void Awake()
    {
        if (backgroundSprite == null)
        {
            backgroundSprite = Resources.Load<Sprite>("UI/background");
        }
        if (musicClip == null)
        {
            musicClip = Resources.Load<AudioClip>("Music/Music");
        }

        BuildUI();
        EnsureCameraData();
        PlayMusic();
        AppSettings.Load();
    }

    private void BuildUI()
    {
        canvas = CreateCanvas();
        CreateBackground(canvas.transform);
        titleObject = CreateTitle(canvas.transform);
        buttonContainer = CreateButtons(canvas.transform);
        settingsPanel = CreateSettingsPanel(canvas.transform);
        EnsureEventSystem();
    }

    private Canvas CreateCanvas()
    {
        var go = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.pixelPerfect = true;
        c.sortingOrder = 50;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return c;
    }

    private void CreateBackground(Transform parent)
    {
        var go = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = backgroundSprite;
        img.color = Color.white;
        img.raycastTarget = false;
        img.preserveAspect = true;
    }

    private GameObject CreateButtons(Transform parent)
    {
        var container = new GameObject("Buttons", typeof(RectTransform));
        container.transform.SetParent(parent, false);
        var rt = container.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(buttonWidth, buttonHeight * 3 + buttonSpacing * 2);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -40f);

        CreateButton(container.transform, "Play", 0, OnPlayClicked);
        CreateButton(container.transform, "Settings", 1, OnSettingsClicked);
        CreateButton(container.transform, "Exit", 2, OnExitClicked);
        return container;
    }

    private Button CreateButton(Transform parent, string label, int index, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -index * (buttonHeight + buttonSpacing));

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.5f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var colors = btn.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0.55f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.65f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = rt.sizeDelta;
        trt.anchoredPosition = Vector2.zero;

        var txt = textGo.GetComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 24;
        txt.color = Color.white;
        txt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.raycastTarget = false;

        return btn;
    }

    private GameObject CreateTitle(Transform parent)
    {
        var go = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 160f);
        rt.sizeDelta = new Vector2(800, 120);

        var txt = go.GetComponent<Text>();
        txt.text = "BrokeCraft v1.0";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = titleFontSize;
        txt.color = titleColor;
        txt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.raycastTarget = false;
        return go;
    }

    private void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
        {
            return;
        }

        var go = new GameObject(
            "EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)
        );
        go.transform.SetParent(transform, false);
    }

    private void OnPlayClicked()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        StartCoroutine(LoadGame());
    }

    private void OnSettingsClicked()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            if (buttonContainer != null) buttonContainer.SetActive(false);
            if (titleObject != null) titleObject.SetActive(false);
        }
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadGame()
    {
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(false);
        }
        if (titleObject != null)
        {
            titleObject.SetActive(false);
        }
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        ShowLoadingOverlay();
        AsyncOperation op = SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Single);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            if (op.progress >= 0.9f)
            {
                op.allowSceneActivation = true;
            }
            yield return null;
        }
    }

    private void ShowLoadingOverlay()
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.SetActive(true);
            return;
        }

        loadingOverlay = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        loadingOverlay.transform.SetParent(canvas.transform, false);

        var rt = loadingOverlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = loadingOverlay.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        img.raycastTarget = true;

        var labelGo = new GameObject("LoadingLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(loadingOverlay.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(400, 80);

        var txt = labelGo.GetComponent<Text>();
        txt.text = "Optimizing Chunks...";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 28;
        txt.color = Color.white;
        txt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.raycastTarget = false;
    }

    public GameObject CreateSettingsPanel(Transform parent)
    {
        var panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(520, 420);
        prt.anchoredPosition = new Vector2(0f, 20f);

        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        float yStart = 150f;
        float yStep = 110f;

        CreateSettingsLabel(panel.transform, "Resolution", 0, yStart);
        var resDropdown = CreateDropdown(panel.transform, 0, yStart - 45f);
        PopulateResolutions(resDropdown);
        resDropdown.onValueChanged.AddListener(index =>
        {
            ApplyResolution(index);
        });

        CreateSettingsLabel(panel.transform, "Audio Volume", 1, yStart - yStep);
        var volumeSlider = CreateSlider(panel.transform, 1, yStart - yStep - 45f, 0f, 1f, AppSettings.AudioVolume);
        volumeSlider.onValueChanged.AddListener(value =>
        {
            musicVolume = value;
            PersistentMusicPlayer.EnsureExists(musicClip, musicVolume);
            AppSettings.SetAudioVolume(musicVolume);
            AppSettings.Save();
        });

        CreateSettingsLabel(panel.transform, "Render Distance (H/V)", 2, yStart - yStep * 2);
        var renderDropdown = CreateDropdown(panel.transform, 2, yStart - yStep * 2 - 45f);
        PopulateRenderDistances(renderDropdown);
        renderDropdown.onValueChanged.AddListener(index =>
        {
            ApplyRenderDistance(index);
        });

        Button backBtn = CreateButton(panel.transform, "Back", 3, () =>
        {
            panel.SetActive(false);
            if (buttonContainer != null) buttonContainer.SetActive(true);
            if (titleObject != null) titleObject.SetActive(true);
            AppSettings.Save();
        });
        backBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -yStart - 40f);

        panel.SetActive(false);
        return panel;
    }

    private void CreateSettingsLabel(Transform parent, string text, int order, float y)
    {
        var go = new GameObject(text + "Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(400, 28);

        var txt = go.GetComponent<Text>();
        txt.text = text;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 22;
        txt.color = Color.white;
        txt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.raycastTarget = false;
    }

    private Dropdown CreateDropdown(Transform parent, int order, float y)
    {
        var root = new GameObject("Dropdown" + order, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Dropdown));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360, 40);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        // Caption label
        var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        label.transform.SetParent(root.transform, false);
        var labelRT = label.GetComponent<RectTransform>();
        labelRT.anchorMin = labelRT.anchorMax = new Vector2(0f, 0.5f);
        labelRT.pivot = new Vector2(0f, 0.5f);
        labelRT.sizeDelta = new Vector2(300, 30);
        labelRT.anchoredPosition = new Vector2(12f, 0f);

        var labelTxt = label.GetComponent<Text>();
        labelTxt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelTxt.fontSize = 18;
        labelTxt.color = Color.white;
        labelTxt.alignment = TextAnchor.MiddleLeft;
        labelTxt.text = "Select";

        // Arrow
        var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        arrow.transform.SetParent(root.transform, false);
        var arrowRT = arrow.GetComponent<RectTransform>();
        arrowRT.anchorMin = arrowRT.anchorMax = new Vector2(1f, 0.5f);
        arrowRT.sizeDelta = new Vector2(20f, 20f);
        arrowRT.anchoredPosition = new Vector2(-15f, 0f);
        var arrowTxt = arrow.GetComponent<Text>();
        arrowTxt.text = "▼";
        arrowTxt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        arrowTxt.fontSize = 16;
        arrowTxt.color = Color.white;
        arrowTxt.alignment = TextAnchor.MiddleCenter;

        // Template root
        var template = new GameObject("Template", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(root.transform, false);
        var templateRT = template.GetComponent<RectTransform>();
        templateRT.anchorMin = new Vector2(0f, 0f);
        templateRT.anchorMax = new Vector2(1f, 0f);
        templateRT.pivot = new Vector2(0.5f, 1f);
        templateRT.sizeDelta = new Vector2(0f, 180f);
        template.SetActive(false);

        var templateImg = template.GetComponent<Image>();
        templateImg.color = new Color(0f, 0f, 0f, 0.85f);

        var scrollRect = template.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Viewport
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        var viewportRT = viewport.GetComponent<RectTransform>();
        viewportRT.anchorMin = new Vector2(0f, 0f);
        viewportRT.anchorMax = new Vector2(1f, 1f);
        viewportRT.sizeDelta = Vector2.zero;
        viewportRT.pivot = new Vector2(0.5f, 0.5f);

        var viewportImg = viewport.GetComponent<Image>();
        viewportImg.color = new Color(0f, 0f, 0f, 0.75f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewportRT;

        // Content
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 0f);
        scrollRect.content = contentRT;

        // Item
        var item = new GameObject("Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Toggle), typeof(LayoutElement));
        item.transform.SetParent(content.transform, false);
        var itemRT = item.GetComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0f, 0.5f);
        itemRT.anchorMax = new Vector2(1f, 0.5f);
        itemRT.sizeDelta = new Vector2(0f, 30f);
        itemRT.anchoredPosition = Vector2.zero;
        item.GetComponent<LayoutElement>().preferredHeight = 30f;

        var itemToggle = item.GetComponent<Toggle>();

        var itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        itemBg.transform.SetParent(item.transform, false);
        var itemBgRT = itemBg.GetComponent<RectTransform>();
        itemBgRT.anchorMin = new Vector2(0f, 0f);
        itemBgRT.anchorMax = new Vector2(1f, 1f);
        itemBgRT.sizeDelta = Vector2.zero;
        var itemBgImg = itemBg.GetComponent<Image>();
        itemBgImg.color = new Color(1f, 1f, 1f, 0.05f);

        var checkmark = new GameObject("Item Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        checkmark.transform.SetParent(item.transform, false);
        var checkRT = checkmark.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0f, 0.5f);
        checkRT.anchorMax = new Vector2(0f, 0.5f);
        checkRT.sizeDelta = new Vector2(20f, 20f);
        checkRT.anchoredPosition = new Vector2(15f, 0f);
        var checkImg = checkmark.GetComponent<Image>();
        checkImg.color = new Color(0.2f, 0.8f, 1f, 0.9f);

        var itemLabelGO = new GameObject("Item Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        itemLabelGO.transform.SetParent(item.transform, false);
        var itemLabelRT = itemLabelGO.GetComponent<RectTransform>();
        itemLabelRT.anchorMin = new Vector2(0f, 0f);
        itemLabelRT.anchorMax = new Vector2(1f, 1f);
        itemLabelRT.offsetMin = new Vector2(40f, 0f);
        itemLabelRT.offsetMax = new Vector2(-10f, 0f);

        var itemLabel = itemLabelGO.GetComponent<Text>();
        itemLabel.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        itemLabel.fontSize = 18;
        itemLabel.color = Color.white;
        itemLabel.alignment = TextAnchor.MiddleLeft;
        itemLabel.raycastTarget = false;

        itemToggle.targetGraphic = itemBgImg;
        itemToggle.graphic = checkImg;
        itemToggle.isOn = false;

        var layoutGroup = content.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.spacing = 2f;

        var fitter = content.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        // Scrollbar (optional)
        var scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        scrollbarGO.transform.SetParent(template.transform, false);
        var scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 1f);
        scrollbarRT.sizeDelta = new Vector2(12f, 0f);
        scrollbarRT.offsetMin = new Vector2(-12f, 0f);
        scrollbarRT.offsetMax = Vector2.zero;

        var scrollbarImg = scrollbarGO.GetComponent<Image>();
        scrollbarImg.color = new Color(1f, 1f, 1f, 0.2f);
        var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -3f;

        var dd = root.GetComponent<Dropdown>();
        dd.template = templateRT;
        dd.captionText = labelTxt;
        dd.itemText = itemLabel;
        dd.targetGraphic = bg;

        return dd;
    }

    private Text CreateDropdownText(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(340, 30);

        var txt = go.GetComponent<Text>();
        txt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 18;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.raycastTarget = false;
        return txt;
    }

    private Slider CreateSlider(Transform parent, int order, float y, float min, float max, float value)
    {
        var go = new GameObject("Slider" + order, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360, 30);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        var slider = go.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = new Vector2(10f, 0f);
        fillAreaRt.offsetMax = new Vector2(-10f, 0f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = new Color(0.2f, 0.6f, 1f, 0.8f);

        var handleSlideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlideArea.transform.SetParent(go.transform, false);
        var handleAreaRt = handleSlideArea.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = new Vector2(0f, 0f);
        handleAreaRt.anchorMax = new Vector2(1f, 1f);
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(handleSlideArea.transform, false);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 30f);
        var handleImg = handle.GetComponent<Image>();
        handleImg.color = new Color(1f, 1f, 1f, 0.9f);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }


    private void PopulateResolutions(Dropdown dropdown)
    {
        dropdown.options.Clear();
        Resolution[] resolutions = Screen.resolutions;
        int current = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            var r = resolutions[i];
            string label = $"{r.width} x {r.height}";
            dropdown.options.Add(new Dropdown.OptionData(label));

            if ((AppSettings.ResolutionWidth == 0 && i == resolutions.Length - 1) ||
                (r.width == AppSettings.ResolutionWidth && r.height == AppSettings.ResolutionHeight))
            {
                current = i;
            }
        }

        dropdown.value = current;
        dropdown.RefreshShownValue();
    }

    private void ApplyResolution(int dropdownIndex)
    {
        Resolution[] resolutions = Screen.resolutions;
        if (dropdownIndex < 0 || dropdownIndex >= resolutions.Length)
        {
            return;
        }

        Resolution r = resolutions[dropdownIndex];
        Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
        AppSettings.SetResolution(r.width, r.height);
        AppSettings.Save();
    }

    private void PopulateRenderDistances(Dropdown dropdown)
    {
        dropdown.options.Clear();
        (int h, int v)[] options = new (int, int)[]
        {
            (24, 12),
            (32, 16),
            (40, 20),
            (48, 24)
        };

        int current = 0;
        for (int i = 0; i < options.Length; i++)
        {
            var o = options[i];
            dropdown.options.Add(new Dropdown.OptionData($"{o.h} / {o.v} chunks"));

            if (AppSettings.HorizontalViewDistance == o.h && AppSettings.VerticalViewDistance == o.v)
            {
                current = i;
            }
        }

        dropdown.value = current;
        dropdown.RefreshShownValue();
    }

    private void ApplyRenderDistance(int dropdownIndex)
    {
        (int h, int v)[] options = new (int, int)[]
        {
            (24, 12),
            (32, 16),
            (40, 20),
            (48, 24)
        };

        if (dropdownIndex < 0 || dropdownIndex >= options.Length)
        {
            return;
        }

        var o = options[dropdownIndex];
        AppSettings.SetViewDistances(o.h, o.v);
        AppSettings.Save();
    }

    private void EnsureCameraData()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                return;
            }
        }

        // Add URP additional camera data if available to silence warnings
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null && cam.GetComponent(urpType) == null)
        {
            cam.gameObject.AddComponent(urpType);
        }
    }

    private void PlayMusic()
    {
        if (musicClip == null)
        {
            return;
        }

        if (AppSettings.AudioVolume > 0f)
        {
            musicVolume = AppSettings.AudioVolume;
        }

        PersistentMusicPlayer.EnsureExists(musicClip, musicVolume);
        AppSettings.SetAudioVolume(musicVolume);
    }
}
