using System.Collections;
using UnityEngine;

/// <summary>
/// The anti-camp mechanic. Periodically spawns a blocker wall on the line between
/// the boss and the player; the wall then SLOWLY drifts toward the player so just
/// dodging once isn't enough — the player has to keep moving to outpace it.
///
/// Forces the player to circulate to keep a clean firing lane.
/// </summary>
[CreateAssetMenu(fileName = "TrackingWall", menuName = "Bosses/Defenses/Tracking Wall", order = 1)]
public class TrackingWallSO : BossDefenseSO
{
    [Header("Prefab")]
    [Tooltip("Wall prefab to spawn — needs a collider on the layer the player ammo collides with.")]
    public GameObject wallPrefab;

    [Header("Spawn")]
    [Tooltip("Seconds between successive walls.")]
    public float spawnInterval = 3.5f;
    [Tooltip("How long each wall persists.")]
    public float wallLifetime  = 5f;
    [Tooltip("0 = right at the boss, 1 = right at the player. Mid values block shots without trapping the player.")]
    [Range(0.1f, 0.9f)] public float lerpFromBossToPlayer = 0.5f;

    [Header("Tracking")]
    [Tooltip("How fast the wall drifts toward the player after spawning, in m/s. Keep low so the player can outrun it by walking.")]
    public float trackSpeed = 1.2f;

    private Coroutine _loop;

    public override void OnEnter(BossRuntime ctx)
    {
        if (wallPrefab == null || ctx.controller == null) return;
        _loop = ctx.controller.StartCoroutine(Loop(ctx));
    }

    public override void OnExit(BossRuntime ctx)
    {
        if (_loop != null && ctx.controller != null) ctx.controller.StopCoroutine(_loop);
        _loop = null;
    }

    private IEnumerator Loop(BossRuntime ctx)
    {
        // Initial delay so a phase doesn't immediately blast a wall in the player's face on entry.
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            if (ctx.player != null)
            {
                Vector3 bossPos   = ctx.boss.position;
                Vector3 playerPos = ctx.player.position;
                Vector3 toPlayer  = playerPos - bossPos;
                toPlayer.y = 0f;

                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    Vector3 spawn = Vector3.Lerp(bossPos, playerPos, lerpFromBossToPlayer);
                    spawn.y = bossPos.y;

                    Quaternion rot = Quaternion.LookRotation(toPlayer.normalized);

                    GameObject wall = Object.Instantiate(wallPrefab, spawn, rot);
                    var lifetime = wall.GetComponent<BossWall>() ?? wall.AddComponent<BossWall>();
                    lifetime.Configure(wallLifetime);

                    if (trackSpeed > 0f)
                    {
                        var tracker = wall.AddComponent<TrackingWallMover>();
                        tracker.target = ctx.player;
                        tracker.speed  = trackSpeed;
                    }
                }
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    /// <summary>
    /// Runtime mover added to spawned walls. Drifts horizontally toward the player at
    /// `speed` m/s. Kept simple so the player can outrun it by walking — the *threat*
    /// is staying still, not getting caught.
    /// </summary>
    private class TrackingWallMover : MonoBehaviour
    {
        public Transform target;
        public float speed;

        private void Update()
        {
            if (target == null) return;

            Vector3 to = target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;

            Vector3 step = to.normalized * speed * Time.deltaTime;
            transform.position += step;

            // Keep the wall facing along its travel direction so its blocking face stays useful.
            transform.rotation = Quaternion.LookRotation(to.normalized);
        }
    }
}
