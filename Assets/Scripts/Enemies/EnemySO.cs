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

    [Header("AI - Line of Sight & Flanking")]
    [Tooltip("Layers that block sight/bullets AND count as ground for jump detection. Should NOT include the player or enemies.")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("How high above the player's feet the LoS ray aims (chest height).")]
    public float losTargetHeight = 1.0f;

    [Header("AI - Jumping")]
    public float jumpForce = 6f;
    public float jumpCooldown = 0.8f;

    [Header("AI - Panic")]
    [Tooltip("Seconds without line of sight before the enemy switches from simple pursuit to aggressive flanking.")]
    public float panicTime = 5f;
    [Tooltip("Speed multiplier (on moveSpeed) when sight is lost but panic hasn't started. >1 = actively hunts faster than normal pursuit.")]
    public float huntSpeedMult = 1.2f;
    [Tooltip("Base strafe speed multiplier while panicking (applied to lateralStrength). Lower = calmer side-to-side movement.")]
    public float aggroStrafeMult = 1.4f;
    [Tooltip("Seconds AFTER panic begins to reach full desperation (max strafe/backup amplification).")]
    public float desperationRampTime = 4f;
    [Tooltip("Maximum extra strafe magnitude added at full desperation. 1.5 = strafe up to 2.5x its base.")]
    public float desperationStrafeBoost = 1.5f;
    [Tooltip("Maximum extra backup duration added at full desperation. 1.0 = backup lasts up to 2x as long.")]
    public float desperationBackupBoost = 1f;

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