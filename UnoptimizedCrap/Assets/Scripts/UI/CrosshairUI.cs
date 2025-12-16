using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a simple crosshair in the center of the screen using the provided texture.
/// </summary>
[DisallowMultipleComponent]
public class CrosshairUI : MonoBehaviour
{
    [Header("Crosshair")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 crosshairSize = new Vector2(24f, 24f);
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private string resourcePath = "UI/crosshair";

    private const string CanvasName = "CrosshairCanvas";
    private const string ImageName = "Crosshair";
    private Image crosshairImage;

    private void Start()
    {
        BuildCrosshair();
    }

    private void BuildCrosshair()
    {
        if (crosshairImage != null)
        {
            return;
        }

        Canvas canvas = GetOrCreateCanvas();
        crosshairImage = CreateCrosshairImage(canvas.transform);
    }

    private Canvas GetOrCreateCanvas()
    {
        Transform existing = transform.Find(CanvasName);
        if (existing != null)
        {
            Canvas existingCanvas = existing.GetComponent<Canvas>();
            if (existingCanvas != null)
            {
                return existingCanvas;
            }
        }

        GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private Image CreateCrosshairImage(Transform parent)
    {
        Sprite sprite = LoadSprite();

        GameObject imageObject = new GameObject(ImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        Vector2 size = crosshairSize;
        if (size.sqrMagnitude <= 0.001f)
        {
            size = new Vector2(sprite.rect.width, sprite.rect.height);
        }
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = crosshairColor;
        image.raycastTarget = false;
        image.preserveAspect = true;

        return image;
    }

    private Sprite LoadSprite()
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            texture = CreateFallbackTexture();
        }

        EnsureTextureSettings(texture);

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
    }

    private static Texture2D CreateFallbackTexture()
    {
        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        int mid = size / 2;
        for (int i = mid - 2; i <= mid + 2; i++)
        {
            texture.SetPixel(mid, i, white);
            texture.SetPixel(i, mid, white);
        }

        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        return texture;
    }

    private static void EnsureTextureSettings(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
    }
}
