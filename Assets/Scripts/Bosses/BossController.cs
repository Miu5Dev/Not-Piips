using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a Touhou-style boss fight. Owns the phase loop, the look-at-player visual,
/// the HP-trigger phase swaps, and the death pipeline. Stateless data lives on BossSO assets;
/// per-fight state lives here and on the BossRuntime context.
/// </summary>
[RequireComponent(typeof(HealthController))]
public class BossController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] public BossSO bossData;

    [Header("Scene References")]
    [Tooltip("Visual that smoothly rotates to face the player. Falls back to this transform if null.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("World-space origin of every spawned bullet. Falls back to this transform if null.")]
    [SerializeField] private Transform bulletOrigin;
    public LayerMask PlayerLayer;
    [Tooltip("Optional. If left empty the player is resolved from OnPlayerSpawnEvent.")]
    [SerializeField] private Transform player;

    [Header("Visual")]
    [SerializeField] private float lookAtSpeed = 5f;
    [SerializeField] private bool  yawOnly     = true;

    [Header("Audio")]
    [SerializeField] private AudioClip ambientClip;
    [SerializeField] private AudioClip battleMusicClip;
    [SerializeField] private float     ambientFadeDuration = 10f;

    [Header("Intro")]
    [SerializeField] private float introDuration = 10f;

    private AudioSource _ambientSource;
    private AudioSource _musicSource;

    /// <summary>Fires once when the boss dies. Boss-room cleaner subscribes to unlock the doors.</summary>
    public event Action OnDefeated;

    public HealthController _health;
    private BossRuntime      _runtime;
    private Coroutine        _phaseLoop;
    private bool             _alive;
    private bool             _invincible;

    // HP trigger state
    private BossPhaseSO[] _activePhases;            // currently-running cycle (default or trigger-replaced)
    private bool[]        _triggersFired;
    private bool          _phaseInterruptRequested;

    // Active defense instances (cloned from SO assets to avoid mutating shared assets).
    private readonly List<BossDefenseSO> _passiveInstances = new();
    private readonly List<BossDefenseSO> _phaseInstances   = new();

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        _health = GetComponent<HealthController>();
        if (visualRoot   == null) visualRoot   = transform;
        if (bulletOrigin == null) bulletOrigin = transform;

        _ambientSource      = gameObject.AddComponent<AudioSource>();
        _ambientSource.loop = true;
        _ambientSource.clip = ambientClip;

        _musicSource        = gameObject.AddComponent<AudioSource>();
        _musicSource.loop   = true;
        _musicSource.clip   = battleMusicClip;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<OnDieEvent>(OnDie);
        EventBus.Subscribe<OnHealthChangedEvent>(OnHealth);
        EventBus.Subscribe<OnPlayerSpawnEvent>(OnPlayerSpawn);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<OnDieEvent>(OnDie);
        EventBus.Unsubscribe<OnHealthChangedEvent>(OnHealth);
        EventBus.Unsubscribe<OnPlayerSpawnEvent>(OnPlayerSpawn);
    }

    private void Start()
    {
        if (bossData == null)
        {
            Debug.LogError($"[BossController] '{name}' has no BossSO assigned.");
            return;
        }

        _health.maxHealth           = bossData.maxHealth;
        _health.health              = bossData.maxHealth;
        _health.weakPointMultiplier = bossData.weakPointMultiplier;

        _runtime = new BossRuntime
        {
            boss              = transform,
            bulletOrigin      = bulletOrigin,
            player            = player,
            weapon            = bossData.defaultWeapon,
            controller        = this,
            bossSpawnPosition = transform.position,
        };

        _activePhases  = bossData.phases;
        _triggersFired = new bool[bossData.healthTriggers != null ? bossData.healthTriggers.Length : 0];
        _alive = true;

        EnterPassiveDefenses();

        if (ambientClip != null) _ambientSource.Play();
    }

    public void StartFight()
    {
        if (!_alive || _phaseLoop != null) return;
        StartCoroutine(FightIntro());
    }

    private IEnumerator FightIntro()
    {
        _invincible          = true;
        _health.isInvincible = true;

        if (battleMusicClip != null) _musicSource.Play();
        StartCoroutine(FadeOutAmbient());

        yield return new WaitForSeconds(introDuration);

        _invincible          = false;
        _health.isInvincible = false;
        _phaseLoop           = StartCoroutine(PhaseLoop());
    }

    private IEnumerator FadeOutAmbient()
    {
        float startVolume = _ambientSource.volume;
        float elapsed     = 0f;

        while (elapsed < ambientFadeDuration)
        {
            elapsed               += Time.deltaTime;
            _ambientSource.volume  = Mathf.Lerp(startVolume, 0f, elapsed / ambientFadeDuration);
            yield return null;
        }

        _ambientSource.volume = 0f;
        _ambientSource.Stop();
    }

    private void Update()
    {
        if (!_alive || _runtime == null || _runtime.player == null) return;

        Vector3 toPlayer = _runtime.player.position - visualRoot.position;
        if (yawOnly) toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(toPlayer.normalized);
        visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, target, Time.deltaTime * lookAtSpeed);
    }

    // =========================================================
    // PHASE LOOP
    // =========================================================

    private IEnumerator PhaseLoop()
    {
        if (_activePhases == null || _activePhases.Length == 0)
        {
            Debug.LogWarning($"[BossController] '{bossData.name}' has no phases. Boss will idle.");
            yield break;
        }

        int phaseIndex = 0;
        while (_alive)
        {
            // Trigger fired: swap cycle and restart from index 0 of the new array.
            if (_phaseInterruptRequested)
            {
                _phaseInterruptRequested = false;
                phaseIndex = 0;
                if (_activePhases == null || _activePhases.Length == 0)
                {
                    yield return null;
                    continue;
                }
            }

            BossPhaseSO phase = _activePhases[phaseIndex];
            yield return RunPhase(phase);

            // After the phase ends (timed out OR interrupted), advance index.
            // If we were interrupted, the top-of-loop check resets phaseIndex anyway.
            if (_activePhases != null && _activePhases.Length > 0)
                phaseIndex = (phaseIndex + 1) % _activePhases.Length;
        }
    }

    private IEnumerator RunPhase(BossPhaseSO phase)
    {
        _runtime.weapon = phase.weaponOverride != null ? phase.weaponOverride : bossData.defaultWeapon;

        EnterPhaseDefenses(phase);

        var running = new List<Coroutine>();
        if (phase.patterns != null)
        {
            foreach (var pattern in phase.patterns)
            {
                if (pattern == null) continue;
                running.Add(StartCoroutine(pattern.Loop(_runtime)));
            }
        }

        // Phase ends when its duration elapses, the boss dies, or a trigger interrupts.
        float endTime = Time.time + phase.duration;
        while (_alive && Time.time < endTime && !_phaseInterruptRequested)
            yield return null;

        foreach (var c in running)
            if (c != null) StopCoroutine(c);

        ExitPhaseDefenses();
    }

    // =========================================================
    // DEFENSES
    // =========================================================

    private void EnterPassiveDefenses()
    {
        if (bossData.passiveDefenses == null) return;
        foreach (var defAsset in bossData.passiveDefenses)
        {
            if (defAsset == null) continue;
            var inst = Instantiate(defAsset);
            _passiveInstances.Add(inst);
            inst.OnEnter(_runtime);
        }
    }

    private void EnterPhaseDefenses(BossPhaseSO phase)
    {
        if (phase.phaseDefenses == null) return;
        foreach (var defAsset in phase.phaseDefenses)
        {
            if (defAsset == null) continue;
            var inst = Instantiate(defAsset);
            _phaseInstances.Add(inst);
            inst.OnEnter(_runtime);
        }
    }

    private void ExitPhaseDefenses()
    {
        foreach (var inst in _phaseInstances)
        {
            if (inst == null) continue;
            inst.OnExit(_runtime);
            Destroy(inst);
        }
        _phaseInstances.Clear();
    }

    private void ExitPassiveDefenses()
    {
        foreach (var inst in _passiveInstances)
        {
            if (inst == null) continue;
            inst.OnExit(_runtime);
            Destroy(inst);
        }
        _passiveInstances.Clear();
    }

    // =========================================================
    // EVENTS
    // =========================================================

    private void OnHealth(OnHealthChangedEvent e)
    {
        if (e.target != gameObject || !_alive) return;
        if (bossData.healthTriggers == null || bossData.healthTriggers.Length == 0) return;

        float pct = (float)_health.health / Mathf.Max(1, _health.maxHealth) * 100f;

        // Mark every newly-crossed trigger as fired, but apply only the LOWEST one's
        // phases — that's the "deepest" desperation the boss has currently entered.
        int   winnerIndex = -1;
        float winnerPct   = float.PositiveInfinity;

        for (int i = 0; i < bossData.healthTriggers.Length; i++)
        {
            if (_triggersFired[i]) continue;
            var trig = bossData.healthTriggers[i];
            if (pct > trig.healthPercent) continue;

            _triggersFired[i] = true;

            bool valid = trig.replacementPhases != null && trig.replacementPhases.Length > 0;
            if (valid && trig.healthPercent < winnerPct)
            {
                winnerPct   = trig.healthPercent;
                winnerIndex = i;
            }
        }

        if (winnerIndex >= 0)
        {
            _activePhases            = bossData.healthTriggers[winnerIndex].replacementPhases;
            _phaseInterruptRequested = true;
        }
    }

    private void OnDie(OnDieEvent e)
    {
        if (e.murderedObject != gameObject || !_alive) return;

        _alive = false;
        if (_phaseLoop != null) StopCoroutine(_phaseLoop);
        StopAllCoroutines();

        ExitPhaseDefenses();
        ExitPassiveDefenses();

        OnDefeated?.Invoke();
    }

    private void OnPlayerSpawn(OnPlayerSpawnEvent e)
    {
        player = e.Player_Enemies_Target;
        if (_runtime != null) _runtime.player = player;
    }

    public void onPingEventReceived(OnPingEvent e)
    {
        if ((PlayerLayer.value & (1 << e.sender.layer)) != 0)
        {
            player = e.sender.transform;
        }
    }
}
