using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using JetBrains.Annotations;

public enum GameFlowState
{
    None,
    Map,
    Shop,
    Battle,
    Event,
    Treasure,
    WorkShop
}

public class GameManager : MonoBehaviour
{
    private const string DefaultOpenMapKey = "M";
    private const string DefaultOpenSettingsKey = "F1";

    private static class AchievementIds
    {
        public const string OtherFirstRun = "other_first_run";
        public const string OtherFirstEvent = "other_first_event";
        public const string OtherFirstTreasure = "other_first_treasure";
        public const string OtherFirstWorkshop = "other_first_workshop";
        public const string OtherGameOver = "other_game_over";
        public const string OtherGameOver5 = "other_game_over_5";

        public const string CombatFirstStage = "combat_first_stage";
        public const string CombatStage5 = "combat_stage_5";
        public const string CombatStage10 = "combat_stage_10";
        public const string CombatStage20 = "combat_stage_20";
        public const string CombatStage30 = "combat_stage_30";
        public const string StageClearChain = "stage_clear_chain";

        public const string CombatFirstBoss = "combat_first_boss";
        public const string CombatBoss3 = "combat_boss_3";
        public const string CombatBoss5 = "combat_boss_5";
        public const string CombatFirstCapture = "combat_first_capture";
        public const string CombatCapture5 = "combat_capture_5";
        public const string CombatCapture10 = "combat_capture_10";
        public const string CombatCapture30 = "combat_capture_30";
        public const string CombatCapture50 = "combat_capture_50";
        public const string CombatCapture100 = "combat_capture_100";
        public const string CombatCaptureCannon = "combat_capture_cannon";
        public const string CombatCaptureChariot = "combat_capture_chariot";
        public const string TestFirstCapture = "test_first_capture";

        public const string CollectFirstPurchase = "collect_first_purchase";
        public const string CollectPurchase5 = "collect_purchase_5";
        public const string CollectPurchase20 = "collect_purchase_20";
        public const string CollectArtifactEnhance = "collect_artifact_enhance";

        public const string OtherTurn50 = "other_turn_50";
        public const string OtherTurn100 = "other_turn_100";
        public const string OtherTurn200 = "other_turn_200";
        public const string OtherSynthesis5 = "other_synthesis_5";
    }

    public static GameManager Instance { get; private set; }

    [SerializeField] private GameFlowState currentFlowState = GameFlowState.None;
    public GameFlowState CurrentFlowState => currentFlowState;

    public event Action<GameFlowState> OnFlowStateChanged;

    [SerializeField] private int clearedStage = 0;
    public int ClearedStage => clearedStage;
    
    [SerializeField] private int clearedBosses = 0;
    public int ClearedBosses => clearedBosses;
    
    public bool BossJustCleared { get; set; } = false;
    public int Coin = 0;
    public int EnemyPiecesCaptured { get; private set; } = 0;
    public int PlayerPiecesCaptured { get; private set; } = 0;

