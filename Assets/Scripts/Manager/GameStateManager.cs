using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }
    public enum GameState
    {
        None,   // 디폴트 상태
        Prepare,    // 게임 시작 전 기물 배치 상태
        GamePlay,   // 게임 진행중 상태
        Win,    // 게임 승리 상태
        GameOver,    // 게임 패배 상태
        Reward,     // 보상 선택 상태
        Cleanup     // 승리 후 정리 연출/정리 처리 상태
    }

    [SerializeField] private GameState currentState;
    public GameState CurrentState => currentState;

    public delegate void StateChangeHandler(GameState newState);
    public event StateChangeHandler OnStateChanged;
    public Button gameStartButton;
    public GameObject gameOverPanel;

    [Header("Inventory Panels")]
    [SerializeField] private GameObject pieceInventoryPanel;
    [SerializeField] private GameObject sealInventoryPanel;
    [SerializeField] private Image pieceInventoryHoverImage;
    [SerializeField] private Image sealInventoryHoverImage;
    [SerializeField] private float inventorySlideDistance = 120f;
    [SerializeField] private float inventorySlideDuration = 0.3f;

    private RectTransform pieceInventoryRect;
    private CanvasGroup pieceInventoryCanvasGroup;
    private Vector2 pieceInventoryShownPosition;
    private Vector2 pieceInventoryHiddenPosition;
    private bool pieceInventoryPositionsCached;

    private RectTransform sealInventoryRect;
    private CanvasGroup sealInventoryCanvasGroup;
    private Vector2 sealInventoryShownPosition;
    private Vector2 sealInventoryHiddenPosition;
    private bool sealInventoryPositionsCached;

    private bool pieceInventoryHovered;
    private bool sealInventoryHovered;
    private Sequence pieceInventorySequence;
    private Sequence sealInventorySequence;

    private Dictionary<GameState, Action> stateActions = new Dictionary<GameState, Action>();

    public void RegisterStateAction(GameState state, Action action)
    {
        if (stateActions.ContainsKey(state))
        {
            stateActions[state] += action;
        }
        else
        {
            stateActions[state] = action;
        }
    }

    public void UnregisterStateAction(GameState state, Action action)
    {
        if (stateActions.ContainsKey(state))
        {
            stateActions[state] -= action;
        }
    }

    public void ChangeState(GameState newState)
    {
        if (currentState != newState)
        {
            // 아군 기물이 없으면 어떤 경로(적 전멸, 자폭, 다수결, 치트 등)로 승리 상태 진입을 시도하더라도 무조건 GameOver(탈락) 처리
            if (newState == GameState.Win)
            {
                int playerPiecesOnBoard = PieceManager.Instance != null
                    ? PieceManager.Instance.GetPlayerPieceCountOnBoard()
                    : 0;

                if (playerPiecesOnBoard <= 0)
                {
                    Debug.LogWarning("[GameStateManager] 아군 기물이 없는 상태에서 Win 전환 시도 감지 -> GameOver(탈락)로 강제 전환합니다.");
                    newState = GameState.GameOver;
                }
            }

            if ((newState == GameState.Win || newState == GameState.GameOver) && currentState != GameState.GamePlay)
            {
                // 예외: Win 상태로 진입했더라도 보드에 아군 기물이 0개라면 GameOver 전환 허용 (무조건 탈락 보장)
                if (newState == GameState.GameOver && currentState == GameState.Win)
                {
                    int playerPiecesOnBoard = PieceManager.Instance != null
                        ? PieceManager.Instance.GetPlayerPieceCountOnBoard()
                        : 0;

                    if (playerPiecesOnBoard > 0)
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            if (newState == GameState.Win || newState == GameState.Cleanup || newState == GameState.Reward || newState == GameState.GameOver)
            {
                if (PieceManager.Instance != null)
                {
                    PieceManager.Instance.ClearSelection();
                    PieceManager.Instance.ClearMarkers();
                    PieceManager.Instance.ClearAllPieceBuffs();
                    if (PieceManager.Instance.gridManager != null)
                    {
                        PieceManager.Instance.gridManager.ClearLastMoveHighlight();
                        PieceManager.Instance.gridManager.ResetZoneHighlights();
                    }
                }
            }

            currentState = newState;
            OnStateChanged?.Invoke(newState);

            UpdateGameStartButton(newState);
            UpdateInventoryHoverTargets(newState);
            UpdateInventoryPanels(newState);

            if (newState == GameState.GameOver && gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }

            if (stateActions.TryGetValue(newState, out var action))
            {
                action?.Invoke();
            }
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            // 인스펙터에서 상태를 변경했을 때 이벤트 발생
            OnStateChanged?.Invoke(currentState);
            if (stateActions.TryGetValue(currentState, out var action))
            {
                action?.Invoke();
            }

            UpdateInventoryPanels(currentState);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeInventoryPanels();
        InitializeInventoryHoverTargets();
    }

    void Start()
    {
        InitializeInventoryPanels();
        InitializeInventoryHoverTargets();
        if (gameStartButton != null)
        {
            gameStartButton.onClick.AddListener(OnGameStartButtonClick);
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= HandleFlowStateChanged;
            GameManager.Instance.OnFlowStateChanged += HandleFlowStateChanged;
        }
        ChangeState(GameState.None);
    }
    
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= HandleFlowStateChanged;
            GameManager.Instance.OnFlowStateChanged += HandleFlowStateChanged;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= HandleFlowStateChanged;
        }
    }

    private void HandleFlowStateChanged(GameFlowState newFlowState)
    {
        UpdateInventoryHoverTargets(currentState);
        UpdateInventoryPanels(currentState);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (gameStartButton == null)
        {
            GameObject btnObj = GameObject.Find("GameStartButton");
            if (btnObj != null)
            {
                gameStartButton = btnObj.GetComponent<Button>();
                gameStartButton.onClick.RemoveListener(OnGameStartButtonClick);
                gameStartButton.onClick.AddListener(OnGameStartButtonClick);
            }
        }

        if (gameOverPanel == null)
        {
            gameOverPanel = GameObject.Find("GameOverPanel");
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }

        InitializeInventoryPanels();
        InitializeInventoryHoverTargets();
        UpdateInventoryHoverTargets(currentState);
        UpdateInventoryPanels(currentState);

        UpdateGameStartButton(currentState);
    }

    private void OnGameStartButtonClick()
    {
        if (HasPlayerPiecesOnBoard())
        {
            // Capture placement snapshot at the moment the player confirms placement
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.CapturePlacementSnapshot();
            }

            if (PieceSpawner.Instance == null || !PieceSpawner.Instance.TryStartGameWithEnemyReveal())
            {
                ChangeState(GameState.GamePlay);
            }
        }
        else
        {
            gameStartButton.transform.DOShakePosition(0.5f, new Vector3(5f, 1f, 0f), 30, 90f, false, true);
        }
    }

    private bool HasPlayerPiecesOnBoard()
    {
        if (PieceManager.Instance == null) return false;

        foreach (var piece in PieceManager.Instance.Pieces)
        {
            if (piece != null && !piece.IsEnemy) return true;
        }
        return false;
    }

    private void UpdateGameStartButton(GameState newState)
    {
        if (gameStartButton == null) return;

        gameStartButton.transform.DOKill();

        if (newState == GameState.Prepare)
        {
            gameStartButton.gameObject.SetActive(true);
            gameStartButton.transform.localScale = Vector3.zero;
            gameStartButton.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }
        else
        {
            if (gameStartButton.gameObject.activeSelf)
            {
                gameStartButton.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        if (gameStartButton != null)
                            gameStartButton.gameObject.SetActive(false);
                    });
            }
        }
    }

    private void InitializeInventoryPanels()
    {
        if (pieceInventoryPanel == null)
        {
            GameObject panelObject = GameObject.Find("PieceInventoryPanel");
            if (panelObject != null)
            {
                pieceInventoryPanel = panelObject;
            }
        }

        if (sealInventoryPanel == null)
        {
            GameObject panelObject = GameObject.Find("SealInventoryPanel");
            if (panelObject != null)
            {
                sealInventoryPanel = panelObject;
            }
        }

        if (pieceInventoryPanel != null && !pieceInventoryPanel.activeSelf)
        {
            pieceInventoryPanel.SetActive(true);
        }

        if (sealInventoryPanel != null && !sealInventoryPanel.activeSelf)
        {
            sealInventoryPanel.SetActive(true);
        }

        CacheInventoryPanel(pieceInventoryPanel, ref pieceInventoryRect, ref pieceInventoryCanvasGroup, ref pieceInventoryShownPosition, ref pieceInventoryHiddenPosition, ref pieceInventoryPositionsCached, slideLeft: true);
        CacheInventoryPanel(sealInventoryPanel, ref sealInventoryRect, ref sealInventoryCanvasGroup, ref sealInventoryShownPosition, ref sealInventoryHiddenPosition, ref sealInventoryPositionsCached, slideLeft: false);
    }

    private void InitializeInventoryHoverTargets()
    {
        SetupInventoryHoverTarget(pieceInventoryHoverImage, true);
        SetupInventoryHoverTarget(sealInventoryHoverImage, false);
    }

    private void SetupInventoryHoverTarget(Image hoverImage, bool isPieceInventory)
    {
        if (hoverImage == null)
        {
            return;
        }

        EventTrigger trigger = hoverImage.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = hoverImage.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers ??= new List<EventTrigger.Entry>();
        trigger.triggers.Clear();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => SetInventoryHoverState(isPieceInventory, true));
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => SetInventoryHoverState(isPieceInventory, false));
        trigger.triggers.Add(exit);
    }

    private void UpdateInventoryHoverTargets(GameState newState)
    {
        bool isShop = GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.Shop;
        bool enableHover = newState != GameState.Prepare && !isShop;

        if (newState == GameState.Prepare || isShop)
        {
            pieceInventoryHovered = false;
            sealInventoryHovered = false;
        }

        if (pieceInventoryHoverImage != null)
        {
            pieceInventoryHoverImage.raycastTarget = enableHover;
        }

        if (sealInventoryHoverImage != null)
        {
            sealInventoryHoverImage.raycastTarget = enableHover;
        }
    }

    private void SetInventoryHoverState(bool isPieceInventory, bool hovered)
    {
        bool isShop = GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.Shop;
        if (currentState == GameState.Prepare || isShop)
        {
            return;
        }

        if (isPieceInventory)
        {
            if (pieceInventoryHovered == hovered) return;
            pieceInventoryHovered = hovered;
        }
        else
        {
            if (sealInventoryHovered == hovered) return;
            sealInventoryHovered = hovered;
        }

        UpdateInventoryPanels(currentState);
    }

    public void RefreshInventoryPanels()
    {
        UpdateInventoryHoverTargets(currentState);
        UpdateInventoryPanels(currentState);
    }

    private void CacheInventoryPanel(
        GameObject panelObject,
        ref RectTransform panelRect,
        ref CanvasGroup panelCanvasGroup,
        ref Vector2 shownPosition,
        ref Vector2 hiddenPosition,
        ref bool positionsCached,
        bool slideLeft)
    {
        if (panelObject == null)
        {
            return;
        }

        if (panelRect == null)
        {
            panelRect = panelObject.GetComponent<RectTransform>();
        }

        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = panelObject.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = panelObject.AddComponent<CanvasGroup>();
            }
        }

        if (!positionsCached && panelRect != null)
        {
            shownPosition = panelRect.anchoredPosition;
            hiddenPosition = shownPosition + (slideLeft ? Vector2.left : Vector2.right) * inventorySlideDistance;
            positionsCached = true;
        }
    }

    private void UpdateInventoryPanels(GameState newState)
    {
        bool isShop = GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.Shop;

        if (newState == GameState.Prepare || isShop)
        {
            pieceInventoryHovered = false;
            sealInventoryHovered = false;
        }

        // Keep inventory visible during battle states (Prepare, GamePlay, Win, GameOver, Cleanup) or Shop flow
        bool isBattleState = newState == GameState.Prepare || newState == GameState.GamePlay || newState == GameState.Win || newState == GameState.GameOver || newState == GameState.Cleanup;
        bool pieceShouldShow = isBattleState || isShop || pieceInventoryHovered;
        bool sealShouldShow = isBattleState || isShop || sealInventoryHovered;

        SetInventoryPanelVisible(pieceInventoryPanel, pieceInventoryRect, pieceInventoryCanvasGroup, pieceInventoryShownPosition, pieceInventoryHiddenPosition, ref pieceInventorySequence, pieceShouldShow);
        SetInventoryPanelVisible(sealInventoryPanel, sealInventoryRect, sealInventoryCanvasGroup, sealInventoryShownPosition, sealInventoryHiddenPosition, ref sealInventorySequence, sealShouldShow);

        if (sealShouldShow)
        {
            SealInventoryPanel sealPanel = sealInventoryPanel != null ? sealInventoryPanel.GetComponent<SealInventoryPanel>() : FindFirstObjectByType<SealInventoryPanel>();
            sealPanel?.RefreshSealItems();
        }
    }

    private void SetInventoryPanelVisible(
        GameObject panelObject,
        RectTransform panelRect,
        CanvasGroup panelCanvasGroup,
        Vector2 shownPosition,
        Vector2 hiddenPosition,
        ref Sequence panelSequence,
        bool visible)
    {
        if (panelObject == null || panelRect == null || panelCanvasGroup == null)
        {
            return;
        }

        if (panelSequence != null)
        {
            panelSequence.Kill();
            panelSequence = null;
        }
        panelRect.DOKill();
        panelCanvasGroup.DOKill();

        // Always ensure the panel GameObject remains active so slots and events function properly
        if (!panelObject.activeSelf)
        {
            panelObject.SetActive(true);
        }

        if (visible)
        {
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;

            panelSequence = DOTween.Sequence().SetLink(panelObject);
            panelSequence.Join(panelRect.DOAnchorPos(shownPosition, inventorySlideDuration).SetEase(Ease.OutCubic));
            panelSequence.Join(panelCanvasGroup.DOFade(1f, inventorySlideDuration).SetEase(Ease.OutCubic));
            return;
        }

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        panelSequence = DOTween.Sequence().SetLink(panelObject);
        panelSequence.Join(panelRect.DOAnchorPos(hiddenPosition, inventorySlideDuration).SetEase(Ease.InCubic));
        panelSequence.Join(panelCanvasGroup.DOFade(0f, inventorySlideDuration).SetEase(Ease.InCubic));
        // NOTE: SetActive(false) is intentionally avoided so inventory slots remain discoverable.
    }
}
