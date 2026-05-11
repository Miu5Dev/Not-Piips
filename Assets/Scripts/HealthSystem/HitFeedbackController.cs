using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escucha OnChangeHealthUIEvent y reproduce:
///   - Borde rojo en pantalla (vignette de daño)
///   - Camera shake con Cinemachine Impulse
/// Dispara cuando baja la vida O el escudo.
/// No gestiona la vida; solo reacciona visualmente al evento.
/// </summary>
public class HitFeedbackController : MonoBehaviour
{
    [Header("Damage Vignette")]
    [SerializeField] private Image damageVignetteImage;
    [SerializeField] private float vignetteFadeInDuration  = 0.05f;
    [SerializeField] private float vignetteHoldDuration    = 0.1f;
    [SerializeField] private float vignetteFadeOutDuration = 0.4f;
    [SerializeField] private AnimationCurve vignetteCurve  = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Camera Shake (Cinemachine Impulse)")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private float impulseForce = 1f;

    private int _lastHealth = int.MaxValue;
    private int _lastShield = int.MaxValue;
    private Coroutine _vignetteCoroutine;

    // ------------------------------------------------------------------ //
    //  Suscripción al evento                                               //
    // ------------------------------------------------------------------ //

    private void OnEnable()
    {
        EventBus.Subscribe<OnChangeHealthUIEvent>(OnHealthChange);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<OnChangeHealthUIEvent>(OnHealthChange);
    }

    // ------------------------------------------------------------------ //
    //  Handler principal                                                   //
    // ------------------------------------------------------------------ //

    private void OnHealthChange(OnChangeHealthUIEvent e)
    {
        if (_lastHealth == int.MaxValue || _lastShield == int.MaxValue)
        {
            _lastHealth = e.newHealth;
            _lastShield = e.newShield;
            return;
        }

        bool tookDamage = e.newHealth < _lastHealth || e.newShield < _lastShield;

        _lastHealth = e.newHealth;
        _lastShield = e.newShield;

        if (!tookDamage) return;

        TriggerCameraShake();
        TriggerDamageVignette();
    }

    // ------------------------------------------------------------------ //
    //  Camera shake                                                        //
    // ------------------------------------------------------------------ //

    private void TriggerCameraShake()
    {
        if (impulseSource == null)
        {
            Debug.LogWarning("[HitFeedbackController] CinemachineImpulseSource no asignado.", this);
            return;
        }

        impulseSource.GenerateImpulse(impulseForce);
    }

    // ------------------------------------------------------------------ //
    //  Damage vignette                                                     //
    // ------------------------------------------------------------------ //

    private void TriggerDamageVignette()
    {
        if (damageVignetteImage == null)
        {
            Debug.LogWarning("[HitFeedbackController] damageVignetteImage no asignado.", this);
            return;
        }

        if (_vignetteCoroutine != null)
            StopCoroutine(_vignetteCoroutine);

        _vignetteCoroutine = StartCoroutine(VignetteRoutine());
    }

    private IEnumerator VignetteRoutine()
    {
        yield return FadeVignette(0f, 1f, vignetteFadeInDuration);
        yield return new WaitForSeconds(vignetteHoldDuration);
        yield return FadeVignette(1f, 0f, vignetteFadeOutDuration);
    }

    private IEnumerator FadeVignette(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetVignetteAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(from, to, vignetteCurve.Evaluate(t));
            SetVignetteAlpha(alpha);
            yield return null;
        }

        SetVignetteAlpha(to);
    }

    private void SetVignetteAlpha(float alpha)
    {
        Color c = damageVignetteImage.color;
        c.a = alpha;
        damageVignetteImage.color = c;
    }
}