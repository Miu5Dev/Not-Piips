using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonHandler : MonoBehaviour
{
    public void ResetScene()
    {
        // Restaurar estado global antes de recargar
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // Destruir objetos persistentes (DontDestroyOnLoad)
        Scene dontDestroyScene = default;
        GameObject temp = new GameObject("_Temp");
        DontDestroyOnLoad(temp);
        dontDestroyScene = temp.scene;
        Destroy(temp);

        foreach (var obj in dontDestroyScene.GetRootGameObjects())
            Destroy(obj);

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}