using UnityEngine;

/// <summary>
/// Abstract base for boss defenses (rotating shields, tracking walls, orbiting wards…).
/// Defenses are lifecycle objects: OnEnter spawns/starts state, OnExit cleans it up.
/// Drop into BossSO.passiveDefenses for always-on, or BossPhaseSO.phaseDefenses for per-phase.
///
/// Defenses are runtime-instantiated per fight via CreateInstance so each defense asset is
/// reusable across bosses without sharing state.
/// </summary>
public abstract class BossDefenseSO : ScriptableObject
{
    public abstract void OnEnter(BossRuntime ctx);
    public abstract void OnExit (BossRuntime ctx);
}
