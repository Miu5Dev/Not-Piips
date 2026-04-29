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

    [Header("Wave Settings")]
    [SerializeField] private int   enemiesPerWave    = 5;
    [SerializeField] private float timeBetweenSpawns = 0.3f;
    [SerializeField] private float timeBetweenWaves  = 5f;
    [SerializeField] private bool  infiniteWaves     = true;
    [SerializeField] private int   maxWaves          = 5;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    // ── State ─────────────────────────────────────────────────────────────────
    private int  _currentWave;
    private int  _aliveEnemies;
    private bool _waveInProgress;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Start()
    {
        if (enemyTypes == null || enemyTypes.Length == 0)
        {
            Debug.LogWarning($"[EnemySpawner] No enemy types assigned on {gameObject.name}.");
            return;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning($"[EnemySpawner] Player Transform not assigned on {gameObject.name}.");
            return;
        }

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

            // Wait until all enemies in the wave are dead
            yield return new WaitUntil(() => _aliveEnemies <= 0);
            yield return new WaitForSeconds(timeBetweenWaves);

            _currentWave++;
        }
    }

    private IEnumerator SpawnWave()
    {
        _waveInProgress = true;
        _aliveEnemies   = 0;

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
        Vector2 circle = Random.insideUnitCircle * spawnRadius;

        // Start just above the spawner's own height — avoids hitting ceilings in low rooms
        Vector3 origin = transform.position + new Vector3(circle.x, spawnHeightCheck, circle.y);

        // Cast down only far enough to find a floor near the spawner's level
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, spawnHeightCheck + 3f))
            return hit.point;

        // Fallback: spawner's XZ offset at spawner height
        return transform.position + new Vector3(circle.x, 0f, circle.y);
    }

    // =========================================================
    // EDITOR GIZMO
    // =========================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
#endif
}
