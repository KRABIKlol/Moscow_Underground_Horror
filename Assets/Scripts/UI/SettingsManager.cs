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
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    bool _ready;

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
        if (isEnabled)
        {
            QualitySettings.vSyncCount = 1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
        }
    }
    public void SetQuality()
    {
        int qualityIndex = qualityDropdown.value;
        QualitySettings.SetQualityLevel(qualityIndex);
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
    }
    

    void OnEnable()
    {
        var am = AudioManager.I;

        if (masterSlider)
        {
            masterSlider.SetValueWithoutNotify(am ? am.master : 1f);
            masterSlider.onValueChanged.RemoveAllListeners();
            masterSlider.onValueChanged.AddListener(v =>
            {
                if (AudioManager.I) AudioManager.I.SetMaster(v);                
            });            
        }

        if (musicSlider)
        {
            musicSlider.SetValueWithoutNotify(am ? am.musicVolume : 0.4f);
            musicSlider.onValueChanged.RemoveAllListeners();
            musicSlider.onValueChanged.AddListener(v =>
            {
                if (AudioManager.I) AudioManager.I.SetMusic(v);                
            });            
        }

        if (sfxSlider)
        {
            sfxSlider.SetValueWithoutNotify(am ? am.sfxVolume : 1f);
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.AddListener(v =>
            {
                if (AudioManager.I)
                {
                    AudioManager.I.SetSfx(v);
                    if (_ready) AudioManager.I.Hover();   
                }                
            });            
        }
        _ready = true;
    }

    void OnDisable() => _ready = false;
}