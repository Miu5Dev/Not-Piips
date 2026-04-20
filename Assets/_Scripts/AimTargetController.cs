using UnityEngine;

public class AimTargetController : MonoBehaviour
{
    [Header("Aim")]
    [Tooltip("Camera used to cast the aim ray. Defaults to Camera.main if not assigned.")]
    public Camera aimCamera;

    [Tooltip("How far the aim point is placed when the ray hits nothing.")]
    public float maxAimDistance = 100f;

    [Tooltip("Layers the aim ray can hit. Exclude the player's own colliders.")]
    public LayerMask aimMask = ~0;

    /// <summary>
    /// World-space point the crosshair is currently aimed at.
    /// Updated every LateUpdate — read this from ShootController when spawning.
    /// </summary>
    public Vector3 AimPoint { get; private set; }

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (aimCamera == null) return;

        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        AimPoint = Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask)
            ? hit.point
            : ray.GetPoint(maxAimDistance);

        // Rotate spawnpoint toward the aim point
        Vector3 direction = (AimPoint - transform.position).normalized;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}
