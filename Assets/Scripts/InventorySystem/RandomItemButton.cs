using System.Collections.Generic;
using UnityEngine;

public class RandomItemButton : MonoBehaviour
{
    [System.Serializable]
    public class ItemEntry
    {
        public itemSO item;
        [Min(1)] public int minAmount = 1;
        [Min(1)] public int maxAmount = 1;
        [Min(0)] public float weight = 1f;
    }

    [SerializeField] List<ItemEntry> possibleItems = new();

    readonly System.Random _rng = new System.Random(System.Guid.NewGuid().GetHashCode());

    public itemSO ChosenItem { get; private set; }
    public int ChosenAmount { get; private set; }

    void Start()
    {
        if (possibleItems.Count == 0) return;

        ItemEntry chosen = PickWeighted();
        if (chosen?.item == null) return;

        ChosenItem = chosen.item;
        ChosenAmount = chosen.item is WeaponSO
            ? 1
            : _rng.Next(chosen.minAmount, chosen.maxAmount + 1);

        GetComponent<WorldItemVisual>()?.Setup(ChosenItem, ChosenAmount);
    }

    ItemEntry PickWeighted()
    {
        float total = 0f;
        foreach (var e in possibleItems) total += e.weight;

        if (total <= 0f) return possibleItems[_rng.Next(possibleItems.Count)];

        float roll = (float)(_rng.NextDouble() * total);
        float cumulative = 0f;
        foreach (var e in possibleItems)
        {
            cumulative += e.weight;
            if (roll < cumulative) return e;
        }

        return possibleItems[^1];
    }

    public void AddItem()
    {
        if (InventoryGridUI.Instance == null || ChosenItem == null) return;

        for (int i = 0; i < ChosenAmount; i++)
            if (!InventoryGridUI.Instance.TryAddItem(ChosenItem)) break;

        Destroy(gameObject);
    }
}