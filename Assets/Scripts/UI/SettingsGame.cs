using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SettingsGame : MonoBehaviour
{
    [Header("Ссылки на элементы UI в этой сцене")]
    public TMP_Dropdown fpsDropdown;
    public TMP_Dropdown qualityDropdown;
    public Toggle vSyncToggle;
    public Toggle fullScreenToggle;

    void OnEnable()
    {
        // Этот метод срабатывает каждый раз, когда меню паузы открывается (активируется)
        SyncUIWithCurrentSettings();
    }

    void SyncUIWithCurrentSettings()
    {
        // 1. Синхронизируем графику
        if (qualityDropdown != null)
        {
            int currentQuality = QualitySettings.GetQualityLevel();
            qualityDropdown.value = currentQuality;
            qualityDropdown.RefreshShownValue();
        }

        // 2. Синхронизируем FPS
        if (fpsDropdown != null)
        {
            int currentFPS = Application.targetFrameRate;
            int fpsIndex = 1; // По умолчанию 60

            if (currentFPS == 30) fpsIndex = 0;
            else if (currentFPS == 60) fpsIndex = 1;
            else if (currentFPS == -1) fpsIndex = 2; // Без ограничения

            fpsDropdown.value = fpsIndex;
            fpsDropdown.RefreshShownValue();
        }

        // 3. Синхронизируем VSync
        if (vSyncToggle != null)
        {
            vSyncToggle.isOn = (QualitySettings.vSyncCount > 0);
        }

        // 4. Синхронизируем полноэкранный режим
        if (fullScreenToggle != null)
        {
            fullScreenToggle.isOn = Screen.fullScreen;
        }
    }
}