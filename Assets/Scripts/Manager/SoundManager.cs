using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioDatabase audioDatabase;

    private float masterVolume = 1f;
    private float bgmVolume = 0.8f;
    private float sfxVolume = 0.9f;
    private float uiVolume = 1f;
    private bool muteInBackground = true;

    private bool isAppFocused = true;
    private GameManager boundGameManager;
    private GameStateManager boundGameStateManager;
    private BGMType? currentAutoBgmType;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
    }

    private Dictionary<BGMType, AudioClip> bgmCache = new Dictionary<BGMType, AudioClip>();
    private Dictionary<SFXType, AudioClip> sfxCache = new Dictionary<SFXType, AudioClip>();
    private Dictionary<UIType, AudioClip> uiCache = new Dictionary<UIType, AudioClip>();

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
                OnSettingsChanged();
        }

        RefreshContextBinding();
        StartCoroutine(RefreshAutoBGMNextFrame());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged;
        }

        UnbindContextManagers();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshContextBinding();
        StartCoroutine(RefreshAutoBGMNextFrame());
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        isAppFocused = hasFocus;
        UpdateVolumes();
    }

    private void OnApplicationPause(bool isPaused)
    {
        isAppFocused = !isPaused;
        UpdateVolumes();
    }

    private void InitializeAudioSources()
    {
        if (bgmSource == null)
        {
            GameObject bgmGO = new GameObject("BGMSource");
            bgmGO.transform.SetParent(transform);
            bgmSource = bgmGO.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.priority = 100;
        }

        if (sfxSource == null)
        {
            GameObject sfxGO = new GameObject("SFXSource");
            sfxGO.transform.SetParent(transform);
            sfxSource = sfxGO.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.priority = 128;
        }

        if (uiSource == null)
        {
            GameObject uiGO = new GameObject("UISource");
            uiGO.transform.SetParent(transform);
            uiSource = uiGO.AddComponent<AudioSource>();
            uiSource.loop = false;
            uiSource.priority = 64;
        }
    }

    private void OnSettingsChanged()
    {
        if (SettingsManager.Instance == null) return;

        var settings = SettingsManager.Instance.Settings;
        masterVolume = settings.masterVolume;
        bgmVolume = settings.bgmVolume;
        sfxVolume = settings.sfxVolume;
        uiVolume = settings.uiVolume;
        muteInBackground = settings.muteInBackground;

        UpdateVolumes();
    }

    private void RefreshContextBinding()
    {
        if (boundGameManager != GameManager.Instance)
        {
            if (boundGameManager != null)
            {
                boundGameManager.OnFlowStateChanged -= OnGameFlowStateChanged;
            }

            boundGameManager = GameManager.Instance;

            if (boundGameManager != null)
            {
                boundGameManager.OnFlowStateChanged += OnGameFlowStateChanged;
            }
        }

        if (boundGameStateManager != GameStateManager.Instance)
        {
            if (boundGameStateManager != null)
            {
                boundGameStateManager.OnStateChanged -= OnGameStateChanged;
            }

            boundGameStateManager = GameStateManager.Instance;

            if (boundGameStateManager != null)
            {
                boundGameStateManager.OnStateChanged += OnGameStateChanged;
            }
        }
    }

    private void UnbindContextManagers()
    {
        if (boundGameManager != null)
        {
            boundGameManager.OnFlowStateChanged -= OnGameFlowStateChanged;
            boundGameManager = null;
        }

        if (boundGameStateManager != null)
        {
            boundGameStateManager.OnStateChanged -= OnGameStateChanged;
            boundGameStateManager = null;
        }
    }

    private System.Collections.IEnumerator RefreshAutoBGMNextFrame()
    {
        yield return null;
        RefreshAutoBGM();
    }

    private void OnGameFlowStateChanged(GameFlowState newState)
    {
        RefreshAutoBGM();
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        RefreshAutoBGM();
    }

    private void RefreshAutoBGM()
    {
        RefreshContextBinding();

        if (!TryGetAutoBgmType(out var bgmType))
        {
            return;
        }

        var clip = GetBGMClip(bgmType);
        if (clip == null)
        {
            return;
        }

        if (bgmSource != null && bgmSource.clip == clip && bgmSource.isPlaying)
        {
            currentAutoBgmType = bgmType;
            return;
        }

        currentAutoBgmType = bgmType;
        PlayBGM(clip);
    }

    private bool TryGetAutoBgmType(out BGMType bgmType)
    {
        bgmType = BGMType.GameplayBGM;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == SceneName.TitleScene.ToString())
        {
            bgmType = BGMType.TitleBGM;
            return true;
        }

        if (sceneName != SceneName.GameScene.ToString())
        {
            return false;
        }

        if (boundGameStateManager != null)
        {
            switch (boundGameStateManager.CurrentState)
            {
                case GameStateManager.GameState.GameOver:
                    bgmType = BGMType.DefeatBGM;
                    return true;
                case GameStateManager.GameState.Prepare:
                case GameStateManager.GameState.GamePlay:
                    bgmType = BGMType.BattleBGM;
                    return true;
                case GameStateManager.GameState.Win:
                case GameStateManager.GameState.Reward:
                case GameStateManager.GameState.None:
                default:
                    break;
            }
        }

        if (boundGameManager != null)
        {
            switch (boundGameManager.CurrentFlowState)
            {
                case GameFlowState.Shop:
                    bgmType = BGMType.ShopBGM;
                    return true;
                case GameFlowState.WorkShop:
                    bgmType = BGMType.WorkshopBGM;
                    return true;
                case GameFlowState.Treasure:
                    bgmType = BGMType.TreasureBGM;
                    return true;
                case GameFlowState.Battle:
                    bgmType = BGMType.BattleBGM;
                    return true;
                case GameFlowState.Map:
                case GameFlowState.Event:
                case GameFlowState.None:
                default:
                    bgmType = BGMType.GameplayBGM;
                    return true;
            }
        }

        bgmType = BGMType.GameplayBGM;
        return true;
    }

    private void UpdateVolumes()
    {
        float effectiveMultiplier = (isAppFocused || !muteInBackground) ? 1f : 0f;

        if (bgmSource != null) // 볼륨 값을 0-1 범위로 정규화 (기존 값은 0-10)
            bgmSource.volume = (bgmVolume / 10f) * (masterVolume / 10f) * effectiveMultiplier;

        if (sfxSource != null) // 볼륨 값을 0-1 범위로 정규화
            sfxSource.volume = (sfxVolume / 10f) * (masterVolume / 10f) * effectiveMultiplier;

        if (uiSource != null) // 볼륨 값을 0-1 범위로 정규화
            uiSource.volume = (uiVolume / 10f) * (masterVolume / 10f) * effectiveMultiplier;
    }

    public void PlayBGM(BGMType bgmType)
    {
        AudioClip clip = GetBGMClip(bgmType);
        PlayBGM(clip);
    }

    public void PlayBGM(AudioClip clip)
    {
        if (bgmSource == null || clip == null) return;

        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    public void FadeBGM(float duration)
    {
        StartCoroutine(FadeBGMCoroutine(duration));
    }

    private System.Collections.IEnumerator FadeBGMCoroutine(float duration)
    {
        if (bgmSource == null) yield break;

        float startVolume = bgmSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.volume = startVolume;
    }

    public void PlaySFX(SFXType sfxType)
    {
        AudioClip clip = GetSFXClip(sfxType);
        PlaySFX(clip);
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, sfxSource.volume);
    }

    public void PlayUI(UIType uiType)
    {
        AudioClip clip = GetUIClip(uiType);
        PlayUI(clip);
    }

    public void PlayUI(AudioClip clip)
    {
        if (uiSource == null || clip == null) return;
        uiSource.PlayOneShot(clip, uiSource.volume);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetUIVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    private AudioClip GetBGMClip(BGMType bgmType)
    {
        if (audioDatabase != null)
        {
            var dbClip = audioDatabase.GetBGM(bgmType);
            if (dbClip != null) return dbClip;
        }

        if (bgmCache.TryGetValue(bgmType, out var cached))
            return cached;

        string path = $"Audio/BGM/{bgmType}";
        var clip = Resources.Load<AudioClip>(path);
        if (clip != null)
        {
            bgmCache[bgmType] = clip;
            return clip;
        }

        return null;
    }

    private AudioClip GetSFXClip(SFXType sfxType)
    {
        if (audioDatabase != null)
        {
            var dbClip = audioDatabase.GetSFX(sfxType);
            if (dbClip != null) return dbClip;
        }

        if (sfxCache.TryGetValue(sfxType, out var cached))
            return cached;

        string path = $"Audio/SFX/{sfxType}";
        var clip = Resources.Load<AudioClip>(path);
        if (clip != null)
        {
            sfxCache[sfxType] = clip;
            return clip;
        }

        return null;
    }

    private AudioClip GetUIClip(UIType uiType)
    {
        if (audioDatabase != null)
        {
            var dbClip = audioDatabase.GetUI(uiType);
            if (dbClip != null) return dbClip;
        }

        if (uiCache.TryGetValue(uiType, out var cached))
            return cached;

        string path = $"Audio/UI/{uiType}";
        var clip = Resources.Load<AudioClip>(path);
        if (clip != null)
        {
            uiCache[uiType] = clip;
            return clip;
        }

        return null;
    }

    public System.Collections.IEnumerator PreloadAsync(IEnumerable<BGMType> bgms, IEnumerable<SFXType> sfxs, IEnumerable<UIType> uis)
    {
        if (bgms != null)
        {
            foreach (var b in bgms)
            {
                if (audioDatabase != null && audioDatabase.GetBGM(b) != null) continue;
                if (bgmCache.ContainsKey(b)) continue;

                var req = Resources.LoadAsync<AudioClip>($"Audio/BGM/{b}");
                yield return req;
                var clip = req.asset as AudioClip;
                if (clip != null) bgmCache[b] = clip;
            }
        }

        if (sfxs != null)
        {
            foreach (var s in sfxs)
            {
                if (audioDatabase != null && audioDatabase.GetSFX(s) != null) continue;
                if (sfxCache.ContainsKey(s)) continue;

                var req = Resources.LoadAsync<AudioClip>($"Audio/SFX/{s}");
                yield return req;
                var clip = req.asset as AudioClip;
                if (clip != null) sfxCache[s] = clip;
            }
        }

        if (uis != null)
        {
            foreach (var u in uis)
            {
                if (audioDatabase != null && audioDatabase.GetUI(u) != null) continue;
                if (uiCache.ContainsKey(u)) continue;

                var req = Resources.LoadAsync<AudioClip>($"Audio/UI/{u}");
                yield return req;
                var clip = req.asset as AudioClip;
                if (clip != null) uiCache[u] = clip;
            }
        }
    }

    public void PreloadForTitleScene()
    {
        StartCoroutine(PreloadAsync(
            new[] { BGMType.TitleBGM },
            new[] { SFXType.Click, SFXType.Confirm, SFXType.Cancel, SFXType.Hover, SFXType.OpenPanel, SFXType.ClosePanel },
            new[] { UIType.ButtonClick, UIType.ButtonHover, UIType.ButtonPress, UIType.ToggleOn, UIType.ToggleOff, UIType.Open, UIType.Close, UIType.Back }
        ));
    }

    public float GetEffectiveVolume(AudioCategory category)
    {
        float effectiveMultiplier = (isAppFocused || !muteInBackground) ? 1f : 0f;

        return category switch
        {
            AudioCategory.BGM => bgmVolume * masterVolume * effectiveMultiplier,
            AudioCategory.SFX => sfxVolume * masterVolume * effectiveMultiplier,
            AudioCategory.UI => uiVolume * masterVolume * effectiveMultiplier,
            _ => 0f
        };
    }

    public bool IsBGMPlaying => bgmSource != null && bgmSource.isPlaying;

    public AudioClip GetCurrentBGM => bgmSource != null ? bgmSource.clip : null;
}

public enum AudioCategory
{
    BGM,
    SFX,
    UI
}
