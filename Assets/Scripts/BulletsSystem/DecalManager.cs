using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that limits the number of active decals in the scene.
/// When the limit is reached, the oldest decal is destroyed and replaced.
/// Add this component to any persistent GameObject (e.g. GameManager).
/// </summary>
public class DecalManager : MonoBehaviour
{
    public static DecalManager Instance { get; private set; }

    [Tooltip("Maximum number of decals alive at the same time.")]
    [SerializeField] private int maxDecals = 30;

    private readonly Queue<GameObject> _active = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Spawns a decal prefab at the given position/rotation.
    /// If the limit is reached, destroys the oldest one first.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        // Evict oldest if at capacity
        while (_active.Count >= maxDecals)
        {
            var oldest = _active.Dequeue();
            if (oldest != null) Destroy(oldest);
        }

        var decal = Instantiate(prefab, position, rotation, parent);
        _active.Enqueue(decal);
        return decal;
    }

    /// <summary>Call this if a decal is destroyed externally (e.g. scene cleanup).</summary>
    public void Clear()
    {
        while (_active.Count > 0)
        {
            var d = _active.Dequeue();
            if (d != null) Destroy(d);
        }
    }
}
