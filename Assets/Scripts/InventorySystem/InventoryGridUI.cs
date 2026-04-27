using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryGridUI : MonoBehaviour
{
    public static InventoryGridUI Instance { get; private set; }

    [Header("Grid Config")]
    [Min(1)] public int   columns  = 10;
    [Min(1)] public int   rows     = 5;
    [Min(1)] public float cellSize = 60f;

    [Header("Visuals")]
    public Color cellColor   = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    public Color borderColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [Range(0f, 20f)] public float cellSpacing = 2f;

    public int Columns => columns;
    public int Rows    => rows;
    public RectTransform PanelRt => transform.parent as RectTransform ?? GetComponent<RectTransform>();

    Canvas _canvas;
    GridLayoutGroup _grid;
    InventoryGrid   _logicGrid;
    readonly List<InventoryCell>   _cells     = new();
    readonly List<InventoryItemUI> _itemViews = new();

    RectTransform   _wildcardSlot;
    InventoryItemUI _wildcardItem;
    RectTransform   _discardSlot;

    public RectTransform WildcardSlot   => _wildcardSlot;
    public RectTransform DiscardSlot    => _discardSlot;
    public bool          WildcardEmpty  => _wildcardItem == null;

    void Awake()
    {
        Instance = this;
        _canvas  = GetComponentInParent<Canvas>();
        BuildGrid();
        BuildWildcardSlot();
        BuildDiscardSlot();
    }

    void OnEnable()
    {
        if (_cells.Count > 0) ApplySize();
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
        _grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
        _grid.spacing         = new Vector2(cellSpacing, cellSpacing);
        _grid.childAlignment  = TextAnchor.UpperLeft;
        _grid.padding         = new RectOffset(0, 0, 0, 0);

        ApplySize();
        SpawnCells();
    }

    void ApplySize()
    {
        float totalW = cellSize * columns + cellSpacing * (columns - 1);
        float totalH = cellSize * rows    + cellSpacing * (rows    - 1);

        var rt = GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(totalW, totalH);

        if (transform.parent is RectTransform parentRt)
            parentRt.sizeDelta = new Vector2(totalW, totalH);

        if (_grid != null)
            _grid.cellSize = new Vector2(cellSize, cellSize);
    }

    void SpawnCells()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var go = new GameObject($"Cell_{col}_{row}", typeof(RectTransform), typeof(Image), typeof(InventoryCell));
                go.transform.SetParent(transform, false);
                go.GetComponent<InventoryCell>().Init(col, row, cellColor, borderColor);
                _cells.Add(go.GetComponent<InventoryCell>());
            }
        }
    }

    void BuildWildcardSlot()
    {
        _wildcardSlot = BuildSidecarSlot("WildcardSlot",
            new Vector2(20f,  cellSize * 0.5f + 5f),
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

    RectTransform BuildSidecarSlot(string name, Vector2 anchoredPos, Color color, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(PanelRt, false);
        go.transform.SetAsFirstSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0.5f);
        rt.anchorMax        = new Vector2(1f, 0.5f);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = new Vector2(cellSize, cellSize);

        go.GetComponent<Image>().color = color;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(rt, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text          = label;
        tmp.fontSize      = cellSize * 0.35f;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = new Color(1f, 1f, 1f, 0.7f);
        tmp.raycastTarget = false;

        return rt;
    }

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

        if (_logicGrid.TryAdd(item.size, out var origin, out var rotated))
        {
            var view = CreateItemVisual(item, rotated);
            view.Reposition(origin, rotated);
            _itemViews.Add(view);
            return true;
        }

        if (_wildcardItem == null)
        {
            Debug.Log($"[Inventory] Inventory is full — placing {item.name} in wildcard slot.");
            var view = CreateItemVisual(item, false);
            PlaceInWildcard(view);
            return true;
        }

        Debug.Log($"[Inventory] Cannot pick up {item.name} — wildcard slot is occupied.");
        return false;
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
    {
        return CreateItemVisual(item, rotated);
    }

    public bool IsValidPlacement(Vector2Int itemSize, Vector2Int origin, bool rotated)
    {
        return _logicGrid != null && _logicGrid.CanFit(itemSize, origin, rotated);
    }

    public void PlaceItem(InventoryItemUI view, Vector2Int origin, bool rotated)
    {
        _logicGrid.ForcePlace(view.Item.size, origin, rotated);
        view.Reposition(origin, rotated);
        _itemViews.Add(view);
    }

    public void FreeItem(InventoryItemUI view)
    {
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

    // ── Nuevos métodos para el navegador ─────────────────────────────────────

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

    public InventoryItemUI GetWildcardItem()
    {
        return _wildcardItem;
    }

    public void Rebuild(int newColumns, int newRows)
    {
        foreach (var c in _cells)   Destroy(c.gameObject);
        foreach (var v in _itemViews) Destroy(v.gameObject);
        _cells.Clear();
        _itemViews.Clear();
        columns = newColumns;
        rows    = newRows;
        BuildGrid();
    }
}