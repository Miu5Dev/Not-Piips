using UnityEngine;

[CreateAssetMenu(fileName = "New Boss", menuName = "Bosses/Boss", order = 0)]
public class BossSO : ScriptableObject
{
    [Header("Identity")]
    public string bossName = "Boss";

    [Header("Stats")]
    public int maxHealth           = 1000;
    public int weakPointMultiplier = 5;

    [Header("Weapon")]
    [Tooltip("Default weapon used when a phase has no override. The weapon's damage / pellets / spread / ammo all apply per shot.")]
    public WeaponSO defaultWeapon;

    [Header("Phases")]
    [Tooltip("Run in order, then loop back to phase 0. Cycles forever until the boss dies or a health trigger fires.")]
    public BossPhaseSO[] phases;

    [Header("Passive Defenses")]
    [Tooltip("Active for the entire fight, alongside whatever the current phase adds.")]
    public BossDefenseSO[] passiveDefenses;

    [Header("Health Triggers")]
    [Tooltip("When the boss drops below a trigger's healthPercent, its replacement phases REPLACE the active cycle for the rest of the fight (until a lower-percent trigger fires). Each trigger fires once.")]
    public BossHealthTrigger[] healthTriggers;
}

[System.Serializable]
public class BossHealthTrigger
{
    [Tooltip("When current HP drops to or below this percent of max, the replacement phases take over.")]
    [Range(0f, 100f)]
    public float healthPercent = 50f;

    [Tooltip("New phase cycle that takes over once this trigger fires. Cycles among these in order.")]
    public BossPhaseSO[] replacementPhases;
}
