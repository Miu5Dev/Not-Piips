using System.Collections;
using UnityEngine;

/// <summary>
/// Abstract base for every boss attack. Each subclass is its own ScriptableObject asset.
/// Patterns are SELF-LOOPING: a phase starts a pattern's Loop coroutine and lets it run
/// until the phase ends.
///
/// Two cadence models, controlled by `activeDuration`:
///   - activeDuration = 0  →  pattern fires every `repeatInterval` seconds, forever.
///   - activeDuration > 0  →  pattern fires for `activeDuration` seconds (every `repeatInterval`),
///                            then sleeps for `cooldown` seconds, then repeats.
///
/// The windowed mode is how you make a "side attack": e.g. activeDuration = 2, cooldown = 5
/// means a 2-second burst every 7 seconds while the phase is active.
/// </summary>
public abstract class BossPatternSO : ScriptableObject
{
    [Header("Cadence")]
    [Tooltip("Wait this long before the first execution.")]
    public float startDelay = 0f;

    [Tooltip("Seconds between successive executions of this pattern (within an active window).")]
    public float repeatInterval = 1f;

    [Tooltip("0 = always active. >0 = pattern fires for this many seconds, then enters cooldown. Use this to make a side attack.")]
    public float activeDuration = 0f;

    [Tooltip("Seconds to sleep between active windows. Only used when activeDuration > 0.")]
    public float cooldown = 0f;

    /// <summary>One full execution of the pattern's shape.</summary>
    public abstract IEnumerator ExecuteOnce(BossRuntime ctx);

    /// <summary>
    /// Default loop driver. Phases call this once per pattern.
    /// Subclasses only have to implement the shape; cadence is handled here.
    /// </summary>
    public IEnumerator Loop(BossRuntime ctx)
    {
        if (startDelay > 0f) yield return ctx.Wait(startDelay);

        if (activeDuration > 0f)
        {
            // Windowed: active period (firing every repeatInterval) → cooldown → repeat.
            while (true)
            {
                float windowEnd = Time.time + activeDuration;
                while (Time.time < windowEnd)
                {
                    yield return ExecuteOnce(ctx);
                    if (repeatInterval <= 0f) break;
                    yield return ctx.Wait(repeatInterval);
                }
                if (cooldown > 0f) yield return ctx.Wait(cooldown);
                else                yield return null; // safety: avoid tight loop
            }
        }
        else
        {
            // Continuous: fire repeatedly with repeatInterval gaps until the phase stops us.
            while (true)
            {
                yield return ExecuteOnce(ctx);
                if (repeatInterval <= 0f) yield break;
                yield return ctx.Wait(repeatInterval);
            }
        }
    }
}
