using System.Collections;
using TMPro;
using UnityEngine;

public class InventoryDragHandler : MonoBehaviour
{
    public static InventoryDragHandler Instance { get; private set; }

    [SerializeField] Color  validColor   = new Color(0.35f, 1f,    0.35f, 0.85f);
    [SerializeField] Color  invalidColor = new Color(1f,    0.25f, 0.25f, 0.85f);
    [SerializeField] string invalidMsg   = "You can not place it here";
    [SerializeField] float  popupSeconds = 1.5f;

    InventoryItemUI _held;
    bool            _isNew;
    bool            _isFromWildcard;
    Vector2Int      _cancelOrigin;
    bool            _cancelRotated;
    bool            _blinking;
    int             _dragStartFrame = -1;
    bool            _justPlaced;

    Vector2 _currentMousePos;

    TextMeshProUGUI _popupText;
    Coroutine       _popupCo;
    Coroutine       _blinkCo; // tracks the blink so we can stop it early

    public bool            IsDragging   => _held != null;
    public bool            JustPlaced   => _justPlaced;
    public InventoryItemUI HeldItem     => _held;
    public Color           ValidColor   => validColor;
    public Color           InvalidColor => invalidColor;
    public Vector2 CurrentMousePos
    {
        get => _currentMousePos;
        set => _currentMousePos = value;
    }

    void Awake()
    {
        Instance = this;
        CreatePopup();
    }

    void OnDisable()
    {
        if (_held != null) Cancel();
    }

    // ── Popup ─────────────────────────────────────────────────────────────

    void CreatePopup()
    {
        var go = new GameObject("InvalidPopup", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 12f);
        rt.sizeDelta        = new Vector2(500f, 60f);

        _popupText               = go.GetComponent<TextMeshProUGUI>();
        _popupText.text          = string.Empty;
        _popupText.fontSize      = 28;
        _popupText.alignment     = TextAlignmentOptions.Center;
        _popupText.color         = new Color(1f, 0.3f, 0.3f, 1f);
        _popupText.fontStyle     = FontStyles.Bold;
        _popupText.raycastTarget = false;
        go.SetActive(false);
    }

    public void ShowPopup(string msg)
    {
        if (_popupCo != null) StopCoroutine(_popupCo);
        _popupCo = StartCoroutine(PopupRoutine(msg));
    }

    IEnumerator PopupRoutine(string msg)
    {
        _popupText.text = msg;
        _popupText.gameObject.SetActive(true);
        var c = _popupText.color; c.a = 1f; _popupText.color = c;

        yield return new WaitForSeconds(popupSeconds);

        float t = 0f;
        while (t < 0.4f)
        {
            t   += Time.deltaTime;
            c.a  = Mathf.Lerp(1f, 0f, t / 0.4f);
            _popupText.color = c;
            yield return null;
        }
        _popupText.gameObject.SetActive(false);
    }

    IEnumerator BlinkRed(InventoryItemUI item)
    {
        _blinking = true;
        for (int i = 0; i < 3; i++)
        {
            if (item == null) break;
            item.SetDragColor(invalidColor);
            yield return new WaitForSeconds(0.08f);
            if (item == null) break;
            item.SetDragColor(new Color(1f, 1f, 1f, 0.05f));
            yield return new WaitForSeconds(0.08f);
        }
        // Only set red at the end if the item is still being held.
        // If dropped during blink, StopBlink() already stopped this coroutine
        // before reaching here, so this line never runs for placed items.
        if (item != null && _held == item)
            item.SetDragColor(invalidColor);

        _blinking = false;
        _blinkCo  = null;
    }

    // Stops any active blink coroutine and resets the blinking flag.
    // Does NOT set any color — the caller (PlaceItem / Cancel / Destroy)
    // is responsible for the item's final appearance. Setting a color here
    // would race against whatever the placement code does, causing glitches.
    void StopBlink()
    {
        if (_blinkCo != null)
        {
            StopCoroutine(_blinkCo);
            _blinkCo = null;
        }
        _blinking = false;
    }

    /// Shows popup + blink feedback for the navigator
    public void ShowInvalidPlacement(InventoryItemUI item)
    {
        ShowPopup(invalidMsg);
        StopBlink();
        _blinkCo = StartCoroutine(BlinkRed(item));
    }