    [Header("UI")]
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI stageText;
    public GameObject mapObject;
    [SerializeField] private Button openMapButton;
    [SerializeField] private Button openSettingsButton;
    private MapManager mapManager;
    private Coroutine cleanupFlowCoroutine;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Time.timeScale = 1f;
        SettingsManager.Instance?.ApplyRuntimeSettings();
    }

    public void ResetRunData()
    {
        currentFlowState = GameFlowState.None;
        clearedStage = 0;
        clearedBosses = 0;
        BossJustCleared = false;
        Coin = 100;
        EnemyPiecesCaptured = 0;
        PlayerPiecesCaptured = 0;

        if (coinText != null)
        {
            UpdateCoinUI();
        }

        if (stageText != null)
        {
            UpdateStageUI();
        }
        RewardService.Instance?.ResetForNewRun();
        AchievementManager.Instance?.ResetForNewRun();
        TooltipManager.Instance?.ResetForNewRun();
    }

    private void Start()
    {
        TryAddAchievementProgress(AchievementIds.OtherFirstRun);

        InitializeMapReferences();

        currentFlowState = GameFlowState.None;
        StartCoroutine(DelayedMapTransition());

        BindShortcutButtons();
        SubscribeToGameStateManager();
        AddCoin(100);
        UpdateStageUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindShortcutButtons();
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SubscribeToGameStateManager();
        Time.timeScale = 1f;
        SettingsManager.Instance?.ApplyRuntimeSettings();

        if (coinText == null)
        {
            GameObject coinTextObj = GameObject.Find("CoinText");
            if (coinTextObj != null)
            {
                coinText = coinTextObj.GetComponent<TextMeshProUGUI>();
                UpdateCoinUI();
            }
        }

        if (stageText == null)
        {
            GameObject stageTextObj = GameObject.Find("StageText");
            if (stageTextObj != null)
            {
                stageText = stageTextObj.GetComponent<TextMeshProUGUI>();
                UpdateStageUI();
            }
        }

        openMapButton = null;
        openSettingsButton = null;

        BindShortcutButtons();
        InitializeMapReferences();

        if (currentFlowState == GameFlowState.None)
        {
            StartCoroutine(DelayedMapTransition());
        }
    }

    private void BindShortcutButtons()
    {
        if (openMapButton == null)
        {
            openMapButton = FindButtonByName("MapButton");
        }

        if (openMapButton != null)
        {
            openMapButton.onClick.RemoveListener(OpenMap);
            openMapButton.onClick.AddListener(OpenMap);
        }

        if (openSettingsButton == null)
        {
            openSettingsButton = FindButtonByName("SettingButton");
        }

        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.RemoveListener(OpenSettings);
            openSettingsButton.onClick.AddListener(OpenSettings);
        }

        UpdateMapButtonInteractable();
    }

    private Button FindButtonByName(string buttonName)
    {
        GameObject obj = GameObject.Find(buttonName);
        if (obj != null)
        {
            return obj.GetComponent<Button>();
        }
        return null;
    }

    private void UnbindShortcutButtons()
    {
        if (openMapButton != null)
        {
            openMapButton.onClick.RemoveListener(OpenMap);
        }

        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.RemoveListener(OpenSettings);
        }
    }

    private IEnumerator DelayedMapTransition()
    {
        yield return null;
        ChangeFlowState(GameFlowState.Map);
    }

    private void InitializeMapReferences()
    {
        if (mapManager == null) mapManager = MapManager.Instance;

        if (mapObject == null)
        {
            if (mapManager != null) mapObject = mapManager.gameObject;
            else mapObject = GameObject.Find("MapObject");
        }

        if (mapManager == null && mapObject != null) mapManager = mapObject.GetComponent<MapManager>();

        UpdateMapButtonInteractable();
    }

    private bool IsMapPanelOpen()
    {
        if (mapManager != null)
        {
            return mapManager.gameObject.activeSelf;
        }

        return mapObject != null && mapObject.activeSelf;
    }

    private void UpdateMapButtonInteractable()
    {
        if (openMapButton == null)
        {
            return;
        }

        openMapButton.interactable = currentFlowState != GameFlowState.Map;
    }

    private void Update()
    {
        HandleGlobalShortcuts();

#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            PieceController[] pieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
            foreach (var piece in pieces)
            {
                if (piece != null && piece.IsEnemy)
                {
                    Destroy(piece.gameObject);
                }
            }

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Win);
            }
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (CurrentFlowState == GameFlowState.Map && MapManager.Instance != null)
            {
                MapManager.Instance.ReloadMap();
            }
        }
