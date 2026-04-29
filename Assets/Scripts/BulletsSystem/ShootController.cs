using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShootController : MonoBehaviour
{
    public static ShootController Instance { get; private set; } // ← nuevo

    [SerializeField] private WeaponSO currentWeapon;
    [SerializeField] private Transform spawnpoint;
    [SerializeField] private float hipFireDuration = 0.2f;
    [SerializeField] private float shotSpawnDelay  = 0.06f;

    [SerializeField] private AimTargetController aimTarget;

    [SerializeField] private int _currentMagazineField;
    private int _currentMagazine
    {
        get => _currentMagazineField;
        set
        {
            if (_currentMagazineField == value) return;
            _currentMagazineField = value;
            InventoryGridUI.Instance?.RefreshWeaponAmmoLabels();
        }
    }
    private bool  _isReloading;

    private float _hipFireTimer;
    private bool  _isHipFiring;
    private float _lastShotTime;
    private bool  _isFireHeld;
    private bool  _canSemiAutoShootAgain = true;

    private WaitForSeconds             _waitForDelay;
    private OnHipFireStateChangedEvent _hipFireOn;
    private OnHipFireStateChangedEvent _hipFireOff;

    public int  CurrentMagazine => _currentMagazine;
    public int  MaxMagazineSize => currentWeapon != null ? currentWeapon.maxMagazineSize : 0;
    public bool IsReloading     => _isReloading;
    public bool IsMagazineEmpty => _currentMagazine <= 0;
    public WeaponSO CurrentWeapon => currentWeapon;

    private void Awake()
    {
        Instance = this; // ← nuevo

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

    // ── Equip ─────────────────────────────────────────────────────────────
    // initialAmmo = -1 → usar maxMagazineSize (comportamiento original)
    public void EquipWeapon(WeaponSO weapon, int initialAmmo = -1)
    {
        currentWeapon    = weapon;
        _currentMagazine = (weapon != null && initialAmmo >= 0)
            ? initialAmmo
            : (weapon != null ? weapon.maxMagazineSize : 0);
        _isReloading = false;
    }

    // ── Fire input ────────────────────────────────────────────────────────
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

    // ── Core shoot logic ──────────────────────────────────────────────────
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
                ? GetSpreadRotation(baseRot, currentWeapon.spreadAngle, currentWeapon.spreadOnlyHorizontal)
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

    // ── Reload ────────────────────────────────────────────────────────────
// ── Reload ────────────────────────────────────────────────────────────────
    public void Reload()
    {
        if (_isReloading) return;
        if (currentWeapon == null) return;
        if (_currentMagazine >= currentWeapon.maxMagazineSize) return;

        if (AmmoInventory.GetCount(currentWeapon.ammo) <= 0)
        {
            Debug.Log("[ShootController] Sin munición en el inventario.");
            return;
        }

        StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        _isReloading = true;

        yield return new WaitForSeconds(currentWeapon.reloadTime);

        bool isShotgunStyle = currentWeapon.shotType == ShotType.Manual;

        // Consume exactamente 1 item del stack en ambos casos
        int consumed = AmmoInventory.Consume(currentWeapon.ammo, 1);

        if (consumed > 0)
        {
            if (isShotgunStyle)
            {
                // 1 item = 1 cartucho introducido
                _currentMagazine = Mathf.Min(_currentMagazine + 1, currentWeapon.maxMagazineSize);
                Debug.Log($"[ShootController] +1 cartucho ({_currentMagazine}/{currentWeapon.maxMagazineSize})");
            }
            else
            {
                // 1 item = cargador entero
                _currentMagazine = currentWeapon.maxMagazineSize;
                Debug.Log($"[ShootController] Recargado completo ({_currentMagazine}/{currentWeapon.maxMagazineSize})");
            }
        }
        else
        {
            Debug.Log("[ShootController] Sin munición en el inventario.");
        }

        _isReloading = false;
    }

    // ── Spread & Hip fire ─────────────────────────────────────────────────
    private static Quaternion GetSpreadRotation(Quaternion baseRotation, float spreadAngle, bool horizontalOnly)
    {
        if (horizontalOnly)
        {
            float h = (Random.value * 2f - 1f) * spreadAngle;
            return baseRotation * Quaternion.Euler(0f, h, 0f);
        }
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