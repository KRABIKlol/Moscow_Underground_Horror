using UnityEngine;
using UnityEngine.SceneManagement; 

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("Demo");
    }

    public void QuitGame()
    {
        Debug.Log("Игра закрыта!");
        Application.Quit();
    }
}
