using UnityEngine;

public class InventoryEquipHandler : MonoBehaviour
{
    public static InventoryEquipHandler Instance { get; private set; }

    InventoryItemUI _currentlyEquipped;

    public InventoryItemUI EquippedItem => _currentlyEquipped;

    void Awake() => Instance = this;

    public void HandleEquip(OnEquipKeyEvent e)
    {
        if (!e.pressed) return;

        InventoryItemUI target;

        if (InventoryNavigator.Instance != null && InventoryNavigator.Instance.IsNavigating)
            target = InventoryNavigator.Instance.GetCurrentItem();
        else
            target = InventoryItemUI.HoveredItem;

        if (target == null) return;

        // ── Recarga instantánea al equipar munición compatible ────────────
        if (target.Item is AmmoSO ammoItem)
        {
            var shooter = ShootController.Instance;
            if (shooter != null
                && shooter.CurrentWeapon != null
                && shooter.CurrentWeapon.ammo == ammoItem
                && shooter.CurrentMagazine < shooter.MaxMagazineSize)
            {
                bool isManual = shooter.CurrentWeapon.shotType == ShotType.Manual;

                if (isManual)
                {
                    // 1 item del stack = 1 bala → consume lo que necesite hasta llenar
                    int needed   = shooter.MaxMagazineSize - shooter.CurrentMagazine;
                    int consumed = AmmoInventory.Consume(ammoItem, needed);
                    if (consumed > 0)
                    {
                        shooter.AddAmmo(consumed);
                        InventoryDragHandler.Instance?.ShowPopup(
                            $"Loaded {consumed} rounds ({shooter.CurrentMagazine}/{shooter.MaxMagazineSize})"
                        );
                    }
                }
                else
                {
                    // 1 item del stack = cargador entero → consume exactamente 1
                    int consumed = AmmoInventory.Consume(ammoItem, 1);
                    if (consumed > 0)
                    {
                        shooter.AddAmmo(shooter.MaxMagazineSize); // llena el cargador
                        InventoryDragHandler.Instance?.ShowPopup(
                            $"Magazine loaded! ({shooter.CurrentMagazine}/{shooter.MaxMagazineSize})"
                        );
                    }
                }
            }
            InventoryNavigator.Instance?.HandleEquip(e);
            return;
        }
        
        // ── Usar item de salud ────────────────────────────────────────────
        if (target.Item is HealthSO healthItem)
        {
            if (target.StackCount > 0)
            {
                target.RemoveFromStack(1);

                EventBus.Raise(new OnHealthChangeEvent
                {
                    healthType   = healthItem.healthType,
                    amount       = (int)healthItem.restoreAmount,
                    target       = gameObject.transform.root.gameObject,
                    WeakPointHit = false
                });

                InventoryDragHandler.Instance?.ShowPopup(
                    $"+{healthItem.restoreAmount} {healthItem.healthType}!"
                );

                // Si se agotó el stack, eliminar el item del inventario
                if (target.StackCount <= 0)
                    InventoryGridUI.Instance?.RemoveItem(target);
            }

            InventoryNavigator.Instance?.HandleEquip(e);
            return;
        }
        
        // ─────────────────────────────────────────────────────────────────

        if (target.Item is not WeaponSO weapon) return;

        if (_currentlyEquipped != null && _currentlyEquipped != target)
            _currentlyEquipped.SetEquipped(false);

        _currentlyEquipped = target;
        target.SetEquipped(true);

        EventBus.Raise(new OnWeaponEquipEvent
        {
            weaponToEquip = weapon,
            initialAmmo   = target.StoredAmmo
        });

        InventoryDragHandler.Instance?.ShowPopup($"{weapon.name} Equipped!");
        InventoryNavigator.Instance?.HandleEquip(e);
    }
    
    /// <summary>
    /// Desequipa el arma actual del inventario (limpia el highlight en la UI).
    /// Llamado automáticamente por ShootController al activar el arma backup.
    /// </summary>
    public void UnequipCurrent()
    {
        if (_currentlyEquipped == null) return;
        _currentlyEquipped.SetEquipped(false);
        _currentlyEquipped = null;
    }
}