#endif
    }

    private void HandleGlobalShortcuts()
    {
        if (ModalManager.IsKeyboardBlocked)
        {
            return;
        }

        if (IsConfiguredKeyPressed(GetShortcutKeyName(isSettingsShortcut: false), DefaultOpenMapKey))
        {
            OpenMap();
            return;
        }

        if (IsConfiguredKeyPressed(GetShortcutKeyName(isSettingsShortcut: true), DefaultOpenSettingsKey))
        {
            OpenSettings();
        }
    }

    private string GetShortcutKeyName(bool isSettingsShortcut)
    {
        if (SettingsManager.Instance == null || SettingsManager.Instance.Settings == null)
        {
            return isSettingsShortcut ? DefaultOpenSettingsKey : DefaultOpenMapKey;
        }

        string keyName = isSettingsShortcut
            ? SettingsManager.Instance.Settings.keyOpenSettings
            : SettingsManager.Instance.Settings.keyOpenMap;

        return string.IsNullOrWhiteSpace(keyName)
            ? (isSettingsShortcut ? DefaultOpenSettingsKey : DefaultOpenMapKey)
            : keyName;
    }

    private static bool IsConfiguredKeyPressed(string keyName, string fallbackKeyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            keyName = fallbackKeyName;
        }

        if (!Enum.TryParse<KeyCode>(keyName, true, out var parsedKey))
        {
            return false;
        }

        return Input.GetKeyDown(parsedKey);
    }

    public void OpenMap()
    {
        InitializeMapReferences();
        if (IsMapPanelOpen() && currentFlowState != GameFlowState.Map)
        {
            if (mapManager != null)
            {
                mapManager.Close();
            }
            else if (mapObject != null)
            {
                mapObject.SetActive(false);
            }

            UpdateMapButtonInteractable();
            return;
        }

        if (mapManager != null)
        {
            mapManager.Open();
        }
        else if (mapObject != null)
        {
            mapObject.SetActive(true);
        }

        UpdateMapButtonInteractable();
    }

    public void OpenSettings()
    {
        SettingsManager.OpenSettingsPanelGlobal();
    }

    private void SubscribeToGameStateManager()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        switch (newState)
        {
            case GameStateManager.GameState.Win:
                clearedStage++;
                UpdateStageUI();
                TryAddAchievementProgress(AchievementIds.CombatFirstStage);
                TryAddAchievementProgress(AchievementIds.CombatStage5);
                TryAddAchievementProgress(AchievementIds.CombatStage10);
                TryAddAchievementProgress(AchievementIds.CombatStage20);
                TryAddAchievementProgress(AchievementIds.CombatStage30);
                TryAddAchievementProgress(AchievementIds.StageClearChain);
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Cleanup);
                break;
            case GameStateManager.GameState.Cleanup:
                if (cleanupFlowCoroutine != null)
                {
                    StopCoroutine(cleanupFlowCoroutine);
                }
                cleanupFlowCoroutine = StartCoroutine(HandleCleanupFlow());
                break;
            case GameStateManager.GameState.Reward:
                if (PresentationManager.Instance != null && PresentationManager.Instance.HasPersistentBossSprite)
                {
                    PresentationManager.Instance.RemovePersistentBossSprite();
                }

                if (PieceManager.Instance != null)
                {
                    PieceManager.Instance.RevertPromotionSealedPiecesToSoldier();
                }

                if (PieceInventory.Instance != null)
                {
                    PieceInventory.Instance.SavePiecesForNextStage();
                }
                break;
            case GameStateManager.GameState.GameOver:
                TryAddAchievementProgress(AchievementIds.OtherGameOver);
                TryAddAchievementProgress(AchievementIds.OtherGameOver5);
                break;
        }
    }

    private IEnumerator HandleCleanupFlow()
    {
        yield return null;

        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameStateManager.GameState.Cleanup)
        {
            cleanupFlowCoroutine = null;
            yield break;
        }

        if (PresentationManager.Instance != null)
        {
            bool presentationComplete = false;
            PresentationManager.Instance.PlayCustomPresentation("WIN", null, () =>
            {
                presentationComplete = true;
            });

            while (!presentationComplete)
            {
                yield return null;
            }
        }

        PieceManager pieceManager = PieceManager.Instance;
        if (pieceManager == null)
        {
            pieceManager = FindFirstObjectByType<PieceManager>(FindObjectsInactive.Include);
        }

        if (pieceManager != null)
        {
            pieceManager.RestorePlacementPositions();
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Cleanup)
        {
            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Reward);
        }

        cleanupFlowCoroutine = null;
    }

    public void ChangeFlowState(GameFlowState newState)
    {
        if (currentFlowState == newState) return;

        GameFlowState previousFlowState = currentFlowState;

        if (previousFlowState == GameFlowState.Battle && newState != GameFlowState.Battle)
        {
            if (PieceInventory.Instance != null)
            {
                PieceInventory.Instance.SavePiecesForNextStage();
            }
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.ClearPlacementSnapshot();
            }
        }

        currentFlowState = newState;
        ReportFlowStateEntryAchievements(currentFlowState);

        OnFlowStateChanged?.Invoke(currentFlowState);

        if (currentFlowState == GameFlowState.Shop)
        {
            if (ShopManager.Instance != null) ShopManager.Instance.EnterShopNode();
        }
        else
        {
            if (ShopManager.Instance != null) ShopManager.Instance.Close();
        }

        if (currentFlowState == GameFlowState.Treasure)
        {
            if (TreasureManager.Instance != null) TreasureManager.Instance.Open();
        }
        else
        {
            if (TreasureManager.Instance != null) TreasureManager.Instance.Close();
        }

        if (currentFlowState == GameFlowState.WorkShop)
        {
            if (WorkShopManager.Instance != null) WorkShopManager.Instance.Open();
        }
        else
        {
            if (WorkShopManager.Instance != null) WorkShopManager.Instance.Close();
        }

        bool shouldDimBoard = currentFlowState != GameFlowState.Battle;

        if (PieceManager.Instance != null && PieceManager.Instance.gridManager != null)
        {
            PieceManager.Instance.gridManager.SetBoardPresentation(shouldDimBoard, 0.35f);
        }

        InitializeMapReferences();

        if (currentFlowState == GameFlowState.Map)
        {
            if (mapManager != null)
            {
                mapManager.Open();
            }
            else if (mapObject != null)
            {
                if (!mapObject.activeSelf) mapObject.SetActive(true);
            }
        }
        else
        {
            if (mapManager != null) mapManager.Close();
            else if (mapObject != null) mapObject.SetActive(false);
        }

        if (currentFlowState != GameFlowState.Battle)
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.None);
            }
        }

        switch (currentFlowState)
        {
            case GameFlowState.Battle:
                if (GameStateManager.Instance != null)
                {
                            if (PieceManager.Instance != null && PieceManager.Instance.gridManager != null)
                            {
                                PieceManager.Instance.gridManager.SetBoardPresentation(false, 0.4f);
                            }

                            if (PresentationManager.Instance != null && PresentationManager.Instance.IsPresenting)
                            {
                                Action handler = null;
                                handler = () =>
                                {
                                    PresentationManager.Instance.OnPresentationComplete -= handler;
                                    if (GameStateManager.Instance != null)
                                    {
                                        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Prepare);
                                    }
                                };
                                PresentationManager.Instance.OnPresentationComplete += handler;
                            }
                            else
                            {
                                GameStateManager.Instance.ChangeState(GameStateManager.GameState.Prepare);
                            }
                }
                break;
            case GameFlowState.Event:
                break;
        }

            UpdateMapButtonInteractable();
    }

    public void RestartGame()
    {
        if (PieceInventory.Instance != null)
        {
            PieceInventory.Instance.SaveBoardPieces();
        }

        currentFlowState = GameFlowState.None;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void AddCoin(int amount)
    {
        if (amount < 0)
        {
            return;
        }
        Coin += amount;
        UpdateCoinUI();
    }

    public bool UseCoin(int amount)
    {
        if (amount < 0)
        {
            return false;
        }

        if (Coin >= amount)
        {
            Coin -= amount;
            UpdateCoinUI();
            AnimateCoinUsage(amount);
            return true;
        }

        return false;
    }

    public int GetCoin()
    {
        return Coin;
    }

    private void UpdateCoinUI()
    {
        if (coinText != null)
        {
            coinText.text = Coin.ToString();

            coinText.transform.DOKill();
            coinText.transform.localScale = Vector3.one;
            coinText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 10, 1);
        }
    }

    private void UpdateStageUI()
    {
        if (stageText != null)
        {
            int mapProgress = clearedBosses + 1;

            int nodeProgress = 1;
            if (mapManager != null)
            {
                nodeProgress = mapManager.VisitedNodeCount + 1;
            }
            
            stageText.text = $"{mapProgress}-{nodeProgress}";

            stageText.transform.DOKill();
            stageText.transform.localScale = Vector3.one;
            stageText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 10, 1);
        }
    }

    private void AnimateCoinUsage(int amount)
    {
        if (coinText == null) return;

        GameObject flyObj = new GameObject("CoinUsageText");
        
        Canvas rootCanvas = coinText.GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            flyObj.transform.SetParent(rootCanvas.transform, true);
        }
        else
        {
            flyObj.transform.SetParent(coinText.transform, true);
        }
        
        flyObj.transform.position = coinText.transform.position;
        flyObj.transform.localScale = Vector3.one;

        TextMeshProUGUI flyText = flyObj.AddComponent<TextMeshProUGUI>();
        flyText.text = $"-{amount}";
        flyText.fontSize = coinText.fontSize;
        flyText.font = coinText.font;
        flyText.color = Color.red;
        flyText.alignment = TextAlignmentOptions.Center;
        flyText.textWrappingMode = TextWrappingModes.NoWrap;

        Sequence seq = DOTween.Sequence();
        seq.Append(flyObj.transform.DOPunchScale(Vector3.one * 0.5f, 0.5f, 10, 1));
        seq.Join(flyObj.transform.DOMoveY(flyObj.transform.position.y + 3f, 2.5f).SetEase(Ease.OutQuad));
        seq.Join(flyText.DOFade(0f, 2.0f).SetEase(Ease.InQuad));
        
        seq.OnComplete(() =>
        {
            Destroy(flyObj);
        });
    }

    public void OnBossCleared()
    {
        clearedBosses++;
        BossJustCleared = true;
        TryAddAchievementProgress(AchievementIds.CombatFirstBoss);
        TryAddAchievementProgress(AchievementIds.CombatBoss3);
        TryAddAchievementProgress(AchievementIds.CombatBoss5);
    }

    public void RecordCapture(bool isEnemyPieceCaptured, PieceType capturedPieceType = PieceType.Soldier)
    {
        if (isEnemyPieceCaptured)
        {
            EnemyPiecesCaptured++;
            TryAddAchievementProgress(AchievementIds.CombatFirstCapture);
            TryAddAchievementProgress(AchievementIds.CombatCapture5);
            TryAddAchievementProgress(AchievementIds.CombatCapture10);
            TryAddAchievementProgress(AchievementIds.CombatCapture30);
            TryAddAchievementProgress(AchievementIds.CombatCapture50);
            TryAddAchievementProgress(AchievementIds.CombatCapture100);
            TryAddAchievementProgress(AchievementIds.TestFirstCapture);

            if (capturedPieceType == PieceType.Cannon)
            {
                TryAddAchievementProgress(AchievementIds.CombatCaptureCannon);
            }
            else if (capturedPieceType == PieceType.Chariot)
            {
                TryAddAchievementProgress(AchievementIds.CombatCaptureChariot);
            }
        }
        else
        {
            PlayerPiecesCaptured++;
        }
    }

    public void RecordPurchase()
    {
        TryAddAchievementProgress(AchievementIds.CollectFirstPurchase);
        TryAddAchievementProgress(AchievementIds.CollectPurchase5);
        TryAddAchievementProgress(AchievementIds.CollectPurchase20);
    }

    public void RecordArtifactEnhanced()
    {
        TryAddAchievementProgress(AchievementIds.CollectArtifactEnhance);
    }

    public void RecordSynthesis()
    {
        TryAddAchievementProgress(AchievementIds.OtherSynthesis5);
    }

    public void RecordPlayerTurnProgress()
    {
        TryAddAchievementProgress(AchievementIds.OtherTurn50);
        TryAddAchievementProgress(AchievementIds.OtherTurn100);
        TryAddAchievementProgress(AchievementIds.OtherTurn200);
    }

    private void ReportFlowStateEntryAchievements(GameFlowState flowState)
    {
        switch (flowState)
        {
            case GameFlowState.Event:
                TryAddAchievementProgress(AchievementIds.OtherFirstEvent);
                break;
            case GameFlowState.Treasure:
                TryAddAchievementProgress(AchievementIds.OtherFirstTreasure);
                break;
            case GameFlowState.WorkShop:
                TryAddAchievementProgress(AchievementIds.OtherFirstWorkshop);
                break;
        }
    }

    private void TryAddAchievementProgress(string achievementId, int amount = 1)
    {
        if (string.IsNullOrEmpty(achievementId) || amount <= 0)
        {
            return;
        }

        AchievementManager.Instance?.AddProgress(achievementId, amount);
    }
}
