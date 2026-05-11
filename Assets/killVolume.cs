using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KillVolume : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag filter — leave empty to kill everything.")]
    [SerializeField] string[] killTags = { "Player", "Enemy", "WorldItem" };
    [Tooltip("Damage sent via OnHealthChangeEvent to objects with a HealthController.")]
    [SerializeField] int instakillDamage = 99999;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[KillVolume] Entered: {other.gameObject.name} | Tag: {other.tag} | Root: {other.transform.root.name}");

        if (!ShouldKill(other.gameObject))
        {
            Debug.Log($"[KillVolume] Skipped {other.gameObject.name} — tag not in kill list");
            return;
        }

        Kill(other.gameObject);
    }

    bool ShouldKill(GameObject go)
    {
        if (killTags == null || killTags.Length == 0) return true;
        foreach (var tag in killTags)
            if (go.CompareTag(tag)) return true;
        return false;
    }

    void Kill(GameObject go)
    {
        // 1. Has HealthController — fire event and stop, let the object handle its own death
        var health = go.GetComponentInParent<HealthController>();
        if (health != null)
        {
            EventBus.Raise(new OnHealthChangedEvent
            {
                amount       = -instakillDamage,
                target       = health.gameObject,
                WeakPointHit = false
            });
            return; // ← nunca llegar al fallback
        }

        // 2. WorldItem — no pool, safe to destroy directly
        var worldItem = go.GetComponentInParent<WorldItemVisual>();
        if (worldItem != null)
        {
            Destroy(worldItem.gameObject);
            return;
        }

        // 3. Fallback — only hits objects with NO HealthController and NO WorldItemVisual
        Destroy(go.transform.root.gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.25f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.7f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif
}