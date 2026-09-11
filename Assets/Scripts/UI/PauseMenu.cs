using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Пауза по Escape: останавливает время, освобождает курсор, открывает меню.
public class PauseMenu : MonoBehaviour
{
    [Header("Панели")]
    public GameObject pausePanel;
    public GameObject settingsPanel;

    [Header("Кнопки")]
    public Button resumeButton;
    public Button settingsButton;
    public Button settingsBackButton;
    public Button menuButton;
    public Button quitButton;

    [Header("Ссылки")]
    public PlayerInteractor interactor;
    [Tooltip("Что выключать на время паузы: игрок, камера.")]
    public List<GameObject> objectsToFreeze = new List<GameObject>();
    

    void Bind(Button b, UnityEngine.Events.UnityAction a)
    {
        if (!b) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(a);
    }

    void Click() { if (AudioManager.I) AudioManager.I.Click(); }
    void Back() { if (AudioManager.I) AudioManager.I.Back(); }

    [Header("Сцены")]
    public string mainMenuScene = "MainMenu";

    public bool IsPaused { get; private set; }

    readonly List<MonoBehaviour> _disabled = new List<MonoBehaviour>();

    
    void Awake()
    {
        if (!interactor) interactor = FindFirstObjectByType<PlayerInteractor>();

        Bind(resumeButton,       Resume);
        Bind(settingsButton,     () => { Click(); ShowSettings(true); });
        Bind(settingsBackButton, () => { Back();  ShowSettings(false); });
        Bind(menuButton,         ToMainMenu);
        Bind(quitButton,         Quit);

        if (pausePanel) pausePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
    }   
    

    void Update()
    {
        if (!EscapePressed()) return;

        // Панель проверки главнее: Escape сначала закрывает её.
        if (!IsPaused && interactor &&
            (interactor.PanelOpen || interactor.LastCloseFrame == Time.frameCount)) return;

        if (IsPaused)
        {
            if (settingsPanel && settingsPanel.activeSelf) ShowSettings(false);
            else Resume();
        }
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;

        IsPaused = true;
        Time.timeScale = 0f;
        Freeze(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pausePanel) pausePanel.SetActive(true);
        Back();
    }

    public void Resume()
    {
        if (!IsPaused) return;

        Click();
        IsPaused = false;
        Time.timeScale = 1f;
        Freeze(false);

        if (pausePanel) pausePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ShowSettings(bool on)
    {
        if (settingsPanel) settingsPanel.SetActive(on);
        if (pausePanel) pausePanel.SetActive(!on);
    }

    void ToMainMenu()
    {
        Back();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    void Quit()
    {
        Back();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Freeze(bool on)
    {
        if (on)
        {
            _disabled.Clear();
            foreach (var go in objectsToFreeze)
            {
                if (!go) continue;
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!mb || mb == this || !mb.enabled) continue;
                    if (mb is PauseMenu) continue;
                    mb.enabled = false;
                    _disabled.Add(mb);
                }
            }
        }
        else
        {
            foreach (var mb in _disabled) if (mb) mb.enabled = true;
            _disabled.Clear();
        }
    }

    bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
