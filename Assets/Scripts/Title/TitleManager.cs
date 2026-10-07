using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public enum GameMode
{
    Normal,
    Speedrun,
    Hardcore
}

public class TitleManager : MonoBehaviour
{
    private const string DefaultOpenSettingsKey = "F1";

    [Header("Scene")]
    [SerializeField] private SceneName gameScene = SceneName.GameScene;

    [Header("Mode")]
    [SerializeField] private GameMode selectedMode = GameMode.Normal;

    [Header("Buttons")]
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button achievementButton;
    [SerializeField] private Button collectionButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeAchievementButton;
    [SerializeField] private Button closeCollectionButton;
    [SerializeField] private Button closeSettingsButton;

    [Header("Panels")]
    [SerializeField] private GameObject achievementPanel;
    [SerializeField] private AchievementPanelView achievementPanelView;
    [SerializeField] private GameObject collectionPanel;
    [SerializeField] private CollectionPanelView collectionPanelView;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private SettingPanelView settingsPanelView;

    private void Awake()
    {
        if (achievementPanel != null)
        {
            var anim = achievementPanel.GetComponent<PanelAnimator>();
            if (anim != null) anim.Hide(true);
            else achievementPanel.SetActive(false);
        }

        if (collectionPanel != null)
        {
            var anim = collectionPanel.GetComponent<PanelAnimator>();
            if (anim != null) anim.Hide(true);
            else collectionPanel.SetActive(false);
        }

        InitializeSettingsPanel();

        UpdateMainTitleButtonsInteractable();
    }

    private void InitializeSettingsPanel()
    {
        if (settingsPanelView == null && settingsPanel != null)
        {
            settingsPanelView = settingsPanel.GetComponent<SettingPanelView>();
        }

        if (SettingPanelView.Instance != null && settingsPanelView != SettingPanelView.Instance)
        {
            settingsPanelView = SettingPanelView.Instance;
            settingsPanel = settingsPanelView.gameObject;
        }

        if (settingsPanel != null && !settingsPanel.activeSelf)
        {
            settingsPanel.SetActive(true);
        }

        if (SettingPanelView.Instance != null)
        {
            settingsPanelView = SettingPanelView.Instance;
            settingsPanel = settingsPanelView.gameObject;
        }

        if (settingsPanel != null)
        {
            var anim = settingsPanel.GetComponent<PanelAnimator>();
            if (anim != null) anim.Hide(true);
            else settingsPanel.SetActive(false);
        }
    }

    // Keyboard navigation state for main title buttons
    private Button[] mainButtons;
    private int selectedIndex = -1;
    private string[] originalButtonTexts;

    private bool HandleShortcutInput()
    {
        if (!CanUseTitleShortcuts())
        {
            return false;
        }

        if (!IsSettingsShortcutPressed())
        {
            return false;
        }

        OnSettingsClicked();
        return true;
    }

    private bool IsSettingsShortcutPressed()
    {
        string keyName = GetSettingsShortcutKeyName();

        if (!System.Enum.TryParse<KeyCode>(keyName, true, out var parsedKey))
        {
            return Input.GetKeyDown(KeyCode.F1);
        }

        return Input.GetKeyDown(parsedKey);
    }

    private string GetSettingsShortcutKeyName()
    {
        if (SettingsManager.Instance == null || SettingsManager.Instance.Settings == null)
        {
            return DefaultOpenSettingsKey;
        }

        string keyName = SettingsManager.Instance.Settings.keyOpenSettings;
        return string.IsNullOrWhiteSpace(keyName) ? DefaultOpenSettingsKey : keyName;
    }

