using UnityEngine;

[CreateAssetMenu(
    fileName = "New Weapon",
    menuName = "Objects/Weapon",
    order = 0)]
public class WeaponSO : ScriptableObject
{
    [Header("Stats")]
    public WeaponType weaponType;
    public ShotType shotType;
    public AmmoSO ammo;
    public int damage;
    public float weight;

    [Header("Fire")]
    public float fireRate = 8f;
    public int pellets = 1;
    public float spreadAngle = 0f;

    [Header("Prefab")]
    public GameObject weaponPrefab;
}