using UnityEngine;

/// <summary>
/// Lifetimed obstacle spawned by boss defenses (TrackingWallSO, OrbitingWardsSO…).
/// Self-destroys after `lifetime` seconds. The actual collision/visuals are on the
/// prefab the defense instantiates — this script just owns the timer.
/// </summary>
public class BossWall : MonoBehaviour
{
    public float lifetime = 4f;

    private float _spawnedAt;

    public void Configure(float lifetime)
    {
        this.lifetime = lifetime;
        _spawnedAt = Time.time;
    }

    private void OnEnable()  => _spawnedAt = Time.time;

    private void Update()
    {
        if (Time.time - _spawnedAt >= lifetime)
            Destroy(gameObject);
    }
}
