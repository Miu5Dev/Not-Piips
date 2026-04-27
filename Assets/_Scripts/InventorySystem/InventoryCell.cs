using UnityEngine;
using UnityEngine.UI;

public class InventoryCell : MonoBehaviour
{
    public int x;
    public int y;

    private Image _bg;
    private Image _border;

    public void Init(int cellX, int cellY, Color bgColor, Color borderColor)
    {
        x = cellX;
        y = cellY;

        _bg = GetComponent<Image>();
        _bg.color = bgColor;

        // Inner border via a child outline image
        var borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(transform, false);

        var rt = borderGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _border = borderGO.GetComponent<Image>();
        _border.color = borderColor;
        _border.raycastTarget = false;

        // Use Unity's built-in sliced sprite trick for a 1px border effect
        _border.type = Image.Type.Sliced;
        _border.fillCenter = false;
    }
}
