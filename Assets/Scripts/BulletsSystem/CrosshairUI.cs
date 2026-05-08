using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    [Header("References")]
    public Image       crosshairImage;
    public Image       reloadArc;
    public CanvasGroup reloadArcGroup;   // CanvasGroup en el mismo GO que reloadArc

    [Header("Settings")]
    public float fadeSpeed      = 6f;
    public float arcCompleteSpeed = 8f; // velocidad a la que completa la vuelta al llegar a 1

    bool  _isReloading;
    float _reloadProgress;
    bool  _completing;   // true cuando la recarga terminó y el arc va hacia 1

    public void OnReload(OnReloadEvent e)
    {
        if (e.IsReloading)
        {
            // Recarga en progreso — mostrar arc
            _isReloading    = true;
            _completing     = false;
            _reloadProgress = e.Progress;
            reloadArc.enabled        = true;
            reloadArcGroup.alpha     = 1f;
        }
        else
        {
            // Recarga terminó — iniciar fase de completado
            _isReloading = false;
            _completing  = true;
        }
    }

    void OnEnable()
    {
        if (ShootController.Instance == null) return;

        // FIX: consultar estado real en lugar de depender del evento cacheado
        bool reloading = ShootController.Instance.IsReloading;
        float progress = ShootController.Instance.ReloadProgress; // 0-1

        if (reloading)
        {
            _isReloading         = true;
            _completing          = false;
            _reloadProgress      = progress;
            reloadArc.enabled    = true;
            reloadArcGroup.alpha = 1f;
            reloadArc.fillAmount = progress;
        }
        else if (_completing)
        {
            reloadArc.enabled    = true;
            reloadArcGroup.alpha = 1f;
        }
        else
        {
            reloadArc.fillAmount = 0f;
            reloadArc.enabled    = false;
        }
    }
    
    void Update()
    {
        UpdateCrosshairAlpha();
        UpdateReloadArc();
    }

    void UpdateCrosshairAlpha()
    {
        float targetAlpha    = _isReloading ? 0.3f : 1f;
        Color c              = crosshairImage.color;
        c.a                  = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
        crosshairImage.color = c;
    }

    void UpdateReloadArc()
    {
        if (_isReloading)
        {
            // Seguir el progreso real directamente — sin lerp para que sea exacto
            reloadArc.fillAmount = _reloadProgress;
            return;
        }

        if (_completing)
        {
            // Completar la vuelta hacia 1
            reloadArc.fillAmount = Mathf.MoveTowards(
                reloadArc.fillAmount, 1f,
                Time.deltaTime * arcCompleteSpeed
            );

            if (reloadArc.fillAmount >= 1f)
            {
                // Vuelta completa — hacer fade out del arc
                reloadArcGroup.alpha = Mathf.MoveTowards(
                    reloadArcGroup.alpha, 0f,
                    Time.deltaTime * fadeSpeed
                );

                if (reloadArcGroup.alpha <= 0f)
                {
                    reloadArc.fillAmount  = 0f;
                    reloadArc.enabled     = false;
                    _completing           = false;
                }
            }
            return;
        }

        // Estado idle — arc oculto
        reloadArc.enabled = false;
    }
}