using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(PhysicsController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Hijo del Player que contiene la malla. NUNCA el root.")]
    public Transform modelTransform;
    [Tooltip("Se asigna automáticamente con Camera.main si está vacío.")]
    public Transform cameraTransform;

    [Header("Movement")]
    public float moveSpeed     = 5f;
    public float rotationSpeed = 15f;

    [Header("Jump")]
    public float jumpForce = 8f;

    [Header("Gravity")]
    public float gravity         = -20f;
    public float groundedGravity = -2f;

    [Header("Aim")]
    public CinemachineCamera vcamFollow;
    public CinemachineCamera vcamAim;
    public int   priorityNormal   = 10;
    public int   priorityAim      = 15;
    public float aimRotationSpeed = 25f;

    // PRIVADOS
    private PhysicsController physics;
    private Vector3 velocity;

    private Vector2 moveInput;
    private bool   jumpPressed;
    private bool   isAiming;
    private bool   isHipFiring; // NUEVO

    void Awake()
    {
        physics = GetComponent<PhysicsController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (modelTransform == null)
        {
            modelTransform = transform;
            Debug.LogWarning("[PlayerMovement] modelTransform no asignado. Asigna el hijo 'Model' para evitar el giro en círculos.");
        }

        if (vcamFollow != null) vcamFollow.Priority = priorityNormal;
        if (vcamAim    != null) vcamAim.Priority    = 0;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    public void OnMove(Vector2 direction) => moveInput = direction;
    public void OnJump(bool pressed) => jumpPressed = pressed;

    public void OnAim(bool pressed)
    {
        isAiming = pressed;

        if (vcamFollow != null) vcamFollow.Priority = isAiming ? 0           : priorityNormal;
        if (vcamAim    != null) vcamAim.Priority    = isAiming ? priorityAim : 0;
    }

    // NUEVO: estado de hipfire
    public void OnHipFireStateChanged(Transform shooter, bool IsHipFiring)
    {
        if (shooter != transform) return;
        isHipFiring = IsHipFiring;
    }

    void FixedUpdate()
    {
        GroundInfo ground = physics.Ground;

        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;

        Vector3 moveDir = camForward * moveInput.y + camRight * moveInput.x;

        velocity.x = moveDir.x * moveSpeed;
        velocity.z = moveDir.z * moveSpeed;

        HandleRotation(moveDir);

        if (jumpPressed && ground.isGrounded)
            velocity.y = jumpForce;

        if (!ground.isGrounded)
            velocity.y += gravity * Time.fixedDeltaTime;
        else if (velocity.y < 0f)
            velocity.y = groundedGravity;

        MoveResult result = physics.Move(velocity * Time.fixedDeltaTime);

        if (result.HitCeiling() && velocity.y > 0f)
            velocity.y = 0f;
    }

    private void HandleRotation(Vector3 moveDir)
    {
        Vector3 camForwardOnPlane = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        if (camForwardOnPlane.sqrMagnitude < 0.01f) return;

        if (isAiming || isHipFiring)
        {
            Quaternion targetRot = Quaternion.LookRotation(camForwardOnPlane, Vector3.up);

            modelTransform.rotation = Quaternion.Slerp(
                modelTransform.rotation,
                targetRot,
                aimRotationSpeed * Time.fixedDeltaTime
            );
        }
        else
        {
            if (moveDir.sqrMagnitude <= 0.01f)
                return;

            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);

            modelTransform.rotation = Quaternion.Slerp(
                modelTransform.rotation,
                targetRot,
                rotationSpeed * Time.fixedDeltaTime
            );
        }
    }
}