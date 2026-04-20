using System.Collections;
using UnityEngine;

/// <summary>
/// Handles smoke trail fade when a bullet dies.
/// Pool-friendly: instead of detaching the trail, spawns a lightweight
/// ghost GameObject that copies the trail visually and fades it out,
/// while the original trail stays on the bullet ready for reuse.
/// </summary>
public class TrailFader : MonoBehaviour
{
    public static void Detach(TrailRenderer trail)
    {
        if (trail == null) return;

        // --- Spawn a ghost trail that handles the fade ---
        // We duplicate the trail onto a new GameObject so the original
        // stays on the bullet (ready for pool reuse) and the ghost fades out.
        GameObject ghost = new GameObject("TrailGhost");
        ghost.transform.SetPositionAndRotation(
            trail.transform.position,
            trail.transform.rotation
        );

        TrailRenderer ghostTrail = ghost.AddComponent<TrailRenderer>();
        CopyTrail(trail, ghostTrail);
        EnsureFadeGradient(ghostTrail);

        // Ghost fades and self-destructs
        TrailFader fader = ghost.AddComponent<TrailFader>();
        fader.StartCoroutine(fader.FadeOut(ghostTrail));

        // --- Reset the original trail on the bullet ---
        // Clear all existing points so it starts fresh on next use
        trail.Clear();
        trail.emitting = true; // ready to emit again when bullet reactivates
    }

    private IEnumerator FadeOut(TrailRenderer trail)
    {
        trail.emitting = false;
        yield return new WaitForSeconds(trail.time);
        Destroy(gameObject);
    }

    // =========================================================
    // HELPERS
    // =========================================================

    /// <summary>Copies all visual properties from source to destination TrailRenderer.</summary>
    private static void CopyTrail(TrailRenderer src, TrailRenderer dst)
    {
        dst.time               = src.time;
        dst.widthCurve         = src.widthCurve;
        dst.widthMultiplier    = src.widthMultiplier;
        dst.colorGradient      = src.colorGradient;
        dst.material           = src.material;
        dst.shadowCastingMode  = src.shadowCastingMode;
        dst.receiveShadows     = src.receiveShadows;
        dst.minVertexDistance  = src.minVertexDistance;
        dst.textureMode        = src.textureMode;
        dst.alignment          = src.alignment;
        dst.numCapVertices     = src.numCapVertices;
        dst.numCornerVertices  = src.numCornerVertices;

        // Bake current trail positions into the ghost
        Vector3[] positions = new Vector3[src.positionCount];
        src.GetPositions(positions);
        dst.AddPositions(positions);
    }

    private static void EnsureFadeGradient(TrailRenderer trail)
    {
        Gradient g = trail.colorGradient;
        GradientAlphaKey[] alphaKeys = g.alphaKeys;

        if (alphaKeys.Length > 0 && alphaKeys[alphaKeys.Length - 1].alpha < 0.01f)
            return;

        g.SetKeys(
            g.colorKeys,
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = g;
    }
}
