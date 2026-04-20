using UnityEngine;

[CreateAssetMenu(
    fileName = "New Ammo",
    menuName  = "Objects/Ammo",
    order     = 0)]
public class AmmoSO : itemSO
{
    [Header("Stats")]
    public float speed;
    public float gravityForce;

    [Header("Ammo Prefab")]
    public GameObject ammoPrefab;

    [Header("Decal")]
    [Tooltip("Prefab spawned on the surface when the bullet hits.\nCan be a URP DecalProjector or a simple quad with a sprite material.")]
    public GameObject decalPrefab;
}
