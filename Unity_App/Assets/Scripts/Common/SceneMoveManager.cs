using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMoveManager : MonoBehaviour
{
    public void LoadHomeScene()
    {
        SceneManager.LoadScene("Home");
    }

    public void LoadLoginScene()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.Logout();
        }

        SceneManager.LoadScene("Login");
    }

    public void LoadRealScene()
    {
        SceneManager.LoadScene("Real");
    }

    public void LoadScenarioScene()
    {
        SceneManager.LoadScene("Scenario");
    }

    public void LoadTermScene()
    {
        SceneManager.LoadScene("Term");
    }

    public void QuitApp()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
