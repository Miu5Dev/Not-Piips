using UnityEngine;

public class InventoryEquipHandler : MonoBehaviour
{
    InventoryItemUI _currentlyEquipped;

    void OnEnable()  => EventBus.Subscribe<OnEquipKeyEvent>(HandleEquip);
    void OnDisable() => EventBus.Unsubscribe<OnEquipKeyEvent>(HandleEquip);

    void HandleEquip(OnEquipKeyEvent e)
    {
        if (!e.pressed) return;

        var target = InventoryItemUI.HoveredItem;
        if (target == null && InventoryNavigator.Instance != null)
            target = InventoryNavigator.Instance.GetCurrentItem();

        if (target == null) return;
        if (target.Item is not WeaponSO weapon) return;

        if (_currentlyEquipped != null && _currentlyEquipped != target)
            _currentlyEquipped.SetEquipped(false);

        _currentlyEquipped = target;
        target.SetEquipped(true);

        EventBus.Raise(new OnWeaponEquipEvent { weaponToEquip = weapon });
        InventoryDragHandler.Instance?.ShowPopup($"{weapon.name} Equipped!");
    }
}
