using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CPU-side minimap renderer. Clears a Texture2D every LateUpdate and stamps
/// colored pixel circles for the player, enemies, and bullets. Works with any
/// render pipeline — no cameras, no layers, no GPU instancing required.
/// </summary>
[DefaultExecutionOrder(-10)]
public class MinimapRenderer : MonoBehaviour
{
    public static MinimapRenderer Instance { get; private set; }

    // ── Entity registry ───────────────────────────────────────────────────────
    private static readonly HashSet<EnemyController> _enemies = new();
    private static readonly HashSet<Shot>            _bullets = new();
    private static readonly HashSet<MinimapWall>     _walls   = new();

    // ── Config ────────────────────────────────────────────────────────────────
    private Transform _playerTransform;
    private Transform _cameraTransform;
    private float     _worldRadius  = 60f;
    private int       _playerRadius = 5;
    private int       _enemyRadius  = 4;
    private int       _bulletRadius = 2;

    // ── Texture ───────────────────────────────────────────────────────────────
    private const int TexSize = 256;
    private Texture2D _tex;
    private Color32[] _pixels;
    private bool      _ready;

    // ── Colors ────────────────────────────────────────────────────────────────
    private static readonly Color32 BgColor     = new(220,  220,  220,  255);
    private static readonly Color32 PlayerColor = new(  0,  220,    0,  255);
    private static readonly Color32 EnemyColor  = new( 20,   20,   20,  255);
    private static readonly Color32 BulletColor = new(220,    0,    0,  255);
    private static readonly Color32 WallColor   = new( 60,   60,   60,  255);
    private static readonly Color32 DoorColor   = new(180,  130,   60,  255);

