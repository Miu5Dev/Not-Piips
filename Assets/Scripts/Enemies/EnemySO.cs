using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy", menuName = "Objects/Enemy", order = 1)]
public class EnemySO : ScriptableObject
{
    [Header("Stats")]
    public int   maxHealth = 100;
    public int   maxShield = 0;
    public float moveSpeed = 3.5f;

    [Header("Combat Zone")]
    [Tooltip("Enemy retreats when closer than this distance.")]
    public float fromPlayerMin = 7f;
    [Tooltip("Enemy approaches when farther than this distance.")]
    public float fromPlayerMax = 14f;

    [Header("Wander")]
    public float wanderFrequency = 0.8f;
    [Tooltip("Max lateral speed while inside the comfort zone.")]
    public float lateralStrength = 2.5f;

    [Header("AI")]
    [Tooltip("Beyond this distance the tick rate drops to the LOD rate.")]
    public float aiLodDistance = 80f;

    [Header("Weapons")]
    public WeaponSO[] availableWeapons;
    [Tooltip("Extra pause in seconds added between every shot, on top of the weapon's fire rate.")]
    public float shootBuffer = 0.3f;

    [Header("Pool")]
    [Tooltip("Enemy prefab to instantiate.")]
    public GameObject prefab;
    [Range(1, 30)]
    public int poolSize = 10;

    [Header("Death")]
    public GameObject deathEffect;
    [Tooltip("Freeze the Rigidbody on death to prevent physics from launching the corpse.")]
    public bool freezeOnDeath = true;

    [Header("Loot Drop")]
    [Tooltip("WorldItemVisual prefab to spawn when this enemy dies. Leave empty for no drop.")]
    public GameObject dropPrefab;
    [Tooltip("Probability of dropping the item (0 = never, 1 = always).")]
    [Range(0f, 1f)]
    public float dropChance = 1f;
    [Tooltip("Upward force applied to the dropped item on spawn.")]
    public float dropUpForce = 3f;
}