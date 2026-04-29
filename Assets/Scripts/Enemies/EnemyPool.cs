using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton object pool for enemies. One queue per EnemySO type, capped at 30.
/// </summary>
public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance { get; private set; }

    private readonly Dictionary<EnemySO, Queue<EnemyController>> _pools     = new();
    private readonly Dictionary<EnemyController, EnemySO>        _ownerType = new();

    private const int MaxPoolPerType = 30;

    // =========================================================
    // AUTO-CREATE (mirrors BulletPool pattern)
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>Returns an active enemy of the requested type, expanding the pool if needed.</summary>
    public EnemyController GetEnemy(EnemySO type)
    {
        if (!_pools.TryGetValue(type, out var queue))
        {
            queue = new Queue<EnemyController>(type.poolSize);
            _pools[type] = queue;
        }

        EnemyController enemy = queue.Count > 0 ? queue.Dequeue() : CreateEnemy(type);
        enemy.gameObject.SetActive(true);
        return enemy;
    }

    /// <summary>Disables the enemy and returns it to its pool.</summary>
    public void ReturnEnemy(EnemyController enemy)
    {
        if (enemy == null) return;

        enemy.gameObject.SetActive(false);

        if (_ownerType.TryGetValue(enemy, out var type) && _pools.TryGetValue(type, out var queue))
            queue.Enqueue(enemy);
        else
            Destroy(enemy.gameObject);
    }

    // =========================================================
    // INTERNAL
    // =========================================================

    private EnemyController CreateEnemy(EnemySO type)
    {
        var go    = new GameObject($"Enemy_{type.name}");
        go.transform.SetParent(transform);
        go.SetActive(false);

        var controller = go.AddComponent<EnemyController>();

        // Flag the ShootController as enemy-owned NOW, while the GameObject is still
        // inactive, so ShootController.Awake() sees IsPlayerController=false when
        // SetActive(true) is first called and does not overwrite ShootController.Instance.
        var sc = go.GetComponent<ShootController>();
        if (sc != null) sc.IsPlayerController = false;

        // Instantiate model as a child of the root at pool creation time
        if (type.model != null)
        {
            var model = Instantiate(type.model, go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            controller.SetModel(model);
        }

        _ownerType[controller] = type;
        return controller;
    }
}
