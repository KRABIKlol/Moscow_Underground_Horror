using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Главное меню: Играть, Настройки, Выход.
public class MainMenu : MonoBehaviour
{
    [Header("Панели")]
    public GameObject rootPanel;
    public GameObject settingsPanel;

    [Header("Кнопки")]
    public Button playButton;
    public Button settingsButton;
    public Button settingsBackButton;
    public Button quitButton;

    [Header("Сцена игры")]
    public string gameScene = "Demo";

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Bind(playButton, () =>
        {
            Click();
            SceneManager.LoadScene(gameScene);
        });

        Bind(settingsButton,     () => { Click(); Show(false); });
        Bind(settingsBackButton, () => { Back();  Show(true); });

        Bind(quitButton, () =>
        {
            Back();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        Show(true);
    }

    void Show(bool menu)
    {
        if (rootPanel)
        {
            rootPanel.SetActive(menu);
        }
        else
        {
            // Общего контейнера нет - прячем сами кнопки.
            if (playButton)     playButton.gameObject.SetActive(menu);
            if (settingsButton) settingsButton.gameObject.SetActive(menu);
            if (quitButton)     quitButton.gameObject.SetActive(menu);
        }

        if (settingsPanel) settingsPanel.SetActive(!menu);
    }

    void Bind(Button b, UnityEngine.Events.UnityAction a)
    {
        if (!b) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(a);
    }

    void Click() { if (AudioManager.I) AudioManager.I.Click(); }
    void Back()  { if (AudioManager.I) AudioManager.I.Back(); }
}
