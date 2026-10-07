using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    public SettingsData Settings { get; private set; }

    public event Action OnSettingsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstanceOnLoad()
    {
        EnsureInstance();
    }

    public static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("SettingsManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SettingsManager>();
        // Awake will run and load settings
    }

    public void Load()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            try
            {
                Settings = JsonUtility.FromJson<SettingsData>(json);
            }
            catch (Exception)
            {
                Settings = SettingsData.Default();
            }
        }
        else
        {
            Settings = SettingsData.Default();
        }

        ApplyRuntimeSettings();
        OnSettingsChanged?.Invoke();
    }

    public void Save()
    {
        if (Settings == null) Settings = SettingsData.Default();
        string json = JsonUtility.ToJson(Settings);
        File.WriteAllText(SavePath, json);

        ApplyRuntimeSettings();
        OnSettingsChanged?.Invoke();
    }

    public void ApplySettings(SettingsData newSettings, bool save = false)
    {
        if (newSettings == null)
        {
            Settings = SettingsData.Default();
        }
        else
        {
            Settings = JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(newSettings));
        }

        ApplyRuntimeSettings();

        if (save)
        {
            Save();
        }
        else
        {
            OnSettingsChanged?.Invoke();
        }
    }

    public void ResetToDefaults()
    {
        Settings = SettingsData.Default();
        Save();
    }

    public void ApplyRuntimeSettings()
    {
        SettingsData settings = Settings ?? SettingsData.Default();

        ApplyGameSpeed(settings.gameSpeed);
        ApplyDisplaySettings(settings);

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.SetTooltipDelay(settings.tooltipDelay);
        }
    }

    public bool OpenSettingsPanel()
    {
        return SettingPanelView.OpenSingleton();
    }

    public bool CloseSettingsPanel()
    {
        return SettingPanelView.CloseSingleton();
    }

    public static bool OpenSettingsPanelGlobal()
    {
        return SettingPanelView.OpenSingleton();
    }

    public static bool CloseSettingsPanelGlobal()
    {
        return SettingPanelView.CloseSingleton();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyRuntimeSettings();
        StartCoroutine(ApplyRuntimeSettingsNextFrame());
    }

    private IEnumerator ApplyRuntimeSettingsNextFrame()
    {
        yield return null;
        ApplyRuntimeSettings();
    }

    private void ApplyGameSpeed(float gameSpeed)
    {
        float appliedSpeed = Mathf.Max(0.01f, gameSpeed);
        DOTween.timeScale = appliedSpeed;
    }

    private void ApplyDisplaySettings(SettingsData settings)
    {
        QualitySettings.vSyncCount = settings.vSync ? 1 : 0;
        ApplyPixelPerfect(settings.pixelPerfect);
    }

    private void ApplyPixelPerfect(bool enabled)
    {
        Type pixelPerfectType = Type.GetType("UnityEngine.Rendering.Universal.PixelPerfectCamera, Unity.RenderPipelines.Universal.Runtime");
        if (pixelPerfectType == null)
        {
            return;
        }

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            Component component = camera.GetComponent(pixelPerfectType);
            if (component is Behaviour behaviour)
            {
                behaviour.enabled = enabled;
            }
        }
    }

    private static string SavePath => Path.Combine(Application.persistentDataPath, "settings.json");
}
