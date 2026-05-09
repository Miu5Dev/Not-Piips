using System.Collections;
using UnityEngine;

/// <summary>
/// One horizontal wave of bullets sweeping at low altitude toward the player.
/// Designed so sidestepping doesn't help — the player must JUMP over it.
/// Use repeatInterval on the base class to control how often the wave fires.
/// </summary>
[CreateAssetMenu(fileName = "JumpWave", menuName = "Bosses/Patterns/Jump Wave", order = 4)]
public class JumpWaveSO : BossPatternSO
{
    [Header("Wave")]
    [Min(2)] public int   bulletsPerLine = 14;
    [Tooltip("Width of the wave (perpendicular to travel direction), in metres.")]
    public float          lineWidth      = 8f;
    [Tooltip("Height above the floor at which the wave skims. The pattern computes spawn Y as bulletOrigin.y - 1 + skimHeight.")]
    public float          skimHeight     = 0.4f;

    [Header("Telegraph")]
    [Tooltip("Wait this long after locking in the player direction before firing — gives them time to prep a jump.")]
    public float telegraph = 0.6f;

    [Header("Bullet")]
    public float speedScale = 1.2f;

    [Header("Curve")]
    public float curveTurnRate = 0f;

    public override IEnumerator ExecuteOnce(BossRuntime ctx)
    {
        Vector3 toPlayer = ctx.DirectionToPlayer();
        Vector3 right    = Vector3.Cross(Vector3.up, toPlayer).normalized;

        if (telegraph > 0f) yield return ctx.Wait(telegraph);

        for (int i = 0; i < bulletsPerLine; i++)
        {
            float t      = bulletsPerLine == 1 ? 0.5f : (float)i / (bulletsPerLine - 1);
            float offset = (t - 0.5f) * lineWidth;

            Vector3 spawn = ctx.bulletOrigin.position + right * offset;
            spawn.y = ctx.bulletOrigin.position.y - 1f + skimHeight;

            // gravityOverride = 0 keeps the wave at constant height for the full skim.
            ctx.FireShotFrom(spawn, toPlayer, speedScale, gravityOverride: 0f, turnRate: curveTurnRate);
        }
    }
}
