using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ShootController))]
[RequireComponent(typeof(HealthController))]
public class EnemyController : MonoBehaviour
{
    // ── Cached references ──────────────────────────────────────────────────
    private EnemySO _data;
    private Transform _player;
    private Rigidbody _rb;
    private Transform _muzzle;
    private GameObject _modelRoot;
    private ShootController _shootController;
    private HealthController _healthController;

    // ── AI desired state ───────────────────────────────────────────────────
    private Vector3 _desiredVelocity;
    private Quaternion _targetRotation;

    // ── Runtime state ──────────────────────────────────────────────────────
    private float _uniqueOffset;
    private float _lastShotTime;
    private bool _alive;
    private System.Action _onDeath;

    // ── Tick constants ─────────────────────────────────────────────────────
    private const float TickMin    = 0.10f;
    private const float TickMax    = 0.20f;
    private const float TickJitter = 0.05f;
    private const float LodTick    = 0.40f;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _targetRotation = transform.rotation;

        _shootController = GetComponent<ShootController>();
        _shootController.IsPlayerController = false;

        _healthController = GetComponent<HealthController>();

        EnsureMuzzle();
    }

    private void OnEnable()
    {
        MinimapRenderer.Register(this);
        EventBus.Subscribe<OnDieEvent>(OnDieEventReceived);
    }

    private void OnDisable()
    {
        MinimapRenderer.Unregister(this);
        EventBus.Unsubscribe<OnDieEvent>(OnDieEventReceived);
        _shootController?.OnFireReleased();
        StopAllCoroutines();
        _alive = false;
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
        _data    = data;
        _player  = player;
        _onDeath = onDeath;
        _alive   = true;
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
        StartCoroutine(AITick());
    }

    public void SetModel(GameObject modelRoot) => _modelRoot = modelRoot;

    // =========================================================
    // DEATH EVENT (EventBus)
    // =========================================================

    private void OnDieEventReceived(OnDieEvent e)
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

        float lateralAmount = Mathf.Sin(Time.time * _data.wanderFrequency + _uniqueOffset)
            * _data.lateralStrength;
        Vector3 right = transform.right;

        if (dist > _data.fromPlayerMax)
            _desiredVelocity = toPlayer * _data.moveSpeed + right * (lateralAmount * 0.25f);
        else if (dist < _data.fromPlayerMin)
        {
            float urgency = 1f + Mathf.Clamp01(1f - dist / _data.fromPlayerMin);
            _desiredVelocity = -toPlayer * (_data.moveSpeed * urgency) + right * (lateralAmount * 0.5f);
        }
        else
            _desiredVelocity = right * lateralAmount;
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
        _alive = false;
        _desiredVelocity = Vector3.zero;

        if (_data.freezeOnDeath)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }

        if (_data.deathEffect != null)
        {
            var fx = Instantiate(_data.deathEffect, transform.position, Quaternion.identity);
            Destroy(fx, 5f);
        }

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