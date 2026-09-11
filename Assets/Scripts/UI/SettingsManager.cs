using UnityEngine;
using TMPro; 
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    
    public TMP_Dropdown fpsDropdown;
    public TMP_Dropdown qualityDropdown;
    public Toggle vSyncT;
    public Toggle fullS;
    void Start()
    {
       
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            int defaultQualityIndex = 2;
            int defaultFPS = 60;
            QualitySettings.SetQualityLevel(defaultQualityIndex);
            fpsDropdown.value = 1;
            qualityDropdown.value = defaultQualityIndex;
            Application.targetFrameRate = defaultFPS;
            QualitySettings.vSyncCount = 1;
            vSyncT.isOn = false;
            Screen.fullScreen = true;
            fullS.isOn = true;
        }
        

    }

    public void SetVSync(bool isEnabled)
    {
        QualitySettings.vSyncCount = isEnabled ? 1 : 0;
        
    }
    public void SetQuality()
    {
        int qualityIndex = qualityDropdown.value; // Получаем индекс выбранного пункта (0, 1, 2...)
        int targetValue = 1;
        switch (qualityIndex)
        {
            case 0: targetValue = 0; break;
            case 1: targetValue = 1; break;
            case 2: targetValue = 2; break;
        }
        QualitySettings.SetQualityLevel(targetValue); // Применяем уровень графики в Unity
        Debug.Log("Установлен уровень графики (индекс): " + qualityIndex);
    }

    public void SetFullScreen(bool isFullScreen)
    {
        Screen.fullScreen = isFullScreen;
        
    }

    public void SetLockFPS()
    {
        if (fpsDropdown == null)
        {
            return;
        }

        int selectedIndex = fpsDropdown.value;        
        int targetFPS = 60;

        switch (selectedIndex)
        {
            case 0: targetFPS = 30; break;
            case 1: targetFPS = 60; break;            
            case 2: targetFPS = -1; break;
        }

        Application.targetFrameRate = targetFPS;
        Debug.Log("Лимит FPS установлен на: " + targetFPS);
    }
}