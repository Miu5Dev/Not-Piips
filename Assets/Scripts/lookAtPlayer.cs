using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    [Header("Target")]
        
    public LayerMask TargetLayer;
    [SerializeField] private Transform target;

    [Header("Settings")]
    [SerializeField] private bool lockXAxis = false;
    [SerializeField] private float rotationSpeed = 0f;

    [Header("Rotation Offset")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    [Header("Hit Reaction")]
    [SerializeField] private float recoilAngle = 50f;
    [SerializeField] private float recoilSpeed = 18f;
    [SerializeField] private float recoveryDelay = 0.35f;
    [SerializeField] private float recoverySpeed = 1.5f;

    private enum State { Tracking, Recoil, Recovering }
    private State _state = State.Tracking;
    private Quaternion _recoilTarget;
    private float _recoveryTimer;

    private void OnEnable()
    {
        EventBus.Subscribe<OnHealthChangeEvent>(OnHealthChange);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<OnHealthChangeEvent>(OnHealthChange);
    }

    private void OnHealthChange(OnHealthChangeEvent e)
    {
        if (e.hitObject != gameObject || !e.WeakPointHit) return;

        Vector3 randomAxis = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;

        _recoilTarget = transform.rotation * Quaternion.AngleAxis(recoilAngle, randomAxis);
        _state = State.Recoil;
    }

    private void Update()
    {
        switch (_state)
        {
            case State.Tracking:   DoTracking();   break;
            case State.Recoil:     DoRecoil();     break;
            case State.Recovering: DoRecovery();   break;
        }
    }

    private void DoTracking()
    {
        if (target == null) return;

        Vector3 direction = target.position - transform.position;
        if (lockXAxis) direction.y = 0f;
        if (direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(rotationOffset);

        if (rotationSpeed <= 0f)
            transform.rotation = targetRotation;
        else
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void DoRecoil()
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, _recoilTarget, recoilSpeed * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, _recoilTarget) < 1f)
        {
            _recoveryTimer = recoveryDelay;
            _state = State.Recovering;
        }
    }

    private void DoRecovery()
    {
        _recoveryTimer -= Time.deltaTime;
        if (_recoveryTimer > 0f) return;

        if (target == null) { _state = State.Tracking; return; }

        Vector3 direction = target.position - transform.position;
        if (lockXAxis) direction.y = 0f;
        if (direction == Vector3.zero) { _state = State.Tracking; return; }

        Quaternion trackingRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(rotationOffset);
        transform.rotation = Quaternion.Slerp(transform.rotation, trackingRotation, recoverySpeed * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, trackingRotation) < 2f)
            _state = State.Tracking;
    }
    
    public void onPingEventReceived(OnPingEvent e)
    {
        if ((TargetLayer.value & (1 << e.sender.layer)) != 0)
        {
            target = e.sender.transform;
        }
    }
}
