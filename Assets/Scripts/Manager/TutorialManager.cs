using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum TutorialPhase
{
    None,
    IntroQuestion,
    PlacementKing,
    PlacementPawn,
    PlacementComplete,
    Movement,
    Capture,
    Reward,
    StageSelect,
    PlacementPawnStage2,
    SealIntro,
    SealEquip,
    SealEquipComplete,
    SealMovement,
    PromotionAdvance2,
    PromotionAdvance3,
    PromotionChoice,
    PromotionComplete,
    RewardStage2,
    LightningArtifactIntro,
    LightningArtifactTarget,
    LightningArtifactComplete,
    PassiveArtifact,
    Completed
}

[System.Serializable]
public class TutorialMessagesData
{
    public TutorialIntroData intro = new TutorialIntroData();
    public List<string> placementKing = new List<string>();
    public List<string> placementPawn = new List<string>();
    public List<string> placementComplete = new List<string>();
    public List<string> placement = new List<string>();
    public List<string> movement = new List<string>();
    public List<string> capture = new List<string>();
    public List<string> reward = new List<string>();
    public List<string> stageSelect = new List<string>();
    public List<string> stageSelectStage3 = new List<string>();
    public List<string> placementPawnStage2 = new List<string>();
    public List<string> sealIntro = new List<string>();
    public List<string> sealEquip = new List<string>();
    public List<string> sealEquipComplete = new List<string>();
    public List<string> sealMovement = new List<string>();
    public List<string> promotionAdvance2 = new List<string>();
    public List<string> promotionAdvance3 = new List<string>();
    public List<string> promotionChoice = new List<string>();
    public List<string> promotionComplete = new List<string>();
    public List<string> rewardStage2 = new List<string>();
    public List<string> lightningArtifactIntro = new List<string>();
    public List<string> lightningArtifactTarget = new List<string>();
    public List<string> lightningArtifactComplete = new List<string>();
    public List<string> passiveArtifact = new List<string>();
}

[System.Serializable]
public class TutorialIntroData
{
    public string question = "";
    public string proceedButton = "";
    public string skipButton = "";
}

