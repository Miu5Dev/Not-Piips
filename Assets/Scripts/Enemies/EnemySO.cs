using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy", menuName = "Objects/Enemy", order = 1)]
public class EnemySO : ScriptableObject
{
    [Header("Stats")]
    public int maxHealth = 100;
    public int maxShield = 0;
    public float moveSpeed = 3.5f;

    [Header("Combat Zone")]
    [Tooltip("Enemy retreats when closer than this.")]
    public float fromPlayerMin = 7f;
    [Tooltip("Enemy approaches when farther than this.")]
    public float fromPlayerMax = 14f;

    [Header("Wander")]
    public float wanderFrequency = 0.8f;
    [Tooltip("Max lateral speed while in comfort zone.")]
    public float lateralStrength = 2.5f;

    [Header("AI")]
    [Tooltip("Beyond this distance the tick rate drops to LOD rate.")]
    public float aiLodDistance = 80f;

    [Header("Weapons")]
    public WeaponSO[] availableWeapons;
    [Tooltip("Extra pause (seconds) added between every shot, on top of the weapon's fire rate.")]
    public float shootBuffer = 0.3f;

    [Header("Pool")]
    [Tooltip("Enemy Prefab to use :D")]
    public GameObject prefab;
    [Range(1, 30)]
    public int poolSize = 10;

    [Header("Death")]
    public GameObject deathEffect;
    [Tooltip("Freeze the rigidbody on death to prevent physics from launching the corpse.")]
    public bool freezeOnDeath = true;
}