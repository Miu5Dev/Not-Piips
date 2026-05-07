using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    [Header("References")]
    public Image crosshairImage;
    public Image reloadArc;

    [Header("Settings")]
    public float fadeSpeed = 6f;

    private bool _isReloading;
    private float _reloadProgress;
    
    public void OnReload(OnReloadEvent e)
    {
        _isReloading = e.IsReloading;
        _reloadProgress = e.Progress;
    }

    void Update()
    {
        float targetAlpha = _isReloading ? 0.3f : 1f;
        Color c = crosshairImage.color;
        c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
        crosshairImage.color = c;

        float targetFill = _isReloading ? _reloadProgress : 0f;
        reloadArc.fillAmount = Mathf.Lerp(reloadArc.fillAmount, targetFill, Time.deltaTime * fadeSpeed);
        reloadArc.enabled = reloadArc.fillAmount > 0.01f;
    }
}