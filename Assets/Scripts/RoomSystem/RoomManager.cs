using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Place one instance in your scene on any persistent GameObject.
/// Maintains a max of 2 loaded rooms — oldest is destroyed when a third is opened.
/// </summary>
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Rooms")]
    [Tooltip("Pool of room prefabs — one is picked at random each time a door opens.")]
    [SerializeField] private RoomController[] roomPrefabs;
    [Tooltip("The room already placed in the scene when the game starts.")]
    [SerializeField] private RoomController startRoom;

    [Header("References")]
    [Tooltip("Forwarded to enemy spawners. Auto-filled by OnPlayerSpawnEvent if left empty.")]
    [SerializeField] private Transform playerTransform;

    private readonly List<RoomController> _loadedRooms = new();

    // The most recently orphaned exit-door branch (detached from a destroyed
    // room so its closed visual could plug the surviving room's wall hole).
    // Kept around for one extra transition, then destroyed when the next
    // orphan is created — by that point the room it was plugging has itself
    // been despawned, so the floating door is no longer visible to the player.
    private GameObject _previousOrphanedDoorBranch;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()  => EventBus.Subscribe<OnPlayerSpawnEvent>(OnPlayerSpawn);
    private void OnDisable() => EventBus.Unsubscribe<OnPlayerSpawnEvent>(OnPlayerSpawn);

    private void Start()
    {
        if (startRoom != null) RegisterStartRoom(startRoom);
        else Debug.LogWarning("[RoomManager] No start room assigned.");
    }

    // =========================================================
    // ROOM LOADING
    // =========================================================

    private void RegisterStartRoom(RoomController room)
    {
        _loadedRooms.Add(room);
        room.Initialize(isStartRoom: true, playerTransform);
    }

    /// <summary>Called by DoorController when the player interacts with an unlocked door.</summary>
    public void OpenNextRoom(DoorController activeDoor)
    {
        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            Debug.LogWarning("[RoomManager] No room prefabs assigned.");
            return;
        }

        activeDoor.SetState(DoorState.Sealed);

        // Resolve the room the player is leaving from the door itself, so this
        // works even if the player backtracked into an older room before opening
        // a door. Then record activeDoor as that room's exit door.
        RoomController activeRoom = activeDoor.GetComponentInParent<RoomController>();
        if (activeRoom != null)
        {
            activeRoom.SetExitDoor(activeDoor);

            // Seal every other unlocked door in the room the player is leaving.
            // Once they walk through activeDoor, this room becomes stale and is
            // queued for despawn — letting them open another of its doors would
            // try to despawn the room they're standing in. Sealing them here
            // makes that path impossible. Player can still physically walk back
            // through the entry hole, but can't trigger another transition.
            foreach (var door in activeRoom.GetComponentsInChildren<DoorController>(includeInactive: true))
            {
                if (door != activeDoor && door.State == DoorState.Unlocked)
                    door.SetState(DoorState.Sealed);
            }
        }

        RoomController prefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
        RoomController newRoom = Instantiate(prefab);

        DoorController entryDoor = newRoom.GetRandomDoor();
        if (entryDoor == null)
        {
            Debug.LogError($"[RoomManager] Spawned room '{newRoom.name}' returned no entry door — aborting.");
            Destroy(newRoom.gameObject);
            return;
        }

        AlignRoom(newRoom, activeDoor.transform, entryDoor);

        // MinimapWall corners are cached in Awake (before AlignRoom moves the room).
        // Refresh them now so the minimap reflects the final world position.
        foreach (var wall in newRoom.GetComponentsInChildren<MinimapWall>())
            wall.RefreshCorners();

        newRoom.Initialize(isStartRoom: false, playerTransform, entryDoor);

        Physics.SyncTransforms();
        newRoom.CutAllDoorWalls();

        activeDoor.ResetCut();
        activeDoor.CutWallNow();

        _loadedRooms.Add(newRoom);

        if (_loadedRooms.Count > 2)
        {
            RoomController oldRoom = _loadedRooms[0];

            // Only the exit door needs to survive: it sits in the doorway shared
            // with the surviving next room, and its close animation plugs the
            // hole in that room's wall. Other doors of oldRoom are either inactive
            // (entry door — would throw on StartCoroutine) or have no wall left
            // around them after oldRoom is destroyed, so they go away with it.
            DoorController exitDoor = oldRoom.ExitDoor;
            if (exitDoor != null && exitDoor.gameObject.activeInHierarchy)
            {
                // Walk up to the topmost ancestor that's still a direct child of oldRoom,
                // and detach THAT — this brings along any Animator / visuals / colliders
                // that live on parents or siblings of the DoorController within the same
                // door prefab branch. Detaching just exitDoor.transform would orphan them.
                Transform branch = exitDoor.transform;
                while (branch.parent != null && branch.parent != oldRoom.transform)
                    branch = branch.parent;
                branch.SetParent(null, worldPositionStays: true);

                // Leave the closed door in the world for now — it plugs the
                // hole in the surviving room's wall. It's already in DoorState.Sealed
                // (set when the player opened it), so TryOpen will reject any
                // future interaction.
                exitDoor.CloseAndThen(null);

                // Destroy the previous orphan (from the transition before this
                // one). The room it was plugging is the one being destroyed
                // right now, so the orphan is no longer visible anywhere the
                // player can see — safe to remove. This caps the trail at one
                // surviving door instead of letting it grow unbounded.
                if (_previousOrphanedDoorBranch != null)
                    Destroy(_previousOrphanedDoorBranch);

                _previousOrphanedDoorBranch = branch.gameObject;
            }

            Destroy(oldRoom.gameObject);
            _loadedRooms.RemoveAt(0);
        }
    }

    // =========================================================
    // ALIGNMENT
    // =========================================================

    // Rotates and translates newRoom so that entryDoor snaps flush to activeDoor,
    // with the two doors facing each other.
    private static void AlignRoom(RoomController newRoom, Transform activeDoor, DoorController entryDoor)
    {
        Transform doorTransform = entryDoor.transform;
        Transform roomRoot = newRoom.transform;

        // Get the door's rotation relative to the room root (not just its immediate parent)
        Quaternion doorLocalToRoom = Quaternion.Inverse(roomRoot.rotation) * doorTransform.rotation;

        // Rotate room so entryDoor.forward faces -activeDoor.forward
        roomRoot.rotation = Quaternion.LookRotation(-activeDoor.forward, Vector3.up)
                            * Quaternion.Inverse(doorLocalToRoom);

        // Translate room so entryDoor lands exactly on activeDoor
        roomRoot.position += activeDoor.position - doorTransform.position;
    }

    // =========================================================
    // EVENTS
    // =========================================================

    private void OnPlayerSpawn(OnPlayerSpawnEvent e) => playerTransform = e.Player_Enemies_Target;
}
