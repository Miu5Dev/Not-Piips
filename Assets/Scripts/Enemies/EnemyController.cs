using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ShootController))]
public class EnemyController : MonoBehaviour
{
    // ── Cached references ─────────────────────────────────────────────────────
    private EnemySO         _data;
    private Transform       _player;
    private Rigidbody       _rb;
    private Transform       _muzzle;
    private GameObject      _modelRoot;
    private ShootController _shootController;

    // ── AI desired state (written by coroutine, consumed by FixedUpdate) ──────
    private Vector3    _desiredVelocity;
    private Quaternion _targetRotation;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private float         _currentHealth;
    private float         _uniqueOffset;
    private float         _lastShotTime;
    private bool          _alive;
    private System.Action _onDeath;

    // ── Tick constants ────────────────────────────────────────────────────────
    private const float TickMin    = 0.10f;
    private const float TickMax    = 0.20f;
    private const float TickJitter = 0.05f;
    private const float LodTick   = 0.40f;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        _rb             = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _targetRotation = transform.rotation;

        _shootController = GetComponent<ShootController>();
        _shootController.IsPlayerController = false;

        EnsureMuzzle(); // muzzle must exist before any Initialize call
    }

    private void OnEnable() => MinimapRenderer.Register(this);

    private void FixedUpdate()
    {
        if (!_alive) return;

        // Apply velocity computed by the AI tick
        _rb.linearVelocity = new Vector3(
            _desiredVelocity.x,
            _rb.linearVelocity.y,
            _desiredVelocity.z);

        // Smooth rotation every physics step — not AI logic, just presentation
        transform.rotation = Quaternion.Slerp(
            transform.rotation, _targetRotation,
            Time.fixedDeltaTime * 10f);
    }

    private void OnDisable()
    {
        MinimapRenderer.Unregister(this);
        _shootController?.OnFireReleased();
        StopAllCoroutines();
        _alive           = false;
        _desiredVelocity = Vector3.zero;
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <param name="onDeath">Optional callback — called the moment the enemy is returned to pool.</param>
    public void Initialize(EnemySO data, Transform player, System.Action onDeath = null)
    {
        _data          = data;
        _player        = player;
        _onDeath       = onDeath;
        _currentHealth = data.health;
        _alive         = true;
        _uniqueOffset  = Random.Range(0f, Mathf.PI * 2f);
        _lastShotTime  = Time.time + Random.Range(0f, 1f); // stagger first shot

        var weapon = data.availableWeapons != null && data.availableWeapons.Length > 0
            ? data.availableWeapons[Random.Range(0, data.availableWeapons.Length)]
            : null;

        _shootController.EquipWeapon(weapon);

        StartCoroutine(AITick());
    }

    public void TakeDamage(float amount)
    {
        if (!_alive) return;
        _currentHealth -= amount;
        if (_currentHealth <= 0f)
            StartCoroutine(Die());
    }

    /// <summary>Called by EnemyPool to attach the model child at creation time.</summary>
    public void SetModel(GameObject modelRoot) => _modelRoot = modelRoot;

    // =========================================================
    // AI TICK
    // =========================================================

    private IEnumerator AITick()
    {
        // Stagger initial tick so enemies spawned together don't all compute at once
        yield return new WaitForSeconds(Random.Range(0f, TickMax));

        while (_alive)
        {
            if (_player != null)
            {
                float dist = Vector3.Distance(transform.position, _player.position);
                ComputeMovement(dist);
                TryShoot(dist);

                float tick = dist > _data.aiLodDistance
                    ? LodTick
                    : Mathf.Clamp(Random.Range(TickMin, TickMax)
                        + Random.Range(-TickJitter, TickJitter), TickMin, TickMax + TickJitter);

                yield return new WaitForSeconds(tick);
            }
            else
            {
                yield return new WaitForSeconds(TickMax);
            }
        }
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    private void ComputeMovement(float dist)
    {
        Vector3 toPlayer = (_player.position - transform.position).normalized;
        if (toPlayer != Vector3.zero)
            _targetRotation = Quaternion.LookRotation(toPlayer);

        // Sine-wave lateral component — unique phase prevents enemy stacking
        float   lateralAmount = Mathf.Sin(Time.time * _data.wanderFrequency + _uniqueOffset)
                                * _data.lateralStrength;
        Vector3 right         = transform.right;

        if (dist > _data.fromPlayerMax)
        {
            // Too far — move toward player, small lateral drift
            _desiredVelocity = toPlayer * _data.moveSpeed + right * (lateralAmount * 0.25f);
        }
        else if (dist < _data.fromPlayerMin)
        {
            // Too close — soft repulsion: speed scales with how much inside min distance
            float urgency    = 1f + Mathf.Clamp01(1f - dist / _data.fromPlayerMin);
            _desiredVelocity = -toPlayer * (_data.moveSpeed * urgency) + right * (lateralAmount * 0.5f);
        }
        else
        {
            // Comfort zone — pure lateral wander, no approach/retreat
            _desiredVelocity = right * lateralAmount;
        }
    }

    // =========================================================
    // SHOOT — delegates entirely to ShootController
    // =========================================================

    private void TryShoot(float dist)
    {
        if (_shootController == null) return;

        if (_shootController.IsMagazineEmpty)
        {
            _shootController.Reload();
            return;
        }

        if (_shootController.IsReloading) return;
        if (Time.time < _lastShotTime + _data.shootBuffer) return;

        // Aim the muzzle (ShootController's spawnpoint) at the player
        Vector3 dir = (_player.position + Vector3.up * 0.5f - _muzzle.position).normalized;
        if (dir == Vector3.zero) return;
        _muzzle.rotation = Quaternion.LookRotation(dir);

        _lastShotTime = Time.time;
        _shootController.OnFirePressed();
        _shootController.OnFireReleased();
    }

    // =========================================================
    // DEATH
    // =========================================================

    private IEnumerator Die()
    {
        _alive           = false;
        _desiredVelocity = Vector3.zero;

        if (_modelRoot != null)
            _modelRoot.SetActive(false);

        if (_data.deathEffect != null)
        {
            var fx = Instantiate(_data.deathEffect, transform.position, Quaternion.identity);
            Destroy(fx, _data.deathEffectDuration + 1f);
        }

        yield return new WaitForSeconds(_data.deathEffectDuration);

        if (_modelRoot != null)
            _modelRoot.SetActive(true);

        _onDeath?.Invoke();
        _onDeath = null;
        EnemyPool.Instance.ReturnEnemy(this);
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
