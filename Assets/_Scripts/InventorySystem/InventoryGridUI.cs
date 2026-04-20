using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryGridUI : MonoBehaviour
{
    [Header("Grid Config")]
    [Min(1)] public int columns = 10;
    [Min(1)] public int rows    = 5;

    [Header("Visuals")]
    public Color cellColor       = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    public Color borderColor     = new Color(0.4f, 0.4f, 0.4f, 1f);
    [Range(0f, 20f)] public float cellSpacing = 2f;

    private GridLayoutGroup _grid;
    private readonly List<InventoryCell> _cells = new();

    void Start()
    {
        BuildGrid();
    }

    void OnEnable()
    {
        // Recalculate cell size every time the inventory is shown
        // in case the resolution changed while it was closed.
        if (_cells.Count > 0)
            RecalculateCellSize();
    }

    void BuildGrid()
    {
        _grid = GetComponent<GridLayoutGroup>() ?? gameObject.AddComponent<GridLayoutGroup>();
        _grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
        _grid.spacing         = new Vector2(cellSpacing, cellSpacing);
        _grid.childAlignment  = TextAnchor.UpperLeft;
        _grid.padding         = new RectOffset(0, 0, 0, 0);

        RecalculateCellSize();
        SpawnCells();
    }

    void RecalculateCellSize()
    {
        // Force layout so RectTransform.rect is accurate before we read it
        Canvas.ForceUpdateCanvases();

        var rt    = GetComponent<RectTransform>();
        float w   = (rt.rect.width  - cellSpacing * (columns - 1)) / columns;
        float h   = (rt.rect.height - cellSpacing * (rows    - 1)) / rows;

        if (_grid != null)
            _grid.cellSize = new Vector2(w, h);
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

    // Call this if the grid dimensions are changed at runtime
    public void Rebuild(int newColumns, int newRows)
    {
        foreach (var cell in _cells)
            Destroy(cell.gameObject);
        _cells.Clear();

        columns = newColumns;
        rows    = newRows;
        BuildGrid();
    }
}
