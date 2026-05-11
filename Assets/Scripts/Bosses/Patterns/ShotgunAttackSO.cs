using System.Collections;
using UnityEngine;

/// <summary>
/// A fan of bullet streams diverging from a single point. Same shape language as
/// CircleAttack but limited to one direction (a slice of the circle).
///
/// `lineCount` streams spaced over `arcAngle` degrees, optionally mirrored backward,
/// optionally with a continuous spin so the whole fan slowly rotates.
/// </summary>
[CreateAssetMenu(fileName = "ShotgunAttack", menuName = "Bosses/Patterns/Shotgun Attack", order = 3)]
public class ShotgunAttackSO : BossPatternSO
{
    [Header("Fan")]
    [Min(1)] public int   lineCount = 4;
    [Tooltip("Total angular spread of the fan, in degrees.")]
    [Range(1f, 360f)] public float arcAngle = 30f;

    [Header("Stream")]
    [Min(1)] public int bulletsPerLine = 1;
    [Tooltip("Wait between successive bullets along each line. Ignored when bulletsPerLine = 1.")]
    public float bulletInterval = 0.08f;

    [Header("Aim")]
    public bool aimAtPlayer = true;
    [Tooltip("Also fire a mirrored fan out the back of the boss.")]
    public bool mirrorBack  = false;

    [Header("Continuous Spin")]
    [Tooltip("Smooth time-driven rotation of the fan, in degrees per second.")]
    public float continuousSpinSpeed = 0f;

    [Header("Bullet")]
    public float speedScale   = 1f;
    public float gravity      = 0f;
    public float bulletHeight = 0f;

    [Header("Curve")]
    public float curveTurnRate = 0f;

    public override IEnumerator ExecuteOnce(BossRuntime ctx)
    {
        Vector3 forward = aimAtPlayer ? ctx.DirectionToPlayer() : Vector3.forward;
        Vector3 spawn   = ctx.bulletOrigin.position + Vector3.up * bulletHeight;
        float   spinOffset = Time.time * continuousSpinSpeed;

        for (int b = 0; b < bulletsPerLine; b++)
        {
            for (int i = 0; i < lineCount; i++)
            {
                float t      = lineCount == 1 ? 0.5f : (float)i / (lineCount - 1);
                float angle  = (t - 0.5f) * arcAngle + spinOffset;

                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                ctx.FireShotFrom(spawn, dir, speedScale, gravityOverride: gravity, turnRate: curveTurnRate);

                if (mirrorBack)
                {
                    Vector3 backDir = Quaternion.AngleAxis(angle, Vector3.up) * -forward;
                    ctx.FireShotFrom(spawn, backDir, speedScale, gravityOverride: gravity, turnRate: -curveTurnRate);
                }
            }

            if (b < bulletsPerLine - 1) yield return ctx.Wait(bulletInterval);
        }
    }
}
