using System.Collections;
using UnityEngine;

/// <summary>
/// The workhorse pattern. One execution = one or more rings of bullets fired outward.
/// Toggles let you author full circles, focused cones (aimAtPlayer + small arcAngle),
/// rotating curtains (ringRotationOffset), low jumpable walls (bulletHeight), and
/// curved sweeps (curveTurnRate) — all from the same SO type.
/// </summary>
[CreateAssetMenu(fileName = "CircleAttack", menuName = "Bosses/Patterns/Circle Attack", order = 0)]
public class CircleAttackSO : BossPatternSO
{
    [Header("Shape")]
    [Min(1)] public int   bulletsPerRing = 24;
    [Min(1)] public int   ringCount      = 1;
    [Tooltip("Wait between successive rings within one execution.")]
    public float          ringInterval   = 0.25f;

    [Tooltip("0 = full 360° circle. Smaller values = focused cone (great for 'aim toward player' attacks).")]
    [Range(1f, 360f)] public float arcAngle = 360f;

    [Tooltip("If true, the arc is centered on the direction toward the player. Otherwise it's centered on world +Z.")]
    public bool aimAtPlayer = false;

    [Tooltip("Each successive ring within this execution rotates by this many degrees — produces a slow-spinning curtain when ringCount > 1.")]
    public float ringRotationOffset = 0f;

    [Header("Continuous Spin")]
    [Tooltip("Adds a time-driven rotation to all bullet angles, in degrees per second. Different executions therefore start at different angles, giving smooth infinite rotation across repetitions.")]
    public float continuousSpinSpeed = 0f;

    [Header("Bullet")]
    public float speedScale = 1f;

    [Tooltip("Negative = bullets fall (real gravity). Positive = bullets rise. 0 = straight horizontal.")]
    public float gravity = 0f;

    [Tooltip("Y offset from the bulletOrigin transform. Use a low value (e.g. -1) for jumpable floor waves.")]
    public float bulletHeight = 0f;

    [Header("Curve")]
    [Tooltip("Bullets curve sideways at this many degrees per second. 0 = straight line. Positive = clockwise from above.")]
    public float curveTurnRate = 0f;

    public override IEnumerator ExecuteOnce(BossRuntime ctx)
    {
        // Time-driven base angle: changes between executions, so successive ring bursts
        // appear rotated relative to each other — smooth continuous spin.
        float spinBase = Time.time * continuousSpinSpeed;

        for (int r = 0; r < ringCount; r++)
        {
            // Ring center direction: either toward the player or world forward.
            Vector3 center = aimAtPlayer ? ctx.DirectionToPlayer() : Vector3.forward;

            float ringRotation = ringRotationOffset * r + spinBase;

            // Arc spans `arcAngle` degrees centered on `center`.
            // For arcAngle = 360 the wrap-around makes the math identical to a full ring.
            float step = bulletsPerRing > 1 ? arcAngle / bulletsPerRing : 0f;
            float startOffset = -arcAngle * 0.5f + step * 0.5f;
            if (Mathf.Approximately(arcAngle, 360f))
            {
                // For full circles, even spacing without the half-step inset.
                step        = 360f / bulletsPerRing;
                startOffset = 0f;
            }

            for (int i = 0; i < bulletsPerRing; i++)
            {
                float angle = startOffset + step * i + ringRotation;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * center;

                Vector3 spawn = ctx.bulletOrigin.position + Vector3.up * bulletHeight;
                ctx.FireShotFrom(spawn, dir, speedScale, gravityOverride: gravity, turnRate: curveTurnRate);
            }

            if (r < ringCount - 1) yield return ctx.Wait(ringInterval);
        }
    }
}
