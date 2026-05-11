using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Attach to the panel GameObject on the HUD Canvas (the one that wraps the TMP_Text).
/// Requires a CanvasGroup on the SAME GameObject as this script.
///
/// Display format (per-wave, counts up from 0):
///   Enemies:    0/5  →  1/5  →  5/5   (resets each new wave)
///   Wave:       1/2  →  2/2
///
/// Fades in when a room with enemies starts, fades out when cleared or no enemies.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class RoomHUDController : MonoBehaviour
{
    [SerializeField] private TMP_Text roomInfoText;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration  = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private CanvasGroup  _canvasGroup;
    private Coroutine    _fadeCoroutine;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        _canvasGroup       = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        // Keep the GameObject always active — visibility is handled by alpha,
        // not SetActive, so coroutines keep running.
        _canvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<OnRoomStateChangedEvent>(OnRoomStateChanged);
        EventBus.Subscribe<OnRoomClearedEvent>(OnRoomCleared);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<OnRoomStateChangedEvent>(OnRoomStateChanged);
        EventBus.Unsubscribe<OnRoomClearedEvent>(OnRoomCleared);
    }

    // =========================================================
    // EVENT HANDLERS
    // =========================================================

    private void OnRoomStateChanged(OnRoomStateChangedEvent e)
    {
        if (e.EnemiesPerWave == 0)
        {
            FadeTo(0f, fadeOutDuration);
            return;
        }

        string enemiesLine = $"Enemies:\t{e.EnemiesKilledThisWave}/{e.EnemiesPerWave}";
        string waveLine    = e.TotalWaves == 0
            ? $"Wave:\t{e.CurrentWave}/\u221e"
            : $"Wave:\t{e.CurrentWave}/{e.TotalWaves}";

        roomInfoText.text = $"{enemiesLine}\n{waveLine}";
        FadeTo(1f, fadeInDuration);
    }

    private void OnRoomCleared(OnRoomClearedEvent e) => FadeTo(0f, fadeOutDuration);

    // =========================================================
    // FADE
    // =========================================================

    private void FadeTo(float targetAlpha, float duration)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeCoroutine(targetAlpha, duration));
    }

    private IEnumerator FadeCoroutine(float targetAlpha, float duration)
    {
        float startAlpha = _canvasGroup.alpha;
        float elapsed    = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha          = targetAlpha;
        _canvasGroup.blocksRaycasts = targetAlpha > 0f;
        _fadeCoroutine              = null;
    }
}
