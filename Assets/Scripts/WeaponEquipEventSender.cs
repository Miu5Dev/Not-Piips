using UnityEngine;

public class WeaponEquipEventSender : MonoBehaviour
{
    public void EquipWeapon(WeaponSO Weapon)
    {
        EventBus.Raise(new OnWeaponEquipEvent()
        {
            weaponToEquip = Weapon
        });
    }
}
