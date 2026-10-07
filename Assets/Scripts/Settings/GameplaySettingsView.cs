using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.EventSystems;

public class GameplaySettingsView : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private SettingsChoiceSelector languageSelector;
    [SerializeField] private SettingsChoiceSelector gameSpeedSelector; // options: 1.0x, 1.5x, 2.0x
    [SerializeField] private SettingsChoiceSelector tooltipDelaySelector; // Immediate, 0.5s, 1s
    [SerializeField] private Toggle autoEndTurnToggle;
    [SerializeField] private Toggle simpleTurnLogToggle;
    [SerializeField] private Button resetSaveDataButton; // red warning button

    private SettingsData staged;
    private GameObject lastSelectedObject;

    private void OnEnable()
    {
        UpdateSelectionVisuals();
    }

    private void Start()
    {
        SettingsManager.EnsureInstance();
        PopulateOptions();
    }

    private void Update()
    {
        SyncSelectionVisuals();
    }

    private void OnDestroy()
    {
        UnbindUI();
    }

    public void SetTarget(SettingsData target)
    {
        staged = target;
        ApplyToUI();
        BindUI();
        UpdateSelectionVisuals();
    }

    private void PopulateOptions()
    {
        if (languageSelector != null)
        {
            languageSelector.SetLabel("언어");
            languageSelector.SetOptions(new System.Collections.Generic.List<string> { "한국어", "English" });
        }

        if (gameSpeedSelector != null)
        {
            gameSpeedSelector.SetLabel("게임 속도");
            gameSpeedSelector.SetOptions(new System.Collections.Generic.List<string> { "1.0x", "1.5x", "2.0x" });
        }

        if (tooltipDelaySelector != null)
        {
            tooltipDelaySelector.SetLabel("툴팁 지연");
            tooltipDelaySelector.SetOptions(new System.Collections.Generic.List<string> { "즉시", "0.5초", "1초" });
        }

        if (autoEndTurnToggle != null)
        {
            SetToggleLabel(autoEndTurnToggle, "자동 턴 종료", "켜짐", "꺼짐");
        }

        if (simpleTurnLogToggle != null)
        {
            SetToggleLabel(simpleTurnLogToggle, "간소화된 턴 로그", "켜짐", "꺼짐");
        }

        if (resetSaveDataButton != null)
        {
            var sel = resetSaveDataButton.GetComponent<SettingsButtonSelector>();
            if (sel != null) sel.SetLabel("데이터 관리");
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

        if (languageSelector != null)
            languageSelector.SetValueWithoutNotify((int)s.language);

        if (gameSpeedSelector != null)
        {
            if (Mathf.Approximately(s.gameSpeed, 1f)) gameSpeedSelector.SetValueWithoutNotify(0);
            else if (Mathf.Approximately(s.gameSpeed, 1.5f)) gameSpeedSelector.SetValueWithoutNotify(1);
            else gameSpeedSelector.SetValueWithoutNotify(2);
        }

        if (tooltipDelaySelector != null)
            tooltipDelaySelector.SetValueWithoutNotify((int)s.tooltipDelay);

        if (autoEndTurnToggle != null)
            autoEndTurnToggle.isOn = s.autoEndTurn;

        if (simpleTurnLogToggle != null)
            simpleTurnLogToggle.isOn = s.simpleTurnLog;

        UpdateSelectionVisuals();
    }

    private void BindUI()
    {
        UnbindUI();
        if (languageSelector != null) languageSelector.OnValueChanged += OnLanguageChanged;
        if (gameSpeedSelector != null) gameSpeedSelector.OnValueChanged += OnGameSpeedChanged;
        if (tooltipDelaySelector != null) tooltipDelaySelector.OnValueChanged += OnTooltipDelayChanged;
        if (autoEndTurnToggle != null) autoEndTurnToggle.onValueChanged.AddListener(OnAutoEndTurnChanged);
        if (simpleTurnLogToggle != null) simpleTurnLogToggle.onValueChanged.AddListener(OnSimpleTurnLogChanged);
        if (resetSaveDataButton != null) resetSaveDataButton.onClick.AddListener(OnResetSaveDataClicked);
    }

    private void UnbindUI()
    {
        if (languageSelector != null) languageSelector.OnValueChanged -= OnLanguageChanged;
        if (gameSpeedSelector != null) gameSpeedSelector.OnValueChanged -= OnGameSpeedChanged;
        if (tooltipDelaySelector != null) tooltipDelaySelector.OnValueChanged -= OnTooltipDelayChanged;
        if (autoEndTurnToggle != null) autoEndTurnToggle.onValueChanged.RemoveListener(OnAutoEndTurnChanged);
        if (simpleTurnLogToggle != null) simpleTurnLogToggle.onValueChanged.RemoveListener(OnSimpleTurnLogChanged);
        if (resetSaveDataButton != null) resetSaveDataButton.onClick.RemoveListener(OnResetSaveDataClicked);
    }

    private void OnLanguageChanged(int idx)
    {
        if (staged == null) return;
        staged.language = (SettingsData.Language)idx;
        UpdateSelectionVisuals();
    }

    private void OnGameSpeedChanged(int idx)
    {
        if (staged == null) return;
        staged.gameSpeed = idx == 0 ? 1f : (idx == 1 ? 1.5f : 2f);
        UpdateSelectionVisuals();
    }

    private void OnTooltipDelayChanged(int idx)
    {
        if (staged == null) return;
        staged.tooltipDelay = (SettingsData.TooltipDelay)idx;
        UpdateSelectionVisuals();
    }

    private void OnAutoEndTurnChanged(bool value)
    {
        if (staged == null) return;
        staged.autoEndTurn = value;
        UpdateSelectionVisuals();
    }

    private void OnSimpleTurnLogChanged(bool value)
    {
        if (staged == null) return;
        staged.simpleTurnLog = value;
        UpdateSelectionVisuals();
    }

    private async void OnResetSaveDataClicked()
    {
        if (ModalManager.Instance == null)
        {
            return;
        }
    
        bool confirmed = await ModalManager.Instance.ShowModalAsync("정말로 모든 저장 데이터를 초기화할까요?\n업적과 잠금 해제 내역이 모두 사라지며, 되돌릴 수 없습니다.");
    
        if (!confirmed)
        {
            return;
        }
    
        // AchievementManager를 찾아 데이터 리셋
        var achievementManager = FindFirstObjectByType<AchievementManager>();
        if (achievementManager != null)
        {
            achievementManager.ResetAllProgress();
        }

        // CollectionManager를 찾아 데이터 리셋
        var collectionManager = FindFirstObjectByType<CollectionManager>();
        if (collectionManager != null)
        {
            collectionManager.ResetAllProgress();
        }

        // 타이틀 씬으로 돌아가서 완전히 새로 시작
        SceneManager.LoadScene(SceneName.TitleScene.ToString());
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
        if (languageSelector != null && languageSelector.SelectableButton != null) return languageSelector.SelectableButton;
        if (gameSpeedSelector != null && gameSpeedSelector.SelectableButton != null) return gameSpeedSelector.SelectableButton;
        if (tooltipDelaySelector != null && tooltipDelaySelector.SelectableButton != null) return tooltipDelaySelector.SelectableButton;
        if (autoEndTurnToggle != null) return autoEndTurnToggle;
        if (simpleTurnLogToggle != null) return simpleTurnLogToggle;
        return resetSaveDataButton;
    }

    private bool IsCurrentSelectionInView(GameObject selectedObject)
    {
        return selectedObject == (languageSelector != null && languageSelector.SelectableButton != null ? languageSelector.SelectableButton.gameObject : null)
            || selectedObject == (gameSpeedSelector != null && gameSpeedSelector.SelectableButton != null ? gameSpeedSelector.SelectableButton.gameObject : null)
            || selectedObject == (tooltipDelaySelector != null && tooltipDelaySelector.SelectableButton != null ? tooltipDelaySelector.SelectableButton.gameObject : null)
            || selectedObject == (autoEndTurnToggle != null ? autoEndTurnToggle.gameObject : null)
            || selectedObject == (simpleTurnLogToggle != null ? simpleTurnLogToggle.gameObject : null)
            || selectedObject == (resetSaveDataButton != null ? resetSaveDataButton.gameObject : null);
    }

    private void UpdateSelectionVisuals()
    {
        SettingsSelectionTextUtility.SetMarkedToggleText(autoEndTurnToggle, IsSelected(autoEndTurnToggle));
        SettingsSelectionTextUtility.SetMarkedToggleText(simpleTurnLogToggle, IsSelected(simpleTurnLogToggle));
        SettingsSelectionTextUtility.SetMarkedButtonText(resetSaveDataButton, IsSelected(resetSaveDataButton));
    }

    private bool IsSelected(Selectable selectable)
    {
        return EventSystem.current != null && selectable != null && EventSystem.current.currentSelectedGameObject == selectable.gameObject;
    }
}
