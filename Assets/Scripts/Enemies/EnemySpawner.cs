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
    [Tooltip("How many units above the spawner's Y to start the ground raycast. Keep this low so low-ceiling rooms don't spawn enemies on roofs.")]
    [SerializeField] private float spawnHeightCheck = 4f;
    [Tooltip("Only these layers count as valid ground. Assign your Ground layer here to avoid hitting enemies, triggers or props.")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [Tooltip("How many random points to try before falling back to the spawner's own position.")]
    [SerializeField] private int maxSpawnAttempts = 10;
    [Tooltip("Vertical offset added to the spawn point so the enemy doesn't clip through the floor. Adjust to match the enemy's pivot height.")]
    [SerializeField] private float spawnGroundOffset = 0.1f;

    [Header("Wave Settings")]
    [SerializeField] private int   enemiesPerWave    = 5;
    [SerializeField] private float timeBetweenSpawns = 0.3f;
    [SerializeField] private float timeBetweenWaves  = 5f;
    [SerializeField] private bool  infiniteWaves     = true;
    [SerializeField] private int   maxWaves          = 5;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    [Header("Room Settings")]
    [Tooltip("Disable when this spawner lives inside a room prefab — RoomController will call StartSpawning() instead. Enabling this AND having RoomController call StartSpawning() will double-spawn enemies.")]
    [SerializeField] private bool autoStart = true;

    // Invoked once when all waves are done (only fires if infiniteWaves = false).
    public System.Action OnAllCleared;

    // ── State ─────────────────────────────────────────────────────────────────
    private int  _currentWave;
    private int  _aliveEnemies;
    private bool _waveInProgress;
    private bool _started;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Start()
    {
        if (autoStart)
            StartSpawning();
    }

    /// <summary>Called by RoomController after setting the player transform.</summary>
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
            yield return StartCoroutine(SpawnWave());

            // Wait until every enemy from this wave is dead before moving on
            yield return new WaitUntil(() => _aliveEnemies <= 0);
            yield return new WaitForSeconds(timeBetweenWaves);

            _currentWave++;
        }

        OnAllCleared?.Invoke();
    }

    private IEnumerator SpawnWave()
    {
        _waveInProgress = true;

        for (int i = 0; i < enemiesPerWave; i++)
        {
            SpawnOne();
            yield return new WaitForSeconds(timeBetweenSpawns);
        }

        _waveInProgress = false;
    }

    private void SpawnOne()
    {
        EnemySO type  = enemyTypes[Random.Range(0, enemyTypes.Length)];
        Vector3 spawn = FindSpawnPosition();

        EnemyController enemy = EnemyPool.Instance.GetEnemy(type);
        enemy.transform.position = spawn;

        _aliveEnemies++;
        enemy.Initialize(type, playerTransform, () => _aliveEnemies--);
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

            // Origin starts above the expected floor level so the ray travels downward through geometry
            Vector3 origin = transform.position + new Vector3(circle.x, spawnHeightCheck, circle.y);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, castDistance, groundLayers))
            {
                // Reject walls and ceilings — only surfaces pointing mostly upward are valid floors
                if (hit.normal.y > 0.5f)
                    return hit.point + Vector3.up * spawnGroundOffset;
            }
        }

        // Fallback: spawner's own position (should always have solid ground beneath it)
        Debug.LogWarning($"[EnemySpawner] Could not find a valid spawn position after {maxSpawnAttempts} attempts on {gameObject.name}.");
        return transform.position + Vector3.up * spawnGroundOffset;
    }

    public void SetPlayerTransform(Transform target)
    {
        playerTransform = target;
    }

    // =========================================================
    // EDITOR GIZMO
    // =========================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Spawn radius ring
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // Raycast origin height indicator
        Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * spawnHeightCheck, 0.2f);

        // Raycast total length visualized as a vertical line
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Vector3 rayStart = transform.position + Vector3.up * spawnHeightCheck;
        Vector3 rayEnd   = rayStart + Vector3.down * (spawnHeightCheck + 5f);
        Gizmos.DrawLine(rayStart, rayEnd);

        // Ground offset indicator at spawner position
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * spawnGroundOffset, 0.15f);
    }
#endif
}
