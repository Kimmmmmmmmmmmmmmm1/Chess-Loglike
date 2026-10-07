using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class DisplaySettingsView : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private SettingsChoiceSelector screenModeSelector; // Fullscreen / Borderless / Windowed
    [SerializeField] private SettingsChoiceSelector resolutionSelector; // e.g. 1920x1080
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private SettingsSliderSelector screenShakeSelector; // 0..1
    [SerializeField] private Toggle pixelPerfectToggle;

    private GameObject lastSelectedObject;

    private void OnEnable()
    {
        UpdateSelectionVisuals();
    }

    private void Start()
    {
        SettingsManager.EnsureInstance();
        PopulateResolutions();
    }

    private void Update()
    {
        SyncSelectionVisuals();
    }

    private void OnDestroy()
    {
        UnbindUI();
    }

    private SettingsData staged;

    public void SetTarget(SettingsData target)
    {
        staged = target;
        ApplyToUI();
        BindUI();
        UpdateSelectionVisuals();
    }

    private void PopulateResolutions()
    {
        var opts = new System.Collections.Generic.List<string>();
        foreach (var r in Screen.resolutions)
        {
            opts.Add($"{r.width}x{r.height}");
        }
        // Ensure there is at least a default
        if (opts.Count == 0) opts.Add("1920x1080");

        if (screenModeSelector != null)
        {
            screenModeSelector.SetLabel("화면 모드");
            screenModeSelector.SetOptions(new System.Collections.Generic.List<string> { "Fullscreen", "Borderless", "Windowed" });
        }

        if (resolutionSelector != null)
        {
            resolutionSelector.SetLabel("해상도");
            resolutionSelector.SetOptions(opts);
        }

        if (vSyncToggle != null)
        {
            SetToggleLabel(vSyncToggle, "수직동기화", "켜짐", "꺼짐");
        }

        if (screenShakeSelector != null)
        {
            screenShakeSelector.SetLabel("화면 흔들림");
        }
        
        if (pixelPerfectToggle != null)
        {
            SetToggleLabel(pixelPerfectToggle, "픽셀 퍼펙트", "켜짐", "꺼짐");
        }
    }

    private void SetToggleLabel(Toggle toggle, string label, string onText = null, string offText = null)
    {
        var selector = toggle?.GetComponent<SettingsToggleSelector>();
        if (selector == null) return;
        selector.SetLabel(label);
        if (onText != null || offText != null) selector.SetStateTexts(onText, offText);
    }

    private void ApplyToUI()
    {
        var s = staged ?? (SettingsManager.Instance != null ? SettingsManager.Instance.Settings : null);
        if (s == null) return;

        if (screenModeSelector != null) screenModeSelector.SetValueWithoutNotify((int)s.screenMode);

        if (resolutionSelector != null)
        {
            int idx = resolutionSelector.SelectedIndex;
            for (int i = 0; i < Screen.resolutions.Length; i++)
            {
                if ($"{Screen.resolutions[i].width}x{Screen.resolutions[i].height}" == s.resolution)
                {
                    idx = i;
                    break;
                }
            }

            resolutionSelector.SetValueWithoutNotify(idx);
        }

        if (vSyncToggle != null) vSyncToggle.isOn = s.vSync;
        if (screenShakeSelector != null) screenShakeSelector.SetValueWithoutNotify(s.screenShake);
        if (pixelPerfectToggle != null) pixelPerfectToggle.isOn = s.pixelPerfect;

        UpdateSelectionVisuals();
    }

    private void BindUI()
    {
        UnbindUI();
        if (screenModeSelector != null) screenModeSelector.OnValueChanged += OnScreenModeChanged;
        if (resolutionSelector != null) resolutionSelector.OnValueChanged += OnResolutionChanged;
        if (vSyncToggle != null) vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        if (screenShakeSelector != null) screenShakeSelector.OnValueChanged += OnScreenShakeChanged;
        if (pixelPerfectToggle != null) pixelPerfectToggle.onValueChanged.AddListener(OnPixelPerfectChanged);
    }

    private void UnbindUI()
    {
        if (screenModeSelector != null) screenModeSelector.OnValueChanged -= OnScreenModeChanged;
        if (resolutionSelector != null) resolutionSelector.OnValueChanged -= OnResolutionChanged;
        if (vSyncToggle != null) vSyncToggle.onValueChanged.RemoveListener(OnVSyncChanged);
        if (screenShakeSelector != null) screenShakeSelector.OnValueChanged -= OnScreenShakeChanged;
        if (pixelPerfectToggle != null) pixelPerfectToggle.onValueChanged.RemoveListener(OnPixelPerfectChanged);
    }

    private void OnScreenModeChanged(int idx)
    {
        if (staged == null) return;
        staged.screenMode = (SettingsData.ScreenMode)idx;
        UpdateSelectionVisuals();
    }

    private void OnResolutionChanged(int idx)
    {
        if (staged == null) return;
        if (resolutionSelector != null)
        {
            staged.resolution = resolutionSelector.SelectedOption;
        }
        UpdateSelectionVisuals();
    }

    private void OnVSyncChanged(bool val)
    {
        if (staged == null) return;
        staged.vSync = val;
        UpdateSelectionVisuals();
    }

    private void OnScreenShakeChanged(float val)
    {
        if (staged == null) return;
        staged.screenShake = val;
        UpdateSelectionVisuals();
    }

    private void OnPixelPerfectChanged(bool val)
    {
        if (staged == null) return;
        staged.pixelPerfect = val;
        UpdateSelectionVisuals();
    }

    private void SyncSelectionVisuals()
    {
        GameObject currentSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (currentSelected == lastSelectedObject)
        {
            return;
        }

        lastSelectedObject = currentSelected;
        UpdateSelectionVisuals();
    }

    private void EnsureSelection()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (IsCurrentSelectionInView(currentSelected))
        {
            return;
        }

        SelectFirstFocusable();
    }

    private void SelectFirstFocusable()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        Selectable firstSelectable = GetFirstFocusable();
        if (firstSelectable != null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
            lastSelectedObject = firstSelectable.gameObject;
        }
    }

    private Selectable GetFirstFocusable()
    {
        if (screenModeSelector != null && screenModeSelector.SelectableButton != null) return screenModeSelector.SelectableButton;
        if (resolutionSelector != null && resolutionSelector.SelectableButton != null) return resolutionSelector.SelectableButton;
        if (vSyncToggle != null) return vSyncToggle;
        if (screenShakeSelector != null && screenShakeSelector.SelectableControl != null) return screenShakeSelector.SelectableControl;
        return pixelPerfectToggle;
    }

    private bool IsCurrentSelectionInView(GameObject selectedObject)
    {
        return selectedObject == (screenModeSelector != null && screenModeSelector.SelectableButton != null ? screenModeSelector.SelectableButton.gameObject : null)
            || selectedObject == (resolutionSelector != null && resolutionSelector.SelectableButton != null ? resolutionSelector.SelectableButton.gameObject : null)
            || selectedObject == (vSyncToggle != null ? vSyncToggle.gameObject : null)
            || selectedObject == (screenShakeSelector != null && screenShakeSelector.SelectableControl != null ? screenShakeSelector.SelectableControl.gameObject : null)
            || selectedObject == (pixelPerfectToggle != null ? pixelPerfectToggle.gameObject : null);
    }

    private void UpdateSelectionVisuals()
    {
        SettingsSelectionTextUtility.SetMarkedToggleText(vSyncToggle, IsSelected(vSyncToggle));
        SettingsSelectionTextUtility.SetMarkedToggleText(pixelPerfectToggle, IsSelected(pixelPerfectToggle));
    }

    private bool IsSelected(Selectable selectable)
    {
        return EventSystem.current != null && selectable != null && EventSystem.current.currentSelectedGameObject == selectable.gameObject;
    }
}
