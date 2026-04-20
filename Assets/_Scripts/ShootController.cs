using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShootController : MonoBehaviour
{
    [SerializeField] private WeaponSO currentWeapon;
    [SerializeField] private Transform spawnpoint;
    [SerializeField] private float hipFireDuration = 0.2f;
    [SerializeField] private float shotSpawnDelay  = 0.06f;

    [Tooltip("Reference to AimTargetController to read the exact aim point at shoot time.")]
    [SerializeField] private AimTargetController aimTarget;

    private float hipFireTimer;
    private bool  isHipFiring;
    private float lastShotTime;

    private bool isFireHeld;
    private bool canSemiAutoShootAgain = true;

    // =========================================================
    // UPDATE
    // =========================================================
    private void Update()
    {
        HandleHipFireTimer();

        if (currentWeapon == null || currentWeapon.ammo == null)
            return;

        if (currentWeapon.shotType == ShotType.Automatic && isFireHeld)
            TryShoot();
    }

    // =========================================================
    // PUBLIC API — called from EventBusListener in the Inspector
    // =========================================================
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
        isFireHeld = false;
        canSemiAutoShootAgain = true;
    }

    // =========================================================
    // INTERNAL
    // =========================================================
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
        yield return new WaitForSeconds(shotSpawnDelay);

        // Read AimPoint HERE — at actual spawn time, with spawnpoint already in its new position.
        // This guarantees direction = (currentAimPoint - currentSpawnPos), always in sync.
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
        var go = Instantiate(currentWeapon.ammo.ammoPrefab, spawnpoint.position, rotation);

        Shot shot = go.GetComponent<Shot>();
        if (shot == null)
            shot = go.AddComponent<Shot>();

        shot.Initialize(
            currentWeapon.damage,
            currentWeapon.ammo.speed,
            currentWeapon.ammo.gravityForce,
            currentWeapon.ammo.decalPrefab
        );
    }

    private Quaternion GetSpreadRotation(Quaternion baseRotation, float spreadAngle)
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
            EventBus.Raise(new OnHipFireStateChangedEvent
            {
                Shooter     = transform,
                IsHipFiring = true
            });
        }
    }

    private void HandleHipFireTimer()
    {
        if (!isHipFiring) return;

        hipFireTimer -= Time.deltaTime;

        if (hipFireTimer <= 0f)
        {
            isHipFiring = false;
            EventBus.Raise(new OnHipFireStateChangedEvent
            {
                Shooter     = transform,
                IsHipFiring = false
            });
        }
    }
}

public class OnHipFireStateChangedEvent
{
    public Transform Shooter;
    public bool IsHipFiring;
}
