using System.Collections;
using TMPro;
using UnityEngine;

public class InventoryDragHandler : MonoBehaviour
{
    public static InventoryDragHandler Instance { get; private set; }

    [SerializeField] Color validColor   = new Color(0.35f, 1f,    0.35f, 0.85f);
    [SerializeField] Color invalidColor = new Color(1f,    0.25f, 0.25f, 0.85f);
    [SerializeField] string invalidMsg  = "You can not place it here";
    [SerializeField] float  popupSeconds = 1.5f;

    InventoryItemUI _held;
    bool            _isNew;
    bool            _isFromWildcard;
    Vector2Int      _cancelOrigin;
    bool            _cancelRotated;
    int             _dragStartFrame = -1;
    bool            _blinking;

    TextMeshProUGUI _popupText;
    Coroutine       _popupCo;

    public bool IsDragging => _held != null;

    void Awake()
    {
        Instance = this;
        CreatePopup();
    }

    void OnDisable() { if (_held != null) Cancel(); }

    // ── Popup ─────────────────────────────────────────────────────────────────

    void CreatePopup()
    {
        var go = new GameObject("InvalidPopup", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 12f);
        rt.sizeDelta        = new Vector2(500f, 60f);

        _popupText = go.GetComponent<TextMeshProUGUI>();
        _popupText.text          = string.Empty;
        _popupText.fontSize      = 28;
        _popupText.alignment     = TextAlignmentOptions.Center;
        _popupText.color         = new Color(1f, 0.3f, 0.3f, 1f);
        _popupText.fontStyle     = FontStyles.Bold;
        _popupText.raycastTarget = false;
        go.SetActive(false);
    }

    void ShowPopup(string msg)
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
            t += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, t / 0.4f);
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
        if (item != null) item.SetDragColor(invalidColor);
        _blinking = false;
    }

    // ── Begin drag ────────────────────────────────────────────────────────────

    public void BeginDrag(itemSO item)
    {
        if (_held != null || InventoryGridUI.Instance == null) return;
        _held           = InventoryGridUI.Instance.CreateFloatingVisual(item, false);
        _isNew          = true;
        _isFromWildcard = false;
        _dragStartFrame = Time.frameCount;
    }

    public void BeginDragExisting(InventoryItemUI view)
    {
        if (_held != null || InventoryGridUI.Instance == null) return;

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

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_held == null) return;
        var grid = InventoryGridUI.Instance;
        if (grid == null) return;

        if (Input.GetKeyDown(KeyCode.R))
            _held.Reposition(_held.Origin, !_held.Rotated);

        if (Input.GetMouseButtonDown(1)) { Cancel(); return; }

        // Skip the click that started the drag this frame
        bool placeClick = Input.GetMouseButtonDown(0) && Time.frameCount != _dragStartFrame;

        // Discard slot has priority over grid — drop here to delete the item
        if (grid.IsMouseOver(grid.DiscardSlot, Input.mousePosition))
        {
            _held.FollowScreen(Input.mousePosition);
            if (!_blinking) _held.SetDragColor(new Color(1f, 0.2f, 0.2f, 0.95f));

            if (placeClick)
            {
                Debug.Log($"[Inventory] Discarded {_held.Item.name}.");
                Destroy(_held.gameObject);
                _held = null;
            }
            return;
        }

        int w = _held.Rotated ? _held.Item.size.y : _held.Item.size.x;
        int h = _held.Rotated ? _held.Item.size.x : _held.Item.size.y;

        Vector2Int? rawCell = grid.GetCellFromScreen(Input.mousePosition);

        if (rawCell.HasValue)
        {
            int col  = Mathf.Clamp(rawCell.Value.x - w / 2, 0, Mathf.Max(0, grid.Columns - w));
            int row  = Mathf.Clamp(rawCell.Value.y - h / 2, 0, Mathf.Max(0, grid.Rows    - h));
            var snap = new Vector2Int(col, row);

            bool valid = grid.IsValidPlacement(_held.Item.size, snap, _held.Rotated);
            _held.Reposition(snap, _held.Rotated);
            if (!_blinking) _held.SetDragColor(valid ? validColor : invalidColor);

            if (placeClick)
            {
                if (valid)
                {
                    grid.PlaceItem(_held, snap, _held.Rotated);
                    _held = null;
                }
                else
                {
                    ShowPopup(invalidMsg);
                    StartCoroutine(BlinkRed(_held));
                }
            }
        }
        else
        {
            _held.FollowScreen(Input.mousePosition);
            if (!_blinking) _held.SetDragColor(invalidColor);

            if (placeClick)
            {
                ShowPopup(invalidMsg);
                StartCoroutine(BlinkRed(_held));
            }
        }
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    void Cancel()
    {
        if (_held == null) return;

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
