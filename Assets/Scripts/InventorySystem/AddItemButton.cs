using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AddItemButton : MonoBehaviour
{
    [SerializeField] itemSO item;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (InventoryGridUI.Instance == null) return;

        if (!InventoryGridUI.Instance.TryAddItem(item))
            Debug.Log($"Inventory full — could not place {item.name}");
    }
}
