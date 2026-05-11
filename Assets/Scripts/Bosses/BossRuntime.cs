using UnityEngine;

/// <summary>
/// Per-fight context handed to every BossPatternSO and BossDefenseSO.
/// Owns the shot-firing helper so patterns stay pure shape logic.
///
/// A "shot" is one trigger pull of `weapon`: it spawns weapon.pellets bullets,
/// each rotated by weapon.spreadAngle, each dealing weapon.damage, each using
/// weapon.ammo's projectile prefab + speed + gravity.
/// </summary>
public class BossRuntime
{
    public Transform      boss;             // root — does NOT rotate to face player
    public Transform      bulletOrigin;     // where bullets emit from
    public Transform      player;
    public WeaponSO       weapon;           // current weapon (phase override or default)
    public BossController controller;
    public Vector3        bossSpawnPosition;

    public void FireShot(Vector3 direction, float speedScale = 1f, float? gravityOverride = null, float turnRate = 0f)
    {
        FireShotFrom(bulletOrigin.position, direction, speedScale, gravityOverride, turnRate);
    }

    public void FireShotFrom(Vector3 position, Vector3 direction, float speedScale = 1f, float? gravityOverride = null, float turnRate = 0f)
    {
        if (weapon == null || weapon.ammo == null || weapon.ammo.ammoPrefab == null) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion baseRot = Quaternion.LookRotation(direction.normalized);
        int pellets = Mathf.Max(1, weapon.pellets);

        for (int i = 0; i < pellets; i++)
        {
            Quaternion rot = weapon.spreadAngle > 0f
                ? ApplySpread(baseRot, weapon.spreadAngle, weapon.spreadOnlyHorizontal)
                : baseRot;

            SpawnSingle(position, rot, speedScale, gravityOverride, turnRate);
        }
    }

    private void SpawnSingle(Vector3 position, Quaternion rotation, float speedScale, float? gravityOverride, float turnRate)
    {
        AmmoSO ammo = weapon.ammo;
        Shot shot = BulletPool.GetOrCreate().Get(ammo.ammoPrefab, position, rotation);

        float speed   = ammo.speed * speedScale;
        float gravity = gravityOverride ?? ammo.gravityForce;

        shot.Initialize(
            weapon.damage,
            speed,
            gravity,
            ammo.decalPrefab,
            ammo.decalLayers,
            ammo.impactVFXPrefab,
            firedByPlayer: false,
            ammo.collisionLayers,
            turnRate
        );
    }

    private static Quaternion ApplySpread(Quaternion baseRotation, float spreadAngle, bool horizontalOnly)
    {
        if (horizontalOnly)
        {
            float h = (Random.value * 2f - 1f) * spreadAngle;
            return baseRotation * Quaternion.Euler(0f, h, 0f);
        }
        Vector2 spread = Random.insideUnitCircle * spreadAngle;
        return baseRotation * Quaternion.Euler(spread.y, spread.x, 0f);
    }

    public WaitForSeconds Wait(float seconds) => new WaitForSeconds(seconds);

    public Vector3 DirectionToPlayer()
    {
        if (player == null) return Vector3.forward;
        Vector3 d = player.position - bulletOrigin.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
    }

    public Vector3 DirectionFromTo(Vector3 from)
    {
        if (player == null) return Vector3.forward;
        Vector3 d = player.position - from;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
    }
}
