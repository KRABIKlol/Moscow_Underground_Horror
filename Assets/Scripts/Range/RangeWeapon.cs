using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class RangeWeapon : MonoBehaviour
{
    
    public string displayName = "АК-74";

    public bool autoFit = true;
   
    public bool flipBarrel;
    
    public int orientationPreset = -1;

    
    public Vector3 holdPosition = Vector3.zero;
    
    public Vector3 holdRotation = Vector3.zero;
 
    public float holdScale = 1f;
       
    public bool automatic = true;
   
    public float fireRate = 600f;
   
    public float shotRange = 60f;
   
    public float spread = 0.25f;
    
    public bool firstShotAccurate = true;
    
    public float spreadRecovery = 0.5f;
    
    public LayerMask hitMask = ~0;


    public int magazineSize = 30;
    public float reloadTime = 2.2f;
   
    public bool infiniteReserve = true;

   
    public float recoilKick = 0.05f;
    public float recoilRise = 4f;
    public float recoilReturn = 10f;
        
   

    public AudioClip fireClip;
    public AudioClip tailClip;

    [Range(0f, 1f)] public float tailVolume = 0.5f;
    
    public AudioClip emptyClip;
    
    public AudioClip reloadClip;

    
    [Range(0f, 1f)] public float volume = 0.7f;
   
    [Range(0f, 0.3f)] public float pitchJitter = 0.06f;
   
    [Range(1, 8)] public int voices = 4;
   
    public bool useSfxVolume = true;

   
    public ParticleSystem muzzleFlash;


    public KeyCode reloadKeyLegacy = KeyCode.R;

    public bool Held { get; private set; }
    public int Ammo { get; private set; }
    public bool Reloading { get; private set; }
    public float ReloadProgress => reloadTime <= 0f ? 1f : Mathf.Clamp01(1f - _reloadLeft / reloadTime);

   
    public bool FireEnabled { get; set; }

    public event Action OnShot;

    Transform _origParent;
    Vector3 _origPos, _origScale, _origLossy;
    Quaternion _origRot;
    readonly List<Collider> _offColliders = new List<Collider>();

    Camera _cam;
    Transform _ignoreRoot;
    AudioSource[] _voices;
    AudioSource _tailVoice;
    int _voiceIndex;
    float _nextShot, _reloadLeft, _kick, _rise, _bloom;
    Quaternion _autoRot = Quaternion.identity;
    Vector3 _autoPos, _heldScale = Vector3.one;
    Bounds _bounds;
    bool _hasBounds;

    public float BarrelLength { get; private set; }

    void Awake()
    {
        Ammo = magazineSize;
    }

    void EnsureVoices()
    {
        if (_voices != null && _voices.Length == Mathf.Max(1, voices)) return;

        var host = transform.Find("WeaponAudio");
        if (!host)
        {
            var go = new GameObject("WeaponAudio");
            go.transform.SetParent(transform, false);
            host = go.transform;
        }

        foreach (var old in host.GetComponents<AudioSource>()) Destroy(old);

        int count = Mathf.Max(1, voices);
        _voices = new AudioSource[count];
        for (int i = 0; i < count; i++) _voices[i] = NewVoice(host.gameObject);

        _tailVoice = NewVoice(host.gameObject);
    }

    static AudioSource NewVoice(GameObject host)
    {
        var src = host.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;   
        return src;
    }

    float Loudness(float own)
    {
        float sfx = useSfxVolume && AudioManager.I ? AudioManager.I.sfxVolume : 1f;
        return Mathf.Clamp01(own * sfx);
    }


    void PlayShotSound()
    {
        var clip = fireClip;
        if (!clip && !tailClip) return;

        EnsureVoices();

        if (clip)
        {
            _voiceIndex = (_voiceIndex + 1) % _voices.Length;
            var voice = _voices[_voiceIndex];
            voice.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            voice.PlayOneShot(clip, Loudness(volume));
        }

        if (tailClip && _tailVoice)
        {
            _tailVoice.pitch = 1f;
            _tailVoice.PlayOneShot(tailClip, Loudness(volume * tailVolume));
        }
    }


    public void Take(Camera cam, Transform ignoreRoot)
    {
        if (Held || !cam) return;

        _cam = cam;
        _ignoreRoot = ignoreRoot;

        _origParent = transform.parent;
        _origPos = transform.localPosition;
        _origRot = transform.localRotation;
        _origScale = transform.localScale;
        _origLossy = transform.lossyScale;

        _offColliders.Clear();
        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (!c.enabled) continue;
            c.enabled = false;
            _offColliders.Add(c);
        }

        transform.SetParent(cam.transform, false);

        Vector3 p = cam.transform.lossyScale;
        _heldScale = new Vector3(
            _origLossy.x / Mathf.Max(0.0001f, p.x),
            _origLossy.y / Mathf.Max(0.0001f, p.y),
            _origLossy.z / Mathf.Max(0.0001f, p.z));
        transform.localScale = _heldScale * Mathf.Max(0.01f, holdScale);

        _hasBounds = TryLocalBounds(out _bounds);  
        ComputeAutoFit();
        transform.localPosition = HoldPos;
        transform.localRotation = HoldRot;

        Held = true;
        _bloom = 0f;
        Reloading = false;
        _reloadLeft = 0f;
        _kick = 0f;
        _rise = 0f;
        Ammo = magazineSize;
    }

    public void PutBack()
    {
        if (!Held) return;

        Held = false;
        FireEnabled = false;
        Reloading = false;
        _reloadLeft = 0f;

        transform.SetParent(_origParent, false);
        transform.localPosition = _origPos;
        transform.localRotation = _origRot;
        transform.localScale = _origScale;

        foreach (var c in _offColliders)
            if (c) c.enabled = true;
        _offColliders.Clear();

        _cam = null;
        _ignoreRoot = null;
    }

    public void Refill()
    {
        Ammo = magazineSize;
        Reloading = false;
        _reloadLeft = 0f;
    }



    void Update()
    {
        if (!Held) return;

      
        _kick = Mathf.Lerp(_kick, 0f, Time.deltaTime * recoilReturn);
        _rise = Mathf.Lerp(_rise, 0f, Time.deltaTime * recoilReturn);
        _bloom = Mathf.MoveTowards(_bloom, 0f, Time.deltaTime / Mathf.Max(0.05f, spreadRecovery));

       
        transform.localScale = _heldScale * Mathf.Max(0.01f, holdScale);
        ComputeAutoFit();
        transform.localPosition = HoldPos - Vector3.forward * _kick;
        transform.localRotation = Quaternion.Euler(-_rise, 0f, 0f) * HoldRot;

        if (Reloading)
        {
            _reloadLeft -= Time.deltaTime;
            if (_reloadLeft <= 0f)
            {
                Reloading = false;
                Ammo = magazineSize;
            }
            return;
        }

        if (!FireEnabled) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;  
        if (ReloadPressed() && Ammo < magazineSize)
        {
            StartReload();
            return;
        }

        bool wantShoot = automatic ? FireHeld() : FirePressed();
        if (wantShoot && Time.time >= _nextShot) Shoot();
    }

    public void StartReload()
    {
        if (Reloading || !Held) return;
        if (!infiniteReserve && Ammo >= magazineSize) return;

        Reloading = true;
        _reloadLeft = reloadTime;
        Play(reloadClip);
    }

    void Shoot()
    {
        if (Ammo <= 0)
        {
            Play(emptyClip);
            _nextShot = Time.time + 0.3f;
            return;
        }

        Ammo--;
        _nextShot = Time.time + 60f / Mathf.Max(1f, fireRate);

        PlayShotSound();
        if (muzzleFlash) muzzleFlash.Play();

        _kick = recoilKick;
        _rise = recoilRise;

        OnShot?.Invoke();
        CastShot();
    }


    public Quaternion HoldRot => Quaternion.Euler(holdRotation) * _autoRot;
    public Vector3 HoldPos => _autoPos + holdPosition;

    [ContextMenu("Сбросить поправки")]
    public void ResetHoldOffsets()
    {
        holdPosition = Vector3.zero;
        holdRotation = Vector3.zero;
        holdScale = 1f;
        orientationPreset = -1;
        flipBarrel = false;
    }

   
    void ComputeAutoFit()
    {
        _autoRot = Quaternion.identity;
        _autoPos = Vector3.zero;
        BarrelLength = 0f;

        if (!autoFit || !_hasBounds) return;

        Bounds b = _bounds;
        Vector3 size = b.size;
        int lo = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
        int sh = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);
        if (sh == lo) sh = (lo + 1) % 3;
        int mid = 3 - lo - sh;

        if (orientationPreset >= 0)
        {
            _autoRot = PresetRotation(orientationPreset);
        }
        else
        {
            Vector3 barrel = AxisVec(lo) * (flipBarrel ? -1f : 1f);
            Vector3 up = AxisVec(mid);
            _autoRot = Quaternion.Inverse(Quaternion.LookRotation(barrel, up));
        }

        Vector3 scale = transform.localScale;
        BarrelLength = size[lo] * Mathf.Abs(scale[lo]);

        float z = Mathf.Clamp(0.26f + BarrelLength * 0.20f, 0.3f, 0.95f);
        Vector3 desiredCenter = new Vector3(0.20f, -0.16f, z);

        _autoPos = desiredCenter - _autoRot * Vector3.Scale(b.center, scale);
    }

    static Vector3 AxisVec(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

    public const int PresetCount = 24;

    public static Quaternion PresetRotation(int index)
    {
        index = ((index % PresetCount) + PresetCount) % PresetCount;

        Vector3[] dirs =
        {
            Vector3.forward, Vector3.back,
            Vector3.right,   Vector3.left,
            Vector3.up,      Vector3.down,
        };

        Vector3 barrel = dirs[index / 4];
        Vector3 seed = Mathf.Abs(barrel.y) > 0.9f ? Vector3.forward : Vector3.up;
        Vector3 up = Quaternion.AngleAxis(90f * (index % 4), barrel) * seed;

        return Quaternion.Inverse(Quaternion.LookRotation(barrel, up));
    }

    bool TryLocalBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;

        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            if (mf && mf.sharedMesh) Add(mf.transform, mf.sharedMesh.bounds, ref bounds, ref any);

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr && smr.sharedMesh) Add(smr.transform, smr.sharedMesh.bounds, ref bounds, ref any);

        return any;
    }

    void Add(Transform t, Bounds local, ref Bounds acc, ref bool any)
    {
        Vector3 c = local.center, e = local.extents;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);

            Vector3 p = transform.InverseTransformPoint(t.TransformPoint(corner));

            if (!any) { acc = new Bounds(p, Vector3.zero); any = true; }
            else acc.Encapsulate(p);
        }
    }

    void CastShot()
    {
        if (!_cam) return;

      
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 dir = ray.direction;

       
        float aimSpread = spread * (firstShotAccurate ? _bloom : 1f);
        _bloom = Mathf.Clamp01(_bloom + 0.34f);

        if (aimSpread > 0f)
        {
            dir = Quaternion.AngleAxis(UnityEngine.Random.Range(-aimSpread, aimSpread), _cam.transform.up) *
                  Quaternion.AngleAxis(UnityEngine.Random.Range(-aimSpread, aimSpread), _cam.transform.right) * dir;
        }

      

        var hits = Physics.RaycastAll(ray.origin, dir, shotRange, hitMask,
                                      QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (!h.collider) continue;
            if (_ignoreRoot && h.transform.IsChildOf(_ignoreRoot)) continue;   
            if (h.transform.IsChildOf(transform)) continue;                    

            var target = h.collider.GetComponentInParent<ShootingTarget>();
            if (target) target.ReportHit(h);

            ImpactMarks.Spawn(h);
            return;
        }
    }

    void Play(AudioClip clip)
    {
        if (!clip) return;

        EnsureVoices();
        _voiceIndex = (_voiceIndex + 1) % _voices.Length;

        var voice = _voices[_voiceIndex];
        voice.pitch = 1f;
        voice.PlayOneShot(clip, Loudness(volume));
    }



    bool FireHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    bool FirePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    bool ReloadPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(reloadKeyLegacy);
#endif
    }
}