    public Texture2D OutputTexture => _tex;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        _tex             = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        _tex.filterMode  = FilterMode.Bilinear;
        _pixels          = new Color32[TexSize * TexSize];
        _ready           = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_tex != null) Destroy(_tex);
    }

    // =========================================================
    // SETUP
    // =========================================================

    public void Setup(Transform player, Transform camera, float worldRadius,
                      float playerDotSize, float enemyDotSize, float bulletDotSize)
    {
        _playerTransform = player;
        // Use the provided camera transform, fall back to Camera.main automatically
        _cameraTransform = camera != null ? camera : Camera.main?.transform;
        _worldRadius     = worldRadius;
        // Convert world-space diameter to pixel radius
        _playerRadius    = ToPixelRadius(playerDotSize);
        _enemyRadius     = ToPixelRadius(enemyDotSize);
        _bulletRadius    = ToPixelRadius(bulletDotSize);
    }

    // =========================================================
    // ENTITY REGISTRY
    // =========================================================

    public static void Register(EnemyController e)   => _enemies.Add(e);
    public static void Unregister(EnemyController e) => _enemies.Remove(e);
    public static void RegisterBullet(Shot s)        => _bullets.Add(s);
    public static void UnregisterBullet(Shot s)      => _bullets.Remove(s);
    public static void RegisterWall(MinimapWall w)   => _walls.Add(w);
    public static void UnregisterWall(MinimapWall w) => _walls.Remove(w);

    // =========================================================
    // RENDER
    // =========================================================

    private void LateUpdate()
    {
        if (!_ready || _playerTransform == null) return;

        // Lazy fallback — resolves Camera.main if the field was left unassigned
        if (_cameraTransform == null && Camera.main != null)
            _cameraTransform = Camera.main.transform;

        System.Array.Fill(_pixels, BgColor);

        float yaw = 0f;
        if (_cameraTransform != null)
        {
            Vector3 flat = _cameraTransform.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f)
                yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        // Walls → bullets → enemies → player: each layer paints over the previous
        foreach (var w in _walls)
            if (w != null) StampWall(w, yaw);

        foreach (var b in _bullets)
            if (b != null) Stamp(b.transform.position, yaw, BulletColor, _bulletRadius);

        foreach (var e in _enemies)
            if (e != null) Stamp(e.transform.position, yaw, EnemyColor, _enemyRadius);

        // Player is always the center dot
        StampAt(TexSize / 2, TexSize / 2, PlayerColor, _playerRadius);

        _tex.SetPixels32(_pixels);
        _tex.Apply(false);
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void Stamp(Vector3 worldPos, float cameraYaw, Color32 color, int radius)
    {
        Vector3 rel = worldPos - _playerTransform.position;

        // Rotate so camera forward = texture up (+Y)
        float rad = cameraYaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float rx  =  rel.x * cos - rel.z * sin;
        float rz  =  rel.x * sin + rel.z * cos;

        float half = TexSize * 0.5f;
        int   cx   = Mathf.RoundToInt( rx / _worldRadius * half) + TexSize / 2;
        int   cy   = Mathf.RoundToInt( rz / _worldRadius * half) + TexSize / 2;

        StampAt(cx, cy, color, radius);
    }

    private void StampAt(int cx, int cy, Color32 color, int radius)
    {
        int r2 = radius * radius;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > r2) continue;
            int px = cx + dx;
            int py = cy + dy;
            if ((uint)px >= TexSize || (uint)py >= TexSize) continue;
            _pixels[py * TexSize + px] = color;
        }
    }

    private int ToPixelRadius(float worldDiameter) =>
        Mathf.Max(1, Mathf.RoundToInt(worldDiameter / _worldRadius * (TexSize * 0.25f)));

    private void StampWall(MinimapWall wall, float cameraYaw)
    {
        float rad      = cameraYaw * Mathf.Deg2Rad;
        float cos      = Mathf.Cos(rad);
        float sin      = Mathf.Sin(rad);
        float half     = TexSize * 0.5f;
        Vector3 origin = _playerTransform.position;

        var tp = new Vector2Int[4];
        for (int i = 0; i < 4; i++)
        {
            float wx = wall.Corners[i].x - origin.x;
            float wz = wall.Corners[i].y - origin.z;   // Corners.y stores world Z
            float rx  =  wx * cos - wz * sin;
            float rz  =  wx * sin + wz * cos;
            tp[i] = new Vector2Int(
                Mathf.RoundToInt(rx / _worldRadius * half) + TexSize / 2,
                Mathf.RoundToInt(rz / _worldRadius * half) + TexSize / 2
            );
        }

        Color32 color = wall.isDoor ? DoorColor : WallColor;
        StampFilledQuad(tp[0], tp[1], tp[2], tp[3], color);
    }


    private void StampFilledQuad(Vector2Int p0, Vector2Int p1, Vector2Int p2, Vector2Int p3, Color32 color)
    {
        int minX = Mathf.Min(p0.x, Mathf.Min(p1.x, Mathf.Min(p2.x, p3.x)));
        int maxX = Mathf.Max(p0.x, Mathf.Max(p1.x, Mathf.Max(p2.x, p3.x)));
        int minY = Mathf.Min(p0.y, Mathf.Min(p1.y, Mathf.Min(p2.y, p3.y)));
        int maxY = Mathf.Max(p0.y, Mathf.Max(p1.y, Mathf.Max(p2.y, p3.y)));

        if (maxX - minX <= 2 && maxY - minY <= 2)
        {
            StampAt((p0.x + p1.x + p2.x + p3.x) / 4,
                    (p0.y + p1.y + p2.y + p3.y) / 4, color, 1);
            return;
        }

        minX = Mathf.Max(0, minX);
        maxX = Mathf.Min(TexSize - 1, maxX);
        minY = Mathf.Max(0, minY);
        maxY = Mathf.Min(TexSize - 1, maxY);

        for (int py = minY; py <= maxY; py++)
        for (int px = minX; px <= maxX; px++)
        {
            if (PointInTriangle(px, py, p0, p1, p2) || PointInTriangle(px, py, p0, p2, p3))
                _pixels[py * TexSize + px] = color;
        }
    }

    // Cross-product sign test; returns 0 on the edge (treated as inside)
    private static bool PointInTriangle(int px, int py, Vector2Int a, Vector2Int b, Vector2Int c)
    {
        int d1 = (px - b.x) * (a.y - b.y) - (a.x - b.x) * (py - b.y);
        int d2 = (px - c.x) * (b.y - c.y) - (b.x - c.x) * (py - c.y);
        int d3 = (px - a.x) * (c.y - a.y) - (c.x - a.x) * (py - a.y);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }
}
