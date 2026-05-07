using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the minimap UI widget and wires it to MinimapRenderer's output texture.
/// No camera or RenderTexture needed — the renderer writes directly to a Texture2D.
/// </summary>
[RequireComponent(typeof(MinimapRenderer))]
public class MinimapManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("Camera whose yaw rotates the minimap. Usually the main camera.")]
    [SerializeField] private Transform cameraTransform;

    [SerializeField] private Canvas canvas;

    [Header("World")]
    [Tooltip("World-unit radius visible on the minimap.")]
    [SerializeField] private float minimapWorldRadius = 60f;

    [Header("Dot Sizes (world units)")]
    [SerializeField] private float playerDotSize = 3f;
    [SerializeField] private float enemyDotSize  = 2f;
    [SerializeField] private float bulletDotSize = 1f;

    [Header("UI")]
    [SerializeField] private float  minimapUISize   = 180f;
    [SerializeField] private float  minimapUIMargin = 16f;
    [Tooltip("Circle sprite for the mask. Leave empty to generate procedurally.")]
    [SerializeField] private Sprite circleMaskSprite;

    private MinimapRenderer _renderer;
    private RectTransform   _minimapViewRect;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        _renderer = GetComponent<MinimapRenderer>();

        if (circleMaskSprite == null)
            circleMaskSprite = GenerateCircleSprite();

        BuildUI();

        _renderer.Setup(playerTransform, cameraTransform, minimapWorldRadius,
                        playerDotSize, enemyDotSize, bulletDotSize);
    }

    // =========================================================
    // UI
    // =========================================================

    private void BuildUI()
    {
        if(canvas==null)
        canvas = FindOrCreateOverlayCanvas();
        
        // Root — anchored to top-right corner
        var rootGo = new GameObject("MinimapRoot");
        rootGo.transform.SetParent(canvas.transform, false);
        var rootRt = rootGo.AddComponent<RectTransform>();
        rootRt.anchorMin        = Vector2.one;
        rootRt.anchorMax        = Vector2.one;
        rootRt.pivot            = Vector2.one;
        rootRt.anchoredPosition = new Vector2(-minimapUIMargin, -minimapUIMargin);
        rootRt.sizeDelta        = new Vector2(minimapUISize, minimapUISize);

        // Border — dark circle slightly behind
        AddImage(rootGo.transform, "MinimapBorder",
            circleMaskSprite, new Color(0.06f, 0.06f, 0.06f, 1f), expand: 4f);

        // Mask — clips children to the circle
        var maskGo  = new GameObject("MinimapMask");
        maskGo.transform.SetParent(rootGo.transform, false);
        FillParent(maskGo.AddComponent<RectTransform>(), 0f);
        var maskImg         = maskGo.AddComponent<Image>();
        maskImg.sprite      = circleMaskSprite;
        maskImg.type        = Image.Type.Simple;
        maskImg.raycastTarget = false;
        maskGo.AddComponent<Mask>().showMaskGraphic = false;

        // RawImage — displays the Texture2D written by MinimapRenderer
        var viewGo = new GameObject("MinimapView");
        viewGo.transform.SetParent(maskGo.transform, false);
        _minimapViewRect  = viewGo.AddComponent<RectTransform>();
        FillParent(_minimapViewRect, 0f);
        var raw           = viewGo.AddComponent<RawImage>();
        raw.texture       = _renderer.OutputTexture;
        raw.raycastTarget = false;
    }

    private void Update()
    {
        if (_minimapViewRect != null)
            _minimapViewRect.localRotation = Quaternion.Euler(0f, 0f, _renderer.CurrentYaw);
    }

    private static void AddImage(Transform parent, string name, Sprite sprite, Color color, float expand)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        FillParent(go.AddComponent<RectTransform>(), expand);
        var img           = go.AddComponent<Image>();
        img.sprite        = sprite;
        img.color         = color;
        img.type          = Image.Type.Simple;
        img.raycastTarget = false;
    }

    private static void FillParent(RectTransform rt, float expand)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(-expand, -expand);
        rt.offsetMax = new Vector2( expand,  expand);
    }

    private static Canvas FindOrCreateOverlayCanvas()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;

        var go     = new GameObject("MinimapCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        return canvas;
    }

    // =========================================================
    // CIRCLE SPRITE
    // =========================================================

    private static Sprite GenerateCircleSprite(int size = 128)
    {
        var   tex      = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center   = size * 0.5f;
        float radius   = center - 1f;
        var   pixels   = new Color32[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx  = x - center + 0.5f;
            float dy  = y - center + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            byte  a    = (byte)(Mathf.Clamp01(radius - dist + 0.5f) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, a);
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
