using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Shot : MonoBehaviour
{
    public int   Damage       { get; private set; }
    public float Speed        { get; private set; }
    public float GravityForce { get; private set; }

    [Header("Decal")]
    [Tooltip("Check if your decal prefab uses a URP DecalProjector. Uncheck for a simple quad.")]
    public bool useDecalProjector = true;

    private GameObject    decalPrefab;
    private Rigidbody     rb;
    private TrailRenderer trail;
    private bool          initialized;
    private Vector3       moveDirection;
    private Vector3       spawnPoint;

    public float DistanceFromSpawn;
    public float despawnDistance = 100f;

    private void Awake()
    {
        rb    = GetComponent<Rigidbody>();
        trail = GetComponentInChildren<TrailRenderer>(includeInactive: true);

        rb.useGravity             = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

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

        RandomizeTrail();
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        Vector3 v = rb.linearVelocity;
        v.x = moveDirection.x * Speed;
        v.z = moveDirection.z * Speed;
        v.y += GravityForce * Time.fixedDeltaTime;
        rb.linearVelocity = v;

        DistanceFromSpawn = Vector3.Distance(spawnPoint, transform.position);
        if (DistanceFromSpawn >= despawnDistance) Die();
    }

    private void OnCollisionEnter(Collision collision)
    {
        SpawnDecal(collision.GetContact(0), collision.transform);
        Die();
    }

    // =========================================================
    // DIE
    // =========================================================
    private void Die()
    {
        if (trail != null)
            TrailFader.Detach(trail);

        Destroy(gameObject);
    }

    // =========================================================
    // TRAIL RANDOMIZATION
    // =========================================================
    private void RandomizeTrail()
    {
        if (trail == null) return;

        float widthScale = Random.Range(0.6f, 1.4f);
        float tip        = Random.Range(0.005f, 0.02f);
        float mid        = Random.Range(0.03f,  0.09f) * widthScale;
        float tail       = Random.Range(0.01f,  0.05f) * widthScale;

        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f,                        tip,  0f, 0f),
            new Keyframe(Random.Range(0.3f, 0.6f), mid,  0f, 0f),
            new Keyframe(1f,                        tail, 0f, 0f)
        );
        trail.widthMultiplier = 1f;
        trail.time            = Random.Range(0.1f, 0.35f);
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
