using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Place on any object positioned inside a wall opening.
/// At Start it auto-detects the wall behind it and cuts a matching hole in the mesh.
/// This object acts as the physical door blocker — its renderers and colliders are
/// disabled when the door is opened, revealing the pre-cut hole.
/// </summary>
/// <remarks>
/// WALL REQUIREMENTS: the wall should be a flat mesh (Plane, Quad, or single-face)
/// and must have a Collider so the raycast can find it.
/// The door's forward (blue arrow) must point OUTWARD from the room.
/// </remarks>
[RequireComponent(typeof(Interactable))]
public class DoorController : MonoBehaviour
{
    [Header("Wall Cutting")]
    [Tooltip("Wall MeshFilter to cut. Leave null to auto-detect by raycasting backward.")]
    [SerializeField] private MeshFilter wallToCut;
    [Tooltip("Override hole size (width, height). Zero = auto-size from this object's bounds.")]
    [SerializeField] private Vector2 holeSizeOverride = Vector2.zero;
    [Tooltip("How far behind the door to search for a wall.")]
    [SerializeField] private float wallDetectDistance = 3f;

    [Header("Visuals (optional)")]
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject unlockedVisual;
    [SerializeField] private GameObject sealedVisual;

    [Header("Animation")]
    [Tooltip("Animator on the door mesh. Leave null if not using animations.")]
    [SerializeField] private Animator doorAnimator;
    [Tooltip("How long to wait for the open animation before disabling the door.")]
    [SerializeField] private float openAnimationDuration = 0.6f;
    [Tooltip("How long to wait for the close animation before the room is destroyed.")]
    [SerializeField] private float closeAnimationDuration = 0.6f;

    private DoorState _state = DoorState.Locked;
    private bool _used;
    private bool _wallCut;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake() => UpdateVisuals();

    // Start() handles standalone doors already in the scene.
    // Spawned room doors are cut explicitly by RoomManager via CutWallNow().
    private void Start() => CutWallNow();

    // =========================================================
    // WALL CUTTING (public so RoomManager can call it in the same frame)
    // =========================================================

    public void CutWallNow()
    {
        if (_wallCut) return;
        _wallCut = true;

        if (wallToCut == null) wallToCut = FindWall();

        if (wallToCut != null)
            CutWall(wallToCut);
        else
            Debug.LogWarning($"[DoorController] '{name}': no wall found. Assign Wall To Cut manually or add a Collider to the wall.");
    }

    public void ResetCut()
    {
        _wallCut = false;
        wallToCut = null;
    }

    // =========================================================
    // CALLED BY Interactable → On Use
    // =========================================================

