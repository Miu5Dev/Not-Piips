using System.Collections;
using UnityEngine;

/// <summary>
/// Place on the root of every room prefab.
/// All DoorControllers anywhere in the hierarchy are auto-discovered.
/// No fixed entrance/exit — RoomManager picks the entry door at spawn time.
/// </summary>
public class RoomController : MonoBehaviour
{
    [Header("Enemies")]
    [SerializeField] private EnemySpawner[] spawners;
    [Tooltip("When true, all exit doors stay locked until every spawner finishes.")]
    [SerializeField] private bool requireEnemiesCleared = true;

    [Header("Boss")]
    [Tooltip("Optional. If assigned, doors stay locked until the boss is defeated — independent of spawners.")]
    [SerializeField] private BossController boss;

    [Header("Entry Lock")]
    [Tooltip("If true, the entry door closes behind the player after they enter. Enable on the boss room prefab.")]
    [SerializeField] private bool lockEntryOnEnter;
    [Tooltip("Seconds after the room spawns before the entry door closes behind the player.")]
    [SerializeField] private float lockEntryDelay = 2f;

    public bool IsCleared { get; private set; }

    public DoorController EntranceDoor { get; private set; }
    public DoorController ExitDoor     { get; private set; }
    public void SetExitDoor(DoorController door) => ExitDoor = door;

    private DoorController[] _doors;
    private int  _spawnersCleared;
    private bool _bossDefeated;
    private bool _bossPending;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        _doors = GetComponentsInChildren<DoorController>();
        if (_doors.Length == 0)
            Debug.LogWarning($"[RoomController] '{name}' has no DoorControllers in children.");
    }

    private void OnDestroy()
    {
        UnsubscribeSpawners();
        if (boss != null) boss.OnDefeated -= HandleBossDefeated;
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public DoorController GetRandomDoor()
    {
        if (_doors.Length == 0) return null;
        if (_doors.Length == 1)
            Debug.LogWarning($"[RoomController] '{name}' has only 1 door — it will have no usable exits after entry is sealed.");
        return _doors[Random.Range(0, _doors.Length)];
    }

    public void Initialize(bool isStartRoom, Transform playerTransform = null, DoorController entryDoor = null)
    {
        EntranceDoor = entryDoor;

        if (entryDoor != null)
        {
            entryDoor.SetState(DoorState.Sealed);
            entryDoor.ClearBlocker();
            if (lockEntryOnEnter)
                StartCoroutine(LockEntryAfterDelay());
        }

        bool hasBoss           = boss != null;
        bool clearsImmediately = isStartRoom || (spawners.Length == 0 && !hasBoss) || !requireEnemiesCleared;
        DoorState exitState    = clearsImmediately ? DoorState.Unlocked : DoorState.Locked;

        foreach (var door in _doors)
        {
            if (door != entryDoor)
                door.SetState(exitState);
        }

        if (clearsImmediately)
        {
            IsCleared = true;
            SubscribeAndStartSpawners(playerTransform);
            return;
        }

        _spawnersCleared = 0;
        SubscribeAndStartSpawners(playerTransform);

        if (hasBoss)
        {
            _bossPending  = true;
            _bossDefeated = false;
            boss.OnDefeated += HandleBossDefeated;
        }
    }

    public void CutAllDoorWalls()
    {
        foreach (var door in _doors)
            door.CutWallNow();
    }

    // =========================================================
    // PRIVATE
    // =========================================================

    private void SubscribeAndStartSpawners(Transform playerTransform)
    {
        if (spawners == null || spawners.Length == 0) return;

        foreach (var spawner in spawners)
        {
            spawner.OnAllCleared   += HandleSpawnerCleared;
            spawner.OnStateChanged += BroadcastRoomState;
            if (playerTransform != null)
                spawner.SetPlayerTransform(playerTransform);
            spawner.StartSpawning();
        }

        BroadcastRoomState();
    }

    private void UnsubscribeSpawners()
    {
        if (spawners == null) return;
        foreach (var spawner in spawners)
        {
            if (spawner == null) continue;
            spawner.OnAllCleared   -= HandleSpawnerCleared;
            spawner.OnStateChanged -= BroadcastRoomState;
        }
    }

    private IEnumerator LockEntryAfterDelay()
    {
        yield return new WaitForSeconds(lockEntryDelay);
        if (EntranceDoor == null) yield break;
        EntranceDoor.gameObject.SetActive(true);
        EntranceDoor.CloseAndThen(null);
    }

    private void HandleSpawnerCleared()
    {
        _spawnersCleared++;
        TryFinishClear();
    }

    private void HandleBossDefeated()
    {
        _bossDefeated = true;
        TryFinishClear();
    }

    private void TryFinishClear()
    {
        if (_spawnersCleared < spawners.Length) return;
        if (_bossPending && !_bossDefeated) return;

        IsCleared = true;

        foreach (var door in _doors)
        {
            if (door != EntranceDoor)
                door.SetState(DoorState.Unlocked);
        }

        EventBus.Raise(new OnRoomClearedEvent { room = this });
    }

    private void BroadcastRoomState()
    {
        int killedThisWave = 0, enemiesPerWave = 0;
        int currentWave    = 0, totalWaves     = 0;

        foreach (var spawner in spawners)
        {
            killedThisWave += spawner.EnemiesKilledThisWave;
            enemiesPerWave += spawner.EnemiesPerWave;

            if (spawner.CurrentWave > currentWave)
            {
                currentWave = spawner.CurrentWave;
                totalWaves  = spawner.TotalWaves;
            }
        }

        EventBus.Raise(new OnRoomStateChangedEvent
        {
            EnemiesKilledThisWave = killedThisWave,
            EnemiesPerWave        = enemiesPerWave,
            CurrentWave           = currentWave,
            TotalWaves            = totalWaves
        });
    }
}
