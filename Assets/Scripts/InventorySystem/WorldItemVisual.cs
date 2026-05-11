using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class WorldItemVisual : MonoBehaviour
{
    [Header("Levitation")]
    [SerializeField] float bobHeight    = 0.15f;
    [SerializeField] float bobSpeed     = 1.8f;
    [SerializeField] float groundOffset = 0.6f;

    [Header("Drop Physics")]
    [SerializeField] bool      useDropPhysics = false;
    [SerializeField] float     dropUpForce    = 3f;
    [SerializeField] float     dropSpinTorque = 2f;
    [SerializeField] LayerMask groundLayers   = ~0;
    [SerializeField] Collider  physicsCollider;

    [Header("Billboard")]
    [SerializeField] bool faceCamera = true;

    [Header("Size")]
    [SerializeField] float fitPadding = 0.85f;

    [Header("Item Colors")]
    public Color weaponColor  = new Color(1f,  0.85f, 0f);
    public Color ammoColor    = new Color(0.7f, 0.7f, 0.7f);
    public Color healthColor  = new Color(0.3f, 0.9f, 0.3f);
    public Color shieldColor  = new Color(0.3f, 0.6f, 1f);
    public Color defaultColor = Color.white;

    SpriteRenderer _sr;
    Transform      _spriteTransform;
    Vector3        _originWorldPos;   // bob origin stored in world space
    Transform      _cam;
    float          _bobOffset;
    Transform      _labelRoot;

    Rigidbody _rb;
    bool      _landed = false;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (physicsCollider == null)
        {
            foreach (var col in GetComponents<Collider>())
            {
                if (!col.isTrigger) { physicsCollider = col; break; }
            }
        }

        if (physicsCollider != null)
            physicsCollider.enabled = false;

        _bobOffset = Random.Range(0f, Mathf.PI * 2f);
        _cam       = Camera.main?.transform;

        if (useDropPhysics)
        {
            if (physicsCollider != null)
                physicsCollider.enabled = true;

            _rb.isKinematic            = false;
            _rb.useGravity             = true;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.interpolation          = RigidbodyInterpolation.Interpolate;
            _rb.AddForce(Vector3.up * dropUpForce, ForceMode.Impulse);
            _rb.AddTorque(Random.insideUnitSphere * dropSpinTorque, ForceMode.Impulse);
        }
        else
        {
            _rb.isKinematic = true;
            _rb.useGravity  = false;
            _originWorldPos = transform.position + Vector3.up * groundOffset;
        }
    }

    // =========================================================
    // SETUP
    // =========================================================

    public void Setup(itemSO item, int amount)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _sr              = null;
        _spriteTransform = null;
        _labelRoot       = null;

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(transform, false);
        _spriteTransform = spriteGo.transform;
        _sr              = spriteGo.AddComponent<SpriteRenderer>();

        _sr.sprite = item.icon;
        _sr.color  = Color.white;
        FitSprite();
        BuildLabel(item, amount);
    }

    // =========================================================
    // LANDING
    // =========================================================

    void OnCollisionEnter(Collision col)
    {
        if (!useDropPhysics || _landed) return;
        if ((groundLayers.value & (1 << col.gameObject.layer)) == 0) return;

        foreach (var contact in col.contacts)
            if (contact.thisCollider.isTrigger) return;

        _landed = true;

        _rb.isKinematic            = true;
        _rb.useGravity             = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _rb.linearVelocity         = Vector3.zero;
        _rb.angularVelocity        = Vector3.zero;

        transform.rotation = Quaternion.identity;

        // Save world position BEFORE parenting so we can restore scale after.
        _originWorldPos = transform.position + Vector3.up * groundOffset;

        // Reparent so the item gets destroyed with the room,
        // but immediately force scale back to (1,1,1) in world space.
        // We set worldPositionStays: false to avoid Unity touching localScale,
        // then manually restore position and lock scale to Vector3.one.
        Vector3 worldScale = transform.lossyScale; // should be (1,1,1) but capture it just in case

        transform.SetParent(col.transform, worldPositionStays: false);

        // Counteract any scale inheritance so the item never stretches.
        // lossyScale = parent.lossyScale * localScale  →  localScale = worldScale / parentLossyScale
        Vector3 ps = col.transform.lossyScale;
        transform.localScale = new Vector3(
            worldScale.x / ps.x,
            worldScale.y / ps.y,
            worldScale.z / ps.z
        );

        if (physicsCollider != null)
            physicsCollider.enabled = false;
    }

    // =========================================================
    // UPDATE — bob & billboard in world space
    // =========================================================

    void Update()
    {
        if (useDropPhysics && !_landed) return;

        float y = Mathf.Sin(Time.time * bobSpeed + _bobOffset) * bobHeight;

        // Move in world space — immune to whatever scale the parent has.
        transform.position = _originWorldPos + Vector3.up * y;

        if (faceCamera && _cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
    }

    // =========================================================
    // PUBLIC
    // =========================================================

    public void LaunchDrop(float upForce)
    {
        useDropPhysics = true;

        if (physicsCollider != null)
            physicsCollider.enabled = true;

        _rb.isKinematic            = false;
        _rb.useGravity             = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.interpolation          = RigidbodyInterpolation.Interpolate;
        _rb.AddForce(Vector3.up * upForce, ForceMode.Impulse);
        _rb.AddTorque(Random.insideUnitSphere * dropSpinTorque, ForceMode.Impulse);
    }

    // =========================================================
    // PRIVATE HELPERS
    // =========================================================

    Color ItemColor(itemSO item)
    {
        if (item is WeaponSO)                                           return weaponColor;
        if (item is AmmoSO)                                             return ammoColor;
        if (item is HealthSO h  && h.healthType  == HealthType.Health)  return healthColor;
        if (item is HealthSO h2 && h2.healthType == HealthType.Shield)  return shieldColor;
        return defaultColor;
    }

    float GetColliderSize()
    {
        var col = GetComponent<SphereCollider>();
        if (col == null) return 1f;
        Vector3 s = col.bounds.size;
        return Mathf.Min(s.x, s.y, s.z);
    }

    void FitSprite()
    {
        if (_sr == null || _sr.sprite == null) return;
        float targetSize = GetColliderSize() * fitPadding;
        float spriteMax  = Mathf.Max(_sr.sprite.bounds.size.x, _sr.sprite.bounds.size.y);
        _spriteTransform.localScale = Vector3.one * (targetSize / spriteMax);
    }

    void BuildLabel(itemSO item, int amount)
    {
        float colSize   = GetColliderSize();
        float textScale = colSize * 0.12f;
        float rowHeight = colSize * 0.18f;
        float topOffset = colSize * fitPadding * 0.5f + rowHeight * (amount > 1 ? 2f : 1f);

        var canvasGo = new GameObject("ItemLabel");
        canvasGo.transform.SetParent(transform, false);
        _labelRoot = canvasGo.transform;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode       = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Default";

        var nameGo = CreateWorldLabel(item.name, ItemColor(item), FontStyles.Bold);
        nameGo.transform.SetParent(canvasGo.transform, false);
        nameGo.transform.localPosition = new Vector3(0f, topOffset, 0f);
        nameGo.transform.localScale    = Vector3.one * textScale;

        if (amount > 1)
        {
            var amountGo = CreateWorldLabel($"x{amount}", Color.white, FontStyles.Normal);
            amountGo.transform.SetParent(canvasGo.transform, false);
            amountGo.transform.localPosition = new Vector3(0f, topOffset - rowHeight, 0f);
            amountGo.transform.localScale    = Vector3.one * textScale * 0.85f;
        }
    }

    GameObject CreateWorldLabel(string text, Color color, FontStyles style)
    {
        var go  = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt  = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 1f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = 1.6f;
        tmp.fontStyle          = style;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.color              = color;
        tmp.raycastTarget      = false;
        tmp.enableWordWrapping = false;

        tmp.fontMaterial = new Material(tmp.fontSharedMaterial);
        tmp.fontMaterial.EnableKeyword("UNDERLAY_ON");
        tmp.fontMaterial.SetColor("_UnderlayColor",    new Color(0f, 0f, 0f, 0.8f));
        tmp.fontMaterial.SetFloat("_UnderlayOffsetX",  0.5f);
        tmp.fontMaterial.SetFloat("_UnderlayOffsetY", -0.5f);
        tmp.fontMaterial.SetFloat("_UnderlaySoftness", 0.2f);

        return go;
    }
}
