using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShootController : MonoBehaviour
{
    public static ShootController Instance { get; private set; }

    public bool IsPlayerController { get; set; } = true;

    [SerializeField] private WeaponSO            currentWeapon;
    [SerializeField] private Transform           spawnpoint;
    [SerializeField] private float               hipFireDuration = 0.2f;
    [SerializeField] private float               shotSpawnDelay  = 0.06f;
    [SerializeField] private AimTargetController aimTarget;
    [SerializeField] private WeaponSO            backupWeapon;

    [SerializeField] private int _currentMagazineField;
    private int _currentMagazine
    {
        get => _currentMagazineField;
        set
        {
            if (_currentMagazineField == value) return;
            _currentMagazineField = value;
            if (IsPlayerController)
            {
                InventoryGridUI.Instance?.RefreshWeaponAmmoLabels();
                EquipedWeaponUIController.Instance?.RefreshAmmo();
            }
        }
    }
    private bool _isReloading;

    private float _hipFireTimer;
    private bool  _isHipFiring;
    private float _lastShotTime;
    private bool  _isFireHeld;
    private bool  _canSemiAutoShootAgain = true;

    private WaitForSeconds             _waitForDelay;
    private OnHipFireStateChangedEvent _hipFireOn;
    private OnHipFireStateChangedEvent _hipFireOff;

    public int      CurrentMagazine => _currentMagazine;
    public int      MaxMagazineSize => currentWeapon != null ? currentWeapon.maxMagazineSize : 0;
    public bool     IsReloading     => _isReloading;
    public bool     IsMagazineEmpty => _currentMagazine <= 0;
    public WeaponSO CurrentWeapon   => currentWeapon;
    public float ReloadProgress { get; private set; }

    public void SetSpawnPoint(Transform t) => spawnpoint = t;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (IsPlayerController && (Instance == null || Instance == this))
            Instance = this;

        _waitForDelay = new WaitForSeconds(shotSpawnDelay);
        _hipFireOn    = new OnHipFireStateChangedEvent { Shooter = transform, IsHipFiring = true  };
        _hipFireOff   = new OnHipFireStateChangedEvent { Shooter = transform, IsHipFiring = false };

        if (currentWeapon != null)
            _currentMagazine = currentWeapon.maxMagazineSize;
    }

    private void Start()
    {
        if (!IsPlayerController) return;

        WeaponSO weaponToShow = currentWeapon ?? backupWeapon;
        if (weaponToShow == null) return;

        if (currentWeapon == null)
            EquipWeapon(backupWeapon);

        EventBus.Raise(new OnWeaponEquipEvent
        {
            weaponToEquip = currentWeapon,
            initialAmmo   = _currentMagazine
        });
    }

    private void Update()
    {
        HandleHipFireTimer();

        if (currentWeapon == null || currentWeapon.ammo == null) return;
        if (currentWeapon.shotType == ShotType.Automatic && _isFireHeld)
            TryShoot();
    }

    // ── Weapon equip event ────────────────────────────────────────────────────

    public void OnWeaponEquip(OnWeaponEquipEvent e)
    {
        if (!IsPlayerController) return;

        if (e.weaponToEquip == null)
        {
            TryEquipBackup($"Switched to {backupWeapon?.name}");
            return;
        }

        EquipWeapon(e.weaponToEquip, e.initialAmmo);
    }

    // ── Equip ─────────────────────────────────────────────────────────────────

    public void EquipWeapon(WeaponSO weapon, int initialAmmo = -1)
    {
        currentWeapon    = weapon;
        _currentMagazine = (weapon != null && initialAmmo >= 0)
            ? initialAmmo
            : (weapon != null ? weapon.maxMagazineSize : 0);
        _isReloading = false;

        if (IsPlayerController)
            EquipedWeaponUIController.Instance?.RefreshAmmo();
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
        if (currentWeapon == null || currentWeapon.ammo == null)
        {
            TryEquipBackup();
            return;
        }
        if (_isReloading) return;

        if (!currentWeapon.infiniteAmmo)
        {
            if (_currentMagazine <= 0) { Reload(); return; }
        }

        if (Time.time < _lastShotTime + (1f / currentWeapon.fireRate)) return;

        if (!currentWeapon.infiniteAmmo)
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
            currentWeapon.ammo.decalPrefab,
            currentWeapon.ammo.decalLayers,
            currentWeapon.ammo.impactVFXPrefab,
            firedByPlayer: Instance == this,
            currentWeapon.ammo.collisionLayers
        );
    }

    // ── Reload ────────────────────────────────────────────────────────────────

    public void Reload()
    {
        if (_isReloading) return;
        if (currentWeapon == null) return;
        if (currentWeapon.infiniteAmmo) return;
        if (_currentMagazine >= currentWeapon.maxMagazineSize) return;

        if (IsPlayerController && AmmoInventory.GetCount(currentWeapon.ammo) <= 0)
        {
            Debug.Log("[ShootController] No ammo in inventory.");
            if (_currentMagazine <= 0)
                TryEquipBackup();
            return;
        }

        StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        _isReloading   = true;
        ReloadProgress = 0f; // ← reset al empezar

        float elapsed        = 0f;
        float duration       = currentWeapon.reloadTime;
        bool  isShotgunStyle = currentWeapon.shotType == ShotType.Manual;
        int   fullMag        = currentWeapon.maxMagazineSize;

        if (IsPlayerController)
            EventBus.Raise(new OnReloadEvent { IsReloading = true, Progress = 0f });

        while (elapsed < duration)
        {
            elapsed        += Time.deltaTime;
            ReloadProgress  = Mathf.Clamp01(elapsed / duration); // ← actualizar propiedad

            if (IsPlayerController)
                EventBus.Raise(new OnReloadEvent
                {
                    IsReloading = true,
                    Progress    = ReloadProgress // ← usar la propiedad directamente
                });
            yield return null;
        }

        ReloadProgress = 1f; // ← completo

        if (IsPlayerController)
        {
            int consumed = AmmoInventory.Consume(currentWeapon.ammo, 1);
            if (consumed > 0)
            {
                _currentMagazine = isShotgunStyle
                    ? Mathf.Min(_currentMagazine + 1, fullMag)
                    : fullMag;
            }
            else
            {
                Debug.Log("[ShootController] No ammo in inventory.");
            }
        }
        else
        {
            _currentMagazine = fullMag;
        }

        if (IsPlayerController)
            EventBus.Raise(new OnReloadEvent { IsReloading = false, Progress = 1f });

        _isReloading = false;
    }

    // ── Spread & Hip fire ─────────────────────────────────────────────────────

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

    // ── Ammo ──────────────────────────────────────────────────────────────────

    public void AddAmmo(int amount)
    {
        _currentMagazine = Mathf.Min(_currentMagazine + amount, currentWeapon.maxMagazineSize);
    }

    // ── Backup weapon ─────────────────────────────────────────────────────────

    private void TryEquipBackup(string reason = null)
    {
        if (backupWeapon == null) return;
        if (currentWeapon == backupWeapon) return;

        InventoryEquipHandler.Instance?.UnequipCurrent();
        EquipWeapon(backupWeapon);

        EventBus.Raise(new OnWeaponEquipEvent
        {
            weaponToEquip = backupWeapon,
            initialAmmo   = backupWeapon.maxMagazineSize
        });

        string msg = reason ?? $"No ammo! Switched to {backupWeapon.name}";
        InventoryDragHandler.Instance?.ShowPopup(msg);
        Debug.Log($"[ShootController] Switched to backup: {backupWeapon.name}. Reason: {reason ?? "no ammo"}");
    }
}