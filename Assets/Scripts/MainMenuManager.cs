using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void QuitGame()
    {
        Application.Quit();
    }

    public void LoadSceneWithDelay()
    {
        StartCoroutine(WaitAndLoad(1));
    }

    private IEnumerator WaitAndLoad(int sceneIndex)
    {
        // Wait for 1 second
        yield return new WaitForSeconds(1f);

        // Load the target scene
        SceneManager.LoadScene(1);
    }
}
