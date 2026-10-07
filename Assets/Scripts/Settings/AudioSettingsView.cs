using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class AudioSettingsView : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private SettingsSliderSelector masterSlider;
    [SerializeField] private SettingsSliderSelector bgmSlider;
    [SerializeField] private SettingsSliderSelector sfxSlider;
    [SerializeField] private SettingsSliderSelector uiSlider;

    [SerializeField] private Toggle muteInBackgroundToggle;

    private GameObject lastSelectedObject;

    private void OnEnable()
    {
        UpdateSelectionVisuals();
    }

    private void Start()
    {
        SettingsManager.EnsureInstance();
        PopulateLabels();
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

    private void PopulateLabels()
    {
        // 슬라이더 레이블 설정
        if (masterSlider != null) masterSlider.SetLabel("전체");
        if (bgmSlider != null) bgmSlider.SetLabel("배경음");
        if (sfxSlider != null) sfxSlider.SetLabel("효과음");
        if (uiSlider != null) uiSlider.SetLabel("UI");

        // 토글 레이블 설정
        SetToggleLabel(muteInBackgroundToggle, "백그라운드에서 음소거", "활성화", "비활성화");
    }

    private void SetToggleLabel(Toggle toggle, string label, string onText = null, string offText = null)
    {
        var selector = toggle?.GetComponent<SettingsToggleSelector>();
        if (selector == null) return;
        
        selector.SetLabel(label);
        if (onText != null || offText != null) {
            selector.SetStateTexts(onText, offText);
        }
    }

    private void ApplyToUI()
    {
        var s = staged ?? (SettingsManager.Instance != null ? SettingsManager.Instance.Settings : null);
        if (s == null) return;

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(s.masterVolume);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(s.bgmVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(s.sfxVolume);
        if (uiSlider != null) uiSlider.SetValueWithoutNotify(s.uiVolume);
        
        if (muteInBackgroundToggle != null) muteInBackgroundToggle.isOn = s.muteInBackground;
        UpdateSelectionVisuals();
    }

    private void BindUI()
    {
        UnbindUI();
        if (masterSlider != null) { masterSlider.OnValueChanged += OnMasterChanged; }
        if (bgmSlider != null) { bgmSlider.OnValueChanged += OnBgmChanged; }
        if (sfxSlider != null) { sfxSlider.OnValueChanged += OnSfxChanged; }
        if (uiSlider != null) { uiSlider.OnValueChanged += OnUiChanged; }
        if (muteInBackgroundToggle != null) muteInBackgroundToggle.onValueChanged.AddListener(OnMuteBackgroundChanged);
    }

    private void UnbindUI()
    {
        if (masterSlider != null) { masterSlider.OnValueChanged -= OnMasterChanged; }
        if (bgmSlider != null) { bgmSlider.OnValueChanged -= OnBgmChanged; }
        if (sfxSlider != null) { sfxSlider.OnValueChanged -= OnSfxChanged; }
        if (uiSlider != null) { uiSlider.OnValueChanged -= OnUiChanged; }
        if (muteInBackgroundToggle != null) muteInBackgroundToggle.onValueChanged.RemoveListener(OnMuteBackgroundChanged);
    }

    private void OnMasterChanged(float v)
    {
        if (staged == null) return;
        staged.masterVolume = v;
        UpdateSelectionVisuals();
    }

    private void OnBgmChanged(float v)
    {
        if (staged == null) return;
        staged.bgmVolume = v;
        UpdateSelectionVisuals();
    }
    
    private void OnSfxChanged(float v)
    {
        if (staged == null) return;
        staged.sfxVolume = v;
        UpdateSelectionVisuals();
    }
    
    private void OnUiChanged(float v)
    {
        if (staged == null) return;
        staged.uiVolume = v;
        UpdateSelectionVisuals();
    }
    
    private void OnMuteBackgroundChanged(bool val)
    {
        if (staged == null) return;
        staged.muteInBackground = val;
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
        if (masterSlider != null && masterSlider.SelectableControl != null) return masterSlider.SelectableControl;
        if (bgmSlider != null && bgmSlider.SelectableControl != null) return bgmSlider.SelectableControl;
        if (sfxSlider != null && sfxSlider.SelectableControl != null) return sfxSlider.SelectableControl;
        if (uiSlider != null && uiSlider.SelectableControl != null) return uiSlider.SelectableControl;
        return muteInBackgroundToggle;
    }

    private bool IsCurrentSelectionInView(GameObject selectedObject)
    {
        return selectedObject == (masterSlider != null ? masterSlider.gameObject : null)
            || selectedObject == (bgmSlider != null ? bgmSlider.gameObject : null)
            || selectedObject == (sfxSlider != null ? sfxSlider.gameObject : null)
            || selectedObject == (uiSlider != null ? uiSlider.gameObject : null)
            || selectedObject == (muteInBackgroundToggle != null ? muteInBackgroundToggle.gameObject : null);
    }

    private void UpdateSelectionVisuals()
    {
        SettingsSelectionTextUtility.SetMarkedToggleText(muteInBackgroundToggle, IsSelected(muteInBackgroundToggle));
    }

    private bool IsSelected(Selectable selectable)
    {
        return EventSystem.current != null && selectable != null && EventSystem.current.currentSelectedGameObject == selectable.gameObject;
    }

    private void UpdateAudioInteractivity(SettingsData s)
    {
        if (s == null) return;

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(s.masterVolume);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(s.bgmVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(s.sfxVolume);
        if (uiSlider != null) uiSlider.SetValueWithoutNotify(s.uiVolume);
    }
}
