using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Self-initializing bullet pool. No manual setup required.
/// Pools are created on demand the first time a prefab is fired.
/// Optionally add to a persistent GameObject for scene-persistent pools;
/// otherwise it creates itself automatically as a new GameObject.
/// </summary>
public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance { get; private set; }

    [Tooltip("Default pool size when a new prefab is registered automatically.")]
    [SerializeField] private int defaultPoolSize = 20;

    private readonly Dictionary<GameObject, Stack<Shot>> _free  = new();
    private readonly Dictionary<Shot, GameObject>        _owner = new();

    // =========================================================
    // AUTO-CREATE
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Returns the existing instance, or creates one automatically if none exists.
    /// Called internally — no manual setup needed.
    /// </summary>
    public static BulletPool GetOrCreate()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("[BulletPool]");
        return go.AddComponent<BulletPool>();
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>
    /// Get a bullet from the pool. Pool for this prefab is created automatically
    /// on first call with defaultPoolSize bullets pre-warmed.
    /// </summary>
    public Shot Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!_free.TryGetValue(prefab, out var stack))
        {
            // First time seeing this prefab — create pool on demand
            stack = new Stack<Shot>(defaultPoolSize);
            _free[prefab] = stack;
            Prewarm(prefab, defaultPoolSize);
        }

        Shot shot = stack.Count > 0 ? stack.Pop() : CreateNew(prefab);

        shot.transform.SetPositionAndRotation(position, rotation);
        shot.gameObject.SetActive(true);
        return shot;
    }

    /// <summary>Return a bullet to its pool.</summary>
    public void Release(Shot shot)
    {
        if (shot == null) return;

        shot.gameObject.SetActive(false);

        if (_owner.TryGetValue(shot, out var prefab) && _free.TryGetValue(prefab, out var stack))
            stack.Push(shot);
        else
            Destroy(shot.gameObject);
    }

    // =========================================================
    // INTERNAL
    // =========================================================

    private void Prewarm(GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Shot shot = CreateNew(prefab);
            shot.gameObject.SetActive(false);
            _free[prefab].Push(shot);
        }
    }

    private Shot CreateNew(GameObject prefab)
    {
        var go   = Instantiate(prefab, transform);
        var shot = go.GetComponent<Shot>() ?? go.AddComponent<Shot>();
        go.SetActive(false);
        _owner[shot] = prefab;
        return shot;
    }
}
