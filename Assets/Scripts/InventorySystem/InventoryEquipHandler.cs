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

        // ── Wildcard items cannot be used or equipped ─────────────────────
        if (target.InWildcard)
        {
            InventoryDragHandler.Instance?.ShowPopup("Move item to inventory first!");
            InventoryNavigator.Instance?.HandleEquip(e);
            return;
        }

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
                    int consumed = AmmoInventory.Consume(ammoItem, 1);
                    if (consumed > 0)
                    {
                        shooter.AddAmmo(shooter.MaxMagazineSize);
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
                    healthType  = healthItem.healthType,
                    amount      = (int)healthItem.restoreAmount,
                    target      = gameObject.transform.root.gameObject,
                    WeakPointHit = false
                });

                InventoryDragHandler.Instance?.ShowPopup(
                    $"+{healthItem.restoreAmount} {healthItem.healthType}!"
                );

                if (target.StackCount <= 0)
                    InventoryGridUI.Instance?.RemoveItem(target);
            }

            InventoryNavigator.Instance?.HandleEquip(e);
            return;
        }

// ── Equipar arma ──────────────────────────────────────────────────────────
        if (target.Item is not WeaponSO weapon) return;

// Si ya está equipada, desequipar
        if (_currentlyEquipped == target)
        {
            UnequipCurrent();
            InventoryDragHandler.Instance?.ShowPopup($"{weapon.name} Unequipped!");
            InventoryNavigator.Instance?.HandleEquip(e);
            return;
        }

        if (_currentlyEquipped != null)
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
    /// Desequipa el arma actual del inventario.
    /// Limpia el highlight en la UI y notifica al ShootController
    /// para que active el arma backup.
    /// </summary>
    public void UnequipCurrent()
    {
        if (_currentlyEquipped == null) return;

        _currentlyEquipped.SetEquipped(false);
        _currentlyEquipped = null;

        // Notificar al ShootController — weaponToEquip = null activa el backup
        EventBus.Raise(new OnWeaponEquipEvent
        {
            weaponToEquip = null,
            initialAmmo   = 0
        });
    }
}