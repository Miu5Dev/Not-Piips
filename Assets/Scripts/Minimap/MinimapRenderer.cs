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
    private static readonly Color32 PlayerColor = new( 0, 220,   0,  255);
    private static readonly Color32 EnemyColor  = new(20,  20,  20,  255);
    private static readonly Color32 BulletColor = new(220,  0,   0,  255);

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

        float yaw = _cameraTransform != null ? _cameraTransform.eulerAngles.y : 0f;

        // Bullets → enemies → player: each layer paints over the previous
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
}
