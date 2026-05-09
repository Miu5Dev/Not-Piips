using System.Collections;
using UnityEngine;

/// <summary>
/// Like StarAttack but instead of rotating smoothly, the firing angle SNAPS between
/// two poses (rotationA and rotationB) and emits a fan at each pose. Bullets always
/// spawn from the same point — only the aim direction toggles back and forth.
/// </summary>
[CreateAssetMenu(fileName = "ZigZagAttack", menuName = "Bosses/Patterns/ZigZag Attack", order = 2)]
public class ZigZagAttackSO : BossPatternSO
{
    [Header("Poses")]
    [Tooltip("Aim rotation (deg around Y) for pose A, relative to the aim baseline.")]
    public float rotationA = -30f;
    [Tooltip("Aim rotation (deg around Y) for pose B, relative to the aim baseline.")]
    public float rotationB =  30f;

    [Tooltip("If true, the rotations are relative to the direction toward the player. Otherwise relative to world +Z.")]
    public bool aimAtPlayer = true;

    [Header("Sequence")]
    [Min(1)] public int swingCount = 6;
    [Tooltip("Time spent at each pose before snapping to the other.")]
    public float stopDuration = 0.4f;

    [Header("Per-Pose Burst")]
    [Min(1)] public int bulletsPerStop = 6;
    [Range(1f, 360f)] public float burstArcAngle = 90f;

    [Header("Bullet")]
    public float speedScale   = 1f;
    public float gravity      = 0f;
    public float bulletHeight = 0f;

    [Header("Curve")]
    public float curveTurnRate = 0f;

    public override IEnumerator ExecuteOnce(BossRuntime ctx)
    {
        for (int i = 0; i < swingCount; i++)
        {
            Vector3 baseline = aimAtPlayer ? ctx.DirectionToPlayer() : Vector3.forward;
            float pose = (i % 2 == 0) ? rotationA : rotationB;
            Vector3 center = Quaternion.AngleAxis(pose, Vector3.up) * baseline;

            EmitBurst(ctx, center);

            yield return ctx.Wait(stopDuration);
        }
    }

    private void EmitBurst(BossRuntime ctx, Vector3 center)
    {
        Vector3 spawn = ctx.bulletOrigin.position + Vector3.up * bulletHeight;

        if (bulletsPerStop == 1)
        {
            ctx.FireShotFrom(spawn, center, speedScale, gravityOverride: gravity, turnRate: curveTurnRate);
            return;
        }

        float step  = burstArcAngle / (bulletsPerStop - 1);
        float start = -burstArcAngle * 0.5f;

        for (int b = 0; b < bulletsPerStop; b++)
        {
            float angle = start + step * b;
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * center;
            ctx.FireShotFrom(spawn, dir, speedScale, gravityOverride: gravity, turnRate: curveTurnRate);
        }
    }
}
