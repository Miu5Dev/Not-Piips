using UnityEngine;

/// <summary>
/// Attach to any GameObject tagged "Wall" or "Door" to make it appear on the minimap.
/// Walls read their shape from a BoxCollider (or Renderer bounds as fallback).
/// Doors use an inspector-set half-extents value instead.
/// Corners are cached once in Awake — suitable for static geometry.
/// </summary>
public class MinimapWall : MonoBehaviour
{
    [Tooltip("When true, renders as a door using the inspector size below instead of the collider.")]
    [SerializeField] public bool isDoor;

    [Tooltip("Half-extents in world units (X = width, Y = depth). Only used when isDoor is true.")]
    [SerializeField] private Vector2 doorHalfExtents = new Vector2(1f, 0.25f);

    [Tooltip("Minimum half-thickness in world units applied to the thin axis of wall colliders. Increase to make walls appear thicker on the minimap.")]
    [SerializeField] private float minimapMinHalfThickness = 5f;

    /// <summary>Four XZ world-space corners of this wall/door, cached at Awake.</summary>
    public Vector2[] Corners { get; private set; }

    private void Awake()
    {
        Corners = isDoor ? BuildDoorCorners() : BuildColliderCorners();
    }

    /// <summary>
    /// Call this after the room has been repositioned/rotated (e.g. after AlignRoom)
    /// so the cached world-space corners reflect the final transform.
    /// </summary>
    public void RefreshCorners()
    {
        Corners = isDoor ? BuildDoorCorners() : BuildColliderCorners();
    }

    private void OnEnable()  => MinimapRenderer.RegisterWall(this);
    private void OnDisable() => MinimapRenderer.UnregisterWall(this);

    private Vector2[] BuildColliderCorners()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            // World-space center of the collider
            Vector3 worldCenter = transform.TransformPoint(box.center);

            // lossyScale already accounts for the full parent hierarchy scale
            // box.size * lossyScale = actual world size
            float wx = box.size.x * transform.lossyScale.x * 0.5f;
            float wz = box.size.z * transform.lossyScale.z * 0.5f;

            // Force a minimum thickness in world units AFTER scale is applied
            wx = Mathf.Max(wx, minimapMinHalfThickness);
            wz = Mathf.Max(wz, minimapMinHalfThickness);

            // Build the 4 corners around the world center
            // NOTE: this ignores rotation — add rotation support below if needed
            float yaw = transform.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw);

            // Rotate the 4 corners by the wall's own yaw
            Vector2 cx = new Vector2( cos, sin) * wx;
            Vector2 cz = new Vector2(-sin, cos) * wz;

            Vector2 center2D = new Vector2(worldCenter.x, worldCenter.z);

            return new Vector2[]
            {
                center2D - cx - cz,
                center2D + cx - cz,
                center2D + cx + cz,
                center2D - cx + cz,
            };
        }

        // Fallback: use renderer AABB
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            Bounds b = rend.bounds;
            return new Vector2[]
            {
                new(b.min.x, b.min.z),
                new(b.max.x, b.min.z),
                new(b.max.x, b.max.z),
                new(b.min.x, b.max.z),
            };
        }

        Debug.LogWarning($"[MinimapWall] {name}: no BoxCollider or Renderer found.", this);
        return new Vector2[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
    }

    private Vector2[] BuildDoorCorners()
    {
        float ex = doorHalfExtents.x;
        float ez = doorHalfExtents.y;

        var local = new Vector3[]
        {
            new(-ex, 0f, -ez),
            new( ex, 0f, -ez),
            new( ex, 0f,  ez),
            new(-ex, 0f,  ez),
        };

        var result = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 w = transform.TransformPoint(local[i]);
            result[i] = new Vector2(w.x, w.z);
        }
        return result;
    }
}
