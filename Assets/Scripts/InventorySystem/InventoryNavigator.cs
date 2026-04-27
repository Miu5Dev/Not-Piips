using UnityEngine;
using UnityEngine.UI;

public class InventoryNavigator : MonoBehaviour
{
    public static InventoryNavigator Instance { get; private set; }

    [Header("Input")]
    [SerializeField] float moveCooldown = 0.12f;

    [Header("Cursor Colors")]
    [SerializeField] Color cursorNormal  = new Color(0.5f, 0.9f, 1f, 0.9f);
    [SerializeField] Color cursorDiscard = new Color(1f, 0.5f, 0.3f, 0.9f);

    [Header("Cursor Visual")]
    [SerializeField] float highlightScale = 0.55f;

    [Header("Referencias (opcional)")]
    [SerializeField] InventoryGridUI      gridUI;
    [SerializeField] InventoryDragHandler dragHandler;

    enum SlotType { Grid, Wildcard, Discard }
    struct Slot
    {
        public SlotType type;
        public Vector2Int cell;
    }

    Slot                   _currentSlot;
    float                  _lastMoveTime = -1f;
    float                  _lastInputTime = -1f;
    bool                   _isNavigating;

    InventoryGridUI        _gridUI;
    InventoryDragHandler   _dragHandler;
    Canvas                 _canvas;

    RectTransform          _highlight;
    Image                  _highlightImg;

    Vector2                _lastMousePos;

    public bool IsNavigating => _isNavigating;

    void Awake()
    {
        Instance = this;
        _gridUI = gridUI != null ? gridUI : GetComponentInParent<InventoryGridUI>();
        if (_gridUI == null) _gridUI = InventoryGridUI.Instance;
        _dragHandler = dragHandler != null ? dragHandler : GetComponent<InventoryDragHandler>();
        if (_dragHandler == null) _dragHandler = InventoryDragHandler.Instance;
        _canvas = GetComponentInParent<Canvas>();

        if (_gridUI == null)
        {
            Debug.LogError("[InventoryNavigator] InventoryGridUI no encontrado.");
            return;
        }
        CreateHighlight();
    }

    void OnEnable()
    {
        _isNavigating = false;
        if (_highlight != null)
            _highlight.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_highlight == null || _gridUI == null) return;

        if (!_gridUI.gameObject.activeInHierarchy)
        {
            _highlight.gameObject.SetActive(false);
            _isNavigating = false;
            return;
        }