    private void InitializeSelectableButtons()
    {
        if (mainButtons != null) return;

        var list = new System.Collections.Generic.List<Button>();
        if (gameStartButton != null) list.Add(gameStartButton);
        if (achievementButton != null) list.Add(achievementButton);
        if (collectionButton != null) list.Add(collectionButton);
        if (settingsButton != null) list.Add(settingsButton);

        mainButtons = list.ToArray();
        originalButtonTexts = new string[mainButtons.Length];
        for (int i = 0; i < mainButtons.Length; i++)
        {
            originalButtonTexts[i] = GetButtonText(mainButtons[i]);
        }

        // start with first selectable
        selectedIndex = -1;

        // add pointer enter/exit handlers to support mouse hover selection
        for (int i = 0; i < mainButtons.Length; i++)
        {
            var b = mainButtons[i];
            if (b == null) continue;

            var trigger = b.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = b.gameObject.AddComponent<EventTrigger>();

            trigger.triggers ??= new System.Collections.Generic.List<EventTrigger.Entry>();

            // Pointer Enter
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            int idxEnter = i;
            enter.callback.AddListener((e) => OnButtonPointerEnter(idxEnter));
            trigger.triggers.Add(enter);

            // Pointer Exit
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            int idxExit = i;
            exit.callback.AddListener((e) => OnButtonPointerExit(idxExit));
            trigger.triggers.Add(exit);
        }
    }