    public void TryOpen()
    {
        if (_used || _state != DoorState.Unlocked) return;
        _used = true;

        RoomManager.Instance?.OpenNextRoom(this);
        StartCoroutine(PlayOpenAnimation()); // no SetActive(false) here anymore
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public DoorState State => _state;

    public void SetState(DoorState state)
    {
        _state = state;
        UpdateVisuals();
    }

    /// <summary>
    /// Removes this door object so the player can walk through the wall hole.
    /// Called on the entry door of a newly spawned room — player never sees this side.
    /// </summary>
    public void ClearBlocker()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Plays the close animation and then invokes the callback (used by RoomManager
    /// to delay room destruction until the door has visually closed).
    /// </summary>
    public void CloseAndThen(Action onComplete)
    {
        StartCoroutine(PlayCloseAnimation(onComplete));
    }

    // =========================================================
    // ANIMATIONS
    // =========================================================

    private IEnumerator PlayOpenAnimation()
    {
        if (doorAnimator != null)
            doorAnimator.SetTrigger("Open");

        // Don't disable — let CloseAndThen() handle it when the room is destroyed
        yield break;
    }

    private IEnumerator PlayCloseAnimation(Action onComplete)
    {
        if (doorAnimator != null)
            doorAnimator.SetTrigger("Close");

        yield return new WaitForSeconds(closeAnimationDuration);
        onComplete?.Invoke();
    }

    // =========================================================
    // WALL DETECTION
    // =========================================================

    private MeshFilter FindWall()
    {
        // ── Pass 1: overlap check ────────────────────────────────────────────
        var selfCol = GetComponent<Collider>();
        if (selfCol != null)
        {
            Bounds b = selfCol.bounds;
            Collider[] overlapping = Physics.OverlapBox(b.center, b.extents, transform.rotation);
            foreach (var c in overlapping)
            {
                if (c.transform == transform) continue;
                if (c.transform.IsChildOf(transform)) continue;
                if (c.transform.IsChildOf(transform.root)) continue;
                if (!c.CompareTag("Wall")) continue;
                var mf = c.GetComponent<MeshFilter>() ?? c.GetComponentInParent<MeshFilter>();
                if (mf != null) return mf;
            }
        }

        // ── Pass 2: directional raycast fallback ─────────────────────────────
        Vector3 rayOrigin = transform.position + transform.forward * 0.2f;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, -transform.forward, wallDetectDistance + 0.2f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform == transform) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.transform.IsChildOf(transform.root)) continue;
            if (!hit.collider.CompareTag("Wall")) continue;
            var mf = hit.collider.GetComponent<MeshFilter>() ?? hit.collider.GetComponentInParent<MeshFilter>();
            if (mf != null) return mf;
        }

