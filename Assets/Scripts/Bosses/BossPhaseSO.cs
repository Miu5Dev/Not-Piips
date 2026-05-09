using UnityEngine;

[CreateAssetMenu(fileName = "New Boss Phase", menuName = "Bosses/Boss Phase", order = 1)]
public class BossPhaseSO : ScriptableObject
{
    [Header("Duration")]
    [Tooltip("How long this phase lasts in real seconds. When elapsed, all running patterns are stopped and the next phase begins.")]
    public float duration = 30f;

    [Header("Attacks")]
    [Tooltip("Patterns run CONCURRENTLY for the whole phase. Each self-loops on its own cadence (see startDelay + repeatInterval on the pattern).")]
    public BossPatternSO[] patterns;

    [Header("Weapon")]
    [Tooltip("Optional weapon override for this phase. Leave null to use the boss's default weapon.")]
    public WeaponSO weaponOverride;

    [Header("Defenses (active only during this phase)")]
    public BossDefenseSO[] phaseDefenses;
}
