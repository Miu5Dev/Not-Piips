using UnityEngine;

public class OpenIventory : MonoBehaviour
{
    void Awake()
    {
        var rt = GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    void Start()
    {
        gameObject.SetActive(false);
    }

    public void OnOpenInventory(OnOpenInventoryEvent e)
    {
        if (e.pressed)
            gameObject.SetActive(!gameObject.activeSelf);
    }
}
