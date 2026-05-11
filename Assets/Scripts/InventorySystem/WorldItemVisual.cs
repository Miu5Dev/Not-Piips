using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class WorldItemVisual : MonoBehaviour
{
    [Header("Levitation")]
    [SerializeField] float bobHeight = 0.15f;
    [SerializeField] float bobSpeed = 1.8f;
    [SerializeField] float groundOffset = 0.6f;

    [Header("Drop Physics")]
    [SerializeField] bool useDropPhysics = false;
    [SerializeField] float dropUpForce = 3f;
    [SerializeField] float dropSpinTorque = 2f;
    [SerializeField] LayerMask groundLayers = ~0;
    [SerializeField] Collider physicsCollider;

    [Header("Landing")]
    [Tooltip("Solo los drops de enemigos deben activarlo.")]
    [SerializeField] bool reparentToHitObjectOnLand = false;

    [Header("Billboard")]
    [SerializeField] bool faceCamera = true;

    [Header("Size")]
    [SerializeField] float fitPadding = 0.85f;

    [Header("Item Colors")]
    public Color weaponColor = new Color(1f, 0.85f, 0f);
    public Color ammoColor = new Color(0.7f, 0.7f, 0.7f);
    public Color healthColor = new Color(0.3f, 0.9f, 0.3f);
    public Color shieldColor = new Color(0.3f, 0.6f, 1f);
    public Color defaultColor = Color.white;

    SpriteRenderer _sr;
    Transform _spriteTransform;
    Vector3 _originLocalPos;
    Vector3 _originWorldPos;
    Transform _cam;
    float _bobOffset;
    Transform _labelRoot;

    Rigidbody _rb;
    bool _landed = false;
    bool _useWorldSpaceBob = false;
    bool _reparentOnLand = false;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (physicsCollider == null)
        {
            foreach (var col in GetComponents<Collider>())
            {
                if (!col.isTrigger)
                {
                    physicsCollider = col;
                    break;
                }
            }
        }

        if (physicsCollider != null)
            physicsCollider.enabled = false;

        _bobOffset = Random.Range(0f, Mathf.PI * 2f);
        _cam = Camera.main?.transform;

        _reparentOnLand = reparentToHitObjectOnLand;

        if (useDropPhysics)
        {
            if (physicsCollider != null)
                physicsCollider.enabled = true;

            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.AddForce(Vector3.up * dropUpForce, ForceMode.Impulse);
            _rb.AddTorque(Random.insideUnitSphere * dropSpinTorque, ForceMode.Impulse);
        }
        else
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;

            _useWorldSpaceBob = false;
            _originLocalPos = transform.localPosition + Vector3.up * groundOffset;
        }
    }

    public void Setup(itemSO item, int amount)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _sr = null;
        _spriteTransform = null;
        _labelRoot = null;

        _landed = false;
        _useWorldSpaceBob = false;
        _reparentOnLand = reparentToHitObjectOnLand;

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(transform, false);
        _spriteTransform = spriteGo.transform;
        _sr = spriteGo.AddComponent<SpriteRenderer>();

        _sr.sprite = item.icon;
        _sr.color = Color.white;
        FitSprite();
        BuildLabel(item, amount);
    }

    void OnCollisionEnter(Collision col)
    {
        if (!useDropPhysics || _landed) return;
        if ((groundLayers.value & (1 << col.gameObject.layer)) == 0) return;

        foreach (var contact in col.contacts)
            if (contact.thisCollider.isTrigger) return;

        _landed = true;

        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;

        if (_reparentOnLand)
        {
            _useWorldSpaceBob = true;
            _originWorldPos = transform.position + Vector3.up * groundOffset;

            Vector3 worldScale = transform.lossyScale;
            transform.SetParent(col.transform, true);

            Vector3 ps = col.transform.lossyScale;
            transform.localScale = new Vector3(
                SafeDivide(worldScale.x, ps.x),
                SafeDivide(worldScale.y, ps.y),
                SafeDivide(worldScale.z, ps.z)
            );
        }
        else
        {
            _useWorldSpaceBob = false;
            _originLocalPos = transform.localPosition + Vector3.up * groundOffset;
        }

        if (physicsCollider != null)
            physicsCollider.enabled = false;
    }

    void Update()
    {
        if (useDropPhysics && !_landed) return;

        float y = Mathf.Sin(Time.time * bobSpeed + _bobOffset) * bobHeight;

        if (_useWorldSpaceBob)
            transform.position = _originWorldPos + Vector3.up * y;
        else
            transform.localPosition = _originLocalPos + Vector3.up * y;

        if (faceCamera && _cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
    }

    public void LaunchDrop(float upForce, bool reparentOnLand)
    {
        _reparentOnLand = reparentOnLand;
        useDropPhysics = true;
        _landed = false;

        if (physicsCollider != null)
            physicsCollider.enabled = true;

        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.AddForce(Vector3.up * upForce, ForceMode.Impulse);
        _rb.AddTorque(Random.insideUnitSphere * dropSpinTorque, ForceMode.Impulse);
    }

    public void LaunchDrop(float upForce)
    {
        LaunchDrop(upForce, false);
    }

    float SafeDivide(float a, float b)
    {
        return Mathf.Abs(b) < 0.0001f ? a : a / b;
    }

    Color ItemColor(itemSO item)
    {
        if (item is WeaponSO) return weaponColor;
        if (item is AmmoSO) return ammoColor;
        if (item is HealthSO h && h.healthType == HealthType.Health) return healthColor;
        if (item is HealthSO h2 && h2.healthType == HealthType.Shield) return shieldColor;
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
        float spriteMax = Mathf.Max(_sr.sprite.bounds.size.x, _sr.sprite.bounds.size.y);
        _spriteTransform.localScale = Vector3.one * (targetSize / spriteMax);
    }

    void BuildLabel(itemSO item, int amount)
    {
        float colSize = GetColliderSize();
        float textScale = colSize * 0.12f;
        float rowHeight = colSize * 0.18f;
        float topOffset = colSize * fitPadding * 0.5f + rowHeight * (amount > 1 ? 2f : 1f);

        var canvasGo = new GameObject("ItemLabel");
        canvasGo.transform.SetParent(transform, false);
        _labelRoot = canvasGo.transform;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Default";

        var nameGo = CreateWorldLabel(item.name, ItemColor(item), FontStyles.Bold);
        nameGo.transform.SetParent(canvasGo.transform, false);
        nameGo.transform.localPosition = new Vector3(0f, topOffset, 0f);
        nameGo.transform.localScale = Vector3.one * textScale;

        if (amount > 1)
        {
            var amountGo = CreateWorldLabel($"x{amount}", Color.white, FontStyles.Normal);
            amountGo.transform.SetParent(canvasGo.transform, false);
            amountGo.transform.localPosition = new Vector3(0f, topOffset - rowHeight, 0f);
            amountGo.transform.localScale = Vector3.one * textScale * 0.85f;
        }
    }

    GameObject CreateWorldLabel(string text, Color color, FontStyles style)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 1f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 1.6f;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;

        tmp.fontMaterial = new Material(tmp.fontSharedMaterial);
        tmp.fontMaterial.EnableKeyword("UNDERLAY_ON");
        tmp.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.8f));
        tmp.fontMaterial.SetFloat("_UnderlayOffsetX", 0.5f);
        tmp.fontMaterial.SetFloat("_UnderlayOffsetY", -0.5f);
        tmp.fontMaterial.SetFloat("_UnderlaySoftness", 0.2f);

        return go;
    }
}