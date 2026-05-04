using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MainCameraEnforcer : MonoBehaviour
{
    private void Awake()
    {
        // Detach from any parent — become a root GameObject
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        // Destroy any other Main Camera that isn't this one
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam == GetComponent<Camera>()) continue;
            if (cam.CompareTag("MainCamera"))
            {
                Debug.Log($"[MainCameraEnforcer] Destroying duplicate Main Camera: {cam.gameObject.name}");
                Destroy(cam.gameObject);
            }
        }

        // Promote self to Main Camera
        gameObject.tag = "MainCamera";
    }
}