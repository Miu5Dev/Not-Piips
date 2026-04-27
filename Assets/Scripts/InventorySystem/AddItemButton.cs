using UnityEngine;

public class AddItemButton : MonoBehaviour
{
    [SerializeField] itemSO item;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
            AddItem();
    }

    // Public so other scripts can call this directly to add the item
    public void AddItem()
    {
        if (InventoryGridUI.Instance == null) return;
        InventoryGridUI.Instance.TryAddItem(item);
    }
}
