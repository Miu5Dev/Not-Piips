using UnityEngine;
using UnityEngine.UI;

public class InventoryItemUI : MonoBehaviour
{
    public itemSO Item    { get; private set; }
    public Vector2Int Origin  { get; private set; }
    public bool Rotated   { get; private set; }

    public void Init(itemSO item, Vector2Int origin, bool rotated)
    {
        Item    = item;
        Origin  = origin;
        Rotated = rotated;

        var img = GetComponent<Image>();
        img.sprite          = item.icon;
        img.preserveAspect  = false;
        img.raycastTarget   = true;
    }
}
