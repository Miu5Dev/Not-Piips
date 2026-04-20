using UnityEngine;

public class OpenIventory : MonoBehaviour
{
    void Start()
    {
        EventBus.Subscribe<OnOpenInventoryEvent>(OnOpenInventory);
        gameObject.SetActive(false);
    }

    void OnDestroy() => EventBus.Unsubscribe<OnOpenInventoryEvent>(OnOpenInventory);

    void OnOpenInventory(OnOpenInventoryEvent e)
    {
        if (e.pressed)
            gameObject.SetActive(!gameObject.activeSelf);
    }
}
