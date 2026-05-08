using System.Collections;
using TMPro;
using UnityEngine;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private TMP_Text popupText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Config")]
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeDuration    = 0.25f;

    Coroutine _current;

    void Awake()
    {
        Instance = this;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void Show(string message)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(PopupRoutine(message));
    }

    IEnumerator PopupRoutine(string message)
    {
        popupText.text = message;

        // Fade in
        yield return Fade(0f, 1f, fadeDuration);

        // Hold
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        yield return Fade(1f, 0f, fadeDuration);

        _current = null;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}