using System.Collections.Generic;
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

    private GridLayoutGroup        _grid;
    private InventoryGrid          _logicGrid;
    private readonly List<InventoryCell>    _cells     = new();
    private readonly List<InventoryItemUI>  _itemViews = new();

    void Awake()
    {
        Instance = this;
        BuildGrid();
    }

    void OnEnable()
    {
        if (_cells.Count > 0)
            ApplySize();
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

        // Keep parent panel the same size so its background image matches
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

                var cell = go.GetComponent<InventoryCell>();
                cell.Init(col, row, cellColor, borderColor);
                _cells.Add(cell);
            }
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public bool TryAddItem(itemSO item)
    {
        if (_logicGrid == null) return false;
        if (!_logicGrid.TryAdd(item.size, out var origin, out var rotated)) return false;

        SpawnItemVisual(item, origin, rotated);
        return true;
    }

    public void RemoveItem(InventoryItemUI view)
    {
        _logicGrid.Remove(view.Origin, view.Item.size, view.Rotated);
        _itemViews.Remove(view);
        Destroy(view.gameObject);
    }

    // ── Visuals ──────────────────────────────────────────────────────────────

    void SpawnItemVisual(itemSO item, Vector2Int origin, bool rotated)
    {
        int w = rotated ? item.size.y : item.size.x;
        int h = rotated ? item.size.x : item.size.y;

        float pixW = w * cellSize + (w - 1) * cellSpacing;
        float pixH = h * cellSize + (h - 1) * cellSpacing;

        // Force layout so cell world corners are accurate
        Canvas.ForceUpdateCanvases();

        // Anchor the item to the top-left world corner of the origin cell
        var originCellRt = _cells[origin.y * columns + origin.x].GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        originCellRt.GetWorldCorners(corners);
        // corners: [0]=bottom-left [1]=top-left [2]=top-right [3]=bottom-right

        var go = new GameObject(item.name, typeof(RectTransform), typeof(Image), typeof(InventoryItemUI));
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();

        var rt      = go.GetComponent<RectTransform>();
        var gridRt  = GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(pixW, pixH);

        // Convert world top-left of origin cell → Grid local space → anchoredPosition
        Vector2 localTopLeft  = gridRt.InverseTransformPoint(corners[1]);
        Vector2 anchorInLocal = new Vector2(gridRt.rect.xMin, gridRt.rect.yMax);
        rt.anchoredPosition   = localTopLeft - anchorInLocal;

        var view = go.GetComponent<InventoryItemUI>();
        view.Init(item, origin, rotated);
        _itemViews.Add(view);
    }

    // ── Rebuild ──────────────────────────────────────────────────────────────

    public void Rebuild(int newColumns, int newRows)
    {
        foreach (var cell in _cells)   Destroy(cell.gameObject);
        foreach (var view in _itemViews) Destroy(view.gameObject);
        _cells.Clear();
        _itemViews.Clear();

        columns = newColumns;
        rows    = newRows;
        BuildGrid();
    }
}
