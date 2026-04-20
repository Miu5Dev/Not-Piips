using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Shot : MonoBehaviour
{
    public int   Damage      { get; private set; }
    public float Speed       { get; private set; }
    public float GravityForce { get; private set; }

    // Assigned by ShootController via Initialize()
    private GameObject decalPrefab;

    private Rigidbody rb;
    private bool      initialized;
    private Vector3   moveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void Initialize(int damage, float speed, float gravityForce, GameObject decal = null)
    {
        if (initialized) return;

        Damage        = damage;
        Speed         = speed;
        GravityForce  = gravityForce;
        decalPrefab   = decal;

        moveDirection      = transform.forward.normalized;
        rb.linearVelocity  = moveDirection * Speed;

        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        Vector3 v = rb.linearVelocity;
        v.x = moveDirection.x * Speed;
        v.z = moveDirection.z * Speed;
        v.y += GravityForce * Time.fixedDeltaTime;
        rb.linearVelocity = v;
    }

    private void OnCollisionEnter(Collision collision)
    {
        SpawnDecal(collision);
        Destroy(gameObject);
    }

    // =========================================================
    // DECAL
    // =========================================================
    private void SpawnDecal(Collision collision)
    {
        if (decalPrefab == null) return;

        // Use the first contact point for position + surface normal
        ContactPoint contact = collision.GetContact(0);

        // Offset slightly off the surface to avoid z-fighting
        Vector3    position = contact.point + contact.normal * 0.001f;

        // Rotate so the decal's forward faces away from the surface (along the normal)
        Quaternion rotation = Quaternion.LookRotation(-contact.normal);

        GameObject decal = Instantiate(decalPrefab, position, rotation);

        // Parent to the hit object so the decal moves with it (e.g. moving platforms)
        decal.transform.SetParent(collision.transform, worldPositionStays: true);
    }
}
