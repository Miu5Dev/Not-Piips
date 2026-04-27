using UnityEngine;

public class AddItemButton : MonoBehaviour
{
    [SerializeField] itemSO item;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (InventoryGridUI.Instance == null) return;

            if (!InventoryGridUI.Instance.TryAddItem(item))
                Debug.Log($"Inventory full — could not place {item.name}");
        }
    }
}
