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

    [Header("Collision")]
    [Tooltip("Layers this bullet physically reacts to. Anything not in this mask is passed through.")]
    public LayerMask collisionLayers = ~0;

    [Header("Decal")]
    [Tooltip("Prefab spawned on the surface when the bullet hits.")]
    public GameObject decalPrefab;

    [Tooltip("Layers on which bullet decals are allowed to spawn.")]
    public LayerMask decalLayers = ~0;

    [Header("Impact VFX")]
    [Tooltip("Particle System GameObject spawned at the impact point.")]
    public GameObject impactVFXPrefab;
}