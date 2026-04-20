using UnityEngine;

public class AimTargetController : MonoBehaviour
{
    [Header("Sensitivity")]
    public float mouseSensitivity = 0.15f;
    public float gamepadSensitivity = 120f;

    [Header("Vertical Clamp")]
    public float minPitch = -40f;
    public float maxPitch = 70f;

    private float pitch = 0f;

    void OnEnable()  => EventBus.Subscribe<OnLookInputEvent>(OnLook);
    void OnDisable() => EventBus.Unsubscribe<OnLookInputEvent>(OnLook);

    private void OnLook(OnLookInputEvent e)
    {
        float sens = e.Source == LookInputSource.Gamepad
            ? gamepadSensitivity * Time.deltaTime
            : mouseSensitivity;

        pitch -= e.Delta.y * sens;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}