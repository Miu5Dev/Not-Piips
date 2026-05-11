using System.Collections;
using UnityEngine;

/// <summary>
/// N spokes radiating outward from the boss, emitting bullets one by one
/// while the whole star rotates. Result reads as N spiral arms.
///
/// Toggle `infinite` for a continuous emitter that never stops on its own —
/// it runs until the phase ends.
/// </summary>
[CreateAssetMenu(fileName = "StarAttack", menuName = "Bosses/Patterns/Star Attack", order = 1)]
public class StarAttackSO : BossPatternSO
{
    [Header("Spokes")]
    [Min(2)] public int spokeCount      = 5;
    [Tooltip("Bullets emitted per spoke before the pattern returns. Ignored when 'infinite' is true.")]
    [Min(1)] public int bulletsPerSpoke = 12;
    [Tooltip("Wait between successive emit ticks.")]
    public float        bulletInterval  = 0.08f;

    [Header("Infinite")]
    [Tooltip("When true, ExecuteOnce never returns — the pattern emits forever until the phase ends. Use this for a permanent spinning emitter.")]
    public bool infinite = false;

    [Header("Rotation")]
    [Tooltip("Discrete rotation added per emit tick. Combine with continuousSpinSpeed for smooth + stepped motion.")]
    public float rotationPerStep = 6f;

    [Tooltip("Smooth time-driven rotation in degrees per second. Most useful with infinite = true.")]
    public float continuousSpinSpeed = 0f;

    [Tooltip("If true, the star's first spoke is aimed at the player. Otherwise it points at world +Z.")]
    public bool aimAtPlayer = false;

    [Header("Bullet")]
    public float speedScale = 1f;
    public float gravity    = 0f;
    public float bulletHeight = 0f;

    [Header("Curve")]
    public float curveTurnRate = 0f;

    public override IEnumerator ExecuteOnce(BossRuntime ctx)
    {
        Vector3 center = aimAtPlayer ? ctx.DirectionToPlayer() : Vector3.forward;
        Vector3 spawn  = ctx.bulletOrigin.position + Vector3.up * bulletHeight;
        float   spokeStep = 360f / spokeCount;

        int t = 0;
        while (true)
        {
            float baseAngle = Time.time * continuousSpinSpeed + rotationPerStep * t;

            for (int i = 0; i < spokeCount; i++)
            {
                float angle = baseAngle + spokeStep * i;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * center;
                ctx.FireShotFrom(spawn, dir, speedScale, gravityOverride: gravity, turnRate: curveTurnRate);
            }

            t++;
            if (!infinite && t >= bulletsPerSpoke) yield break;

            if (bulletInterval > 0f) yield return ctx.Wait(bulletInterval);
            else                     yield return null;
        }
    }
}