        _highlight.gameObject.SetActive(_isNavigating);
        if (_isNavigating)
            UpdateHighlightPosition();
    }

    void CreateHighlight()
    {
        if (_gridUI.PanelRt == null)
        {
            Debug.LogError("[InventoryNavigator] PanelRt es null.");
            return;
        }

        var go = new GameObject("NavHighlight", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_gridUI.PanelRt, false);
        go.transform.SetAsLastSibling();

        _highlight = go.GetComponent<RectTransform>();
        _highlight.anchorMin = _highlight.anchorMax = new Vector2(0.5f, 0.5f);
        _highlight.pivot = new Vector2(0.5f, 0.5f);

        float size = _gridUI.cellSize * highlightScale;
        _highlight.sizeDelta = new Vector2(size, size);
        _highlight.localScale = Vector3.one;

        _highlightImg = go.GetComponent<Image>();
        _highlightImg.sprite = null;
        _highlightImg.color = cursorNormal;
        _highlightImg.raycastTarget = false;

        var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(_highlight, false);
        var bRt = border.GetComponent<RectTransform>();
        bRt.anchorMin = bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.sizeDelta = new Vector2(size * 0.9f, size * 0.9f);
        var bImg = border.GetComponent<Image>();
        bImg.type = Image.Type.Sliced;
        bImg.fillCenter = false;
        bImg.color = Color.white;
        bImg.raycastTarget = false;

        _currentSlot = new Slot { type = SlotType.Grid, cell = Vector2Int.zero };
        UpdateHighlightPosition();
    }

    // ── Eventos ──────────────────────────────────────────────────────────

    public void HandleMove(OnMoveInputEvent e)
    {
        if (!e.pressed || _gridUI == null) return;
        Vector2 dir = e.Direction;
        if (dir == Vector2.zero) return;

        if (Time.time - _lastMoveTime < moveCooldown) return;
        _lastMoveTime = Time.time;
        _lastInputTime = Time.time;
        _isNavigating = true;

        if (_dragHandler.IsDragging)
            MoveWhileDragging(dir);
        else
            MoveCursor(dir);
    }

    public void HandleLeftClick(OnLeftClickEvent e)
    {
        if (!e.pressed || !_isNavigating || _gridUI == null) return;

        if (_dragHandler.IsDragging)
        {
            if (_currentSlot.type == SlotType.Grid)
            {
                var held = _dragHandler.HeldItem;
                if (held != null && _gridUI.IsValidPlacement(held.Item.size, _currentSlot.cell, held.Rotated))
                {
                    _gridUI.PlaceItem(held, _currentSlot.cell, held.Rotated);
                    _dragHandler.ClearHeldItem();
                    _dragHandler.SetJustPlaced();
                }
                else if (held != null)
                {
                    _dragHandler.ShowInvalidPlacement(held);
                }
            }
            else if (_currentSlot.type == SlotType.Discard)
            {
                var held = _dragHandler.HeldItem;
                if (held != null)
                {
                    Debug.Log($"[Inventory] Discarded {held.Item.name} (via navigator).");
                    Destroy(held.gameObject);
                    _dragHandler.ClearHeldItem();
                }
            }
            _lastInputTime = Time.time;
        }
        else
        {
            if (_currentSlot.type == SlotType.Grid)
            {
                var item = _gridUI.GetItemAtCell(_currentSlot.cell);
                if (item != null)
                {
                    _dragHandler.BeginDragExisting(item);
                    PositionHeldItem();
                }
            }
            else if (_currentSlot.type == SlotType.Wildcard)
            {
                var wildcardItem = _gridUI.GetWildcardItem();
                if (wildcardItem != null)
                {
                    _dragHandler.BeginDragExisting(wildcardItem);
                    PositionHeldItem();
                }
            }
        }
    }

    public void HandleRightClick(OnRightClickEvent e)
    {
        if (!e.pressed || !_isNavigating) return;
        if (_dragHandler.IsDragging)
            _dragHandler.HandleRightClick(e);
    }

    public void HandleRotate(OnRotateKeyEvent e)
    {
        if (!e.pressed || !_isNavigating) return;
        if (_dragHandler.IsDragging)
        {
            _dragHandler.HandleRotate(e);
            PositionHeldItem();   // ← feedback inmediato tras rotar
        }
    }

    public void HandlePointerPosition(OnPointerPositionEvent e)
    {
        if (Vector2.Distance(e.Position, _lastMousePos) > 10f)
        {
            _isNavigating = false;
        }
        _lastMousePos = e.Position;
    }

    // ── Movimiento del cursor ───────────────────────────────────────────

    void MoveCursor(Vector2 dir)
    {
        int cols = _gridUI.Columns;
        int rows = _gridUI.Rows;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            dir = new Vector2(Mathf.Sign(dir.x), 0f);
        else
            dir = new Vector2(0f, Mathf.Sign(dir.y));

        switch (_currentSlot.type)
        {
            case SlotType.Grid:
                int newCol = _currentSlot.cell.x + (int)dir.x;
                int newRow = _currentSlot.cell.y - (int)dir.y;

                if (newCol >= 0 && newCol < cols && newRow >= 0 && newRow < rows)
                {
                    _currentSlot.cell = new Vector2Int(newCol, newRow);
                    break;
                }

                if (_currentSlot.cell.x == cols - 1 && dir.x > 0)
                    _currentSlot.type = SlotType.Wildcard;
                else if (_currentSlot.cell.y == 0 && dir.y < 0)
                    _currentSlot.type = SlotType.Discard;
                break;

            case SlotType.Wildcard:
                if (dir.x < 0)
                {
                    _currentSlot.type = SlotType.Grid;
                    _currentSlot.cell = new Vector2Int(cols - 1, Mathf.Clamp(_currentSlot.cell.y, 0, rows - 1));
                }
                else if (dir.y < 0)
                    _currentSlot.type = SlotType.Discard;
                break;

            case SlotType.Discard:
                if (dir.y > 0)
                    _currentSlot.type = SlotType.Wildcard;
                else if (dir.x < 0)
                {
                    _currentSlot.type = SlotType.Grid;
                    _currentSlot.cell = new Vector2Int(cols - 1, 0);
                }
                break;
        }
    }

    void MoveWhileDragging(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            dir = new Vector2(Mathf.Sign(dir.x), 0f);
        else
            dir = new Vector2(0f, Mathf.Sign(dir.y));

        MoveCursorInternal(dir);
        PositionHeldItem();
    }

    void MoveCursorInternal(Vector2 dir) => MoveCursor(dir);

    void PositionHeldItem()
    {
        InventoryItemUI held = _dragHandler.HeldItem;
        if (held == null) return;

        if (_currentSlot.type == SlotType.Grid)
        {
            held.Reposition(_currentSlot.cell, held.Rotated);
            bool valid = _gridUI.IsValidPlacement(held.Item.size, _currentSlot.cell, held.Rotated);
            held.SetDragColor(valid ? _dragHandler.ValidColor : _dragHandler.InvalidColor);
        }
        else if (_currentSlot.type == SlotType.Discard)
        {
            held.FollowScreen(GetCurrentSlotScreenPos());
            held.SetDragColor(new Color(1f, 0.2f, 0.2f, 0.95f));
        }
        else
        {
            held.FollowScreen(GetCurrentSlotScreenPos());
            held.SetDragColor(_dragHandler.InvalidColor);
        }
    }

    // ── Posicionamiento del resalte ─────────────────────────────────────

    void UpdateHighlightPosition()
    {
        RectTransform target = null;

        switch (_currentSlot.type)
        {
            case SlotType.Grid:
                target = GetCellRect(_currentSlot.cell.x, _currentSlot.cell.y);
                _highlightImg.color = cursorNormal;
                break;
            case SlotType.Wildcard:
                target = _gridUI.WildcardSlot;
                _highlightImg.color = cursorNormal;
                break;
            case SlotType.Discard:
                target = _gridUI.DiscardSlot;
                _highlightImg.color = cursorDiscard;
                break;
        }

        if (target != null)
        {
            _highlight.position = target.position;
            _highlight.sizeDelta = target.sizeDelta * highlightScale;
            _highlight.gameObject.SetActive(true);
        }
        else
        {
            _highlight.gameObject.SetActive(false);
        }
    }

    RectTransform GetCellRect(int col, int row)
    {
        Transform t = _gridUI.transform.Find($"Cell_{col}_{row}");
        return t?.GetComponent<RectTransform>();
    }

    Vector2 GetCurrentSlotScreenPos()
    {
        RectTransform rt = null;
        switch (_currentSlot.type)
        {
            case SlotType.Grid:
                rt = GetCellRect(_currentSlot.cell.x, _currentSlot.cell.y);
                break;
            case SlotType.Wildcard:
                rt = _gridUI.WildcardSlot;
                break;
            case SlotType.Discard:
                rt = _gridUI.DiscardSlot;
                break;
        }
        if (rt == null) return Vector2.zero;

        var cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, rt.position);
    }
}