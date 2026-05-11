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

    [Header("Room Activation")]
    [Tooltip("The isTrigger collider that covers the room entrance. Drag it in from the scene.")]
    [SerializeField] private Collider roomTrigger;
    [Tooltip("Seconds the boss stays idle and immune after the player enters the room trigger.")]
    [SerializeField] private float activationDelay = 10f;

    [Header("Audio")]
    [Tooltip("Loops forever as soon as the player enters the room.")]
    [SerializeField] private AudioClip ambientClip;
    [Tooltip("Starts playing once the activation delay expires and the boss goes vulnerable.")]
    [SerializeField] private AudioClip battleMusicClip;

    /// <summary>Fires once when the boss dies. Boss-room cleaner subscribes to unlock the doors.</summary>
    public event Action OnDefeated;

    public HealthController _health;
    private BossRuntime      _runtime;
    private Coroutine        _phaseLoop;
    private bool             _alive;
    private bool             _activated;
    private AudioSource      _ambientAS;
    private AudioSource      _battleMusicAS;

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
        _health.enabled = false;
        if (visualRoot   == null) visualRoot   = transform;
        if (bulletOrigin == null) bulletOrigin = transform;
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

        if (ambientClip != null)
        {
            _ambientAS      = gameObject.AddComponent<AudioSource>();
            _ambientAS.clip = ambientClip;
            _ambientAS.loop = true;
            _ambientAS.Play();
        }

        if (roomTrigger != null)
        {
            var proxy = roomTrigger.gameObject.GetComponent<BossRoomTrigger>();
            if (proxy == null) proxy = roomTrigger.gameObject.AddComponent<BossRoomTrigger>();
            proxy.Init(this, PlayerLayer);
        }
    }

    /// <summary>
    /// Call this to fully initialize and start the boss. Wired to the room trigger automatically,
    /// but can also be called manually from any other script or Unity Event.
    /// </summary>
    public void StartBoss()
    {
        if (_activated || bossData == null) return;
        _activated = true;

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
        _alive         = true;

        StartCoroutine(ActivationSequence());
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
    // ROOM ACTIVATION
    // =========================================================

    private IEnumerator ActivationSequence()
    {
        // Boss is alive but HealthController stays disabled — fully immune during countdown.
        yield return new WaitForSeconds(activationDelay);

        _health.enabled = true;

        if (battleMusicClip != null)
        {
            _battleMusicAS      = gameObject.AddComponent<AudioSource>();
            _battleMusicAS.clip = battleMusicClip;
            _battleMusicAS.Play();
        }

        EnterPassiveDefenses();
        _phaseLoop = StartCoroutine(PhaseLoop());
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

        if (_ambientAS     != null) _ambientAS.Stop();
        if (_battleMusicAS != null) _battleMusicAS.Stop();

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
