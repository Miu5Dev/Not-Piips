using UnityEngine;

[CreateAssetMenu(
    fileName = "New Ammo",
    menuName = "Objects/Ammo",
    order = 0)]
public class AmmoSO : ScriptableObject
{
    [Header("Stats")]
    public float speed;
    public float gravityForce;
    
    [Header("Ammo Prefab")]
    public GameObject ammoPrefab;
}

public enum WeaponType
{
    Pistol,
    Shotgun,
    Smg,
    Mg,
    Rifle,
    Melee
}

public enum ItemType
{
    Heal,
    Shield,
    Ammo,
    Stats,
    Equipment
}

public enum ShotType
{
    Automatic,
    SemiAutomatic,
    Manual
}