    /// Sets the JustPlaced flag (used by the navigator)
    public void SetJustPlaced() => _justPlaced = true;

    // ── Begin drag ────────────────────────────────────────────────────────

    public void BeginDrag(itemSO item)
    {
        if (_held != null || InventoryGridUI.Instance == null || _justPlaced) return;
        _held           = InventoryGridUI.Instance.CreateFloatingVisual(item, false);
        _isNew          = true;
        _isFromWildcard = false;
        _dragStartFrame = Time.frameCount;
    }

    public void BeginDragExisting(InventoryItemUI view)
    {
        if (_held != null || InventoryGridUI.Instance == null || _justPlaced) return;

        if (view.InWildcard)
        {
            InventoryGridUI.Instance.FreeFromWildcard(view);
            view.RestoreFromWildcard();
            _isFromWildcard = true;
            _isNew          = false;
        }
        else
        {
            _cancelOrigin   = view.Origin;
            _cancelRotated  = view.Rotated;
            InventoryGridUI.Instance.FreeItem(view);
            _isFromWildcard = false;
            _isNew          = false;
        }

        _held = view;
        _held.transform.SetAsLastSibling();
        _dragStartFrame = Time.frameCount;
    }

    // ── Event handlers (called from another script) ───────────────────────

    public void HandleRotate(OnRotateKeyEvent e)
    {
        // If the navigator is active, it handles rotation directly — skip here.
        if (InventoryNavigator.Instance != null && InventoryNavigator.Instance.IsNavigating) return;

        if (!e.pressed || _held == null || InventoryGridUI.Instance == null) return;
        _held.Reposition(_held.Origin, !_held.Rotated);
    }

    public void HandleRightClick(OnRightClickEvent e)
    {
        // If the navigator is active, it handles cancellation directly — skip here.
        if (InventoryNavigator.Instance != null && InventoryNavigator.Instance.IsNavigating) return;
        if (!e.pressed || _held == null) return;
        Cancel();
    }

    /// Public cancel entry point used by InventoryNavigator during WASD navigation.
    public void CancelDrag() => Cancel();

    public void HandleLeftClick(OnLeftClickEvent e)
    {
        // If the navigator is active, it handles placement directly — skip here.
        if (InventoryNavigator.Instance != null && InventoryNavigator.Instance.IsNavigating) return;

        if (!e.pressed || _held == null) return;
        if (Time.frameCount == _dragStartFrame) return;

        var grid = InventoryGridUI.Instance;
        if (grid == null) return;

        Vector2 mousePos = _currentMousePos;

        if (grid.IsMouseOver(grid.DiscardSlot, mousePos))
        {
            Debug.Log($"[Inventory] Discarded {_held.Item.name}.");
            StopBlink();
            Destroy(_held.gameObject);
            _held = null;
            return;
        }

        int w = _held.Rotated ? _held.Item.size.y : _held.Item.size.x;
        int h = _held.Rotated ? _held.Item.size.x : _held.Item.size.y;
        Vector2Int? rawCell = grid.GetCellFromScreen(mousePos);

        if (rawCell.HasValue)
        {
            int col  = Mathf.Clamp(rawCell.Value.x - w / 2, 0, Mathf.Max(0, grid.Columns - w));
            int row  = Mathf.Clamp(rawCell.Value.y - h / 2, 0, Mathf.Max(0, grid.Rows    - h));
            var snap = new Vector2Int(col, row);

            if (grid.IsValidPlacement(_held.Item.size, snap, _held.Rotated))
            {
                StopBlink();      // stop coroutine before placing; PlaceItem sets the final color
                grid.PlaceItem(_held, snap, _held.Rotated);
                _held       = null;
                _justPlaced = true;
            }
            else
            {
                ShowPopup(invalidMsg);
                StopBlink();
                _blinkCo = StartCoroutine(BlinkRed(_held));
            }
        }
        else
        {
            ShowPopup(invalidMsg);
            StopBlink();
            _blinkCo = StartCoroutine(BlinkRed(_held));
        }
    }

