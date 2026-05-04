using System.Collections.Generic;
using UnityEngine;

public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance { get; private set; }

    private readonly Dictionary<EnemySO, Queue<EnemyController>> _pools = new();
    private readonly Dictionary<EnemyController, EnemySO> _ownerType = new();

    private const int MaxPoolPerType = 30;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public EnemyController GetEnemy(EnemySO type)
    {
        if (!_pools.TryGetValue(type, out var queue))
        {
            queue = new Queue<EnemyController>(type.poolSize);
            _pools[type] = queue;
        }

        EnemyController enemy = queue.Count > 0 ? queue.Dequeue() : CreateEnemy(type);
        if (enemy == null) return null;
        enemy.gameObject.SetActive(true);
        return enemy;
    }

    public void ReturnEnemy(EnemyController enemy)
    {
        if (enemy == null) return;

        enemy.gameObject.SetActive(false);

        if (_ownerType.TryGetValue(enemy, out var type) && _pools.TryGetValue(type, out var queue))
            queue.Enqueue(enemy);
        else
            Destroy(enemy.gameObject);
    }

    private EnemyController CreateEnemy(EnemySO type)
    {
        if (type.prefab == null)
        {
            Debug.LogError($"[EnemyPool] EnemySO '{type.name}' no tiene prefab asignado.");
            return null;
        }

        var go = Instantiate(type.prefab);
        go.name = $"Enemy_{type.name}";
        go.transform.SetParent(transform);
        go.SetActive(false);

        var controller = go.GetComponent<EnemyController>();
        if (controller == null)
        {
            Debug.LogError($"[EnemyPool] El prefab '{type.prefab.name}' no tiene EnemyController.");
            return null;
        }

        var sc = go.GetComponent<ShootController>();
        if (sc != null) sc.IsPlayerController = false;

        _ownerType[controller] = type;
        return controller;
    }
}