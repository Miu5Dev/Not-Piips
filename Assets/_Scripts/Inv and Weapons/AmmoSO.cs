using UnityEngine;

[CreateAssetMenu(
    fileName = "New Ammo",
    menuName = "Objects/Ammo",
    order = 0)]
public class AmmoSO : itemSO
{
    [Header("Stats")]
    public float speed;
    public float gravityForce;
    
    [Header("Ammo Prefab")]
    public GameObject ammoPrefab;
}