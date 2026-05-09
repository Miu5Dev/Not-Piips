using UnityEngine;

/// <summary>
/// Spawns N small obstacles that orbit the boss at a fixed radius. Player must
/// reposition to find a window through them — combines naturally with the boss's
/// patterns to create "find the firing lane" gameplay.
/// </summary>
[CreateAssetMenu(fileName = "OrbitingWards", menuName = "Bosses/Defenses/Orbiting Wards", order = 2)]
public class OrbitingWardsSO : BossDefenseSO
{
    [Header("Prefab")]
    public GameObject wardPrefab;

    [Header("Orbit")]
    [Min(1)] public int   wardCount     = 3;
    public float          radius        = 3.5f;
    public float          rotationSpeed = 60f;
    public float          height        = 1.0f;

    private GameObject _orbiter;

    public override void OnEnter(BossRuntime ctx)
    {
        if (wardPrefab == null) return;

        _orbiter = new GameObject("OrbitingWards");
        _orbiter.transform.SetParent(ctx.boss, worldPositionStays: false);
        _orbiter.transform.localPosition = new Vector3(0f, height, 0f);

        for (int i = 0; i < wardCount; i++)
        {
            float angle = (360f / wardCount) * i;
            Quaternion outwardRot = Quaternion.Euler(0f, angle, 0f);
            Vector3 localPos = outwardRot * Vector3.forward * radius;

            var ward = Object.Instantiate(wardPrefab, _orbiter.transform);
            ward.transform.localPosition = localPos;
            // Face outward — the ward's forward points away from the boss center.
            // As the parent _orbiter spins, the ward stays in this local "facing-out" rotation,
            // so visually each ward both orbits AND keeps its back to the boss.
            ward.transform.localRotation = outwardRot;
        }

        var spin = _orbiter.AddComponent<OrbitSpin>();
        spin.speed = rotationSpeed;
    }

    public override void OnExit(BossRuntime ctx)
    {
        if (_orbiter != null) Object.Destroy(_orbiter);
        _orbiter = null;
    }

    private class OrbitSpin : MonoBehaviour
    {
        public float speed;
        private void Update() => transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
    }
}
