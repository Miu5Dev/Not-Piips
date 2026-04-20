using UnityEngine;

public class OpenIventory : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EventBus.Subscribe<OnOpenInventoryEvent>(OnOpenInventory);
    }
    void OnDisable() => EventBus.Unsubscribe<OnOpenInventoryEvent>(OnOpenInventory);

    void OnOpenInventory(OnOpenInventoryEvent e)
    {
        if (e.pressed)
        {
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