    public void HandlePointerPosition(OnPointerPositionEvent e)
    {
        if (InventoryNavigator.Instance != null && InventoryNavigator.Instance.IsNavigating) return;
        _currentMousePos = e.Position;
    }

    // ── Public method to place/discard from the navigator ────────────────

    public void TryPlaceOrDiscard(Vector2 screenPos)
    {
        if (_held == null) return;
        var grid = InventoryGridUI.Instance;
        if (grid == null) return;

        if (grid.IsMouseOver(grid.DiscardSlot, screenPos))
        {
            Debug.Log($"[Inventory] Discarded {_held.Item.name}.");
            StopBlink();
            Destroy(_held.gameObject);
            _held = null;
            return;
        }

        int w = _held.Rotated ? _held.Item.size.y : _held.Item.size.x;
        int h = _held.Rotated ? _held.Item.size.x : _held.Item.size.y;
        Vector2Int? rawCell = grid.GetCellFromScreen(screenPos);

        if (rawCell.HasValue)
        {
            int col  = Mathf.Clamp(rawCell.Value.x - w / 2, 0, Mathf.Max(0, grid.Columns - w));
            int row  = Mathf.Clamp(rawCell.Value.y - h / 2, 0, Mathf.Max(0, grid.Rows    - h));
            var snap = new Vector2Int(col, row);

            if (grid.IsValidPlacement(_held.Item.size, snap, _held.Rotated))
            {
                StopBlink();
                grid.PlaceItem(_held, snap, _held.Rotated);
                _held       = null;
                _justPlaced = true;
            }
            else
            {
                ShowPopup(invalidMsg);
                StopBlink();
                _blinkCo = StartCoroutine(BlinkRed(_held));
            }
        }
        else
        {
            ShowPopup(invalidMsg);
            StopBlink();
            _blinkCo = StartCoroutine(BlinkRed(_held));
        }
    }

    public void ClearHeldItem()
    {
        StopBlink(); // stop coroutine; PlaceItem already handled the color before this call
        _held = null;
    }

    // ── Update (visual only) ──────────────────────────────────────────────

    void Update()
    {
        if (_justPlaced) _justPlaced = false;

        if (InventoryNavigator.Instance != null && InventoryNavigator.Instance.IsNavigating)
            return;

        if (_held == null) return;
        var grid = InventoryGridUI.Instance;
        if (grid == null) return;

        Vector2 mousePos = _currentMousePos;

        if (grid.IsMouseOver(grid.DiscardSlot, mousePos))
        {
            _held.FollowScreen(mousePos);
            if (!_blinking) _held.SetDragColor(new Color(1f, 0.2f, 0.2f, 0.95f));
            return;
        }

        int w = _held.Rotated ? _held.Item.size.y : _held.Item.size.x;
        int h = _held.Rotated ? _held.Item.size.x : _held.Item.size.y;
        Vector2Int? rawCell = grid.GetCellFromScreen(mousePos);

        if (rawCell.HasValue)
        {
            int col  = Mathf.Clamp(rawCell.Value.x - w / 2, 0, Mathf.Max(0, grid.Columns - w));
            int row  = Mathf.Clamp(rawCell.Value.y - h / 2, 0, Mathf.Max(0, grid.Rows    - h));
            var snap = new Vector2Int(col, row);

            bool valid = grid.IsValidPlacement(_held.Item.size, snap, _held.Rotated);
            _held.Reposition(snap, _held.Rotated);
            if (!_blinking) _held.SetDragColor(valid ? validColor : invalidColor);
        }
        else
        {
            _held.FollowScreen(mousePos);
            if (!_blinking) _held.SetDragColor(invalidColor);
        }
    }

    // ── Cancel ────────────────────────────────────────────────────────────

    void Cancel()
    {
        if (_held == null) return;

        StopBlink(); // stop coroutine before restoring item position

        var grid = InventoryGridUI.Instance;
        if (_isNew)
        {
            Destroy(_held.gameObject);
        }
        else if (_isFromWildcard)
        {
            grid.PlaceInWildcard(_held);
        }
        else
        {
            grid.PlaceItem(_held, _cancelOrigin, _cancelRotated);
        }

        _held = null;
    }
}
