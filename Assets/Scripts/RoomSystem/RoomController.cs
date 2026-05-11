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

    // The door this room was entered through — set by RoomManager at spawn time.
    public DoorController EntranceDoor { get; private set; }

    // The door the player used to leave this room — set by RoomManager when the
    // next room is opened. Used so the door can survive this room's destruction
    // and play a close animation to plug the hole in the surviving room's wall.
    public DoorController ExitDoor { get; private set; }
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

    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>Returns a random door from this room (used by RoomManager to pick the entry).</summary>
    public DoorController GetRandomDoor()
    {
        if (_doors.Length == 0) return null;
        if (_doors.Length == 1)
            Debug.LogWarning($"[RoomController] '{name}' has only 1 door — it will have no usable exits after entry is sealed.");
        return _doors[Random.Range(0, _doors.Length)];
    }

    /// <param name="isStartRoom">True for the room already in the scene at game start.</param>
    /// <param name="playerTransform">Forwarded to EnemySpawners.</param>
    /// <param name="entryDoor">The door on this room that aligns with the previous room. Null for start room.</param>
    public void Initialize(bool isStartRoom, Transform playerTransform = null, DoorController entryDoor = null)
    {
        EntranceDoor = entryDoor;

        // Seal the entry door visually and remove its physical blocker so the
        // player can walk through from the connected room on the other side.
        if (entryDoor != null)
        {
            entryDoor.SetState(DoorState.Sealed);
            entryDoor.ClearBlocker();
            if (lockEntryOnEnter)
                StartCoroutine(LockEntryAfterDelay());
        }

        bool hasBoss            = boss != null;
        bool clearsImmediately  = isStartRoom || (spawners.Length == 0 && !hasBoss) || !requireEnemiesCleared;
        DoorState exitState     = clearsImmediately ? DoorState.Unlocked : DoorState.Locked;

        foreach (var door in _doors)
        {
            if (door != entryDoor)
                door.SetState(exitState);
        }

        if (clearsImmediately)
        {
            IsCleared = true;
            return;
        }

        _spawnersCleared = 0;
        foreach (var spawner in spawners)
        {
            spawner.OnAllCleared += HandleSpawnerCleared;
            if (playerTransform != null)
                spawner.SetPlayerTransform(playerTransform);
            spawner.StartSpawning();
        }

        if (hasBoss)
        {
            _bossPending  = true;
            _bossDefeated = false;
            boss.OnDefeated += HandleBossDefeated;
        }
    }

    /// <summary>
    /// Cuts holes in the walls of all doors in this room.
    /// Called by RoomManager after Physics.SyncTransforms() so raycasts are accurate.
    /// </summary>
    public void CutAllDoorWalls()
    {
        foreach (var door in _doors)
            door.CutWallNow();
    }

    // =========================================================
    // PRIVATE
    // =========================================================

    private IEnumerator LockEntryAfterDelay()
    {
        yield return new WaitForSeconds(lockEntryDelay);
        if (EntranceDoor == null) yield break;
        EntranceDoor.gameObject.SetActive(true);
        EntranceDoor.CloseAndThen(() => boss?.StartFight());
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
        if (_bossPending && !_bossDefeated)     return;

        IsCleared = true;

        foreach (var door in _doors)
        {
            if (door != EntranceDoor)
                door.SetState(DoorState.Unlocked);
        }

        EventBus.Raise(new OnRoomClearedEvent { room = this });
    }
}
