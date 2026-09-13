using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SettingsGame : MonoBehaviour
{    
    public TMP_Dropdown fpsDropdown;
    public TMP_Dropdown qualityDropdown;
    public Toggle vSyncToggle;
    public Toggle fullScreenToggle;

    void OnEnable()
    {        
        SyncUIWithCurrentSettings();
    }

    void SyncUIWithCurrentSettings()
    {
        if (qualityDropdown != null)
        {
            int currentQuality = QualitySettings.GetQualityLevel();
            qualityDropdown.value = currentQuality;
            qualityDropdown.RefreshShownValue();
        }

        if (fpsDropdown != null)
        {
            int currentFPS = Application.targetFrameRate;
            int fpsIndex = 1; 
            if (currentFPS == 30) fpsIndex = 0;
            else if (currentFPS == 60) fpsIndex = 1;
            else if (currentFPS == -1) fpsIndex = 2; 
            fpsDropdown.value = fpsIndex;
            fpsDropdown.RefreshShownValue();
        }        
        if (vSyncToggle != null)
        {
            vSyncToggle.isOn = (QualitySettings.vSyncCount > 0);
        }        
        if (fullScreenToggle != null)
        {
            fullScreenToggle.isOn = Screen.fullScreen;
        }
    }
}