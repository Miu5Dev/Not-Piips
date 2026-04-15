using UnityEngine;

[CreateAssetMenu(
    fileName = "New Weapon",
    menuName = "Object/Weapon/AmmoType",
    order = 0)]
public class AmmoSO : ScriptableObject
{
    [Header("Stats")]
    public WeaponType weaponType;
    public int damage;
    public float speed;
    public float gravityForce;
    
    [Header("Ammo Prefab")]
    public GameObject ammoPrefab;
}