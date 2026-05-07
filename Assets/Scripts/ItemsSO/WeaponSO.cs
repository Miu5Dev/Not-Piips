using UnityEngine;

[CreateAssetMenu(
    fileName = "New Weapon",
    menuName  = "Objects/Weapon",
    order     = 0)]
public class WeaponSO : itemSO
{
    [Header("Stats")]
    public WeaponType weaponType;
    public ShotType   shotType;
    public AmmoSO     ammo;
    public int        damage;
    public int        maxMagazineSize;

    [Header("Fire")]
    public float fireRate    = 8f;
    public int   pellets     = 1;

    [Header("Spread")]
    public float spreadAngle          = 0f;
    public bool  spreadOnlyHorizontal = false; // ← nuevo

    [Header("Reload")]
    public float reloadTime = 1.5f;

    [Header("Prefab")]
    public GameObject weaponPrefab;
}
