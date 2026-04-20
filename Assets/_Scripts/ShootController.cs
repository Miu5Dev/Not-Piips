using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShootController : MonoBehaviour
{
    [SerializeField] private WeaponSO currentWeapon;
    [SerializeField] private Transform spawnpoint;
    [SerializeField] private float hipFireDuration = 0.2f;
    [SerializeField] private float shotSpawnDelay = 0.06f;

    private float hipFireTimer;
    private bool isHipFiring;
    private float lastShotTime;

    private bool isFireHeld;
    private bool canSemiAutoShootAgain = true;

    private void OnEnable()
    {
        EventBus.Subscribe<OnSwapInputEvent>(OnFireInput);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<OnSwapInputEvent>(OnFireInput);
    }

    private void Update()
    {
        HandleHipFireTimer();

        if (currentWeapon == null || currentWeapon.ammo == null)
            return;

        if (currentWeapon.shotType == ShotType.Automatic && isFireHeld)
        {
            TryShoot();
        }
    }

    private void OnFireInput(OnSwapInputEvent e)
    {
        isFireHeld = e.pressed;

        if (!e.pressed)
        {
            canSemiAutoShootAgain = true;
            return;
        }

        switch (currentWeapon.shotType)
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
                // no dispara aquí continuamente;
                // Update se encarga mientras isFireHeld sea true
                TryShoot(); // opcional para respuesta inmediata al primer click
                break;
        }
    }

    private void TryShoot()
    {
        if (Time.time < lastShotTime + (1f / currentWeapon.fireRate))
            return;

        lastShotTime = Time.time;

        StartHipFire();
        StartCoroutine(FireAfterDelay());
    }

    private IEnumerator FireAfterDelay()
    {
        yield return new WaitForSeconds(shotSpawnDelay);

        int pellets = Mathf.Max(1, currentWeapon.pellets);

        if (pellets == 1)
        {
            SpawnProjectile(spawnpoint.rotation);
        }
        else
        {
            for (int i = 0; i < pellets; i++)
            {
                Quaternion spreadRot = GetSpreadRotation(spawnpoint.rotation, currentWeapon.spreadAngle);
                SpawnProjectile(spreadRot);
            }
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
            currentWeapon.ammo.gravityForce
        );
    }

    private Quaternion GetSpreadRotation(Quaternion baseRotation, float spreadAngle)
    {
        float yaw = Random.Range(-spreadAngle, spreadAngle);
        float pitch = Random.Range(-spreadAngle, spreadAngle);
        return baseRotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    private void StartHipFire()
    {
        hipFireTimer = hipFireDuration;

        if (!isHipFiring)
        {
            isHipFiring = true;

            EventBus.Raise(new OnHipFireStateChangedEvent
            {
                Shooter = transform,
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
                Shooter = transform,
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