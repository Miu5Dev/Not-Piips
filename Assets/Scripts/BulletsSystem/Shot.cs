using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Shot : MonoBehaviour
{
    public int   Damage       { get; private set; }
    public float Speed        { get; private set; }
    public float GravityForce { get; private set; }

    [Header("Decal")]
    public bool useDecalProjector = true;

    [Header("Trail Randomization")]
    [SerializeField] private int trailPresetCount = 8;

    private GameObject    decalPrefab;
    private Rigidbody     rb;
    private TrailRenderer trail;
    private bool          initialized;
    private Vector3       moveDirection;
    private Vector3       spawnPoint;
    private float         despawnSqr;

    public float despawnDistance = 100f;

    private AnimationCurve[] _trailPresets;
    private float[]          _trailTimes;
    private int              _presetIndex;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        rb    = GetComponent<Rigidbody>();
        trail = GetComponentInChildren<TrailRenderer>(includeInactive: true);

        rb.useGravity             = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        despawnSqr                = despawnDistance * despawnDistance;

        PrewarmTrailPresets();
    }

    private void OnEnable()
    {
        // Always re-cache — TrailFader.Detach replaces the trail child
        trail = GetComponentInChildren<TrailRenderer>(includeInactive: true);
        if (trail == null) return;

        trail.gameObject.SetActive(true);
        trail.Clear();
        trail.emitting = true;
    }

    private void OnDisable()
    {
        initialized       = false;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic    = false;
    }

    // =========================================================
    // INITIALIZE
    // =========================================================

    public void Initialize(int damage, float speed, float gravityForce, GameObject decal = null)
    {
        if (initialized) return;

        Damage       = damage;
        Speed        = speed;
        GravityForce = gravityForce;
        decalPrefab  = decal;

        moveDirection     = transform.forward.normalized;
        rb.linearVelocity = moveDirection * Speed;
        spawnPoint        = transform.position;
        initialized       = true;

        ApplyTrailPreset();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void FixedUpdate()
    {
        if (!initialized) return;

        Vector3 v = rb.linearVelocity;
        v.x = moveDirection.x * Speed;
        v.z = moveDirection.z * Speed;
        v.y += GravityForce * Time.fixedDeltaTime;
        rb.linearVelocity = v;

        if ((transform.position - spawnPoint).sqrMagnitude >= despawnSqr)
            Die();
    }

    private void OnCollisionEnter(Collision collision)
    {
        SpawnDecal(collision.GetContact(0), collision.transform);
        Die();
    }

    // =========================================================
    // DIE — detach trail BEFORE releasing to pool so SetActive(false)
    //        on the bullet doesn't kill the trail ghost mid-copy
    // =========================================================

    private void Die()
    {
        if (trail != null)
        {
            TrailFader.Detach(trail, transform);
            trail = null; // fresh trail attached by Detach, re-cached in OnEnable
        }

        BulletPool.GetOrCreate().Release(this);
    }

    // =========================================================
    // TRAIL
    // =========================================================

    private void PrewarmTrailPresets()
    {
        _trailPresets = new AnimationCurve[trailPresetCount];
        _trailTimes   = new float[trailPresetCount];

        for (int i = 0; i < trailPresetCount; i++)
        {
            float widthScale = Random.Range(0.6f, 1.4f);
            float tip        = Random.Range(0.005f, 0.02f);
            float mid        = Random.Range(0.03f,  0.09f) * widthScale;
            float tail       = Random.Range(0.01f,  0.05f) * widthScale;

            _trailPresets[i] = new AnimationCurve(
                new Keyframe(0f,                        tip,  0f, 0f),
                new Keyframe(Random.Range(0.3f, 0.6f), mid,  0f, 0f),
                new Keyframe(1f,                        tail, 0f, 0f)
            );
            _trailTimes[i] = Random.Range(0.1f, 0.35f);
        }
    }

    private void ApplyTrailPreset()
    {
        if (trail == null || _trailPresets == null) return;

        _presetIndex          = (_presetIndex + 1) % _trailPresets.Length;
        trail.widthCurve      = _trailPresets[_presetIndex];
        trail.widthMultiplier = 1f;
        trail.time            = _trailTimes[_presetIndex];
    }

    // =========================================================
    // DECAL
    // =========================================================

    private void SpawnDecal(ContactPoint contact, Transform parent)
    {
        if (decalPrefab == null) return;

        Vector3    position = contact.point + contact.normal * 0.001f;
        Quaternion rotation = Quaternion.LookRotation(-contact.normal);

        if (DecalManager.Instance != null)
            DecalManager.Instance.Spawn(decalPrefab, position, rotation, parent);
        else
        {
            var d = Instantiate(decalPrefab, position, rotation);
            d.transform.SetParent(parent, worldPositionStays: true);
        }
    }
}
