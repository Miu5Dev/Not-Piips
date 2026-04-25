using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(
    fileName = "New Weapon",
    menuName = "Objects/Weapon",
    order = 0)]
public class WeaponSO : itemSO
{
    [Header("Stats")]
    public WeaponType weaponType;
    public ShotType shotType;
    public AmmoSO ammo;
    public int damage;
    public int maxCartridges;

    [Header("Fire")]
    public float fireRate = 8f;
    public int pellets = 1;
    public float spreadAngle = 0f;

    [Header("Prefab")]
    public GameObject weaponPrefab;
    
}