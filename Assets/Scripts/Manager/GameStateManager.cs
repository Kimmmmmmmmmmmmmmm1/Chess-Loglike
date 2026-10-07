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
            if ((newState == GameState.Win || newState == GameState.GameOver) && currentState != GameState.GamePlay)
            {
                return;
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
    }

    void Start()
    {
        ChangeState(GameState.None);
        gameStartButton.onClick.AddListener(OnGameStartButtonClick);
    }
    
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
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

            ChangeState(GameState.GamePlay);
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
        bool enableHover = newState != GameState.Prepare;

        if (newState == GameState.Prepare)
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
        if (currentState == GameState.Prepare)
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
        if (newState == GameState.Prepare)
        {
            pieceInventoryHovered = false;
            sealInventoryHovered = false;
        }

        bool pieceShouldShow = newState == GameState.Prepare || pieceInventoryHovered;
        bool sealShouldShow = newState == GameState.Prepare || sealInventoryHovered;

        SetInventoryPanelVisible(pieceInventoryPanel, pieceInventoryRect, pieceInventoryCanvasGroup, pieceInventoryShownPosition, pieceInventoryHiddenPosition, pieceShouldShow);
        SetInventoryPanelVisible(sealInventoryPanel, sealInventoryRect, sealInventoryCanvasGroup, sealInventoryShownPosition, sealInventoryHiddenPosition, sealShouldShow);

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
        bool visible)
    {
        if (panelObject == null || panelRect == null || panelCanvasGroup == null)
        {
            return;
        }

        panelRect.DOKill();
        panelCanvasGroup.DOKill();

        if (visible)
        {
            if (!panelObject.activeSelf)
            {
                panelObject.SetActive(true);
            }

            panelRect.anchoredPosition = hiddenPosition;
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;

            panelRect.DOAnchorPos(shownPosition, inventorySlideDuration).SetEase(Ease.OutCubic).SetLink(panelObject);
            panelCanvasGroup.DOFade(1f, inventorySlideDuration).SetEase(Ease.OutCubic).SetLink(panelObject);
            return;
        }

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        if (!panelObject.activeSelf)
        {
            panelRect.anchoredPosition = hiddenPosition;
            panelCanvasGroup.alpha = 0f;
            return;
        }

        Sequence hideSequence = DOTween.Sequence().SetLink(panelObject);
        hideSequence.Join(panelRect.DOAnchorPos(hiddenPosition, inventorySlideDuration).SetEase(Ease.InCubic));
        hideSequence.Join(panelCanvasGroup.DOFade(0f, inventorySlideDuration).SetEase(Ease.InCubic));
        hideSequence.OnComplete(() =>
        {
            if (panelObject != null)
            {
                panelObject.SetActive(false);
            }
        });
    }
}