        return null;
    }

    // =========================================================
    // MESH CUTTING
    // =========================================================

    private void CutWall(MeshFilter wallFilter)
    {
        Mesh src = wallFilter.sharedMesh;
        if (src == null) return;

        Bounds wb = src.bounds;
        Vector3 sz = wb.size;

        int dAxis = 0;
        if (sz.y < sz[dAxis]) dAxis = 1;
        if (sz.z < sz[dAxis]) dAxis = 2;

        Vector3 localUp = wallFilter.transform.InverseTransformDirection(Vector3.up);
        float[] ua = { Mathf.Abs(localUp.x), Mathf.Abs(localUp.y), Mathf.Abs(localUp.z) };
        ua[dAxis] = -1f;
        int hAxis = ua[0] >= ua[1] && ua[0] >= ua[2] ? 0 : ua[1] >= ua[2] ? 1 : 2;
        int wAxis = 3 - dAxis - hAxis;

        float wMin = wb.min[wAxis], wMax = wb.max[wAxis];
        float hMin = wb.min[hAxis], hMax = wb.max[hAxis];
        float dMid = wb.center[dAxis];

        Vector3 doorLocal = wallFilter.transform.InverseTransformPoint(transform.position);
        Vector2 hole = holeSizeOverride != Vector2.zero
            ? holeSizeOverride
            : AutoHoleSize(wallFilter.transform, wAxis, hAxis);

        float hWMin = Mathf.Clamp(doorLocal[wAxis] - hole.x * 0.5f, wMin, wMax);
        float hWMax = Mathf.Clamp(doorLocal[wAxis] + hole.x * 0.5f, wMin, wMax);
        float hHMin = Mathf.Clamp(doorLocal[hAxis] - hole.y * 0.5f, hMin, hMax);
        float hHMax = Mathf.Clamp(doorLocal[hAxis] + hole.y * 0.5f, hMin, hMax);

        Mesh frame = BuildFrame(wMin, wMax, hMin, hMax, hWMin, hWMax, hHMin, hHMax,
            dMid, dAxis, wAxis, hAxis);

        // ── Flip winding if the mesh normal points away from the door ──
        // The door's forward points OUTWARD from the room, so the wall normal
        // should point in the same general direction as the door's forward.
        Vector3 meshNormal = wallFilter.transform.TransformDirection(frame.normals[0]);
        if (Vector3.Dot(meshNormal, transform.forward) < 0f)
            FlipNormals(frame);

        wallFilter.mesh = frame;

        var mc = wallFilter.GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.convex = false;
            mc.sharedMesh = null;
            mc.sharedMesh = frame;
        }
    }

    private static void FlipNormals(Mesh mesh)
    {
        // Reverse triangle winding order
        int[] tris = mesh.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            int tmp = tris[i];
            tris[i] = tris[i + 2];
            tris[i + 2] = tmp;
        }
        mesh.triangles = tris;
        mesh.RecalculateNormals();
    }

    private Vector2 AutoHoleSize(Transform wallTransform, int wAxis, int hAxis)
    {
        Bounds? b = null;
        var rend = GetComponentInChildren<Renderer>();
        if (rend != null) b = rend.bounds;
        else { var col = GetComponent<Collider>(); if (col != null) b = col.bounds; }

        if (b.HasValue)
        {
            Vector3 ls = wallTransform.InverseTransformVector(b.Value.size);
            float w = Mathf.Abs(ls[wAxis]);
            float h = Mathf.Abs(ls[hAxis]);
            if (w > 0.05f && h > 0.05f) return new Vector2(w, h);
        }

        return new Vector2(2f, 3f);
    }

    private static Mesh BuildFrame(
        float wMin, float wMax, float hMin, float hMax,
        float hWMin, float hWMax, float hHMin, float hHMax,
        float depth, int dAxis, int wAxis, int hAxis)
    {
        var verts = new Vector3[16];
        var tris  = new int[24];
        var uvs   = new Vector2[16];
        float totalW = wMax - wMin, totalH = hMax - hMin;

        Vector3 Vert(float w, float h)
        {
            var v = Vector3.zero;
            v[wAxis] = w; v[hAxis] = h; v[dAxis] = depth;
            return v;
        }

        int vi = 0, ti = 0;
        void Quad(float x0, float y0, float x1, float y1)
        {
            int b = vi;
            verts[vi] = Vert(x0, y1); uvs[vi++] = new Vector2((x0 - wMin) / totalW, (y1 - hMin) / totalH);
            verts[vi] = Vert(x1, y1); uvs[vi++] = new Vector2((x1 - wMin) / totalW, (y1 - hMin) / totalH);
            verts[vi] = Vert(x1, y0); uvs[vi++] = new Vector2((x1 - wMin) / totalW, (y0 - hMin) / totalH);
            verts[vi] = Vert(x0, y0); uvs[vi++] = new Vector2((x0 - wMin) / totalW, (y0 - hMin) / totalH);
            tris[ti++] = b; tris[ti++] = b + 1; tris[ti++] = b + 2;
            tris[ti++] = b; tris[ti++] = b + 2; tris[ti++] = b + 3;
        }

        Quad(wMin,  hMin,  wMax,  hHMin);
        Quad(wMin,  hHMax, wMax,  hMax);
        Quad(wMin,  hHMin, hWMin, hHMax);
        Quad(hWMax, hHMin, wMax,  hHMax);

        var mesh = new Mesh { name = "WallWithHole" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.uv        = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // =========================================================
    // PRIVATE
    // =========================================================

    private void UpdateVisuals()
    {
        if (lockedVisual   != null) lockedVisual.SetActive(_state   == DoorState.Locked);
        if (unlockedVisual != null) unlockedVisual.SetActive(_state == DoorState.Unlocked);
        if (sealedVisual   != null) sealedVisual.SetActive(_state   == DoorState.Sealed);
    }

    // =========================================================
    // EDITOR
    // =========================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _state switch
        {
            DoorState.Unlocked => new Color(0.2f, 1f,   0.3f, 0.4f),
            DoorState.Locked   => new Color(1f,   0.2f, 0.2f, 0.4f),
            DoorState.Sealed   => new Color(0.5f, 0.5f, 0.5f, 0.4f),
            _                  => Color.white
        };
        var col = GetComponent<Collider>();
        if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * wallDetectDistance);
    }
#endif
}

public enum DoorState { Unlocked, Locked, Sealed }