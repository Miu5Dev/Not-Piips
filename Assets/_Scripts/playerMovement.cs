using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(PhysicsController))]
public class PlayerMovement : MonoBehaviour
{
    // =====================================================
    // INSPECTOR
    // =====================================================

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

    // =====================================================
    // PRIVADOS
    // =====================================================

    private PhysicsController physics;
    private Vector3 velocity;

    private Vector2 moveInput;
    private bool    jumpPressed;
    private bool    isAiming;

    // =====================================================
    // INIT
    // =====================================================

    void Awake()
    {
        physics = GetComponent<PhysicsController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // Fallback: si no asignaron modelTransform, usar el root (no recomendado)
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

    void OnEnable()
    {
        EventBus.Subscribe<OnMoveInputEvent>(OnMove);
        EventBus.Subscribe<OnJumpInputEvent>(OnJump);
        EventBus.Subscribe<OnActionInputEvent>(OnAim);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<OnMoveInputEvent>(OnMove);
        EventBus.Unsubscribe<OnJumpInputEvent>(OnJump);
        EventBus.Unsubscribe<OnActionInputEvent>(OnAim);
    }

    // =====================================================
    // EVENT HANDLERS
    // =====================================================

    private void OnMove(OnMoveInputEvent e) => moveInput = e.Direction;
    private void OnJump(OnJumpInputEvent e) => jumpPressed = e.pressed;

    private void OnAim(OnActionInputEvent e)
    {
        isAiming = e.pressed;

        if (vcamFollow != null) vcamFollow.Priority = isAiming ? 0          : priorityNormal;
        if (vcamAim    != null) vcamAim.Priority    = isAiming ? priorityAim : 0;
    }

    // =====================================================
    // PHYSICS UPDATE
    // =====================================================

    void FixedUpdate()
    {
        GroundInfo ground = physics.Ground;

        // Proyectar ejes de la cámara al plano XZ
        // El root (transform) NUNCA rota → camForward es siempre estable
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;

        Vector3 moveDir = camForward * moveInput.y + camRight * moveInput.x;

        // Velocidad horizontal
        velocity.x = moveDir.x * moveSpeed;
        velocity.z = moveDir.z * moveSpeed;

        // Rotar solo el Model, nunca el root
        HandleRotation(moveDir);

        // Salto
        if (jumpPressed && ground.isGrounded)
            velocity.y = jumpForce;

        // Gravedad
        if (!ground.isGrounded)
            velocity.y += gravity * Time.fixedDeltaTime;
        else if (velocity.y < 0f)
            velocity.y = groundedGravity;

        // Mover con PhysicsController (mueve el root en world space)
        MoveResult result = physics.Move(velocity * Time.fixedDeltaTime);

        // Cancelar Y si golpea techo
        if (result.HitCeiling() && velocity.y > 0f)
            velocity.y = 0f;
    }

    // =====================================================
    // ROTACIÓN — solo afecta modelTransform, nunca transform
    // =====================================================

    private void HandleRotation(Vector3 moveDir)
    {
        if (isAiming)
        {
            // Apuntando → Model mira siempre hacia donde apunta la cámara
            Vector3 aimForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;

            if (aimForward.sqrMagnitude > 0.01f)
            {
                modelTransform.rotation = Quaternion.Slerp(
                    modelTransform.rotation,
                    Quaternion.LookRotation(aimForward, Vector3.up),
                    aimRotationSpeed * Time.fixedDeltaTime
                );
            }
        }
        else
        {
            // Sin apuntar → Model rota hacia la dirección de movimiento
            if (moveDir.sqrMagnitude > 0.01f)
            {
                modelTransform.rotation = Quaternion.Slerp(
                    modelTransform.rotation,
                    Quaternion.LookRotation(moveDir, Vector3.up),
                    rotationSpeed * Time.fixedDeltaTime
                );
            }
        }
    }
}