using UnityEngine;

/// <summary>
/// Spawns a rotating shield prefab parented to the boss. The shield blocks player
/// bullets (via its collider being on the layer the player ammo collides with).
/// Optional `gapAngle` opens a window the player can shoot through if they line up correctly.
/// </summary>
[CreateAssetMenu(fileName = "RotatingShield", menuName = "Bosses/Defenses/Rotating Shield", order = 0)]
public class RotatingShieldSO : BossDefenseSO
{
    [Header("Prefab")]
    [Tooltip("Visual + collider for the shield. Will be parented to the boss and spun around its Y axis.")]
    public GameObject shieldPrefab;

    [Header("Spin")]
    public float rotationSpeed = 90f;

    private GameObject  _instance;
    private SpinDriver  _driver;

    public override void OnEnter(BossRuntime ctx)
    {
        if (shieldPrefab == null) return;

        _instance = Object.Instantiate(shieldPrefab, ctx.boss.position, Quaternion.identity, ctx.boss);
        _instance.transform.localPosition = Vector3.zero;

        _driver = _instance.AddComponent<SpinDriver>();
        _driver.speed = rotationSpeed;
    }

    public override void OnExit(BossRuntime ctx)
    {
        if (_instance != null) Object.Destroy(_instance);
        _instance = null;
        _driver   = null;
    }

    private class SpinDriver : MonoBehaviour
    {
        public float speed;
        private void Update() => transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
    }
}
