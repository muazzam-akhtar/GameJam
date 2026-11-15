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

    /// <summary>PLAY: single player - the Keeper against guard Reapers.</summary>
    public void PlaySolo()
    {
        GameMode.Solo = true;
        LoadSceneWithDelay();
    }

    /// <summary>VERSUS: two players, split screen.</summary>
    public void PlayVersus()
    {
        GameMode.Solo = false;
        LoadSceneWithDelay();
    }

    private IEnumerator WaitAndLoad(int sceneIndex)
    {
        // Wait for 1 second
        yield return new WaitForSeconds(1f);

        // Load the target scene
        SceneManager.LoadScene(1);
    }
}
