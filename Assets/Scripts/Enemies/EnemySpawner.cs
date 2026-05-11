using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to an empty GameObject. Spawns enemies in configurable waves,
/// waiting for the current wave to die before starting the next.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Types")]
    [Tooltip("One or more enemy types — a random type is picked per spawn.")]
    [SerializeField] private EnemySO[] enemyTypes;

    [Header("Spawn Area")]
    [SerializeField] private float spawnRadius = 20f;
    [Tooltip("How many units above the spawner's Y to start the ground raycast.")]
    [SerializeField] private float spawnHeightCheck = 4f;
    [Tooltip("Only these layers count as valid ground.")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [Tooltip("How many random points to try before falling back to the spawner's own position.")]
    [SerializeField] private int maxSpawnAttempts = 10;
    [Tooltip("Vertical offset added to the spawn point so the enemy doesn't clip through the floor.")]
    [SerializeField] private float spawnGroundOffset = 0.1f;

    [Header("Wave Settings")]
    [SerializeField] private int enemiesPerWave = 5;
    [SerializeField] private float timeBetweenSpawns = 0.3f;
    [SerializeField] private float timeBetweenWaves = 5f;
    [SerializeField] private bool infiniteWaves = true;
    [SerializeField] private int maxWaves = 5;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    [Header("Room Settings")]
    [Tooltip("Disable when this spawner lives inside a room prefab — RoomController will call StartSpawning() instead.")]
    [SerializeField] private bool autoStart = true;

    public System.Action OnAllCleared;
    public System.Action OnStateChanged;

    // ── HUD properties — per-wave, progressive ────────────────────────────

    /// <summary>Enemies killed in the CURRENT wave (resets to 0 each new wave).</summary>
    public int EnemiesKilledThisWave { get; private set; }

    /// <summary>Total enemies in the current wave (= enemiesPerWave).</summary>
    public int EnemiesPerWave        { get; private set; }

    /// <summary>Current wave (1-based).</summary>
    public int CurrentWave           { get; private set; }

    /// <summary>Total waves. 0 if infiniteWaves.</summary>
    public int TotalWaves            { get; private set; }

    // ── Internal state ─────────────────────────────────────────────────────
    private int  _currentWave;
    private int  _aliveEnemies;
    private bool _started;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Start()
    {
        if (autoStart)
            StartSpawning();
    }

    public void StartSpawning()
    {
        if (_started) return;
        _started = true;

        if (enemyTypes == null || enemyTypes.Length == 0)
        {
            Debug.LogWarning($"[EnemySpawner] No enemy types assigned on {gameObject.name}.");
            OnAllCleared?.Invoke();
            return;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning($"[EnemySpawner] Player Transform not assigned on {gameObject.name}.");
            OnAllCleared?.Invoke();
            return;
        }

        TotalWaves            = infiniteWaves ? 0 : maxWaves;
        EnemiesPerWave        = enemiesPerWave;
        CurrentWave           = 1;
        EnemiesKilledThisWave = 0;

        _currentWave  = 0;
        _aliveEnemies = 0;

        StartCoroutine(WaveLoop());
    }

    // =========================================================
    // WAVE LOOP
    // =========================================================

    private IEnumerator WaveLoop()
    {
        while (infiniteWaves || _currentWave < maxWaves)
        {
            // Reset per-wave kill counter at the start of each wave
            EnemiesKilledThisWave = 0;
            OnStateChanged?.Invoke();

            yield return StartCoroutine(SpawnWave());

            yield return new WaitUntil(() => _aliveEnemies <= 0);
            yield return new WaitForSeconds(timeBetweenWaves);

            _currentWave++;

            CurrentWave = infiniteWaves
                ? _currentWave + 1
                : Mathf.Min(_currentWave + 1, maxWaves);

            OnStateChanged?.Invoke();
        }

        OnAllCleared?.Invoke();
    }

    private IEnumerator SpawnWave()
    {
        for (int i = 0; i < enemiesPerWave; i++)
        {
            SpawnOne();
            yield return new WaitForSeconds(timeBetweenSpawns);
        }
    }

    private void SpawnOne()
    {
        EnemySO   type  = enemyTypes[Random.Range(0, enemyTypes.Length)];
        Vector3   spawn = FindSpawnPosition();

        EnemyController enemy = EnemyPool.Instance.GetEnemy(type);
        enemy.transform.position = spawn;

        _aliveEnemies++;

        enemy.Initialize(type, playerTransform, () =>
        {
            _aliveEnemies--;
            EnemiesKilledThisWave++;    // counts up within the current wave only
            OnStateChanged?.Invoke();
        });
    }

    // =========================================================
    // SPAWN POSITION
    // =========================================================

    private Vector3 FindSpawnPosition()
    {
        float castDistance = spawnHeightCheck + 5f;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            Vector3 origin = transform.position + new Vector3(circle.x, spawnHeightCheck, circle.y);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, castDistance, groundLayers))
            {
                if (hit.normal.y > 0.5f)
                    return hit.point + Vector3.up * spawnGroundOffset;
            }
        }

        Debug.LogWarning($"[EnemySpawner] Could not find a valid spawn position after {maxSpawnAttempts} attempts on {gameObject.name}.");
        return transform.position + Vector3.up * spawnGroundOffset;
    }

    public void SetPlayerTransform(Transform target) => playerTransform = target;

    // =========================================================
    // EDITOR GIZMO
    // =========================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * spawnHeightCheck, 0.2f);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Vector3 rayStart = transform.position + Vector3.up * spawnHeightCheck;
        Gizmos.DrawLine(rayStart, rayStart + Vector3.down * (spawnHeightCheck + 5f));

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * spawnGroundOffset, 0.15f);
    }
#endif
}
