using System.Collections;
using UnityEngine;

public class TrailFader : MonoBehaviour
{
    public static void Detach(TrailRenderer trail, Transform bulletTransform)
    {
        if (trail == null) return;

        // Attach a fresh clean trail to the bullet BEFORE modifying the old trail,
        // so CopyConfig captures the original gradient rather than the fade-out version.
        GameObject freshGO = new GameObject("Trail");
        freshGO.transform.SetParent(bulletTransform, worldPositionStays: false);
        freshGO.transform.localPosition = Vector3.zero;
        freshGO.transform.localRotation = Quaternion.identity;

        TrailRenderer freshTrail = freshGO.AddComponent<TrailRenderer>();
        CopyConfig(trail, freshTrail);
        freshTrail.Clear();
        freshTrail.emitting = false;
        freshGO.SetActive(false);       // OnEnable will activate it

        // Unparent old trail from bullet so pool's SetActive(false) doesn't cascade to it
        trail.transform.SetParent(null, worldPositionStays: true);

        // Stop emitting — existing points will fade naturally over trail.time
        trail.emitting = false;
        EnsureFadeGradient(trail);

        // Self-destruct after fade
        var fader = trail.gameObject.AddComponent<TrailFader>();
        fader.StartCoroutine(fader.FadeAndDestroy(trail));
    }

    private IEnumerator FadeAndDestroy(TrailRenderer t)
    {
        yield return new WaitForSeconds(t.time);
        if (t != null) Destroy(t.gameObject);
    }

    private static void CopyConfig(TrailRenderer src, TrailRenderer dst)
    {
        dst.time              = src.time;
        dst.widthCurve        = src.widthCurve;
        dst.widthMultiplier   = src.widthMultiplier;
        dst.colorGradient     = src.colorGradient;
        dst.sharedMaterial    = src.sharedMaterial;
        dst.minVertexDistance = src.minVertexDistance;
        dst.textureMode       = src.textureMode;
        dst.alignment         = src.alignment;
        dst.numCapVertices    = src.numCapVertices;
        dst.numCornerVertices = src.numCornerVertices;
    }

    private static void EnsureFadeGradient(TrailRenderer trail)
    {
        Gradient g = trail.colorGradient;
        GradientAlphaKey[] keys = g.alphaKeys;
        if (keys.Length > 0 && keys[keys.Length - 1].alpha < 0.01f) return;

        g.SetKeys(g.colorKeys, new GradientAlphaKey[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(0f, 1f)
        });
        trail.colorGradient = g;
    }
}
