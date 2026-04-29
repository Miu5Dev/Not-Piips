using UnityEngine;

public class InventoryEquipHandler : MonoBehaviour
{
    public static InventoryEquipHandler Instance { get; private set; }

    InventoryItemUI _currentlyEquipped;

    // Instancia específica del item equipado (no el SO)
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
}