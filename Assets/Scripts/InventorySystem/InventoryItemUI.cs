using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItemUI : MonoBehaviour, IPointerDownHandler
{
    public itemSO     Item       { get; private set; }
    public Vector2Int Origin     { get; private set; }
    public bool       Rotated    { get; private set; }
    public bool       InWildcard { get; private set; }

    float _cellSize, _cellSpacing;
    Image _bg;
    Image _icon;
    RectTransform _iconRt;

    static readonly Color BgEmpty = new Color(1f, 1f, 1f, 0.12f);

    public void Init(itemSO item, bool rotated, float cellSize, float cellSpacing)
    {
        Item         = item;
        Rotated      = rotated;
        _cellSize    = cellSize;
        _cellSpacing = cellSpacing;

        // Root background — transparent fill, raycast target for clicks
        _bg = GetComponent<Image>();
        _bg.sprite        = null;
        _bg.raycastTarget = true;
        _bg.color         = BgEmpty;

        // Icon child — separate so it can rotate without affecting brackets/label
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
    }

    // ── Positioning ───────────────────────────────────────────────────────────

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

        // Make sure size reflects current rotation while floating off-grid
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

    // ── Wildcard ──────────────────────────────────────────────────────────────

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

        // Icon shrinks to fit the small slot, no rotation
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

        // Icon back to natural fixed size + rotation
        _iconRt.anchorMin = _iconRt.anchorMax = _iconRt.pivot = new Vector2(0.5f, 0.5f);
        _iconRt.anchoredPosition = Vector2.zero;
        UpdateIconForRotation();
    }

    // ── Click handler ─────────────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (InventoryDragHandler.Instance == null || InventoryDragHandler.Instance.IsDragging) return;

        // While wildcard slot is occupied, only the wildcard item itself can be picked up
       /* if (!InWildcard && InventoryGridUI.Instance != null && !InventoryGridUI.Instance.WildcardEmpty)
        {
            Debug.Log("[Inventory] Cannot pick up — clear the wildcard slot first.");
            return;
        } */

        InventoryDragHandler.Instance.BeginDragExisting(this);
    }

    // ── Visual children ───────────────────────────────────────────────────────

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
        rt.sizeDelta        = new Vector2(-pad * 2f, cellSize * 0.28f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = cellSize * 0.22f;
        tmp.color         = Color.white;
        tmp.alignment     = TextAlignmentOptions.BottomLeft;
        tmp.overflowMode  = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
    }
}
