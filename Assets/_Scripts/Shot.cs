using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Shot : MonoBehaviour
{
    public int Damage { get; private set; }
    public float Speed { get; private set; }
    public float GravityForce { get; private set; }

    private Rigidbody rb;
    private bool initialized;
    private Vector3 moveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void Initialize(int damage, float speed, float gravityForce)
    {
        if (initialized) return;

        Damage = damage;
        Speed = speed;
        GravityForce = gravityForce;

        moveDirection = transform.forward.normalized;
        rb.linearVelocity = moveDirection * Speed;

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
        Destroy(gameObject);
    }
}