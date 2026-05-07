using UnityEngine;

public class InventoryGrid
{
    readonly int _cols, _rows;
    readonly bool[,] _occupied;

    public InventoryGrid(int cols, int rows)
    {
        _cols = cols;
        _rows = rows;
        _occupied = new bool[cols, rows];
    }

    public bool TryAdd(Vector2Int size, out Vector2Int origin, out bool rotated)
    {
        if (size.x <= 0 || size.y <= 0)
        {
            Debug.LogWarning($"[Inventory] Item has invalid size {size} — set a size > 0 on the itemSO.");
            origin = Vector2Int.zero; rotated = false; return false;
        }

        if (TryFind(size.x, size.y, out origin)) { rotated = false; Mark(origin, size.x, size.y, true); return true; }
        if (size.x != size.y && TryFind(size.y, size.x, out origin)) { rotated = true; Mark(origin, size.y, size.x, true); return true; }

        origin = Vector2Int.zero; rotated = false; return false;
    }

    // Read-only check — does not modify state
    public bool CanFit(Vector2Int size, Vector2Int origin, bool rotated)
    {
        int w = rotated ? size.y : size.x;
        int h = rotated ? size.x : size.y;
        if (origin.x < 0 || origin.y < 0 || origin.x + w > _cols || origin.y + h > _rows) return false;
        return Fits(origin.x, origin.y, w, h);
    }

    // Place without searching — caller already validated
    public void ForcePlace(Vector2Int size, Vector2Int origin, bool rotated)
    {
        int w = rotated ? size.y : size.x;
        int h = rotated ? size.x : size.y;
        Mark(origin, w, h, true);
    }

    public void Remove(Vector2Int origin, Vector2Int size, bool rotated)
    {
        int w = rotated ? size.y : size.x;
        int h = rotated ? size.x : size.y;
        Mark(origin, w, h, false);
    }

    bool TryFind(int w, int h, out Vector2Int origin)
    {
        for (int row = 0; row <= _rows - h; row++)
            for (int col = 0; col <= _cols - w; col++)
                if (Fits(col, row, w, h)) { origin = new Vector2Int(col, row); return true; }
        origin = Vector2Int.zero; return false;
    }

    bool Fits(int col, int row, int w, int h)
    {
        for (int r = row; r < row + h; r++)
            for (int c = col; c < col + w; c++)
                if (_occupied[c, r]) return false;
        return true;
    }

    void Mark(Vector2Int o, int w, int h, bool val)
    {
        for (int r = o.y; r < o.y + h; r++)
            for (int c = o.x; c < o.x + w; c++)
                _occupied[c, r] = val;
    }
}
