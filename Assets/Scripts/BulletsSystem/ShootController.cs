using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShootController : MonoBehaviour
{
    [SerializeField] private WeaponSO  currentWeapon;
    [SerializeField] private Transform spawnpoint;
    [SerializeField] private float     hipFireDuration = 0.2f;
    [SerializeField] private float     shotSpawnDelay  = 0.06f;

    [SerializeField] private AimTargetController aimTarget;

    private float hipFireTimer;
    private bool  isHipFiring;
    private float lastShotTime;
    private bool  isFireHeld;
    private bool  canSemiAutoShootAgain = true;

    private WaitForSeconds             _waitForDelay;
    private OnHipFireStateChangedEvent _hipFireOn;
    private OnHipFireStateChangedEvent _hipFireOff;

    private void Awake()
    {
        _waitForDelay = new WaitForSeconds(shotSpawnDelay);
        _hipFireOn    = new OnHipFireStateChangedEvent { Shooter = transform, IsHipFiring = true  };
        _hipFireOff   = new OnHipFireStateChangedEvent { Shooter = transform, IsHipFiring = false };
    }

    private void Update()
    {
        HandleHipFireTimer();

        if (currentWeapon == null || currentWeapon.ammo == null) return;
        if (currentWeapon.shotType == ShotType.Automatic && isFireHeld)
            TryShoot();
    }

    public void OnFirePressed()
    {
        isFireHeld = true;

        switch (currentWeapon != null ? currentWeapon.shotType : ShotType.SemiAutomatic)
        {
            case ShotType.SemiAutomatic:
            case ShotType.Manual:
                if (canSemiAutoShootAgain)
                {
                    TryShoot();
                    canSemiAutoShootAgain = false;
                }
                break;

            case ShotType.Automatic:
                TryShoot();
                break;
        }
    }

    public void OnFireReleased()
    {
        isFireHeld            = false;
        canSemiAutoShootAgain = true;
    }

    private void TryShoot()
    {
        if (currentWeapon == null || currentWeapon.ammo == null) return;
        if (Time.time < lastShotTime + (1f / currentWeapon.fireRate)) return;

        lastShotTime = Time.time;
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
        // GetOrCreate ensures a pool always exists — no manual setup needed
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

    private static Quaternion GetSpreadRotation(Quaternion baseRotation, float spreadAngle)
    {
        Vector2 spread = Random.insideUnitCircle * spreadAngle;
        return baseRotation * Quaternion.Euler(spread.y, spread.x, 0f);
    }

    private void StartHipFire()
    {
        hipFireTimer = hipFireDuration;
        if (!isHipFiring)
        {
            isHipFiring = true;
            EventBus.Raise(_hipFireOn);
        }
    }

    private void HandleHipFireTimer()
    {
        if (!isHipFiring) return;

        hipFireTimer -= Time.deltaTime;
        if (hipFireTimer <= 0f)
        {
            isHipFiring = false;
            EventBus.Raise(_hipFireOff);
        }
    }
}