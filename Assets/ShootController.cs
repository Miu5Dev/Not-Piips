using UnityEngine;

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

public class ShootController : MonoBehaviour
{
    [SerializeField] private AmmoSO ammoToShoot;
    [SerializeField] private Transform spawnpoint;

    public void Shoot()
    {
        Instantiate(ammoToShoot.ammoPrefab, spawnpoint.position, spawnpoint.rotation);
    }
    
}
