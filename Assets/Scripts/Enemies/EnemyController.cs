using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ShootController))]
[RequireComponent(typeof(HealthController))]
public class EnemyController : MonoBehaviour
{
    // ── Cached references ──────────────────────────────────────────────────
    private EnemySO          _data;
    private Transform        _player;
    private Rigidbody        _rb;
    private Transform        _muzzle;
    private GameObject       _modelRoot;
    private ShootController  _shootController;
    private HealthController _healthController;

    // ── AI desired state ───────────────────────────────────────────────────
    private Vector3    _desiredVelocity;
    private Quaternion _targetRotation;

    // ── Runtime state ──────────────────────────────────────────────────────
    private float         _uniqueOffset;
    private float         _lastShotTime;
    private bool          _alive;
    private System.Action _onDeath;

    // ── LoS / flanking state ───────────────────────────────────────────────
    private int     _aiTickCount;
    private bool    _hasLos;
    private bool    _aggressive;
    private int     _strafeDir = 1;
    private float   _nextStrafeFlip;
    private Vector3 _lastKnownPlayerPos;
    private bool    _hasLastKnown;
    private float   _lastJumpTime;
    private int     _stuckTickCount;
    private Vector3 _lastStuckPos;
    private float   _lastStuckTime;
    private float   _backupUntil;
    private float   _lastSeenTime;

    // ── Tick constants ─────────────────────────────────────────────────────
    private const float TickMin    = 0.10f;
    private const float TickMax    = 0.20f;
    private const float TickJitter = 0.05f;
    private const float LodTick    = 0.40f;

    // ── AI tuning constants ────────────────────────────────────────────────
    private const int   LosTickInterval     = 5;     // run LoS every Nth AI tick
    private const float StrafeFlipInterval  = 1.0f;
    private const int   StuckTicksToJump    = 2;     // consecutive stuck ticks before jump
    private const int   StuckTicksToBackUp  = 4;     // …and if still stuck, back up
    private const float BackupDuration      = 0.6f;  // base seconds of reverse movement (scaled by desperation)
    private const float StuckSpeedRatio     = 0.3f;
    private const float ProbeDistance       = 1.0f;
    private const float ProbeOriginHeight   = 0.6f;
    private static readonly float[] ProbeAngles = { 0f, -30f, 30f, -60f, 60f, -90f, 90f };

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        _rb             = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _targetRotation = transform.rotation;

        _shootController                    = GetComponent<ShootController>();
        _shootController.IsPlayerController = false;

        _healthController = GetComponent<HealthController>();

