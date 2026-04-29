using System.Collections.Generic;
using UnityEngine;

/// Fachada estática para consultar y consumir munición del inventario.
/// Compara directamente el AmmoSO del arma con los items del grid —
/// no necesita ningún campo de enlace porque AmmoSO hereda de itemSO.
public static class AmmoInventory
{
    // ── Consulta ──────────────────────────────────────────────────────────

    /// Devuelve el total de balas disponibles del tipo indicado.
    public static int GetCount(AmmoSO ammo)
    {
        if (ammo == null || InventoryGridUI.Instance == null) return 0;

        int total = 0;
        foreach (InventoryItemUI item in InventoryGridUI.Instance.GetAllItems())
        {
            // item.Item es el itemSO; si es exactamente el mismo asset AmmoSO, cuenta
            if (item != null && item.Item == ammo)
                total += item.StackCount;
        }
        return total;
    }

    // ── Consumo ───────────────────────────────────────────────────────────

    /// Intenta consumir <amount> balas. Devuelve cuántas se consumieron realmente.
    public static int Consume(AmmoSO ammo, int amount)
    {
        if (ammo == null || amount <= 0 || InventoryGridUI.Instance == null)
            return 0;

        int remaining = amount;
        var toDestroy = new List<InventoryItemUI>();

        foreach (InventoryItemUI item in InventoryGridUI.Instance.GetAllItems())
        {
            if (remaining <= 0) break;
            if (item == null || item.Item != ammo) continue;

            int take = Mathf.Min(remaining, item.StackCount);
            item.RemoveFromStack(take);
            remaining -= take;

            if (item.StackCount <= 0)
                toDestroy.Add(item);
        }

        foreach (InventoryItemUI empty in toDestroy)
            InventoryGridUI.Instance.RemoveItem(empty);

        return amount - remaining;
    }
}