public partial class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("UI Prefab Reference")]
    [SerializeField] private GameObject tutorialPrefab;

    [Header("Loaded Messages Data")]
    [SerializeField] private TutorialMessagesData messagesData = new TutorialMessagesData();

    public bool IsTutorialActive { get; private set; } = false;
    public TutorialPhase CurrentPhase { get; private set; } = TutorialPhase.None;
    public bool IsDialogShowing => isDialogShowing;
    public bool IsWaitingForQuestion => isWaitingForQuestion;

    private TutorialDialogUI dialogUI;
    private List<string> currentActiveMessages;
    private int currentStepIndex = -1;
    private bool isWaitingForQuestion = false;
    private bool isDialogShowing = false;
    private bool isKeyboardBlocked = false;
    private float canAdvanceTime = 0f;
    private Action onFinishedCallback;

    // 튜토리얼 고정 배치 타겟 그리드 좌표 (5x5 보드 기준: 킹(0, -2) 후방 중앙, 폰(1, -1) 전방 우측)
    public readonly Vector2Int TargetKingPos = new Vector2Int(0, -2);
    public readonly Vector2Int TargetPawnPos = new Vector2Int(1, -1);

    private const string PrefsKeyTutorialCompleted = "Tutorial_Completed";
    private const string PrefsKeyPassiveArtifactShown = "Tutorial_PassiveArtifact_Shown";
    private const string ResourcePrefabPath = "UI/TutorialDialog";
    private const string ResourceMessagesJsonPath = "TutorialMessages";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadMessagesFromJson();
    }

    private void Start()
    {
        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.OnArtifactAdded -= OnArtifactAdded;
            ArtifactManager.Instance.OnArtifactAdded += OnArtifactAdded;
            ArtifactManager.Instance.OnArtifactConsumed -= OnArtifactConsumed;
            ArtifactManager.Instance.OnArtifactConsumed += OnArtifactConsumed;
        }
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        ClearPieceHighlight();
        ClearSealHighlight();
        ClearTargetGridCellHighlight();
        ClearArtifactSlotHighlight();
        HighlightStartButton(false);
        HighlightQueenButton(false);
        SetKeyboardBlock(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        UnsubscribeEvents();
        ClearPieceHighlight();
        ClearSealHighlight();
        ClearTargetGridCellHighlight();
        ClearArtifactSlotHighlight();
        HighlightStartButton(false);
        HighlightQueenButton(false);
        SetKeyboardBlock(false);
    }

    /// <summary>
    /// Resources/TutorialMessages.json 파일로부터 튜토리얼 텍스트 데이터를 로드합니다.
    /// 파싱 오류 발생 시 콘솔에 경고를 출력합니다.
    /// </summary>
    public void LoadMessagesFromJson()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>(ResourceMessagesJsonPath);
        if (jsonAsset == null || string.IsNullOrEmpty(jsonAsset.text))
        {
            Debug.LogWarning($"[TutorialManager] Resources/{ResourceMessagesJsonPath}.json 파일을 찾을 수 없거나 비어 있습니다.");
            messagesData = new TutorialMessagesData();
            return;
        }

        try
        {
            TutorialMessagesData parsed = JsonUtility.FromJson<TutorialMessagesData>(jsonAsset.text);
            if (parsed != null)
            {
                messagesData = parsed;
                Debug.Log("[TutorialManager] TutorialMessages.json 로드 성공");
                return;
            }
            else
            {
                Debug.LogWarning("[TutorialManager] TutorialMessages.json 파싱 결과가 null입니다.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TutorialManager] TutorialMessages.json 파싱 오류 발생: {ex.Message}");
        }

        messagesData = new TutorialMessagesData();
    }

    /// <summary>
    /// 씬에 배치된 TutorialManager 인스턴스를 찾거나 반환합니다. (동적 런타임 생성 안 함)
    /// </summary>
    public static TutorialManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindAnyObjectByType<TutorialManager>(FindObjectsInactive.Include);
        return Instance;
    }

    /// <summary>
    /// 첫 게임 시작 업적 달성 여부를 기준으로 튜토리얼을 표시해야 하는지 확인합니다.
    /// 세이브 파일 초기화 시 업적이 잠금 상태로 돌아가므로 자동으로 튜토리얼이 다시 표시됩니다.
    /// </summary>
    public bool ShouldShowTutorial()
    {
        AchievementManager.EnsureInstance();
        bool isAchievementUnlocked = AchievementManager.Instance != null &&
                                     AchievementManager.Instance.IsUnlocked(GameManager.AchievementIds.OtherFirstRun);

        if (!isAchievementUnlocked)
        {
            return true;
        }

        if (PlayerPrefs.GetInt(PrefsKeyTutorialCompleted, 0) == 1)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// 튜토리얼 플로우를 시작합니다.
    /// </summary>
    public void StartTutorialFlow(Action onFinished, bool force = false)
    {
        if (!force && !ShouldShowTutorial())
        {
            onFinished?.Invoke();
            return;
        }

        IsTutorialActive = true;
        CurrentPhase = TutorialPhase.IntroQuestion;
        isWaitingForQuestion = true;
        hasShownKingPlacement = false;
        hasShownPawnPlacement = false;
        hasShownPlacementComplete = false;
        hasShownMovement = false;
        hasShownCapture = false;
        hasShownReward = false;
        hasShownStageSelect = false;
        hasShownStageSelect3 = false;
        hasShownPlacementPawnStage2 = false;
        hasShownSealIntro = false;
        hasShownSealEquip = false;
        hasShownSealEquipComplete = false;
        hasShownSealMovement = false;
        hasShownPromotionAdvance2 = false;
        hasShownPromotionAdvance3 = false;
        hasShownPromotionChoice = false;
        hasShownPromotionComplete = false;
        hasShownRewardStage2 = false;
        hasShownLightningIntro = false;
        hasShownLightningTarget = false;
        hasShownLightningComplete = false;
        hasShownPassiveArtifact = false;
        isKingHighlightAllowed = false;
        isPawnHighlightAllowed = false;
        isStartButtonHighlightAllowed = false;
        isSealHighlightAllowed = false;

        onFinishedCallback = onFinished;
        StartCoroutine(StartTutorialRoutine());
    }

    private IEnumerator StartTutorialRoutine()
    {
        yield return null;

        SubscribeEvents();
        EnsureDialogUI();

        if (dialogUI == null)
        {
            Debug.LogError("[TutorialManager] 튜토리얼 프리팹 UI 인스턴스화에 실패했습니다.");
            EndTutorial(false);
            yield break;
        }

        IsTutorialActive = true;
        ShowQuestionStep();
    }

    private void EnsureDialogUI()
    {
        if (dialogUI != null)
        {
            return;
        }

        Canvas targetCanvas = FindTargetCanvas();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[TutorialManager] 타겟 캔버스를 찾지 못했습니다.");
            return;
        }

        GameObject prefab = tutorialPrefab;
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>(ResourcePrefabPath);
        }

        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, targetCanvas.transform, false);
            instance.name = "TutorialDialog";
            dialogUI = instance.GetComponent<TutorialDialogUI>();
            if (dialogUI == null)
            {
                dialogUI = instance.AddComponent<TutorialDialogUI>();
            }
        }
        else
        {
            dialogUI = TutorialDialogUI.CreateDefault(targetCanvas.transform);
        }

        if (dialogUI != null)
        {
            dialogUI.ResolveReferences();
            Canvas c = dialogUI.GetComponent<Canvas>();
            if (c == null) c = dialogUI.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 2000;
            if (dialogUI.GetComponent<GraphicRaycaster>() == null)
            {
                dialogUI.gameObject.AddComponent<GraphicRaycaster>();
            }
            dialogUI.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 다른 UI(스테이지 선택, 인벤토리 등)가 열렸을 때 튜토리얼 대화창을 항상 최상위 레이어로 가져옵니다.
    /// </summary>
    public void BringDialogToFront()
    {
        if (dialogUI != null)
        {
            dialogUI.transform.SetAsLastSibling();
            Canvas c = dialogUI.GetComponent<Canvas>();
            if (c != null)
            {
                c.overrideSorting = true;
                c.sortingOrder = 2000;
            }
        }
    }

    private Canvas FindTargetCanvas()
    {
        GameObject mainCanvasObj = GameObject.Find("Canvas");
        if (mainCanvasObj != null)
        {
            Canvas c = mainCanvasObj.GetComponent<Canvas>();
            if (c != null) return c;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (Canvas c in canvases)
        {
            if (c == null) continue;
            string n = c.gameObject.name;
            if (n.Contains("Modal") || n.Contains("Tooltip") || n.Contains("Setting") || n.Contains("Damage")) continue;
            return c;
        }

        if (canvases != null && canvases.Length > 0)
        {
            return canvases[0];
        }

        return null;
    }

    private void SetKeyboardBlock(bool block)
    {
        if (block && !isKeyboardBlocked)
        {
            ModalManager.PushKeyboardBlock();
            isKeyboardBlocked = true;
        }
        else if (!block && isKeyboardBlocked)
        {
            ModalManager.PopKeyboardBlock();
            isKeyboardBlocked = false;
        }
    }

    private void ShowQuestionStep()
    {
        ClearBoardMoveMarkers();
        CurrentPhase = TutorialPhase.IntroQuestion;
        isWaitingForQuestion = true;
        isDialogShowing = true;
        currentStepIndex = -1;
        canAdvanceTime = Time.unscaledTime + 0.2f;

        dialogUI.gameObject.SetActive(true);
        dialogUI.transform.SetAsLastSibling();

        string q = messagesData?.intro?.question ?? "";
        string pText = messagesData?.intro?.proceedButton;
        string sText = messagesData?.intro?.skipButton;

        dialogUI.ShowQuestion(q, OnProceedClicked, OnSkipClicked, pText, sText);
        dialogUI.FadeIn(null);
        SetKeyboardBlock(true);

        Debug.Log("[TutorialManager] 튜토리얼 경험 질문 표시");
    }

    private void OnProceedClicked()
    {
        if (!isWaitingForQuestion) return;
        PlayClickSound();

        isWaitingForQuestion = false;
        canAdvanceTime = Time.unscaledTime + 0.2f;

        if (StageSaveManager.Instance != null && !StageSaveManager.IsRestoring)
        {
            StageSaveManager.Instance.SaveCurrentRun();
        }

        ShowPlacementKingPhase();
    }

    private void OnSkipClicked()
    {
        if (!isWaitingForQuestion) return;
        PlayClickSound();

        ClearPieceHighlight();
        HighlightStartButton(false);
        EndTutorial(skipped: true);

        if (StageManager.Instance != null && GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            StageManager.Instance.ReloadNormalStage();
        }

        GridManager gridManager = PieceManager.Instance != null
            ? PieceManager.Instance.gridManager
            : FindFirstObjectByType<GridManager>();
        if (gridManager != null && GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            gridManager.HighlightInvalidZones();
        }

        if (StageSaveManager.Instance != null && !StageSaveManager.IsRestoring)
        {
            StageSaveManager.Instance.SaveCurrentRun();
        }
    }

    /// <summary>
    /// 현재 스텝이 플레이어의 직접적인 조작(기물 배치, 카드 선택, 버튼 클릭, 인장 장착 등)을 요구하는 스텝인지 판별합니다.
    /// 행동 스텝에서는 대화창을 닫지 않고 화면에 유지하여 안내 메시지를 보면서 행동할 수 있도록 합니다.
    /// </summary>
    public bool IsCurrentStepAction()
    {
        if (currentActiveMessages == null || currentStepIndex < 0) return false;
        bool isLastStep = currentStepIndex >= currentActiveMessages.Count - 1;

        switch (CurrentPhase)
        {
            case TutorialPhase.PlacementKing:
            case TutorialPhase.PlacementPawn:
            case TutorialPhase.PlacementComplete:
            case TutorialPhase.Movement:
            case TutorialPhase.Capture:
            case TutorialPhase.Reward:
            case TutorialPhase.StageSelect:
            case TutorialPhase.PlacementPawnStage2:
            case TutorialPhase.SealEquipComplete:
            case TutorialPhase.SealMovement:
            case TutorialPhase.PromotionAdvance2:
            case TutorialPhase.PromotionAdvance3:
            case TutorialPhase.PromotionChoice:
            case TutorialPhase.RewardStage2:
            case TutorialPhase.LightningArtifactIntro:
                return isLastStep;

            case TutorialPhase.SealEquip:
            case TutorialPhase.LightningArtifactTarget:
                return true;

            default:
                return false;
        }
    }

    private void ShowPhase(TutorialPhase phase, List<string> messages)
    {
        if (messages == null || messages.Count == 0) return;

        CurrentPhase = phase;
        currentActiveMessages = messages;
        currentStepIndex = 0;
        isWaitingForQuestion = false;
        isDialogShowing = true;
        canAdvanceTime = Time.unscaledTime + 0.2f;

        EnsureDialogUI();
        if (dialogUI == null) return;

        dialogUI.gameObject.SetActive(true);
        dialogUI.transform.SetAsLastSibling();
        ClearBoardMoveMarkers();

        bool isAction = IsCurrentStepAction();
        dialogUI.ShowStep(currentActiveMessages[0], isAction);
        dialogUI.FadeIn(null);
        SetKeyboardBlock(!isAction);

        CheckHighlightForCurrentStep();

        Debug.Log($"[TutorialManager] Phase 전환: {phase}, 행동 스텝 여부: {isAction}");
    }

    private void ShowCurrentStep()
    {
        if (currentActiveMessages == null || currentStepIndex < 0 || currentStepIndex >= currentActiveMessages.Count)
        {
            CloseCurrentPhaseDialog();
            return;
        }

        ClearBoardMoveMarkers();
        string msg = currentActiveMessages[currentStepIndex];
        bool isAction = IsCurrentStepAction();
        dialogUI.ShowStep(msg, isAction);
        SetKeyboardBlock(!isAction);
        CheckHighlightForCurrentStep();
    }

    public void AdvanceStep()
    {
        if (isWaitingForQuestion || !isDialogShowing) return;
        if (Time.unscaledTime < canAdvanceTime) return;

        if (IsCurrentStepAction())
        {
            return;
        }

        PlayClickSound();
        canAdvanceTime = Time.unscaledTime + 0.2f;
        currentStepIndex++;

        if (currentActiveMessages == null || currentStepIndex >= currentActiveMessages.Count)
        {
            CloseCurrentPhaseDialog();
        }
        else
        {
            ShowCurrentStep();
        }
    }

    /// <summary>
    /// 현재 대화 스텝이 기물/버튼 배치를 설명하는 스텝(해당 페이즈의 마지막 스텝)인지 판별하여 강조 연출을 시작합니다.
    /// </summary>
    private void CheckHighlightForCurrentStep()
    {
        if (currentActiveMessages == null || currentStepIndex < 0) return;
        bool isActionStep = currentStepIndex >= currentActiveMessages.Count - 1;

        if (CurrentPhase == TutorialPhase.PlacementKing && isActionStep)
        {
            isKingHighlightAllowed = true;
            if (highlightedPiece == null && !IsPieceOnBoard(PieceType.King))
            {
                HighlightInventoryPiece(PieceType.King);
            }
            HighlightTargetGridCell(TargetKingPos);
        }
        else if (CurrentPhase == TutorialPhase.PlacementPawn && isActionStep)
        {
            isPawnHighlightAllowed = true;
            if (highlightedPiece == null && !IsPieceOnBoard(PieceType.Pawn))
            {
                HighlightInventoryPiece(PieceType.Pawn);
            }
            HighlightTargetGridCell(TargetPawnPos);
        }
        else if (CurrentPhase == TutorialPhase.PlacementComplete && isActionStep)
        {
            isStartButtonHighlightAllowed = true;
            if (highlightedStartButton == null)
            {
                HighlightStartButton(true);
            }
        }
        else if (CurrentPhase == TutorialPhase.StageSelect && isActionStep)
        {
            StageChoiceCard[] cards = FindObjectsByType<StageChoiceCard>(FindObjectsInactive.Include);
            foreach (var card in cards)
            {
                if (card != null && card.Option != null && card.Option.nodeType == StageNodeType.NormalBattle)
                {
                    card.SetTutorialHighlighted(true);
                }
            }
        }
        else if (CurrentPhase == TutorialPhase.PlacementPawnStage2 && isActionStep)
        {
            isPawnHighlightAllowed = true;
            if (highlightedPiece == null && !IsPieceOnBoard(PieceType.Pawn))
            {
                HighlightInventoryPiece(PieceType.Pawn);
            }
            HighlightTargetGridCell(TargetPawnPos);
        }
        else if (CurrentPhase == TutorialPhase.SealIntro && isActionStep)
        {
            isSealHighlightAllowed = true;
            HighlightSealInInventory(true);
        }
        else if (CurrentPhase == TutorialPhase.SealEquip)
        {
            HighlightSealInInventory(true);
            HighlightBoardPiece(PieceType.Pawn);
        }
        else if (CurrentPhase == TutorialPhase.SealEquipComplete && isActionStep)
        {
            isStartButtonHighlightAllowed = true;
            if (highlightedStartButton == null)
            {
                HighlightStartButton(true);
            }
        }
        else if (CurrentPhase == TutorialPhase.SealMovement && isActionStep)
        {
            HighlightBoardPiece(PieceType.Pawn);
            HighlightTargetGridCell(new Vector2Int(1, 0));
        }
        else if (CurrentPhase == TutorialPhase.PromotionAdvance2 && isActionStep)
        {
            HighlightBoardPiece(PieceType.Pawn);
            HighlightTargetGridCell(new Vector2Int(1, 1));
        }
        else if (CurrentPhase == TutorialPhase.PromotionAdvance3 && isActionStep)
        {
            HighlightBoardPiece(PieceType.Pawn);
            HighlightTargetGridCell(new Vector2Int(1, 2));
        }
        else if (CurrentPhase == TutorialPhase.PromotionChoice && isActionStep)
        {
            ClearPieceHighlight();
            ClearTargetGridCellHighlight();
            HighlightQueenButton(true);
        }
        else if (CurrentPhase == TutorialPhase.LightningArtifactIntro && isActionStep)
        {
            HighlightArtifactSlot("AC02");
        }
        else if (CurrentPhase == TutorialPhase.LightningArtifactTarget)
        {
            ClearArtifactSlotHighlight();
        }
    }

    private void CloseCurrentPhaseDialog()
    {
        TutorialPhase closedPhase = CurrentPhase;
        isDialogShowing = false;
        SetKeyboardBlock(false);

        if (dialogUI != null)
        {
            dialogUI.FadeOut(() =>
            {
                if (dialogUI != null)
                {
                    dialogUI.gameObject.SetActive(false);
                }
            });
        }

        if (closedPhase == TutorialPhase.PassiveArtifact || closedPhase == TutorialPhase.LightningArtifactIntro)
        {
            ClearArtifactSlotHighlight();

            if (closedPhase == TutorialPhase.PassiveArtifact)
            {
                // 패시브 유물 튜토리얼 종료 후, 스테이지 선택 화면이 이미 열려 있다면 스테이지 선택 튜토리얼을 순차 진행
                if (StageSelectUI.Instance != null &&
                    GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.StageSelect)
                {
                    int nextStage = StageSelectUI.Instance.CurrentNextStage > 0 ? StageSelectUI.Instance.CurrentNextStage : (GameManager.Instance.ClearedStage + 1);
                    ShowStageSelectPhase(nextStage);
                }
            }
        }
        else if (closedPhase == TutorialPhase.SealIntro)
        {
            ShowSealEquipPhase();
        }
        else if (closedPhase == TutorialPhase.LightningArtifactComplete)
        {
            CompleteTutorial();
        }
    }

    /// <summary>
    /// 게임 오버 또는 씬 전환 등으로 인해 진행 중인 튜토리얼을 중단하고 UI/하이라이트를 정리합니다.
    /// </summary>
    public void AbortTutorial()
    {
        IsTutorialActive = false;
        isWaitingForQuestion = false;
        isDialogShowing = false;
        CurrentPhase = TutorialPhase.None;

        ClearPieceHighlight();
        ClearSealHighlight();
        ClearTargetGridCellHighlight();
        ClearArtifactSlotHighlight();
        HighlightStartButton(false);
        HighlightQueenButton(false);
        SetKeyboardBlock(false);
        isKingHighlightAllowed = false;
        isPawnHighlightAllowed = false;
        isStartButtonHighlightAllowed = false;
        isSealHighlightAllowed = false;

        if (dialogUI != null)
        {
            dialogUI.gameObject.SetActive(false);
        }

        onFinishedCallback = null;
    }

    /// <summary>
    /// 튜토리얼을 최종 완료했을 때 호출됩니다.
    /// </summary>
    public void CompleteTutorial()
    {
        if (!IsTutorialActive) return;

        Debug.Log("[TutorialManager] 튜토리얼 최종 완료!");
        EndTutorial(skipped: false);

        if (StageSaveManager.Instance != null && !StageSaveManager.IsRestoring)
        {
            StageSaveManager.Instance.SaveCurrentRun();
        }
    }

    private void EndTutorial(bool skipped)
    {
        IsTutorialActive = false;
        isWaitingForQuestion = false;
        isDialogShowing = false;
        CurrentPhase = TutorialPhase.Completed;

        ClearPieceHighlight();
        ClearSealHighlight();
        ClearTargetGridCellHighlight();
        ClearArtifactSlotHighlight();
        HighlightStartButton(false);
        HighlightQueenButton(false);
        SetKeyboardBlock(false);
        isKingHighlightAllowed = false;
        isPawnHighlightAllowed = false;
        isStartButtonHighlightAllowed = false;
        isSealHighlightAllowed = false;

        PlayerPrefs.SetInt(PrefsKeyTutorialCompleted, 1);
        PlayerPrefs.Save();

        // 첫 게임 시작 업적 해금
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.UnlockAchievement(GameManager.AchievementIds.OtherFirstRun);
        }

        if (dialogUI != null)
        {
            dialogUI.FadeOut(() =>
            {
                if (dialogUI != null)
                {
                    dialogUI.gameObject.SetActive(false);
                }
            });
        }

        Action cb = onFinishedCallback;
        onFinishedCallback = null;
        cb?.Invoke();
    }

    private void PlayClickSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUI(UIType.ButtonClick);
        }
    }

    /// <summary>
    /// 디버그용: 튜토리얼 완료 기록을 초기화하여 다시 볼 수 있도록 합니다.
    /// </summary>
    [ContextMenu("Reset Tutorial Progress")]
    public static void ResetTutorialProgress()
    {
        PlayerPrefs.DeleteKey(PrefsKeyTutorialCompleted);
        PlayerPrefs.DeleteKey(PrefsKeyPassiveArtifactShown);
        PlayerPrefs.Save();
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.SetProgress(GameManager.AchievementIds.OtherFirstRun, 0);
        }
        Debug.Log("[TutorialManager] 튜토리얼 진행 내역이 초기화되었습니다.");
    }

    /// <summary>
    /// 디버그용: 에디터 플레이 모드에서 튜토리얼을 즉시 강제 실행합니다.
    /// </summary>
    [ContextMenu("Force Start Tutorial (Play Mode)")]
    private void DebugForceStartTutorial()
    {
        if (Application.isPlaying)
        {
            StartTutorialFlow(null, force: true);
        }
    }
}
