using UnityEngine;

public class InventoryEquipHandler : MonoBehaviour
{
    InventoryItemUI _currentlyEquipped;

    public void HandleEquip(OnEquipKeyEvent e)
    {
        if (!e.pressed) return;

        InventoryItemUI target;

        // Priority 1: WASD navigator cursor (controller / keyboard mode).
        // Priority 2: mouse hover (mouse mode).
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

        EventBus.Raise(new OnWeaponEquipEvent { weaponToEquip = weapon });
        InventoryDragHandler.Instance?.ShowPopup($"{weapon.name} Equipped!");

        InventoryNavigator.Instance?.HandleEquip(e);
    }
}
