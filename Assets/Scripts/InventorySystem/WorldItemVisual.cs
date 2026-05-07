using UnityEngine;
using TMPro;

public class WorldItemVisual : MonoBehaviour
{
    [Header("Levitation")]
    [SerializeField] float bobHeight = 0.15f;
    [SerializeField] float bobSpeed = 1.8f;
    [SerializeField] float groundOffset = 0.6f;

    [Header("Billboard")]
    [SerializeField] bool faceCamera = true;

    [Header("Size")]
    [SerializeField] float fitPadding = 0.85f;

    [Header("Item Colors")]
    public Color weaponColor  = new Color(1f, 0.85f, 0f);
    public Color ammoColor    = new Color(0.7f, 0.7f, 0.7f);
    public Color healthColor  = new Color(0.3f, 0.9f, 0.3f);
    public Color shieldColor  = new Color(0.3f, 0.6f, 1f);
    public Color defaultColor = Color.white;

    SpriteRenderer _sr;
    Transform      _spriteTransform;
    Vector3        _originLocalPos;   // ✅ local, no world
    Transform      _cam;
    float          _bobOffset;
    Transform      _labelRoot;

    void Awake()
    {
        // ✅ FIX: guardamos el offset en LOCAL space para que siga al parent
        _originLocalPos = transform.localPosition + Vector3.up * groundOffset;
        _bobOffset = Random.Range(0f, Mathf.PI * 2f);
        _cam = Camera.main?.transform;

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(transform, false);
        _spriteTransform = spriteGo.transform;
        _sr = spriteGo.AddComponent<SpriteRenderer>();
    }

    public void Setup(itemSO item, int amount)
    {
        _sr.sprite = item.icon;
        _sr.color  = Color.white;

        FitSprite();
        BuildLabel(item, amount);
    }

    // ── Color por tipo ─────────────────────────────────────────────────────
    Color ItemColor(itemSO item)
    {
        if (item is WeaponSO)                                         return weaponColor;
        if (item is AmmoSO)                                           return ammoColor;
        if (item is HealthSO h  && h.healthType  == HealthType.Health) return healthColor;
        if (item is HealthSO h2 && h2.healthType == HealthType.Shield) return shieldColor;
        return defaultColor;
    }

    // ── Collider size helper ───────────────────────────────────────────────
    float GetColliderSize()
    {
        var col = GetComponent<Collider>();
        if (col == null) return 1f;
        Vector3 s = col.bounds.size;
        return Mathf.Min(s.x, s.y, s.z);
    }

    // ── Escala solo el hijo sprite ─────────────────────────────────────────
    void FitSprite()
    {
        if (_sr.sprite == null) return;
        float targetSize = GetColliderSize() * fitPadding;
        float spriteMax  = Mathf.Max(_sr.sprite.bounds.size.x, _sr.sprite.bounds.size.y);
        float scale      = targetSize / spriteMax;
        _spriteTransform.localScale = Vector3.one * scale;
    }

    // ── Label world-space ──────────────────────────────────────────────────
    void BuildLabel(itemSO item, int amount)
    {
        if (_labelRoot != null) Destroy(_labelRoot.gameObject);

        float colSize   = GetColliderSize();
        float textScale = colSize * 0.12f;
        float rowHeight = colSize * 0.18f;
        float topOffset = colSize * fitPadding * 0.5f + rowHeight * (amount > 1 ? 2f : 1f);

        var canvasGo = new GameObject("ItemLabel");
        canvasGo.transform.SetParent(transform, false);
        _labelRoot = canvasGo.transform;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode      = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Default";

        // Name — top
        var nameGo = CreateWorldLabel(item.name, ItemColor(item), FontStyles.Bold);
        nameGo.transform.SetParent(canvasGo.transform, false);
        nameGo.transform.localPosition = new Vector3(0f, topOffset, 0f);
        nameGo.transform.localScale    = Vector3.one * textScale;

        // Amount — below name
        if (amount > 1)
        {
            var amountGo = CreateWorldLabel($"x{amount}", Color.white, FontStyles.Normal);
            amountGo.transform.SetParent(canvasGo.transform, false);
            amountGo.transform.localPosition = new Vector3(0f, topOffset - rowHeight, 0f);
            amountGo.transform.localScale    = Vector3.one * textScale * 0.85f;
        }
    }

    GameObject CreateWorldLabel(string text, Color color, FontStyles style)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt  = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 1f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = 1.6f;
        tmp.fontStyle          = style;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.color              = color;
        tmp.raycastTarget      = false;
        tmp.enableWordWrapping = false;

        tmp.fontSharedMaterial = new Material(tmp.fontSharedMaterial);
        tmp.fontSharedMaterial.EnableKeyword("UNDERLAY_ON");
        tmp.fontSharedMaterial.SetColor("_UnderlayColor",      new Color(0f, 0f, 0f, 0.8f));
        tmp.fontSharedMaterial.SetFloat("_UnderlayOffsetX",    0.5f);
        tmp.fontSharedMaterial.SetFloat("_UnderlayOffsetY",   -0.5f);
        tmp.fontSharedMaterial.SetFloat("_UnderlaySoftness",   0.2f);

        return go;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * bobSpeed + _bobOffset) * bobHeight;

        // ✅ FIX: localPosition en vez de position → sigue al parent
        transform.localPosition = _originLocalPos + Vector3.up * y;

        // Billboard sigue usando world-space, está correcto
        if (faceCamera && _cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
    }
}
