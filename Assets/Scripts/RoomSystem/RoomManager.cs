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
        newRoom.Initialize(isStartRoom: false, playerTransform, entryDoor);

        Physics.SyncTransforms();
        newRoom.CutAllDoorWalls();

        activeDoor.ResetCut();
        activeDoor.CutWallNow();

        _loadedRooms.Add(newRoom);

        if (_loadedRooms.Count > 2)
        {
            RoomController oldRoom = _loadedRooms[0];

            // Close all doors in the old room before destroying it
            foreach (var door in oldRoom.GetComponentsInChildren<DoorController>(includeInactive: true))
                door.CloseAndThen(() => Destroy(oldRoom.gameObject));

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
