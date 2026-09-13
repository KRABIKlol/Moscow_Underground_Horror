using UnityEngine;


public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    public AudioSource musicSource;
    public AudioSource ambientSource;
    public AudioSource sfxSource;

    public AudioClip uiClick;
    public AudioClip uiBack;
    public AudioClip uiHover;

    public AudioClip stampApprove;
    public AudioClip stampReject;
    public AudioClip mistake;

    public AudioClip scanStart;
    public AudioClip scanClean;
    public AudioClip scanAlarm;

    public AudioClip doorOpen;
    public AudioClip doorClose;
    public AudioClip doorLocked;

    public AudioClip shiftStart;
    public AudioClip shiftEnd;

    public AudioClip music;
    public AudioClip ambient;

    [Range(0f, 1f)] public float master = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    const string KeyMaster = "sc_master";
    const string KeyMusic  = "sc_music";
    const string KeySfx    = "sc_sfx";

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }

        I = this;
        transform.SetParent(null);        

        EnsureSources();
        LoadPrefs();
        ApplyVolumes();
    }

    void Start()
    {
        if (music && musicSource)
        {
            musicSource.clip = music;
            musicSource.loop = true;
            musicSource.Play();
        }

        if (ambient && ambientSource)
        {
            ambientSource.clip = ambient;
            ambientSource.loop = true;
            ambientSource.Play();
        }
    }

    void EnsureSources()
    {
        if (!musicSource)   musicSource   = NewSource("Music",   true);
        if (!ambientSource) ambientSource = NewSource("Ambient", true);
        if (!sfxSource)     sfxSource     = NewSource("Sfx",     false);
    }

    AudioSource NewSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.loop = loop;
        src.playOnAwake = false;
        src.spatialBlend = 0f;      
        return src;
    }


    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (!clip || !sfxSource) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * sfxVolume);
    }

    public void Click()  => PlaySfx(uiClick);
    public void Back()   => PlaySfx(uiBack);
    public void Hover()  => PlaySfx(uiHover, 0.6f);


    public void SetMaster(float v) { master = Mathf.Clamp01(v); ApplyVolumes(); SavePrefs(); }
    public void SetMusic(float v)  { musicVolume = Mathf.Clamp01(v); ApplyVolumes(); SavePrefs(); }
    public void SetSfx(float v)    { sfxVolume = Mathf.Clamp01(v); SavePrefs(); }

    void ApplyVolumes()
    {
        AudioListener.volume = master;
        if (musicSource)   musicSource.volume = musicVolume;
        if (ambientSource) ambientSource.volume = musicVolume * 0.7f;
    }

    void LoadPrefs()
    {
        master      = PlayerPrefs.GetFloat(KeyMaster, master);
        musicVolume = PlayerPrefs.GetFloat(KeyMusic,  musicVolume);
        sfxVolume   = PlayerPrefs.GetFloat(KeySfx,    sfxVolume);
    }

    void SavePrefs()
    {
        PlayerPrefs.SetFloat(KeyMaster, master);
        PlayerPrefs.SetFloat(KeyMusic,  musicVolume);
        PlayerPrefs.SetFloat(KeySfx,    sfxVolume);
        PlayerPrefs.Save();
    }
    public void PauseMusicAndAmbient()
    {
        if (musicSource) musicSource.Pause();
        if (ambientSource) ambientSource.Pause();
    }

    public void ResumeMusicAndAmbient()
    {
        if (musicSource) musicSource.UnPause();
        if (ambientSource) ambientSource.UnPause();
    }
}

