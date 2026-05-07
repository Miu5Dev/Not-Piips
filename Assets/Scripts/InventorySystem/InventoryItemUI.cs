using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItemUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    public itemSO     Item       { get; private set; }
    public Vector2Int Origin     { get; private set; }
    public bool       Rotated    { get; private set; }
    public bool       InWildcard { get; private set; }

    public static InventoryItemUI HoveredItem { get; private set; }

    float _cellSize, _cellSpacing;
    Image _bg;
    Image _icon;
    RectTransform _iconRt;
    readonly System.Collections.Generic.List<Image> _cornerImages = new();

    static readonly Color BgEmpty     = new Color(1f, 1f, 1f, 0.12f);
    static readonly Color CornerEquip = new Color(0.2f, 1f, 0.3f, 1f);

    // ── Stack & ammo (label arriba-derecha) ───────────────────────────────
    int             _stackCount  = 1;
    int             _storedAmmo  = -1; // -1 = no inicializado (solo armas)
    TextMeshProUGUI _topRightLabel;

    public int StackCount  => _stackCount;
    public int StoredAmmo  => _storedAmmo;

    public void SetStoredAmmo(int ammo)
    {
        _storedAmmo = ammo;
        RefreshTopRightLabel();
    }

    public void InitStack(int count)
    {
        _stackCount = Mathf.Max(1, count);
        RefreshTopRightLabel();
    }

    public bool AddToStack(int amount, int maxStack)
    {
        if (_stackCount >= maxStack) return false;
        _stackCount = Mathf.Min(_stackCount + amount, maxStack);
        RefreshTopRightLabel();
        return true;
    }

    public void RemoveFromStack(int amount)
    {
        _stackCount = Mathf.Max(0, _stackCount - amount);
        RefreshTopRightLabel();
    }

    void CreateTopRightLabel()
    {
        var go = new GameObject("TopRightLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        var rt          = go.GetComponent<RectTransform>();
        rt.anchorMin    = rt.anchorMax = new Vector2(1f, 1f); // arriba-derecha
        rt.pivot        = new Vector2(1f, 1f);
        // pequeño padding interior respecto al borde del item
        rt.anchoredPosition = new Vector2(-_cellSize * 0.05f, -_cellSize * 0.05f);
        rt.sizeDelta        = new Vector2(_cellSize * 1.8f, _cellSize * 0.38f);

        _topRightLabel               = go.GetComponent<TextMeshProUGUI>();
        _topRightLabel.fontSize      = _cellSize * 0.28f;
        _topRightLabel.fontStyle     = FontStyles.Bold;
        _topRightLabel.color         = Color.white;
        _topRightLabel.alignment     = TextAlignmentOptions.TopRight;
        _topRightLabel.raycastTarget = false;
        go.SetActive(false);
    }

    void RefreshTopRightLabel()
    {
        if (_topRightLabel == null) return;

        if (Item == null) { _topRightLabel.gameObject.SetActive(false); return; }

        if (Item.isStackable && _stackCount > 1)
        {
            // Munición / items apilables → mostrar cantidad
            _topRightLabel.text = $"×{_stackCount}";
            _topRightLabel.gameObject.SetActive(true);
        }
        else if (Item is WeaponSO weaponSO && _storedAmmo >= 0)
        {
            // Arma → mostrar balas guardadas sobre el máximo
            _topRightLabel.text = $"{_storedAmmo}/{weaponSO.maxMagazineSize}";
            _topRightLabel.gameObject.SetActive(true);
        }
        else
        {
            _topRightLabel.gameObject.SetActive(false);
        }
    }
    // ── Fin label arriba-derecha ───────────────────────────────────────────

    public void Init(itemSO item, bool rotated, float cellSize, float cellSpacing)
    {
        Item         = item;
        Rotated      = rotated;
        _cellSize    = cellSize;
        _cellSpacing = cellSpacing;

        // Armas se inicializan con cargador completo
        if (item is WeaponSO w)
            _storedAmmo = w.maxMagazineSize;

        _bg = GetComponent<Image>();
        _bg.sprite        = null;
        _bg.raycastTarget = true;
        _bg.color         = BgEmpty;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(transform, false);
        _iconRt = iconGo.GetComponent<RectTransform>();
        _iconRt.anchorMin = _iconRt.anchorMax = _iconRt.pivot = new Vector2(0.5f, 0.5f);
        _iconRt.anchoredPosition = Vector2.zero;

        _icon = iconGo.GetComponent<Image>();
        _icon.sprite         = item.icon;
        _icon.preserveAspect = true;
        _icon.raycastTarget  = false;

        float arm   = cellSize * 0.35f;
        float thick = Mathf.Max(2f, cellSize * 0.04f);
        SpawnCorner(new Vector2(0f, 1f), arm, thick);
        SpawnCorner(new Vector2(1f, 1f), arm, thick);
        SpawnCorner(new Vector2(0f, 0f), arm, thick);
        SpawnCorner(new Vector2(1f, 0f), arm, thick);

        SpawnLabel(item.name, cellSize, thick);
        CreateTopRightLabel();
        RefreshTopRightLabel();
    }

    public void Reposition(Vector2Int origin, bool rotated)
    {
        Origin     = origin;
        Rotated    = rotated;
        InWildcard = false;

        int w = rotated ? Item.size.y : Item.size.x;
        int h = rotated ? Item.size.x : Item.size.y;

        float step = _cellSize + _cellSpacing;
        var rt = GetComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(w * _cellSize + (w - 1) * _cellSpacing,
                                          h * _cellSize + (h - 1) * _cellSpacing);
        rt.anchoredPosition = new Vector2(origin.x * step, -origin.y * step);

        UpdateIconForRotation();
        ResetColor();
    }

    public void FollowScreen(Vector2 screenPos)
    {
        if (InventoryGridUI.Instance == null) return;
        var panelRt    = InventoryGridUI.Instance.PanelRt;
        var panelLocal = InventoryGridUI.Instance.ScreenToPanel(screenPos);

        int w = Rotated ? Item.size.y : Item.size.x;
        int h = Rotated ? Item.size.x : Item.size.y;
        var rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w * _cellSize + (w - 1) * _cellSpacing,
                                   h * _cellSize + (h - 1) * _cellSpacing);
        rt.anchoredPosition = new Vector2(
            panelLocal.x - panelRt.rect.xMin,
            panelLocal.y - panelRt.rect.yMax);

        UpdateIconForRotation();
    }

    public void SetDragColor(Color c) => _bg.color = c;
    public void ResetColor()          => _bg.color = BgEmpty;

    void UpdateIconForRotation()
    {
        if (_iconRt == null) return;
        int natW = Item.size.x;
        int natH = Item.size.y;
        _iconRt.sizeDelta = new Vector2(
            natW * _cellSize + (natW - 1) * _cellSpacing,
            natH * _cellSize + (natH - 1) * _cellSpacing);
        _iconRt.localRotation = Rotated ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
    }

    public void SetWildcardMode(RectTransform wildcardSlot)
    {
        InWildcard = true;
        Rotated    = false;
        transform.SetParent(wildcardSlot, false);

        var rt = GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;

        _iconRt.sizeDelta     = Vector2.zero;
        _iconRt.anchorMin     = new Vector2(0.05f, 0.05f);
        _iconRt.anchorMax     = new Vector2(0.95f, 0.95f);
        _iconRt.localRotation = Quaternion.identity;
        ResetColor();
    }

    public void RestoreFromWildcard()
    {
        InWildcard = false;
        transform.SetParent(InventoryGridUI.Instance.PanelRt, false);
        transform.SetAsLastSibling();

        var rt = GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);

        _iconRt.anchorMin = _iconRt.anchorMax = _iconRt.pivot = new Vector2(0.5f, 0.5f);
        _iconRt.anchoredPosition = Vector2.zero;
        UpdateIconForRotation();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (InventoryDragHandler.Instance == null || InventoryDragHandler.Instance.IsDragging) return;
        if (InventoryNavigator.Instance != null && InventoryNavigator.Instance.IsNavigating) return;

        InventoryDragHandler.Instance.BeginDragExisting(this);
    }

    void SpawnCorner(Vector2 corner, float arm, float thick)
    {
        SpawnBar(new Vector2(arm,   thick), corner);
        SpawnBar(new Vector2(thick, arm),   corner);
    }

    void SpawnBar(Vector2 size, Vector2 pivot)
    {
        var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = pivot;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = size;
        var img = go.GetComponent<Image>();
        img.color         = Color.white;
        img.raycastTarget = false;
        _cornerImages.Add(img);
    }

    public void SetEquipped(bool equipped)
    {
        Color c = equipped ? CornerEquip : Color.white;
        foreach (var img in _cornerImages)
            img.color = c;
    }

    public void OnPointerEnter(PointerEventData eventData) => HoveredItem = this;

    public void OnPointerExit(PointerEventData eventData)
    {
        if (HoveredItem == this) HoveredItem = null;
    }

    void OnDestroy()
    {
        if (HoveredItem == this) HoveredItem = null;
    }

    void SpawnLabel(string text, float cellSize, float thick)
    {
        var go = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        float pad = thick + 2f;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = Vector2.zero;
        rt.anchoredPosition = new Vector2(pad, pad);
        rt.sizeDelta        = new Vector2(-pad * 2f, cellSize * 0.35f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = cellSize * 0.25f;
        tmp.color         = Color.white;
        tmp.alignment     = TextAlignmentOptions.BottomLeft;
        tmp.overflowMode  = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
    }
}