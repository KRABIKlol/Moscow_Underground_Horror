using UnityEngine;
using UnityEngine.UI;

/// Настройки звука и экрана. Значения живут в AudioManager и PlayerPrefs.
public class SettingsPanel : MonoBehaviour
{
    [Header("Громкость")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    public Text masterValue;
    public Text musicValue;
    public Text sfxValue;

    [Header("Экран")]
    public Toggle fullscreenToggle;

    bool _ready;

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
                Label(masterValue, v);
            });
            Label(masterValue, masterSlider.value);
        }

        if (musicSlider)
        {
            musicSlider.SetValueWithoutNotify(am ? am.musicVolume : 0.4f);
            musicSlider.onValueChanged.RemoveAllListeners();
            musicSlider.onValueChanged.AddListener(v =>
            {
                if (AudioManager.I) AudioManager.I.SetMusic(v);
                Label(musicValue, v);
            });
            Label(musicValue, musicSlider.value);
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
                    if (_ready) AudioManager.I.Hover();   // мгновенный пример громкости
                }
                Label(sfxValue, v);
            });
            Label(sfxValue, sfxSlider.value);
        }

        if (fullscreenToggle)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            fullscreenToggle.onValueChanged.AddListener(v => Screen.fullScreen = v);
        }

        _ready = true;
    }

    void OnDisable() => _ready = false;

    static void Label(Text t, float v)
    {
        if (t) t.text = Mathf.RoundToInt(v * 100f) + " %";
    }
}