        EnsureMuzzle();
    }

    private void OnEnable()
    {
        MinimapRenderer.Register(this);
    }

    private void OnDisable()
    {
        MinimapRenderer.Unregister(this);
        _shootController?.OnFireReleased();
        StopAllCoroutines();
        _alive           = false;
        _desiredVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (!_alive) return;

        _rb.linearVelocity = new Vector3(
            _desiredVelocity.x,
            _rb.linearVelocity.y,
            _desiredVelocity.z);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, _targetRotation,
            Time.fixedDeltaTime * 10f);
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public void Initialize(EnemySO data, Transform player, System.Action onDeath = null)
    {
        _data         = data;
        _player       = player;
        _onDeath      = onDeath;
        _alive        = true;
        _uniqueOffset = Random.Range(0f, Mathf.PI * 2f);
        _lastShotTime = Time.time + Random.Range(0f, 1f);

        // Restore rigidbody in case it was frozen on death
        _rb.isKinematic = false;

        _healthController.isDead    = false;
        _healthController.maxHealth = data.maxHealth;
        _healthController.maxShield = data.maxShield;
        _healthController.health    = data.maxHealth;
        _healthController.shield    = data.maxShield;

        var weapon = data.availableWeapons != null && data.availableWeapons.Length > 0
            ? data.availableWeapons[Random.Range(0, data.availableWeapons.Length)]
            : null;

        _shootController.EquipWeapon(weapon);

        // Reset flanking / nav state
        _aiTickCount        = 0;
        _hasLos             = false;
        _aggressive         = false;
        _strafeDir          = Random.value < 0.5f ? -1 : 1;
        _nextStrafeFlip     = Time.time + StrafeFlipInterval;
        _hasLastKnown       = false;
        _stuckTickCount     = 0;
        _lastJumpTime       = 0f;
        _lastStuckPos       = transform.position;
        _lastStuckTime      = Time.time;
        _backupUntil        = 0f;
        _lastSeenTime       = Time.time;

        StartCoroutine(AITick());
    }

    public void SetModel(GameObject modelRoot) => _modelRoot = modelRoot;

    // =========================================================
    // DEATH EVENT
    // =========================================================

    public void OnDieEventReceived(OnDieEvent e)
    {
        if (e.murderedObject != gameObject) return;
        if (!_alive) return;
        StartCoroutine(Die());
    }

    // =========================================================
    // AI TICK
    // =========================================================

    private IEnumerator AITick()
    {
        yield return new WaitForSeconds(Random.Range(0f, TickMax));

        while (_alive)
        {
            if (_player != null)
            {
                _aiTickCount++;

                // LoS check on a slower cadence than movement.
                if (_aiTickCount % LosTickInterval == 0)
                    UpdateLineOfSight();

                float dist = Vector3.Distance(transform.position, _player.position);
                UpdateStuckAndJump();
                ComputeMovement(dist);
                TryShoot(dist);

                float tick = dist > _data.aiLodDistance
                    ? LodTick
                    : Mathf.Clamp(
                        Random.Range(TickMin, TickMax) + Random.Range(-TickJitter, TickJitter),
                        TickMin, TickMax + TickJitter);

                yield return new WaitForSeconds(tick);
            }
            else
            {
                yield return new WaitForSeconds(TickMax);
            }
        }
    }

    // 0..1 scalar: ramps from 0 → 1 once panic has begun (i.e. only after PanicTime
    // has elapsed without sight). Stays 0 during the calm "I just walked toward
    // your last known position" phase.
    private float Desperation()
    {
        float panicElapsed = (Time.time - _lastSeenTime) - _data.panicTime;
        if (panicElapsed <= 0f) return 0f;
        return Mathf.Clamp01(panicElapsed / _data.desperationRampTime);
    }

    // =========================================================
    // LINE OF SIGHT
    // =========================================================

    private void UpdateLineOfSight()
    {
        Vector3 origin = _muzzle.position;
        Vector3 target = _player.position + Vector3.up * _data.losTargetHeight;

        // Ray is blocked if anything on obstacleMask sits between us and the player.
        bool blocked = Physics.Linecast(origin, target, _data.obstacleMask, QueryTriggerInteraction.Ignore);

        _hasLos = !blocked;

        if (_hasLos)
        {
            // Sight re-established — commit to the engagement immediately.
            _lastKnownPlayerPos = _player.position;
            _hasLastKnown       = true;
            _lastSeenTime       = Time.time;
            _aggressive         = false;
        }
        // No-LoS branch: nothing to do here. The panic transition is time-driven
        // and handled at the top of ComputeMovement so it can fire between LoS checks.
    }

    // =========================================================
    // STUCK DETECTION + JUMPING
    // =========================================================

    private void UpdateStuckAndJump()
    {
        // Position-delta stuck detection: rb.linearVelocity is overwritten every
        // FixedUpdate, so it always reads as the *desired* velocity even when we're
        // wedged against a wall. Compare actual displacement instead.
        float dt = Time.time - _lastStuckTime;
        if (dt < 0.01f) return;

        Vector3 displacement = transform.position - _lastStuckPos;
        displacement.y = 0f;
        float actualSpeed = displacement.magnitude / dt;

        Vector3 desiredFlat  = new Vector3(_desiredVelocity.x, 0f, _desiredVelocity.z);
        float   desiredSpeed = desiredFlat.magnitude;

        _lastStuckPos  = transform.position;
        _lastStuckTime = Time.time;

        if (desiredSpeed > 0.5f && actualSpeed < desiredSpeed * StuckSpeedRatio)
            _stuckTickCount++;
        else
            _stuckTickCount = 0;

        if (_stuckTickCount < StuckTicksToJump) return;

        // 1) Jump first — cheap, handles low obstacles and small ledges.
        if (Time.time >= _lastJumpTime + _data.jumpCooldown && IsGrounded())
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, _data.jumpForce, _rb.linearVelocity.z);
            _lastJumpTime      = Time.time;
            _stuckTickCount    = 0;
            return;
        }

        // 2) Still stuck after jump (or cooldown not ready)? Back up to escape wedges.
        if (_stuckTickCount >= StuckTicksToBackUp)
        {
            // Desperation extends how far they retreat — more separation = more chance
            // to find a new angle when they re-engage.
            float backupScale = 1f + Desperation() * _data.desperationBackupBoost;
            _backupUntil    = Time.time + BackupDuration * backupScale;
            _stuckTickCount = 0;

            // Flip strafe so we don't immediately re-wedge the same way.
            if (_aggressive)
            {
                _strafeDir      = -_strafeDir;
                _nextStrafeFlip = Time.time + StrafeFlipInterval;
            }
        }
    }

    private bool IsGrounded()
    {
        // Y-velocity is never overwritten by our FixedUpdate, so |y-vel| ≈ 0
        // reliably means gravity has been arrested by something below — i.e. grounded.
        // Pivot-agnostic, mask-agnostic, robust to weird prefab setups.
        if (Mathf.Abs(_rb.linearVelocity.y) < 0.5f)
            return true;

        // Raycast fallback in case of physics jitter.
        return Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            Vector3.down,
            1.0f,
            _data.obstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    // =========================================================
    // NAVIGATION PROBE (find a clear way toward the player)
    // =========================================================

    // Casts a fan of rays around the desired direction; returns the
    // direction with the longest clear distance, biased toward forward.
    private Vector3 ProbeBestDirection(Vector3 desired)
    {
        Vector3 origin = transform.position + Vector3.up * ProbeOriginHeight;
        Vector3 best   = desired;
        float   bestScore = -1f;

        for (int i = 0; i < ProbeAngles.Length; i++)
        {
            float angle = ProbeAngles[i];
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * desired;

            float clearDist = ProbeDistance;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, ProbeDistance,
                                _data.obstacleMask, QueryTriggerInteraction.Ignore))
            {
                clearDist = hit.distance;
            }

            // Forward bias: 0° gets full weight, ±90° gets half.
            float forwardBias = Mathf.Lerp(1f, 0.5f, Mathf.Abs(angle) / 90f);
            float score       = clearDist * forwardBias;

            if (score > bestScore)
            {
                bestScore = score;
                best      = dir;
            }
        }
        return best.normalized;
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    private void ComputeMovement(float dist)
    {
        // ── Panic transition (time-driven) ────────────────────────────────────
        // Below PanicTime: behave exactly like the original simple AI — walk
        // toward the player (or last-known position) with normal lateral wander.
        // Past PanicTime: enter aggressive flank/probe behavior.
        bool shouldPanic = !_hasLos && (Time.time - _lastSeenTime) >= _data.panicTime;
        if (shouldPanic && !_aggressive)
        {
            _aggressive     = true;
            _strafeDir      = Random.value < 0.5f ? -1 : 1;
            _nextStrafeFlip = Time.time + StrafeFlipInterval;
        }
        else if (!shouldPanic && _aggressive)
        {
            _aggressive = false;
        }

        // Aim point: live player if visible, otherwise last-known position.
        Vector3 aimPoint = _hasLos || !_hasLastKnown
            ? _player.position
            : _lastKnownPlayerPos;

        Vector3 toAim = aimPoint - transform.position;
        toAim.y = 0f;
        Vector3 toAimDir = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : transform.forward;

        // Always face the actual player when we can see them; otherwise face where we're heading.
        Vector3 lookDir = _hasLos ? (_player.position - transform.position) : toAimDir;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.0001f)
            _targetRotation = Quaternion.LookRotation(lookDir.normalized);

        // ── Escape override: temporarily back away when wedged between obstacles ──
        if (Time.time < _backupUntil)
        {
            _desiredVelocity = -toAimDir * _data.moveSpeed;
            return;
        }

        float   lateralAmount = Mathf.Sin(Time.time * _data.wanderFrequency + _uniqueOffset)
                                * _data.lateralStrength;
        Vector3 right         = transform.right;

        // ── Aggressive flank: probe for a clear path, hard-strafe, push in close ─
        if (_aggressive)
        {
            // Reactive flip: if the strafe side is walled, flip *now* — don't wait for the timer.
            Vector3 strafeRayOrigin = transform.position + Vector3.up * ProbeOriginHeight;
            if (Physics.Raycast(strafeRayOrigin, right * _strafeDir, 1.2f,
                                _data.obstacleMask, QueryTriggerInteraction.Ignore))
            {
                _strafeDir      = -_strafeDir;
                _nextStrafeFlip = Time.time + StrafeFlipInterval;
            }
            // Periodic flip so they arc around cover instead of strafing in a straight line forever.
            else if (Time.time >= _nextStrafeFlip)
            {
                _strafeDir      = -_strafeDir;
                _nextStrafeFlip = Time.time + StrafeFlipInterval;
            }

            float desperation = Desperation();

            // Full-desperation charge: if we've been blind this long AND nothing
            // is in our way, abandon the flank and just sprint at the player.
            // Otherwise the strafe amplitude drowns out forward motion at high
            // desperation, leaving them sidestepping in place from far away.
            if (desperation >= 0.99f)
            {
                Vector3 forwardOrigin = transform.position + Vector3.up * ProbeOriginHeight;
                bool forwardClear = !Physics.Raycast(forwardOrigin, toAimDir, ProbeDistance,
                                                     _data.obstacleMask, QueryTriggerInteraction.Ignore);
                if (forwardClear)
                {
                    _desiredVelocity = toAimDir * _data.moveSpeed;
                    return;
                }
            }

            // Desperation scales strafe amplitude — the longer they've gone unseen,
            // the wider/more frantic the side-to-side sweep gets.
            float strafeMult = _data.aggroStrafeMult * (1f + desperation * _data.desperationStrafeBoost);

            Vector3 navDir = ProbeBestDirection(toAimDir);
            Vector3 strafe = right * (_strafeDir * _data.lateralStrength * strafeMult);

            _desiredVelocity = navDir * _data.moveSpeed + strafe;
            return;
        }

        // ── Hunt mode: LoS lost but pre-panic ─────────────────────────────────
        // Bypass the three-zone logic entirely — there's no live target to
        // maintain spacing from. Push toward last-known at hunt speed so they
        // actually go look for the player instead of wandering in the comfort zone.
        if (!_hasLos && _hasLastKnown)
        {
            _desiredVelocity = toAimDir * (_data.moveSpeed * _data.huntSpeedMult)
                             + right * (lateralAmount * 0.25f);
            return;
        }

        // ── Calm engagement: original three-zone logic when we have eyes on the player ─
        if (dist > _data.fromPlayerMax)
        {
            _desiredVelocity = toAimDir * _data.moveSpeed + right * (lateralAmount * 0.25f);
        }
        else if (dist < _data.fromPlayerMin)
        {
            float urgency    = 1f + Mathf.Clamp01(1f - dist / _data.fromPlayerMin);
            _desiredVelocity = -toAimDir * (_data.moveSpeed * urgency) + right * (lateralAmount * 0.5f);
        }
        else
        {
            _desiredVelocity = right * lateralAmount;
        }
    }

    // =========================================================
    // SHOOT
    // =========================================================

    private void TryShoot(float dist)
    {
        if (_shootController == null) return;
        if (_shootController.IsMagazineEmpty) { _shootController.Reload(); return; }
        if (_shootController.IsReloading) return;
        if (Time.time < _lastShotTime + _data.shootBuffer) return;
        if (!_hasLos) return; // don't waste ammo on walls

        Vector3 dir = (_player.position + Vector3.up * 0.5f - _muzzle.position).normalized;
        if (dir == Vector3.zero) return;

        _muzzle.rotation = Quaternion.LookRotation(dir);
        _lastShotTime    = Time.time;
        _shootController.OnFirePressed();
        _shootController.OnFireReleased();
    }

    // =========================================================
    // DEATH
    // =========================================================

    private IEnumerator Die()
    {
        // Cache local — prevents OnDisable from nulling _data mid-coroutine
        EnemySO enemyData = _data;

        _alive           = false;
        _desiredVelocity = Vector3.zero;

        if (enemyData.freezeOnDeath)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic     = true;
        }

        if (enemyData.deathEffect != null)
        {
            var fx = Instantiate(enemyData.deathEffect, transform.position, Quaternion.identity);
            Destroy(fx, 5f);
        }

        // ── Loot drop ──────────────────────────────────────────────────────
        if (enemyData.dropPrefab != null && Random.value <= enemyData.dropChance)
        {
            var drop = Instantiate(
                enemyData.dropPrefab,
                transform.position + Vector3.up * 0.5f,
                Quaternion.identity
            );

            var visual = drop.GetComponent<WorldItemVisual>();
            if (visual != null)
                visual.LaunchDrop(enemyData.dropUpForce);
        }
        // ──────────────────────────────────────────────────────────────────

        _onDeath?.Invoke();
        _onDeath = null;
        EnemyPool.Instance.ReturnEnemy(this);

        yield break;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void EnsureMuzzle()
    {
        if (_muzzle != null) return;

        var go = new GameObject("Muzzle");
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(0f, 0.6f, 0.6f);
        _muzzle = go.transform;

        _shootController.SetSpawnPoint(_muzzle);
    }
}