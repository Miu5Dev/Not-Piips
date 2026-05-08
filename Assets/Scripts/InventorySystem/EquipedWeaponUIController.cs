using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipedWeaponUIController : MonoBehaviour
{
    public static EquipedWeaponUIController Instance { get; private set; }

    [Header("Weapon Info")]
    public TMP_Text  Magazines;
    public TMP_Text  Bullets;
    public Image     currentWeaponImage;
    public Image     AmmoImage;

    void Awake()
    {
        Instance = this;
    }

    public void UpdateDisplay(OnWeaponEquipEvent e)
    {
        if (e.weaponToEquip == null) return;

        currentWeaponImage.sprite = e.weaponToEquip.icon;

        if (e.weaponToEquip.ammo != null)
            AmmoImage.sprite = e.weaponToEquip.ammo.icon;

        RefreshAmmo();
    }

    public void RefreshAmmo()
    {
        if (ShootController.Instance == null) return;

        int inMag  = ShootController.Instance.CurrentMagazine;
        int maxMag = ShootController.Instance.MaxMagazineSize;

        Bullets.text = $"{inMag}";

        WeaponSO weapon = ShootController.Instance.CurrentWeapon;

        if (weapon == null)
        {
            Magazines.text = "—";
            return;
        }

        if (weapon.infiniteAmmo)
        {
            Magazines.text = "∞";
            return;
        }

        if (weapon.ammo != null && InventoryGridUI.Instance != null)
        {
            int reserve = 0;
            foreach (var item in InventoryGridUI.Instance.GetAllItems())
            {
                if (item != null && item.Item == weapon.ammo)
                    reserve += item.StackCount;
            }
            Magazines.text = reserve.ToString();
        }
        else
        {
            Magazines.text = "—";
        }
    }
}