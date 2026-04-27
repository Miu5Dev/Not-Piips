using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShootController : MonoBehaviour
{
    [SerializeField] private WeaponSO currentWeapon;
    [SerializeField] private Transform spawnpoint;
    [SerializeField] private float hipFireDuration = 0.2f;
    [SerializeField] private float shotSpawnDelay  = 0.06f;

    [SerializeField] private AimTargetController aimTarget;

    // ── Runtime state (per-instance, never stored in the SO) ─────────────────
    [SerializeField] private int   _currentMagazine;
    private bool  _isReloading;

    private float _hipFireTimer;
    private bool  _isHipFiring;
    private float _lastShotTime;
    private bool  _isFireHeld;
    private bool  _canSemiAutoShootAgain = true;

    private WaitForSeconds             _waitForDelay;
    private OnHipFireStateChangedEvent _hipFireOn;
    private OnHipFireStateChangedEvent _hipFireOff;

    // ── Public accessors for HUD / UI ─────────────────────────────────────────
    public int  CurrentMagazine => _currentMagazine;
    public int  MaxMagazineSize => currentWeapon != null ? currentWeapon.maxMagazineSize : 0;
    public bool IsReloading     => _isReloading;
    public bool IsMagazineEmpty => _currentMagazine <= 0;

    private void Awake()
    {
        _waitForDelay = new WaitForSeconds(shotSpawnDelay);
        _hipFireOn  = new OnHipFireStateChangedEvent { Shooter = transform, IsHipFiring = true  };
        _hipFireOff = new OnHipFireStateChangedEvent { Shooter = transform, IsHipFiring = false };

        if (currentWeapon != null)
            _currentMagazine = currentWeapon.maxMagazineSize;
    }

    private void Update()
    {
        HandleHipFireTimer();

        if (currentWeapon == null || currentWeapon.ammo == null) return;
        if (currentWeapon.shotType == ShotType.Automatic && _isFireHeld)
            TryShoot();
    }

    // ── Equip ─────────────────────────────────────────────────────────────────
    public void EquipWeapon(WeaponSO weapon)
    {
        currentWeapon    = weapon;
        _currentMagazine = weapon != null ? weapon.maxMagazineSize : 0;
        _isReloading     = false;
    }

    // ── Fire input ────────────────────────────────────────────────────────────
    public void OnFirePressed()
    {
        _isFireHeld = true;

        switch (currentWeapon != null ? currentWeapon.shotType : ShotType.SemiAutomatic)
        {
            case ShotType.SemiAutomatic:
            case ShotType.Manual:
                if (_canSemiAutoShootAgain)
                {
                    TryShoot();
                    _canSemiAutoShootAgain = false;
                }
                break;

            case ShotType.Automatic:
                TryShoot();
                break;
        }
    }

    public void OnFireReleased()
    {
        _isFireHeld            = false;
        _canSemiAutoShootAgain = true;
    }

    // ── Core shoot logic ──────────────────────────────────────────────────────
    private void TryShoot()
    {
        if (currentWeapon == null || currentWeapon.ammo == null) return;
        if (_isReloading) return;
        if (_currentMagazine <= 0) return;
        if (Time.time < _lastShotTime + (1f / currentWeapon.fireRate)) return;

        _currentMagazine--;
        _lastShotTime = Time.time;
        StartHipFire();
        StartCoroutine(FireAfterDelay());
    }

    private IEnumerator FireAfterDelay()
    {
        yield return _waitForDelay;

        Vector3 aimPoint = aimTarget != null
            ? aimTarget.AimPoint
            : spawnpoint.position + spawnpoint.forward * 100f;

        Vector3    baseDir = (aimPoint - spawnpoint.position).normalized;
        Quaternion baseRot = baseDir != Vector3.zero
            ? Quaternion.LookRotation(baseDir)
            : spawnpoint.rotation;

        int pellets = Mathf.Max(1, currentWeapon.pellets);

        for (int i = 0; i < pellets; i++)
        {
            Quaternion shotRot = currentWeapon.spreadAngle > 0f
                ? GetSpreadRotation(baseRot, currentWeapon.spreadAngle)
                : baseRot;

            SpawnProjectile(shotRot);
        }
    }

    private void SpawnProjectile(Quaternion rotation)
    {
        Shot shot = BulletPool.GetOrCreate().Get(
            currentWeapon.ammo.ammoPrefab,
            spawnpoint.position,
            rotation
        );

        shot.Initialize(
            currentWeapon.damage,
            currentWeapon.ammo.speed,
            currentWeapon.ammo.gravityForce,
            currentWeapon.ammo.decalPrefab
        );
    }

    // ── Reload ────────────────────────────────────────────────────────────────
    public void Reload()
    {
        if (_isReloading) return;
        if (currentWeapon == null) return;
        if (_currentMagazine >= currentWeapon.maxMagazineSize) return;

        StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        _isReloading = true;

        yield return new WaitForSeconds(currentWeapon.reloadTime);

        // TODO: Replace with inventory logic when ready:
        // int needed = currentWeapon.maxMagazineSize - _currentMagazine;
        // int toLoad = Mathf.Min(needed, InventorySystem.GetAmmo(currentWeapon.ammo));
        // _currentMagazine += toLoad;
        // InventorySystem.ConsumeAmmo(currentWeapon.ammo, toLoad);
        _currentMagazine = currentWeapon.maxMagazineSize;

        _isReloading = false;
    }
    // ──────────────────────────────────────────────────────────────────────────

    private static Quaternion GetSpreadRotation(Quaternion baseRotation, float spreadAngle)
    {
        Vector2 spread = Random.insideUnitCircle * spreadAngle;
        return baseRotation * Quaternion.Euler(spread.y, spread.x, 0f);
    }

    private void StartHipFire()
    {
        _hipFireTimer = hipFireDuration;
        if (!_isHipFiring)
        {
            _isHipFiring = true;
            EventBus.Raise(_hipFireOn);
        }
    }

    private void HandleHipFireTimer()
    {
        if (!_isHipFiring) return;

        _hipFireTimer -= Time.deltaTime;
        if (_hipFireTimer <= 0f)
        {
            _isHipFiring = false;
            EventBus.Raise(_hipFireOff);
        }
    }
}