    private void Update()
    {
        if (HandleShortcutInput())
        {
            return;
        }

        if (!CanUseTitleShortcuts())
        {
            if (selectedIndex != -1) ClearSelection();
            return;
        }

        if (mainButtons == null) InitializeSelectableButtons();

        if (mainButtons.Length == 0) return;

        // If nothing is selected, only select the top item on first navigation key or confirm key press.
        bool anyNavKey = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)
                         || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)
                         || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                         || Input.GetKeyDown(KeyCode.Space);

        if (selectedIndex == -1)
        {
            if (anyNavKey)
            {
                selectedIndex = FindNextSelectableIndex(0, 1);
                UpdateSelectionVisuals();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            Move(1);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            Move(-1);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            ConfirmSelected();
        }
    }

    private void Start()
    {
        BindButtons();

        // Start background preload of audio used on title to avoid hitches later
        SoundManager.Instance?.PreloadForTitleScene();
    }

    private void LateUpdate()
    {
        UpdateMainTitleButtonsInteractable();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    private void BindButtons()
    {
        if (gameStartButton != null)
        {
            gameStartButton.onClick.RemoveListener(OnGameStartClicked);
            gameStartButton.onClick.AddListener(OnGameStartClicked);
        }

        if (achievementButton != null)
        {
            achievementButton.onClick.RemoveListener(OnAchievementClicked);
            achievementButton.onClick.AddListener(OnAchievementClicked);
        }

        if (collectionButton != null)
        {
            collectionButton.onClick.RemoveListener(OnCollectionClicked);
            collectionButton.onClick.AddListener(OnCollectionClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }
        if (closeAchievementButton != null)
        {
            closeAchievementButton.onClick.RemoveListener(CloseAchievementPanel);
            closeAchievementButton.onClick.AddListener(CloseAchievementPanel);
        }

        if (closeCollectionButton != null)
        {
            closeCollectionButton.onClick.RemoveListener(CloseCollectionPanel);
            closeCollectionButton.onClick.AddListener(CloseCollectionPanel);
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveListener(CloseSettingsPanel);
            closeSettingsButton.onClick.AddListener(CloseSettingsPanel);
        }
    }

    private void UnbindButtons()
    {
        if (gameStartButton != null)
        {
            gameStartButton.onClick.RemoveListener(OnGameStartClicked);
        }

        if (achievementButton != null)
        {
            achievementButton.onClick.RemoveListener(OnAchievementClicked);
        }

        if (collectionButton != null)
        {
            collectionButton.onClick.RemoveListener(OnCollectionClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
        }
        if (closeAchievementButton != null)
        {
            closeAchievementButton.onClick.RemoveListener(CloseAchievementPanel);
        }

        if (closeCollectionButton != null)
        {
            closeCollectionButton.onClick.RemoveListener(CloseCollectionPanel);
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveListener(CloseSettingsPanel);
        }
    }

    private void OnGameStartClicked()
    {
        if (!CanUseTitleShortcuts())
        {
            return;
        }

        ResetPersistentRunData();

        // PieceInventory now supplies initial pieces on scene load.

        PlayerPrefs.SetInt("PendingGameMode", (int)selectedMode);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameScene.ToString());
    }

    private void ResetPersistentRunData()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetRunData();
        }

        if (PieceInventory.Instance != null)
        {
            PieceInventory.Instance.ClearPieces();
            PieceInventory.Instance.ResetStageSnapshot();
        }

        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ResetInventoryRestoreMarker();
        }

        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.ClearArtifacts();
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameStateManager.GameState.None);
        }
    }

    private void OnAchievementClicked()
    {
        if (!CanUseTitleShortcuts())
        {
            return;
        }

        CloseSettingsPanel();
        CloseCollectionPanel();

        if (achievementPanel != null)
        {
            var anim = achievementPanel.GetComponent<PanelAnimator>();
            if (anim != null)
            {
                achievementPanel.SetActive(true);
                anim.Show();
            }
            else achievementPanel.SetActive(true);
        }

        if (achievementPanelView != null)
        {
            achievementPanelView.Refresh();
        }

        UpdateMainTitleButtonsInteractable();
    }

    public void CloseAchievementPanel()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }

        if (achievementPanel != null)
        {
            var anim = achievementPanel.GetComponent<PanelAnimator>();
            if (anim != null) anim.Hide();
            else achievementPanel.SetActive(false);
        }
    }

    private void OnSettingsClicked()
    {
        if (!CanUseTitleShortcuts())
        {
            return;
        }

        CloseAchievementPanel();
        CloseCollectionPanel();

        if (SettingPanelView.Instance != null)
        {
            SettingPanelView.OpenSingleton();
            UpdateMainTitleButtonsInteractable();
            return;
        }

        if (settingsPanelView != null)
        {
            settingsPanelView.OpenPanel();
            UpdateMainTitleButtonsInteractable();
            return;
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            UpdateMainTitleButtonsInteractable();
        }
    }

    public void CloseSettingsPanel()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }

        if (SettingPanelView.Instance != null)
        {
            SettingPanelView.CloseSingleton();
            UpdateMainTitleButtonsInteractable();
            return;
        }

        if (settingsPanelView != null)
        {
            settingsPanelView.gameObject.SetActive(false);
            UpdateMainTitleButtonsInteractable();
            return;
        }

        if (settingsPanel != null)
        {
            var anim = settingsPanel.GetComponent<PanelAnimator>();
            if (anim != null) anim.Hide();
            else settingsPanel.SetActive(false);
        }

        UpdateMainTitleButtonsInteractable();
    }

    private void OnCollectionClicked()
    {
        if (!CanUseTitleShortcuts())
        {
            return;
        }

        CloseAchievementPanel();
        CloseSettingsPanel();

        if (collectionPanel != null)
        {
            var anim = collectionPanel.GetComponent<PanelAnimator>();
            if (anim != null)
            {
                collectionPanel.SetActive(true);
                anim.Show();
            }
            else collectionPanel.SetActive(true);
        }

        if (collectionPanelView != null)
        {
            collectionPanelView.Refresh();
        }

        UpdateMainTitleButtonsInteractable();
    }

    public void CloseCollectionPanel()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }

        if (collectionPanel != null)
        {
            var anim = collectionPanel.GetComponent<PanelAnimator>();
            if (anim != null) anim.Hide();
            else collectionPanel.SetActive(false);
        }

        UpdateMainTitleButtonsInteractable();
    }

    private void UpdateMainTitleButtonsInteractable()
    {
        bool panelsOpen = IsAnyOverlayPanelOpen();

        SetButtonInteractable(gameStartButton, !panelsOpen);
        SetButtonInteractable(achievementButton, !panelsOpen);
        SetButtonInteractable(collectionButton, !panelsOpen);
        SetButtonInteractable(settingsButton, !panelsOpen);
        // when panels open, clear visual selection
        if (panelsOpen)
        {
            if (selectedIndex != -1) ClearSelection();
        }
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private bool IsAnyOverlayPanelOpen()
    {
        return IsPanelOpen(achievementPanel) || IsSettingsPanelOpen() || IsPanelOpen(collectionPanel);
    }

    private bool IsSettingsPanelOpen()
    {
        if (SettingPanelView.Instance != null)
        {
            return SettingPanelView.Instance.gameObject.activeInHierarchy;
        }

        if (settingsPanelView != null)
        {
            return settingsPanelView.gameObject.activeInHierarchy;
        }

        return IsPanelOpen(settingsPanel);
    }

    private bool IsPanelOpen(GameObject panel)
    {
        return panel != null && panel.activeInHierarchy;
    }

    private bool CanUseTitleShortcuts()
    {
        return !IsAnyOverlayPanelOpen();
    }

    // Navigation helpers
    private void Move(int dir)
    {
        if (mainButtons == null || mainButtons.Length == 0) return;

        int next = FindNextSelectableIndex(selectedIndex + (dir > 0 ? 1 : -1), dir);
        if (next != selectedIndex && next >= 0)
        {
            selectedIndex = next;
            UpdateSelectionVisuals();
        }
    }

    private int FindNextSelectableIndex(int start, int dir)
    {
        int n = mainButtons.Length;
        if (n == 0) return -1;

        int idx = ((start % n) + n) % n; // normalize
        for (int i = 0; i < n; i++)
        {
            int tryIdx = ((idx + (dir > 0 ? i : -i)) % n + n) % n;
            if (mainButtons[tryIdx] != null && mainButtons[tryIdx].interactable)
            {
                return tryIdx;
            }
        }

        return -1;
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < mainButtons.Length; i++)
        {
            if (mainButtons[i] == null) continue;
            if (originalButtonTexts == null || originalButtonTexts.Length != mainButtons.Length)
            {
                originalButtonTexts = new string[mainButtons.Length];
                for (int j = 0; j < mainButtons.Length; j++) originalButtonTexts[j] = GetButtonText(mainButtons[j]);
            }

            if (selectedIndex == -1)
            {
                SetButtonText(mainButtons[i], originalButtonTexts[i]);
            }
            else if (i == selectedIndex)
            {
                SetButtonText(mainButtons[i], ">" + originalButtonTexts[i] + "<");
            }
            else
            {
                SetButtonText(mainButtons[i], originalButtonTexts[i]);
            }
        }
    }

    private void ClearSelection()
    {
        if (mainButtons == null) return;
        for (int i = 0; i < mainButtons.Length; i++)
        {
            if (mainButtons[i] == null) continue;
            if (originalButtonTexts != null && i < originalButtonTexts.Length)
                SetButtonText(mainButtons[i], originalButtonTexts[i]);
        }
        selectedIndex = -1;
    }

    // Called by EventTrigger on pointer enter
    private void OnButtonPointerEnter(int idx)
    {
        if (mainButtons == null || idx < 0 || idx >= mainButtons.Length) return;
        if (mainButtons[idx] == null || !mainButtons[idx].interactable) return;
        selectedIndex = idx;
        UpdateSelectionVisuals();
    }

    // Called by EventTrigger on pointer exit
    private void OnButtonPointerExit(int idx)
    {
        if (mainButtons == null || idx < 0 || idx >= mainButtons.Length) return;
        // clear hover selection back to none
        selectedIndex = -1;
        UpdateSelectionVisuals();
    }

    private void ConfirmSelected()
    {
        if (mainButtons == null || selectedIndex < 0 || selectedIndex >= mainButtons.Length) return;
        var btn = mainButtons[selectedIndex];
        if (btn != null && btn.interactable)
        {
            btn.onClick.Invoke();
        }
    }

    private string GetButtonText(Button b)
    {
        if (b == null) return string.Empty;
        var textComponent = b.GetComponentInChildren<TMP_Text>();
        if (textComponent != null) return textComponent.text;
        var legacyText = b.GetComponentInChildren<Text>();
        if (legacyText != null) return legacyText.text;
        return string.Empty;
    }

    private void SetButtonText(Button b, string text)
    {
        if (b == null) return;
        var textComponent = b.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = text;
            return;
        }
        var legacyText = b.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            legacyText.text = text;
        }
    }
}
