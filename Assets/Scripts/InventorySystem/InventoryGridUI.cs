using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryGridUI : MonoBehaviour
{
    public static InventoryGridUI Instance { get; private set; }

    [Header("Grid Config")]
    [Min(1)] public int columns = 10;
    [Min(1)] public int rows = 5;
    [Min(1)] public float cellSize = 60f;

    [Header("Visuals")]
    public Color cellColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    public Color borderColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [Range(0f, 20f)] public float cellSpacing = 2f;

    public int Columns => columns;
    public int Rows => rows;
    public RectTransform PanelRt => transform.parent as RectTransform ?? GetComponent<RectTransform>();

    Canvas _canvas;
    GridLayoutGroup _grid;
    InventoryGrid _logicGrid;
    readonly List<InventoryCell> _cells = new();
    readonly List<InventoryItemUI> _itemViews = new();

    RectTransform _wildcardSlot;
    InventoryItemUI _wildcardItem;
    RectTransform _discardSlot;

    public RectTransform WildcardSlot => _wildcardSlot;
    public RectTransform DiscardSlot => _discardSlot;
    public bool WildcardEmpty => _wildcardItem == null;

    void Awake()
    {
        Instance = this;
        _canvas = GetComponentInParent<Canvas>();
        BuildGrid();
        BuildWildcardSlot();
        BuildDiscardSlot();
    }

    void OnEnable()
    {
        if (_cells.Count > 0) ApplySize();
        RebuildLogicGrid();          // FIX: resync logicGrid from visual state on every open
        RefreshWeaponAmmoLabels();
    }

    // FIX: reconstructs _logicGrid from current item views to prevent desync
    void RebuildLogicGrid()
    {
        _logicGrid.Clear();
        foreach (var view in _itemViews)
        {
            if (view == null || view.InWildcard) continue;
            _logicGrid.ForcePlace(view.Item.size, view.Origin, view.Rotated);
        }
    }

    public void RefreshWeaponAmmoLabels()
    {
        if (ShootController.Instance == null) return;
        if (InventoryEquipHandler.Instance == null) return;

        InventoryItemUI equippedView = InventoryEquipHandler.Instance.EquippedItem;
        equippedView?.SetStoredAmmo(ShootController.Instance.CurrentMagazine);

        EquipedWeaponUIController.Instance?.RefreshAmmo();
    }

    void OnValidate()
    {
        if (_grid == null) return;
        _grid.spacing = new Vector2(cellSpacing, cellSpacing);
        ApplySize();
    }

    void BuildGrid()
    {
        _logicGrid = new InventoryGrid(columns, rows);

        _grid = GetComponent<GridLayoutGroup>() ?? gameObject.AddComponent<GridLayoutGroup>();
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
        _grid.spacing = new Vector2(cellSpacing, cellSpacing);
        _grid.childAlignment = TextAnchor.UpperLeft;
        _grid.padding = new RectOffset(0, 0, 0, 0);

        ApplySize();
        SpawnCells();
    }

    void ApplySize()
    {
        float totalW = cellSize * columns + cellSpacing * (columns - 1);
        float totalH = cellSize * rows    + cellSpacing * (rows    - 1);

        var rt = GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(totalW, totalH);

        if (transform.parent is RectTransform parentRt)
            parentRt.sizeDelta = new Vector2(totalW, totalH);

        if (_grid != null)
            _grid.cellSize = new Vector2(cellSize, cellSize);
    }

    void SpawnCells()
    {
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < columns; col++)
        {
            var go = new GameObject($"Cell_{col}_{row}", typeof(RectTransform), typeof(Image), typeof(InventoryCell));
            go.transform.SetParent(transform, false);
            go.GetComponent<InventoryCell>().Init(col, row, cellColor, borderColor);
            _cells.Add(go.GetComponent<InventoryCell>());
        }
    }

    void BuildWildcardSlot()
    {
        _wildcardSlot = BuildSidecarSlot("WildcardSlot",
            new Vector2(20f, cellSize * 0.5f + 5f),
            new Color(0.2f, 0.25f, 0.4f, 0.85f),
            "*");
    }

    void BuildDiscardSlot()
    {
        _discardSlot = BuildSidecarSlot("DiscardSlot",
            new Vector2(20f, -cellSize * 0.5f - 5f),
            new Color(0.5f, 0.15f, 0.15f, 0.85f),
            "DEL");
    }

    RectTransform BuildSidecarSlot(string slotName, Vector2 anchoredPos, Color color, string label)
    {
        var go = new GameObject(slotName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(PanelRt, false);
        go.transform.SetAsFirstSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(cellSize, cellSize);

        go.GetComponent<Image>().color = color;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(rt, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text       = label;
        tmp.fontSize   = cellSize * 0.35f;
        tmp.fontStyle  = FontStyles.Bold;
        tmp.alignment  = TextAlignmentOptions.Center;
        tmp.color      = new Color(1f, 1f, 1f, 0.7f);
        tmp.raycastTarget = false;

        return rt;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public bool IsMouseOver(RectTransform rt, Vector2 screenPos)
    {
        if (rt == null) return false;
        var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam);
    }

    public bool TryAddItem(itemSO item)
    {
        if (_logicGrid == null) return false;

        if (item.isStackable)
        {
            foreach (var existing in _itemViews)
            {
                if (existing == null || existing.Item != item) continue;
                if (existing.StackCount >= item.maxStackSize) continue;
                existing.AddToStack(1, item.maxStackSize);
                NotifyAmmoHUDIfNeeded(item);
                return true;
            }
            if (_wildcardItem != null
                && _wildcardItem.Item == item
                && _wildcardItem.StackCount < item.maxStackSize)
            {
                _wildcardItem.AddToStack(1, item.maxStackSize);
                NotifyAmmoHUDIfNeeded(item);
                return true;
            }
        }

        if (_logicGrid.TryAdd(item.size, out var origin, out var rotated))
        {
            var view = CreateItemVisual(item, rotated);
            view.Reposition(origin, rotated);
            _itemViews.Add(view);
            NotifyAmmoHUDIfNeeded(item);
            return true;
        }

        if (_wildcardItem == null)
        {
            Debug.Log($"[Inventory] Inventory is full — placing {item.name} in wildcard slot.");
            var view = CreateItemVisual(item, false);
            PlaceInWildcard(view);
            NotifyAmmoHUDIfNeeded(item);
            return true;
        }

        Debug.Log($"[Inventory] Cannot pick up {item.name} — wildcard slot is occupied.");
        return false;
    }

    private void NotifyAmmoHUDIfNeeded(itemSO item)
    {
        if (ShootController.Instance?.CurrentWeapon?.ammo == item)
            EquipedWeaponUIController.Instance?.RefreshAmmo();
    }

    public void PlaceInWildcard(InventoryItemUI view)
    {
        if (_wildcardItem != null && _wildcardItem != view) return;
        _wildcardItem = view;
        view.SetWildcardMode(_wildcardSlot);
    }

    public void FreeFromWildcard(InventoryItemUI view)
    {
        if (_wildcardItem == view) _wildcardItem = null;
    }

    public InventoryItemUI CreateFloatingVisual(itemSO item, bool rotated)
        => CreateItemVisual(item, rotated);

    public bool IsValidPlacement(Vector2Int itemSize, Vector2Int origin, bool rotated)
        => _logicGrid != null && _logicGrid.CanFit(itemSize, origin, rotated);

    public void PlaceItem(InventoryItemUI view, Vector2Int origin, bool rotated)
    {
        FreeFromWildcard(view); // FIX: clear wildcard ref if this view came from there

        if (!_itemViews.Contains(view))
            _itemViews.Add(view);

        view.Reposition(origin, rotated);
        _logicGrid.ForcePlace(view.Item.size, origin, rotated);
    }

    public void FreeItem(InventoryItemUI view)
    {
        if (_wildcardItem == view) _wildcardItem = null; // FIX: avoid ghost wildcard ref

        _logicGrid.Remove(view.Origin, view.Item.size, view.Rotated);
        _itemViews.Remove(view);
    }

    public void RemoveItem(InventoryItemUI view)
    {
        FreeItem(view);
        Destroy(view.gameObject);
    }

    public Vector2Int? GetCellFromScreen(Vector2 screenPos)
    {
        var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;

        var rt = GetComponent<RectTransform>();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out var local))
            return null;

        float step = cellSize + cellSpacing;
        int col = Mathf.FloorToInt((local.x - rt.rect.xMin) / step);
        int row = Mathf.FloorToInt((rt.rect.yMax - local.y) / step);

        if (col < 0 || col >= columns || row < 0 || row >= rows) return null;
        return new Vector2Int(col, row);
    }

    public Vector2 ScreenToPanel(Vector2 screenPos)
    {
        var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(PanelRt, screenPos, cam, out var local);
        return local;
    }

    InventoryItemUI CreateItemVisual(itemSO item, bool rotated)
    {
        var go = new GameObject(item.name, typeof(RectTransform), typeof(Image), typeof(InventoryItemUI));
        go.transform.SetParent(PanelRt, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);

        var view = go.GetComponent<InventoryItemUI>();
        view.Init(item, rotated, cellSize, cellSpacing);
        return view;
    }

    // ── Navigator helpers ─────────────────────────────────────────────────────

    public InventoryItemUI GetItemAtCell(Vector2Int cell)
    {
        foreach (var view in _itemViews)
        {
            if (view.InWildcard) continue;
            int w = view.Rotated ? view.Item.size.y : view.Item.size.x;
            int h = view.Rotated ? view.Item.size.x : view.Item.size.y;
            if (cell.x >= view.Origin.x && cell.x < view.Origin.x + w &&
                cell.y >= view.Origin.y && cell.y < view.Origin.y + h)
                return view;
        }
        return null;
    }

    public InventoryItemUI GetWildcardItem() => _wildcardItem;

    // ── Stacking ──────────────────────────────────────────────────────────────

    public bool TryStackOnto(InventoryItemUI incoming)
    {
        if (incoming == null || incoming.Item == null) return false;
        if (!incoming.Item.isStackable) return false;

        foreach (InventoryItemUI existing in _itemViews)
        {
            if (existing == null || existing.Item != incoming.Item) continue;

            int space = incoming.Item.maxStackSize - existing.StackCount;
            if (space <= 0) continue;

            if (incoming.StackCount <= space)
            {
                existing.AddToStack(incoming.StackCount, incoming.Item.maxStackSize);
                return true;
            }
            else
            {
                existing.AddToStack(space, incoming.Item.maxStackSize);
                incoming.RemoveFromStack(space);
            }
        }
        return false;
    }

    public bool IsInventoryFull(itemSO item = null)
    {
        if (item != null && item.isStackable)
        {
            foreach (var existing in _itemViews)
            {
                if (existing == null) continue;
                if (existing.Item == item && existing.StackCount < item.maxStackSize)
                    return false;
            }
            if (_wildcardItem != null
                && _wildcardItem.Item == item
                && _wildcardItem.StackCount < item.maxStackSize)
                return false;
        }

        if (item != null && _logicGrid.HasSpace(item.size)) return false;
        if (item == null && _logicGrid.HasSpace(Vector2Int.one)) return false;

        return _wildcardItem != null;
    }

    public IEnumerable<InventoryItemUI> GetAllItems()
    {
        foreach (var item in _itemViews)
            if (item != null) yield return item;
        if (_wildcardItem != null) yield return _wildcardItem;
    }

    // ── Rebuild ───────────────────────────────────────────────────────────────

    public void Rebuild(int newColumns, int newRows)
    {
        foreach (var c in _cells) Destroy(c.gameObject);
        foreach (var v in _itemViews) Destroy(v.gameObject);
        _cells.Clear();
        _itemViews.Clear();
        columns = newColumns;
        rows    = newRows;
        BuildGrid();
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Debug: Validate LogicGrid")]
    public void DebugValidateGrid()
    {
        bool clean = true;
        foreach (var view in _itemViews)
        {
            if (view == null || view.InWildcard) continue;
            int w = view.Rotated ? view.Item.size.y : view.Item.size.x;
            int h = view.Rotated ? view.Item.size.x : view.Item.size.y;
            for (int r = view.Origin.y; r < view.Origin.y + h; r++)
            for (int c = view.Origin.x; c < view.Origin.x + w; c++)
            {
                if (!_logicGrid.IsOccupied(c, r))
                {
                    Debug.LogError($"[Inventory] DESYNCED: {view.Item.name} at ({c},{r}) not marked in logicGrid!");
                    clean = false;
                }
            }
        }
        if (clean) Debug.Log("[Inventory] Grid validation passed — no desyncs found.");
    }
#endif
}
