using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public enum PieceType
{
    [InspectorName("킹 (King)")] King = 0,
    [InspectorName("룩 (Rook)")] Rook = 1,
    [InspectorName("나이트 (Knight)")] Knight = 2,
    [InspectorName("비숍 (Bishop)")] Bishop = 3,
    [InspectorName("퀸 (Queen)")] Queen = 4,
    [InspectorName("폰 (Pawn)")] Pawn = 5,
    [InspectorName("아마존 (Amazon)")] Amazon = 6,
    [InspectorName("챈슬러 (Chancellor)")] Chancellor = 7,
    [InspectorName("아치비숍 (Archbishop)")] Archbishop = 8,
    [InspectorName("켄타우로스 (Centaur)")] Centaur = 9,
    [InspectorName("캐논 (Cannon)")] Cannon = 10,
    [InspectorName("그래스호퍼 (Grasshopper)")] Grasshopper = 11
}

public enum PieceLocation
{
    Board,
    Inventory
}

public enum PieceBuffType
{
    [InspectorName("보호")]
    Protection,
    [InspectorName("회피")]
    Evasion,
    [InspectorName("역공")]
    Counterattack,
    [InspectorName("무적")]
    Invincibility
}

public class PieceController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISealDropTarget
{
    private const int TooltipCellSizePercent = 125;

    public Vector2Int? gridPosition;
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject sellButtonPrefab;
    private SellPieceButton activeSellButton;

    [Header("Piece Settings")]
    [SerializeField] private PieceType pieceType = PieceType.Pawn;
    [SerializeField] private Image pieceImage;
    [SerializeField] private Image shadowImage;
    [SerializeField] private bool isEnemy;
    private CanvasGroup prepareVisibilityGroup;
        // PieceSpawner에서 호출할 초기화 함수
        public void Initialize(PieceType pieceType, bool isEnemy)
        {
            // OnEnable 등에서 이미 등록되었을 수 있으므로(기본값으로), 해제 후 다시 등록
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.UnregisterPiece(this);
            }

            this.pieceType = pieceType;
            this.isEnemy = isEnemy;
            if (CollectionManager.Instance != null)
            {
                if (!isEnemy)
                {
                    CollectionManager.Instance.RecordPiece(pieceType);
                }
                else
                {
                    CollectionManager.Instance.RecordPieceSeen(pieceType);
                }
            }
            ApplySprite();
            UpdateUiPosition(); // 초기화 시 위치 설정
            isInitialized = true;
            RegisterSelf();
            if (PieceManager.Instance != null)
            {
                SetHiddenDuringPrepare(isEnemy && PieceManager.Instance.IsEnemyTerritoryHiddenDuringPrepare);
            }
        }

    public PieceType Type => pieceType;
    public PieceType PieceType => pieceType;
    public int Rarity => GetRarityForType(pieceType);
    public Vector2Int? GridPosition => gridPosition;
    public bool IsEnemy => isEnemy;
    public Image PieceImage => pieceImage;
    public bool IsHidden
    {
        get
        {
            if (isEnemy && PieceManager.Instance != null && PieceManager.Instance.IsEnemyTerritoryHiddenDuringPrepare)
                return true;
            if (prepareVisibilityGroup != null && prepareVisibilityGroup.alpha <= 0.01f)
                return true;
            return false;
        }
    }

    public void SetHiddenDuringPrepare(bool hidden)
    {
        if (prepareVisibilityGroup == null)
        {
            prepareVisibilityGroup = GetComponent<CanvasGroup>();
            if (prepareVisibilityGroup == null)
            {
                prepareVisibilityGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        prepareVisibilityGroup.alpha = hidden ? 0f : 1f;
        prepareVisibilityGroup.interactable = !hidden;
        prepareVisibilityGroup.blocksRaycasts = !hidden;
    }

    [Header("State")]
    [SerializeField] private PieceLocation currentLocation = PieceLocation.Board;
    [SerializeField] private bool promotedByMedalThisStage = false;
    [SerializeField] private bool convertedByOthelloThisBattle = false;
    [Header("Buff Stacks")]
    [SerializeField] private int protectionStacks;
    [SerializeField] private int evasionStacks;
    [SerializeField] private int counterattackStacks;
    [Tooltip("무적 턴 수. 0은 비활성, -1은 무한 지속입니다.")]
    [SerializeField] private int invincibilityTurns;
    private readonly Image[] buffAuraImages = new Image[4];
    private static Sprite buffAuraSprite;
    public PieceLocation CurrentLocation => currentLocation;
    public int ProtectionStacks => protectionStacks;
    public int EvasionStacks => evasionStacks;
    public int CounterattackStacks => counterattackStacks;
    public int InvincibilityTurns => invincibilityTurns;
    public bool IsInvincible
    {
        get
        {
            if (invincibilityTurns != 0) return true;
            if (equippedSeals != null)
            {
                for (int i = 0; i < equippedSeals.Count; i++)
                {
                    if (equippedSeals[i] != null && equippedSeals[i].IsInvincible) return true;
                }
            }
            return false;
        }
    }

    [Header("Drag Wobble Settings")]
    [SerializeField] private float maxTiltAngle = 30f;
    [SerializeField] private float tiltSensitivity = 0.5f;
    [SerializeField] private float tiltSmoothTime = 0.1f;

    [Header("Return Animation")]
    [SerializeField] private float returnDuration = 0.3f;
    [SerializeField] private Ease returnEase = Ease.OutBack;

    [Header("Place Animation")]
    [SerializeField] private float placeLiftHeightMultiplier = 0.3f;
    [SerializeField] private float placeDuration = 0.4f;
    [SerializeField] private Ease placeEase = Ease.OutBounce;
    [SerializeField] private Vector2 squashScale = new Vector2(1.05f, 0.95f);
    [SerializeField] private Vector2 stretchScale = new Vector2(0.95f, 1.05f);
    [SerializeField] private float squashDuration = 0.15f;

    [Header("Prepare Hover")]
    [SerializeField] private float hoverLiftAmount = 10f;
    [SerializeField] private float hoverDuration = 0.2f;
    private UnityEngine.UI.Shadow shadowComponent;

    [Header("Threatened Animation")]
    [SerializeField] private float startleJumpPower = 20f;
    [SerializeField] private float startleDuration = 0.5f;
    [SerializeField] private float threatenedShakeStrength = 0.3f;
    [SerializeField] private int shakeFrameInterval = 8;

    [Header("UI Colors")]
    [SerializeField] private Color allyColor = Color.red;
    [SerializeField] private Color enemyColor = Color.blue;

    [Header("Feedback")]
    [SerializeField] private Color invalidZoneTint = new Color(1f, 0.3f, 0.3f, 0.7f);
    private Color originalColor = Color.white;

    [Header("Seal")]
    [SerializeField] private float sealIconSize = 20f;
    [SerializeField] private GameObject sealIconPrefab; // 인장 아이콘 프리팹 연결
    [SerializeField] private float sealIconSpacing = 10f;
    [SerializeField] private Vector2 sealIconOffset = new Vector2(20f, 20f);
    [SerializeField] private float duplicateSealCycleInterval = 1.2f;
    [SerializeField] private float duplicateSealSwitchDuration = 0.18f;
    [SerializeField] private List<SealBase> equippedSeals = new List<SealBase>();
    public List<SealBase> EquippedSeals => equippedSeals;
    public SealData PrimarySealData => (equippedSeals != null && equippedSeals.Count > 0 && equippedSeals[0] != null) ? equippedSeals[0].Data : null;

    /// <summary>
    /// 기물에 장착된 모든 유효한 인장 데이터(SealData) 리스트를 반환합니다.
    /// </summary>
    public List<SealData> GetAllEquippedSealData()
    {
        var list = new List<SealData>();
        if (equippedSeals != null)
        {
            foreach (var seal in equippedSeals)
            {
                if (seal != null && seal.Data != null)
                {
                    list.Add(seal.Data);
                }
            }
        }
        return list;
    }
    public event System.Action<PieceController, SealData, int, int> OnEquippedSealVisualChanged;
    private SealData currentDisplaySealData;
    private int currentDisplaySealIndex;
    public SealData CurrentDisplaySealData => currentDisplaySealData != null ? currentDisplaySealData : PrimarySealData;
    public int CurrentDisplaySealIndex => currentDisplaySealIndex;
    private Coroutine duplicateSealCycleCoroutine;

    public Vector2 GetSealIconOffset()
    {
        return sealIconOffset != Vector2.zero ? sealIconOffset : new Vector2(sealIconSize, sealIconSize);
    }

    private RectTransform rectTransform;
    private Vector2 originalAnchoredPosition;
    private Canvas parentCanvas;
    private bool isDragging;
    private Vector2 previousDragPosition;
    private Quaternion originalRotation;
    private Tweener rotationTween;
    private Tweener positionTween;
    private Tweener scaleTween;
    private Tween threatenedTween;
    private bool isThreatened;
    private Vector3 originalScale;
    private static bool isAnyDragging;

    private Transform originalParent;
    public Transform OriginalParent => originalParent;
    public void SetOriginalParent(Transform parent)
    {
        originalParent = parent;
    }
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI asciiArtText;
    private static Sprite terminalBlockSprite;
    private ButtonTweenAnimation buttonTween;
    private Image hitBoxImage;

    private bool isInitialized = false;
    public static bool IsAnyDragging => isAnyDragging;
    private bool synthesisDropHandled = false;

    private bool isStartling = false;
    private int shakeFrame = 0;
    private readonly Vector2[] shakeDirections = new Vector2[] {
        new Vector2(1, 1),   // 우상
        new Vector2(-1, -1), // 좌하
        new Vector2(-1, 1),  // 좌상
        new Vector2(1, -1)   // 우하
    };

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        originalRotation = rectTransform.localRotation;
        originalScale = rectTransform.localScale;

        buttonTween = GetComponentInChildren<ButtonTweenAnimation>();

        hitBoxImage = GetComponent<Image>();
        if (hitBoxImage == null)
        {
            hitBoxImage = gameObject.AddComponent<Image>();
        }
        hitBoxImage.color = Color.clear;
        hitBoxImage.raycastTarget = true;

        if (selectButton == null)
        {
            selectButton = GetComponentInChildren<Button>(true);
        }

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(SelectSelf);
        }

        if (pieceImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img != hitBoxImage)
                {
                    pieceImage = img;
                    break;
                }
            }
        }

        if (pieceImage != null)
        {
            if (pieceImage != hitBoxImage)
            {
                pieceImage.raycastTarget = false;
            }

            originalColor = pieceImage.color;

            // 그림자용 자식 오브젝트를 별도로 생성/관리
            if (shadowImage != null)
            {
                shadowImage.sprite = pieceImage.sprite;
                shadowImage.raycastTarget = false;
                RectTransform shadowRT = shadowImage.rectTransform;
                RectTransform pieceRT = pieceImage.rectTransform;
                shadowRT.anchorMin = pieceRT.anchorMin;
                shadowRT.anchorMax = pieceRT.anchorMax;
                shadowRT.pivot = pieceRT.pivot;
                shadowRT.sizeDelta = pieceRT.sizeDelta;
                shadowRT.anchoredPosition = pieceRT.anchoredPosition;
                shadowImage.transform.SetAsFirstSibling(); // 기물 이미지보다 뒤에 렌더링

                shadowComponent = shadowImage.GetComponent<UnityEngine.UI.Shadow>();
                if (shadowComponent == null)
                {
                    shadowComponent = shadowImage.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                }
                shadowComponent.effectColor = new Color(0, 0, 0, 0.5f);
                shadowComponent.effectDistance = new Vector2(0, -5);
                shadowComponent.enabled = false;
            }
        }

        CreateBuffAuraVisuals();
        RefreshBuffAuraVisuals();

        if (GetComponent<EnemyPieceController>() != null)
        {
            isEnemy = true;
        }

        nameText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (nameText != null) nameText.enabled = false;
    }

    public void MarkAsEnemy()
    {
        isEnemy = true;
    }

    private void Start()
    {
        if (!isInitialized)
        {
            UpdateUiPosition();
            ApplySprite();
        }
        RegisterSelf();
    }

    private void OnEnable()
    {
        RegisterSelf();
        if (equippedSeals != null && equippedSeals.Count > 1 && duplicateSealCycleCoroutine == null)
        {
            RefreshEquippedSealVisuals();
        }
    }

    private void OnDisable()
    {
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.UnregisterPiece(this);
        }

        if (activeSellButton != null)
        {
            activeSellButton.Close();
            activeSellButton = null;
        }

        if (nameText != null)
        {
            nameText.transform.DOKill();
            nameText.enabled = false;
        }
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }

        if (shadowComponent != null)
        {
            shadowComponent.enabled = false;
        }
    }

    private void RegisterSelf()
    {
        if (PieceManager.Instance != null && currentLocation == PieceLocation.Board)
        {
            PieceManager.Instance.RegisterPiece(this);
        }
    }

    private void Update()
    {
        bool isGamePlay = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay;

        if (isThreatened && !isDragging && currentLocation == PieceLocation.Board && isGamePlay)
        {
            // 알까기 물리 시뮬레이션 중에는 위치 강제 고정(떨림)을 수행하지 않음
            if (PieceFlickLauncher.IsPhysicsActive) return;

            // 놀라는 애니메이션 중에는 떨지 않음
            if (isStartling) return;

            // 매 프레임 움직이면 너무 빠르므로 간격을 둠
            if (Time.frameCount % shakeFrameInterval != 0) return;

            // 이동 애니메이션 중에는 떨지 않음
            if (positionTween != null && positionTween.IsActive()) return;

            if (PieceManager.Instance != null && PieceManager.Instance.gridManager != null && gridPosition.HasValue)
            {
                Vector2 basePos = PieceManager.Instance.gridManager.GridToUiPosition(gridPosition.Value);
                Vector2 offset = shakeDirections[shakeFrame % 4] * threatenedShakeStrength;
                rectTransform.anchoredPosition = basePos + offset;
                shakeFrame++;
            }
        }
    }

    private void ApplySprite()
    {
        if (pieceImage == null || PieceManager.Instance == null)
        {
            return;
        }

        Sprite sprite = isEnemy ? PieceManager.Instance.GetEnemySpriteFor(pieceType) : PieceManager.Instance.GetSpriteFor(pieceType);
        if (sprite != null)
        {
            pieceImage.sprite = sprite;
            pieceImage.preserveAspect = true;

            if (shadowImage != null)
            {
                shadowImage.sprite = sprite;
                shadowImage.preserveAspect = true;
            }
        }

    }

    public static string GetShortAsciiSymbol(PieceType type)
    {
        return type switch
        {
            PieceType.King => "[K]",
            PieceType.Queen => "[Q]",
            PieceType.Rook => "[R]",
            PieceType.Bishop => "[B]",
            PieceType.Knight => "[N]",
            PieceType.Pawn => "[P]",
            PieceType.Amazon => "[A]",
            PieceType.Chancellor => "[C]",
            PieceType.Archbishop => "[AB]",
            PieceType.Centaur => "[T]",
            PieceType.Cannon => "[CN]",
            PieceType.Grasshopper => "[G]",
            _ => "[?]"
        };
    }

    public static string GetAsciiSymbol(PieceType type, bool enemy = false)
    {
        return type switch
        {
            PieceType.King => enemy ? "<K!>" : "{K}",
            PieceType.Queen => enemy ? "[Q!]" : "[Q]",
            PieceType.Rook => enemy ? "[R!]" : "[R]",
            PieceType.Bishop => enemy ? "<B!>" : "<B>",
            PieceType.Knight => enemy ? "/N!\\" : "/N\\",
            PieceType.Pawn => enemy ? "(P!)" : "(P)",
            PieceType.Amazon => enemy ? "{A!}" : "{A}",
            PieceType.Chancellor => enemy ? "[C!]" : "[C]",
            PieceType.Archbishop => enemy ? "<AB!>" : "<AB>",
            PieceType.Centaur => enemy ? "{T!}" : "{T}",
            PieceType.Cannon => enemy ? "[CN!]" : "[CN]",
            PieceType.Grasshopper => enemy ? "<G!>" : "<G>",
            _ => "[?]"
        };
    }

    public static string GetAsciiBlock(PieceType type, bool enemy = false)
    {
        string sym = GetAsciiSymbol(type, enemy);
        string tag = type switch
        {
            PieceType.King => "KING",
            PieceType.Queen => "QUEEN",
            PieceType.Rook => "ROOK",
            PieceType.Bishop => "BSHP",
            PieceType.Knight => "KNGT",
            PieceType.Pawn => "PAWN",
            PieceType.Amazon => "AMZN",
            PieceType.Chancellor => "CHNC",
            PieceType.Archbishop => "ARCH",
            PieceType.Centaur => "CNTR",
            PieceType.Cannon => "CANN",
            PieceType.Grasshopper => "GRAS",
            _ => "PROC"
        };
        return $"{sym}\n{tag}";
    }

    public void SelectSelf()
    {
        if (isDragging)
        {
            return;
        }

        if (isEnemy)
        {
            return;
        }

        bool isPrepare = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;
        bool isShop = GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.Shop;

        // 준비/상점 단계가 아닐 때(GamePlay 등)는 내 턴이 아니거나 프로모션 연출 중이면 선택 불가
        if (!isPrepare && !isShop)
        {
            if (TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn) return;
            if (PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsPromoting) return;
        }

        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.SelectPiece(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Color uiColor = isEnemy ? enemyColor : allyColor;
        bool isPrepare = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;

        if (nameText != null)
        {
            nameText.text = GetPieceNameForType(pieceType);
            nameText.color = uiColor;
            nameText.enabled = true;

            // 띠용 효과 (Scale 0 -> 1)
            nameText.transform.DOKill();
            nameText.transform.localScale = Vector3.zero;
            float scaleP = (pieceType == PieceType.King) ? 1.5f : 1f;
            nameText.transform.DOScale(Vector3.one * scaleP, 0.3f).SetEase(Ease.OutBack);
        }

        if (isDragging || isAnyDragging)
        {
            return;
        }

        if (buttonTween != null)
        {
            buttonTween.SetHoverState(true);
        }

        // 플레이어 기물 호버 효과 (띄우기 + 그림자)
        if (!isEnemy)
        {
            if (buttonTween != null)
            {
                buttonTween.MoveTo(buttonTween.OriginalPosition + Vector2.up * hoverLiftAmount, hoverDuration, Ease.OutQuad);
            }

            if (shadowComponent != null) shadowComponent.enabled = true;
        }

        // 상점 상태일 때 판매 버튼 표시
        if (GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.Shop)
        {
            if (!isEnemy && sellButtonPrefab != null)
            {
                if (activeSellButton == null)
                {
                    GameObject btnObj = Instantiate(sellButtonPrefab, transform.parent);
                    btnObj.transform.position = transform.position;

                    activeSellButton = btnObj.GetComponent<SellPieceButton>();
                    if (activeSellButton != null)
                        activeSellButton.Initialize(this);
                }
                else
                {
                    // 이미 버튼이 있다면 닫기 취소 (다시 돌아온 경우)
                    activeSellButton.CancelClose();
                }
            }
        }

        if (isEnemy)
        {
            if (PieceManager.Instance != null && currentLocation == PieceLocation.Board)
            {
                if (PieceFlickLauncher.IsFlickMode)
                {
                    PieceManager.Instance.ShowEnemyPieceMoveMarkers(this);
                }
                else
                {
                    PieceManager.Instance.SelectPiece(this);
                }
            }
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.ShowPieceTooltip(pieceType, PrimarySealData, transform.position, gameObject, true);
            }
            return;
        }

        // 인벤토리에 있을 때는 모든 플로우에서 이동범위 패턴 표시 (배틀, 상점 등)
        if (currentLocation == PieceLocation.Inventory)
        {
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.ShowPieceTooltip(pieceType, PrimarySealData, transform.position, gameObject, false);
            }
            return;
        }

        if (PieceManager.Instance != null && currentLocation == PieceLocation.Board)
        {
            PieceManager.Instance.SelectPiece(this);
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.ShowPieceTooltip(pieceType, PrimarySealData, transform.position, gameObject, false);
            }
            return;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (nameText != null)
        {
            // 사라질 때 애니메이션 (Scale 1 -> 0)
            nameText.transform.DOKill();
            nameText.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack)
                .OnComplete(() => nameText.enabled = false);
        }

        if (isDragging)
        {
            return;
        }

        if (buttonTween != null)
        {
            buttonTween.SetHoverState(false);
        }

        // 플레이어 기물 호버 해제 (원위치 + 그림자 끄기)
        if (!isEnemy)
        {
            if (buttonTween != null)
            {
                buttonTween.ResetPosition(hoverDuration, Ease.OutQuad);
            }

            if (shadowComponent != null)
            {
                shadowComponent.enabled = false;
            }
        }

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }

        // 상점 상태일 때 판매 버튼 숨기기 처리
        if (activeSellButton != null)
        {
            // 마우스가 판매 버튼으로 이동했다면 숨기지 않음
            if (eventData.pointerEnter != null && (eventData.pointerEnter == activeSellButton.gameObject || eventData.pointerEnter.transform.IsChildOf(activeSellButton.transform)))
            {
                return;
            }
            // 즉시 닫지 않고 지연을 줌 (버튼으로 이동할 시간 확보)
            activeSellButton.Close(0.25f);
        }

        if (isEnemy && PieceFlickLauncher.IsFlickMode)
        {
            PieceManager.Instance?.HideMoveMarkers();
        }

        if (PieceManager.Instance != null && PieceManager.Instance.IsSelected(this))
        {
            PieceManager.Instance.ClearSelection();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        bool isPrepare = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;
        bool isShopOrWorkshop = GameManager.Instance != null && (GameManager.Instance.CurrentFlowState == GameFlowState.Shop || GameManager.Instance.CurrentFlowState == GameFlowState.WorkShop);
        if (!isPrepare && !isShopOrWorkshop)
        {
            if (TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn) return;
            if (PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsPromoting) return;
        }

        if (buttonTween != null) buttonTween.OnPointerDown(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (buttonTween != null) buttonTween.OnPointerUp(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (PieceFlickLauncher.IsFlickMode)
        {
            return;
        }

        // 액티브 유물 대상 선택 중이면 클릭을 유물 대상 지정으로 사용 (우클릭은 취소용)
        if (ActiveArtifactController.IsTargeting)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                ActiveArtifactController.TryHandlePieceClick(this);
            }
            return;
        }

        if (ShotgunKingSystem.TryHandleDaggerAttack(this, eventData.button))
        {
            eventData.Use();
            return;
        }

        if (gridPosition.HasValue && ShotgunKingSystem.TryHandleShotAimClick(gridPosition.Value, eventData.button))
        {
            eventData.Use();
            return;
        }

        if (!isDragging && !isAnyDragging && eventData.button == PointerEventData.InputButton.Left) SelectSelf();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (PieceFlickLauncher.IsFlickMode)
        {
            return;
        }

        if (isEnemy)
        {
            return;
        }

        // 액티브 유물 대상 선택 중에는 드래그 불가
        if (ActiveArtifactController.IsTargeting)
        {
            return;
        }

        // canvasGroup.interactable이 false면 드래그 불가 (애니메이션 중 등)
        if (canvasGroup != null && !canvasGroup.interactable)
        {
            return;
        }

        bool isPrepare = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;
        bool isShopOrWorkshop = GameManager.Instance != null && (GameManager.Instance.CurrentFlowState == GameFlowState.Shop || GameManager.Instance.CurrentFlowState == GameFlowState.WorkShop);

        // 전투 진행 중(GamePlay)에는 내 턴이 아니거나 프로모션 연출 중이면 절대 드래그 불가
        if (!isPrepare && !isShopOrWorkshop)
        {
            if (TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn)
            {
                return;
            }
            if (PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsPromoting)
            {
                return;
            }
        }

        // 현재 부모가 SynthesisSlot이면, 슬롯에서 기물을 제거하고 piece1/piece2를 null로 업데이트
        SynthesisSlot synthesisSlot = transform.parent?.GetComponent<SynthesisSlot>();
        if (synthesisSlot != null)
        {
            synthesisSlot.RemovePiece();
        }

        // 부모가 InventorySlot인 경우를 인벤토리로 판단 (currentLocation보다 확실함)
        bool isInInventorySlot = transform.parent != null && transform.parent.GetComponent<InventorySlot>() != null;

        // 준비/상점/작업장 단계가 아니고 인벤토리에 있으면 드래그 불가능
        if (!isPrepare && !isShopOrWorkshop && (currentLocation == PieceLocation.Inventory || isInInventorySlot))
        {
            return;
        }

        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.SelectPiece(this);
            if (isPrepare)
            {
                PieceManager.Instance.HideMoveMarkers();
            }
        }

        if (activeSellButton != null)
        {
            activeSellButton.Close();
            activeSellButton = null;
        }

        positionTween?.Kill();

        if (buttonTween != null)
        {
            buttonTween.ResetPosition(0.1f, Ease.Linear);
        }

        if (shadowComponent != null)
        {
            shadowComponent.enabled = isPrepare && !isEnemy;
        }

        // 원래 부모 저장 (인벤토리 슬롯 등)
        originalParent = transform.parent;

        // 인벤토리에 있는 경우에만 드래그 중에 보드 부모로 이동
        // (이 시점에는 Prepare/Shop/WorkShop 상태인 것이 보장됨)
        if (currentLocation == PieceLocation.Inventory && PieceSpawner.Instance != null && PieceSpawner.Instance.piecesParent != null)
        {
            transform.SetParent(PieceSpawner.Instance.piecesParent, true);
        }
        else
        {
            // 드래그 시작 시 해당 기물을 부모의 마지막 자식으로 이동시켜 가장 위에 그려지게 함
            transform.SetAsLastSibling();
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        // 원래 위치 계산 (흔들림이나 호버 등 오프셋을 배제하고 그리드 또는 슬롯의 정확한 위치 사용)
        if (currentLocation == PieceLocation.Board && PieceManager.Instance != null && PieceManager.Instance.gridManager != null && gridPosition.HasValue)
        {
            originalAnchoredPosition = PieceManager.Instance.gridManager.GridToUiPosition(gridPosition.Value);
        }
        else if (currentLocation == PieceLocation.Inventory || (originalParent != null && originalParent.GetComponent<InventorySlot>() != null))
        {
            // 인벤토리에서는 부모가 바뀌므로 슬롯 기준 시작 위치를 0으로 간주
            originalAnchoredPosition = Vector2.zero;
        }
        else
        {
            originalAnchoredPosition = rectTransform.anchoredPosition;
        }

        previousDragPosition = eventData.position;
        isDragging = true;
        isAnyDragging = true;
        synthesisDropHandled = false;
        UpdateThreatenedVisuals();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isEnemy)
        {
            return;
        }

        // OnBeginDrag가 실행되지 않았으면 OnDrag도 처리하지 않음
        if (!isDragging)
        {
            return;
        }

        if (rectTransform == null || parentCanvas == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform.parent as RectTransform,
            eventData.position,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out Vector2 localPoint
        );

        rectTransform.anchoredPosition = localPoint;

        // Prepare 상태일 때 비유효 구역 피드백
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            CheckInvalidZoneFeedback();
        }

        // 드래그 중 기물 흔들림(틸트) 방지 - 정자세 유지
        rotationTween?.Kill();
        rectTransform.localRotation = originalRotation;

        previousDragPosition = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isEnemy)
        {
            return;
        }

        // OnBeginDrag가 실행되지 않았으면 OnEndDrag도 처리하지 않음
        if (!isDragging)
        {
            return;
        }

        // 합성 슬롯 OnDrop에서 이미 처리되었으면 추가 복귀/이동 로직을 타지 않음
        if (synthesisDropHandled)
        {
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
            }
            isDragging = false;
            isAnyDragging = false;
            synthesisDropHandled = false;
            return;
        }

        // 드롭 대상이 SynthesisSlot(기물 합성)인 경우 OnDrop이 처리했으므로 여기서는 기본 복귀 로직을 건너뜀
        if (eventData.pointerEnter != null)
        {
            SynthesisSlot synthesisSlot = eventData.pointerEnter.GetComponentInParent<SynthesisSlot>();
            if (synthesisSlot != null)
            {
                // 합성 슬롯이 드롭을 처리했으므로 여기서는 추가 처리 없음
                if (canvasGroup != null)
                {
                    canvasGroup.blocksRaycasts = true;
                }
                isDragging = false;
                isAnyDragging = false;
                return;
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        isDragging = false;
        isAnyDragging = false;

        if (shadowComponent != null)
        {
            shadowComponent.enabled = false;
        }

        ResetPieceColor();

        rotationTween?.Kill();
        rotationTween = rectTransform.DOLocalRotateQuaternion(originalRotation, 0.2f).SetEase(Ease.OutBack);

        if (PieceManager.Instance == null)
        {
            ReturnToOriginalPosition();
            return;
        }

        // Drag Sequence
        bool isPrepare = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;
        bool isShopOrWorkshop = GameManager.Instance != null && (GameManager.Instance.CurrentFlowState == GameFlowState.Shop || GameManager.Instance.CurrentFlowState == GameFlowState.WorkShop);

        if (isPrepare || isShopOrWorkshop)
        {
            // 1. 인벤토리로 드롭 시도 (이동, 교체, 보드->인벤토리 모두 처리)
            if (TryDropToInventory(eventData)) return;

            // 드래그 아이콘의 최종 위치를 기준으로 그리드 좌표 계산
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                PieceManager.Instance.gridManager.gridCellsParent,
                eventData.position,
                parentCanvas.worldCamera,
                out localPoint);
            Vector2Int? targetGridPos = PieceManager.Instance.gridManager.GetNearestGridPosition(localPoint);

            // 유효한 그리드 위치이고, 플레이어 진영(설정된 범위 내)인 경우
            if (targetGridPos.HasValue && targetGridPos.Value.y <= PieceManager.Instance.PlayerPrepareMaxY)
            {
                PieceController targetPiece = PieceManager.Instance.GetPieceAt(targetGridPos.Value);

                // 빈 칸이면 이동
                if (targetPiece == null)
                {
                    // 인벤토리에서 보드로 이동하는 경우, 기물 한계치 확인
                    if (currentLocation == PieceLocation.Inventory)
                    {
                        if (PieceManager.Instance.GetPlayerPieceCountOnBoard() >= PieceManager.Instance.MaxPlayerPiecesOnBoard)
                        {
                            ReturnToOriginalPosition();
                            return; // 이동 중단
                        }
                    }
                    MoveToGrid(targetGridPos.Value);
                }
                // 다른 기물이 있으면 교체 (Swap) - 적 기물이 아닐 때만
                else if (!targetPiece.IsEnemy)
                {
                    // 1. 인벤토리 -> 장기판 교체
                    if (currentLocation == PieceLocation.Inventory)
                    {
                        Transform myOriginalSlot = originalParent;
                        if (myOriginalSlot != null)
                        {
                            // 타겟 기물 -> 인벤토리 (내 원래 슬롯)
                            targetPiece.MoveToInventory(myOriginalSlot);
                            // 내 기물 -> 장기판 (타겟 위치)
                            MoveToGrid(targetGridPos.Value);
                        }
                        else ReturnToOriginalPosition();
                    }
                    // 2. 장기판 -> 장기판 교체
                    else if (currentLocation == PieceLocation.Board)
                    {
                        Vector2Int myPrevPos = gridPosition.Value;
                        MoveToGrid(targetGridPos.Value); // 내 기물 -> 타겟 위치
                        targetPiece.MoveToGrid(myPrevPos); // 타겟 기물 -> 내 원래 위치
                    }
                }
                else ReturnToOriginalPosition();
            }
            else
            {
                ReturnToOriginalPosition();
            }

            if (currentLocation == PieceLocation.Board)
            {
                PieceManager.Instance.SelectPiece(this);
            }
            return;
        }
        else if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay)
        {
                // 내 턴이 아니거나 프로모션 연출 중이면 무조건 이동 취소
                if ((TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn) ||
                    (PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsPromoting))
                {
                    ReturnToOriginalPosition();
                    return;
                }

                MoveMarker targetMarker = PieceManager.Instance.GetMarkerAtPosition(eventData.position);
                if (targetMarker != null)
                {
                    if (!PieceManager.Instance.MoveSelectedTo(targetMarker.GridPosition))
                    {
                        ReturnToOriginalPosition();
                        PieceManager.Instance.SelectPiece(this);
                    }
                }
                else
                {
                    ReturnToOriginalPosition();
                    PieceManager.Instance.SelectPiece(this);
                }
        }
        else
        {
            ReturnToOriginalPosition();
        }
    }

    // IDropHandler 인터페이스의 OnDrop 메서드
    public void OnDrop(PointerEventData eventData)
    {
        // DraggableSeal.OnEndDrag에서 드롭 대상 판정과 처리를 한 번만 수행합니다.
    }

    // ISealDropTarget 인터페이스의 HandleSealDrop 메서드
    public void HandleSealDrop(DraggableSeal draggableSeal)
    {
        // 전투 준비 단계 또는 상점이 아니면 드롭 불가
        bool isPrepare = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;
        bool isShop = GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.Shop;
        if (!isPrepare && !isShop)
        {
            return;
        }

        // 인벤토리에 있는 기물에는 장착 불가
        if (currentLocation == PieceLocation.Inventory)
        {
            // 아무것도 하지 않고 반환하여, DraggableSeal의 OnEndDrag에서 실패 처리를 하도록 유도합니다.
            return;
        }

        SealData sealToEquip = draggableSeal.SealData;
        if (!isEnemy && pieceType == PieceType.King && ShotgunKingSystem.IsActive)
        {
            sealToEquip = ShotgunKingSystem.ConvertSeal(sealToEquip);
        }

        if (HasSeal(sealToEquip))
        {
            return;
        }

        // 1. 호환성 검사
        bool shotgunKingConversion = !isEnemy && pieceType == PieceType.King && ShotgunKingSystem.IsActive;
        if (!shotgunKingConversion && sealToEquip.compatiblePieces != null && sealToEquip.compatiblePieces.Count > 0)
        {
            if (!sealToEquip.compatiblePieces.Contains(this.pieceType))
            {
                // TODO: 실패 피드백 (사운드, 텍스트 등)
                return;
            }
        }

        // 2. 장착 시도 (SealManager를 통하지 않고 직접 처리)
        if (!EquipSeal(sealToEquip)) return;
        if (draggableSeal.SourcePiece == null && draggableSeal.OriginalInventoryIndex != -1)
        {
            SealInventory.Instance?.ClearSlot(draggableSeal.OriginalInventoryIndex);
            FindFirstObjectByType<SealInventoryPanel>()?.RefreshSealItems();
        }
        draggableSeal.MarkDropHandledSuccessfully();
    }

    public void MarkSynthesisDropHandled()
    {
        synthesisDropHandled = true;
    }

    private void ReturnToOriginalPosition()
    {
        positionTween?.Kill();
        rotationTween?.Kill();

        // 원본 아이콘을 다시 보이게 하고, 상호작용 가능하게 설정
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        // 애니메이션을 위해 최상단으로 렌더링 순서 조정
        transform.SetAsLastSibling();

        // 원래 부모로 복귀
        if (originalParent != null && transform.parent != originalParent)
        {
            transform.SetParent(originalParent, true);
        }

        Vector2 targetPosition = originalAnchoredPosition;
        if (currentLocation == PieceLocation.Board && PieceManager.Instance != null && PieceManager.Instance.gridManager != null && gridPosition.HasValue)
        {
            targetPosition = PieceManager.Instance.gridManager.GridToUiPosition(gridPosition.Value);
            originalAnchoredPosition = targetPosition;
        }
        else if (currentLocation == PieceLocation.Inventory || (originalParent != null && originalParent.GetComponent<InventorySlot>() != null))
        {
            targetPosition = Vector2.zero;
            originalAnchoredPosition = targetPosition;
        }

        rectTransform.DOAnchorPos(targetPosition, returnDuration)
            .SetEase(returnEase)
            .OnComplete(() =>
            {
                // 인벤토리 상태이고 부모가 실제로 바뀌었을 때만 원래 슬롯으로 복귀
                if (currentLocation == PieceLocation.Inventory && originalParent != null && transform.parent != originalParent)
                {
                    MoveToInventory(originalParent);
                }
                // 복귀 완료 후 SynthesisSlot이면 데이터 복구 (OnEndDrag에서 이미 처리됨)
                UpdateThreatenedVisuals();
            });

        rectTransform.DOLocalRotateQuaternion(originalRotation, returnDuration)
            .SetEase(returnEase)
            .OnComplete(() =>
                {
                    UpdateThreatenedVisuals();
                });
    }

    private bool TryDropToInventory(PointerEventData eventData)
    {
        GameObject hitObject = eventData.pointerEnter;
        if (hitObject == null) return false;

        InventorySlot slot = hitObject.GetComponent<InventorySlot>();
        if (slot == null)
        {
            slot = hitObject.GetComponentInParent<InventorySlot>();
        }

        if (slot != null)
        {
            return slot.TryAddPiece(this);
        }
        return false;
    }

    public void MoveToGrid(Vector2Int target)
    {
        PieceLocation previousLocation = currentLocation;
        Vector2Int? previousGridPosition = gridPosition;

        // 인벤토리에서 보드로 이동하는 경우 처리
        if (currentLocation == PieceLocation.Inventory)
        {
            // 인벤토리 데이터에서 제거
            if (PieceInventory.Instance != null)
            {
                PieceInventory.Instance.RemovePiece(pieceType);
            }

            if (PieceSpawner.Instance != null && PieceSpawner.Instance.piecesParent != null)
            {
                transform.SetParent(PieceSpawner.Instance.piecesParent);
            }

            // 위치와 상태를 먼저 설정
            currentLocation = PieceLocation.Board;
            gridPosition = target;

            // PieceManager에 다시 등록 (이제 제대로 카운트됨)
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.RegisterPiece(this);
            }
        }
        else
        {
            // 보드 위 이동인 경우 gridPosition만 업데이트
            gridPosition = target;
        }

        // 오셀로 유물이 활성화되어 있는 경우 착지 후 전향 및 게임 종료 판정 후에 프로모션을 체크하도록 지연
        if (!OthelloArtifactSystem.IsActive)
        {
            // 폰(Pawn)이 적 진영 끝 줄에 도달했는지 체크
            CheckSoldierPromotion();
        }

        // 이동하는 기물이 다른 기물들 위에 보이도록 순서를 가장 마지막으로 변경
        transform.SetAsLastSibling();
        AnimatePlacement();
        PlayMoveSfxIfMoved(previousLocation, previousGridPosition, target);

        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.MarkPiecePositionCacheDirty();
            PieceManager.Instance.UpdateThreatenedStatus();
        }
    }

    private void PlayMoveSfxIfMoved(PieceLocation previousLocation, Vector2Int? previousGridPosition, Vector2Int target)
    {
        if (!Application.isPlaying || SoundManager.Instance == null)
        {
            return;
        }

        bool movedFromInventory = previousLocation == PieceLocation.Inventory;
        bool movedOnBoard = previousGridPosition.HasValue && previousGridPosition.Value != target;

        if (movedFromInventory || movedOnBoard)
        {
            SoundManager.Instance.PlaySFX(SFXType.Move);
        }
    }

    public void CheckSoldierPromotion()
    {
        // 자신이 폰(Pawn)이 아니거나 그리드 위치가 없으면 return
        if (pieceType != PieceType.Pawn || !gridPosition.HasValue)
            return;

        if (GameManager.Instance == null || GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
            return;

        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
            return;

        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null)
            return;

        // 게임 끝 타이밍 체크: 상대 진영 기물이 0개이거나 게임 종료 조건에 도달한 경우 프로모션 안 됨
        if (!isEnemy && PieceManager.Instance.GetEnemyPieceCountOnBoard() == 0)
            return;

        if (isEnemy && PieceManager.Instance.GetPlayerPieceCountOnBoard() == 0)
            return;

        GridManager gridManager = PieceManager.Instance.gridManager;
        int maxY = gridManager.gridMinBounds.y + gridManager.boardHeight - 1;
        int minY = gridManager.gridMinBounds.y;
        int myY = gridPosition.Value.y;

        if (!isEnemy)
        {
            // 아군 폰이 적 진영 끝 줄에 도달하면 승급 패널 표시
            if (myY >= maxY)
            {
                if (PiecePromotionManager.Instance != null)
                {
                    PiecePromotionManager.Instance.ShowPromotionPanel(this);
                }
            }
        }
        else
        {
            // 적군 폰이 아군 진영 끝 줄에 도달하면 적군 승급(퀸) 처리
            if (myY <= minY)
            {
                if (PiecePromotionManager.Instance != null)
                {
                    PiecePromotionManager.Instance.PromoteEnemyPawn(this, PieceType.Queen);
                }
            }
        }
    }

    public Vector2Int? moveWaypoint = null;

    private void AnimatePlacement()
    {
        if (rectTransform == null || PieceManager.Instance == null || PieceManager.Instance.gridManager == null)
        {
            throw new System.Exception("GridManager reference is required in PieceManager.");
        }

        Vector2 targetPosition = PieceManager.Instance.gridManager.GridToUiPosition(gridPosition.Value);
        Vector2 currentPosition = rectTransform.anchoredPosition;
        float liftHeight = PieceManager.Instance.gridManager.cellSize.y * placeLiftHeightMultiplier;

        positionTween?.Kill();
        scaleTween?.Kill();

        Sequence placeSequence = DOTween.Sequence();

        if (moveWaypoint.HasValue)
        {
            Vector2 waypointUi = PieceManager.Instance.gridManager.GridToUiPosition(moveWaypoint.Value);
            moveWaypoint = null; // 소비

            // 1구간 (현재 -> 반사 지점)
            Vector2 mid1 = (currentPosition + waypointUi) / 2f + Vector2.up * (liftHeight * 0.7f);
            placeSequence.Append(rectTransform.DOAnchorPos(mid1, placeDuration * 0.2f).SetEase(Ease.OutQuad));
            placeSequence.Join(rectTransform.DOScale(new Vector3(stretchScale.x, stretchScale.y, 1f), placeDuration * 0.2f).SetEase(Ease.OutQuad));
            placeSequence.Append(rectTransform.DOAnchorPos(waypointUi, placeDuration * 0.25f).SetEase(Ease.Linear));

            // 반사 시 가벼운 임팩트 효과음
            placeSequence.AppendCallback(() =>
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySFX(SFXType.Click);
                }
            });

            // 2구간 (반사 지점 -> 최종 목표지점)
            Vector2 mid2 = (waypointUi + targetPosition) / 2f + Vector2.up * liftHeight;
            placeSequence.Append(rectTransform.DOAnchorPos(mid2, placeDuration * 0.25f).SetEase(Ease.OutQuad));
            placeSequence.Append(rectTransform.DOAnchorPos(targetPosition, placeDuration * 0.3f).SetEase(placeEase));
        }
        else
        {
            Vector2 midPoint = (currentPosition + targetPosition) / 2f + Vector2.up * liftHeight;

            // 위로 올라갈 때 약간 늘어남
            placeSequence.Append(rectTransform.DOAnchorPos(midPoint, placeDuration * 0.3f).SetEase(Ease.OutQuad));
            placeSequence.Join(rectTransform.DOScale(new Vector3(stretchScale.x, stretchScale.y, 1f), placeDuration * 0.3f).SetEase(Ease.OutQuad));

            // 아래로 내려올 때
            placeSequence.Append(rectTransform.DOAnchorPos(targetPosition, placeDuration * 0.5f).SetEase(placeEase));
        }

        // 착지 시 찌그러짐
        placeSequence.Append(rectTransform.DOScale(new Vector3(squashScale.x, squashScale.y, 1f), squashDuration).SetEase(Ease.OutQuad));

        // 원래 크기로 복귀 (통통 튀는 느낌)
        placeSequence.Append(rectTransform.DOScale(originalScale, squashDuration * 1.5f).SetEase(Ease.OutElastic));
    }

    public void MoveToInventory(Transform slotTransform, bool addToInventoryData = true)
    {
        if (slotTransform == null)
        {
            return;
        }

        if (currentLocation == PieceLocation.Inventory && transform.parent == slotTransform)
        {
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        // 보드에서 인벤토리로 이동하는 경우 PieceManager에서 등록 해제
        if (currentLocation == PieceLocation.Board)
        {
            // 인벤토리 데이터에 추가
            if (addToInventoryData && PieceInventory.Instance != null) PieceInventory.Instance.AddPiece(pieceType);

            // 장착된 인장은 SealInventory로 반환하고, 시각적 오브젝트는 인벤토리로 복귀 애니메이션 실행
            List<SealBase> sealsToProcess = new List<SealBase>(equippedSeals);
            equippedSeals.Clear(); // 즉시 리스트를 비워 중복 처리를 방지합니다.

            foreach (var sealBase in sealsToProcess)
            {
                if (sealBase == null) continue;
                if (sealBase is ConversionSeal || ConversionSeal.IsConversionSealData(sealBase.Data))
                {
                    if (sealBase.transform.parent != null) Destroy(sealBase.transform.parent.gameObject);
                    else Destroy(sealBase.gameObject);
                    continue;
                }

                // 1. 데이터 반환
                if (sealBase.Data != null && SealInventory.Instance != null)
                {
                    SealInventory.Instance.AddSeal(sealBase.Data);
                }

                // 2. 시각적 오브젝트 복귀 애니메이션 처리
                DraggableSeal draggable = sealBase.GetComponentInParent<DraggableSeal>();
                if (draggable != null)
                {
                    draggable.ReturnToSource();
                }
            }

            foreach (var sealBase in equippedSeals)
            {
                if (sealBase != null && sealBase.Data != null && !ConversionSeal.IsConversionSealData(sealBase.Data) && SealInventory.Instance != null)
                    SealInventory.Instance.AddSeal(sealBase.Data);
            }
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.UnregisterPiece(this);
            }
        }

        currentLocation = PieceLocation.Inventory;
        gridPosition = null;
        transform.SetParent(slotTransform);

        positionTween?.Kill();
        scaleTween?.Kill();
        rotationTween?.Kill();

        positionTween = rectTransform.DOAnchorPos(Vector2.zero, returnDuration).SetEase(returnEase);
        rotationTween = transform.DOLocalRotate(Vector3.zero, returnDuration).SetEase(returnEase);
        scaleTween = transform.DOScale(Vector3.one, returnDuration).SetEase(returnEase);
        UpdateThreatenedVisuals();

        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.UpdateThreatenedStatus();
        }
    }

    private void UpdateUiPosition()
    {
        if (rectTransform == null || PieceManager.Instance == null || PieceManager.Instance.gridManager == null || !gridPosition.HasValue)
        {
            return;
        }

        rectTransform.anchoredPosition = PieceManager.Instance.gridManager.GridToUiPosition(gridPosition.Value);
    }

    public bool IsOccupied(Vector2Int position)
    {
        if (PieceManager.Instance == null)
        {
            return false;
        }

        return PieceManager.Instance.GetPieceAt(position) != null;
    }

    private bool IsHiddenEnemyDuringPrepare(PieceController piece)
    {
        return piece != null &&
               piece.IsEnemy &&
               PieceManager.Instance != null &&
               PieceManager.Instance.IsEnemyTerritoryHiddenDuringPrepare;
    }

    private bool IsDestroyedCell(Vector2Int position)
    {
        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null)
        {
            return false;
        }

        GridManager gridManager = PieceManager.Instance.gridManager;
        GridCell cell = gridManager.GetGridCell(position);
        return cell != null && cell.isDestroyed;
    }

    public bool CanMoveTo(Vector2Int target)
    {
        if (!IsInBounds(target) || PieceManager.Instance == null)
        {
            return false;
        }

        // 파괴된 타일로는 이동 불가
        GridManager gridManager = PieceManager.Instance.gridManager != null
            ? PieceManager.Instance.gridManager
            : FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            GridCell cell = gridManager.GetGridCell(target);
            if (cell != null && cell.isDestroyed)
            {
                return false;
            }
        }

        PieceController pieceAt = PieceManager.Instance.GetPieceAt(target);
        if (IsHiddenEnemyDuringPrepare(pieceAt))
        {
            pieceAt = null;
        }

        if (pieceAt == null)
        {
            return true;
        }

        if (OthelloArtifactSystem.IsActive)
        {
            return false;
        }

        return pieceAt.IsEnemy != isEnemy;
    }

    public List<Vector2Int> GetCandidateMoves()
    {
        if (!gridPosition.HasValue) return new List<Vector2Int>();

        List<Vector2Int> moves;
        switch (pieceType)
        {
            case PieceType.King:
                moves = GetKingMoves();
                break;
            case PieceType.Queen:
                moves = GetQueenMoves();
                break;
            case PieceType.Rook:
                moves = GetRookMoves();
                break;
            case PieceType.Bishop:
                moves = GetBishopMoves();
                break;
            case PieceType.Knight:
                moves = GetKnightMoves();
                break;
            case PieceType.Amazon:
                moves = GetAmazonMoves();
                break;
            case PieceType.Chancellor:
                moves = GetChancellorMoves();
                break;
            case PieceType.Archbishop:
                moves = GetArchbishopMoves();
                break;
            case PieceType.Centaur:
                moves = GetCentaurMoves();
                break;
            case PieceType.Cannon:
                moves = GetCannonMoves();
                break;
            case PieceType.Grasshopper:
                moves = GetGrasshopperMoves();
                break;
            case PieceType.Pawn:
            default:
                moves = GetPawnMoves();
                break;
        }

        // 📌 Case A Hook: 코드 인젝션(Seal)들에게 이동 경로 수정 요청
        foreach (var seal in equippedSeals) seal.ModifyMoves(ref moves, gridPosition.Value, isEnemy, null, IsOccupied);

        if (OthelloArtifactSystem.IsActive)
        {
            moves.RemoveAll(target =>
            {
                PieceController targetPiece = PieceManager.Instance?.GetPieceAt(target);
                return targetPiece != null && !IsHiddenEnemyDuringPrepare(targetPiece);
            });
        }

        // 무적 기물은 공격 대상 후보에서도 제외합니다.
        moves.RemoveAll(target =>
        {
            PieceController targetPiece = PieceManager.Instance?.GetPieceAt(target);
            return targetPiece != null && targetPiece.IsEnemy != isEnemy && targetPiece.IsInvincible;
        });

        if (ShotgunKingSystem.IsShotgunKing(this))
        {
            moves.RemoveAll(target => PieceManager.Instance?.GetPieceAt(target)?.IsEnemy == true);
        }
        return moves;
    }

    private List<Vector2Int> GetKingMoves()
    {
        List<Vector2Int> moves = new();
        Vector2Int[] offsets =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
            new(1, 1),
            new(1, -1),
            new(-1, 1),
            new(-1, -1)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int target = gridPosition.Value + offsets[i];
            if (CanMoveTo(target))
            {
                moves.Add(target);
            }
        }

        return moves;
    }

    private List<Vector2Int> GetQueenMoves()
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        AddRayMoves(moves, Vector2Int.up);
        AddRayMoves(moves, Vector2Int.down);
        AddRayMoves(moves, Vector2Int.left);
        AddRayMoves(moves, Vector2Int.right);
        AddRayMoves(moves, new Vector2Int(1, 1));
        AddRayMoves(moves, new Vector2Int(1, -1));
        AddRayMoves(moves, new Vector2Int(-1, 1));
        AddRayMoves(moves, new Vector2Int(-1, -1));
        return moves;
    }

    private List<Vector2Int> GetRookMoves()
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        AddRayMoves(moves, Vector2Int.up);
        AddRayMoves(moves, Vector2Int.down);
        AddRayMoves(moves, Vector2Int.left);
        AddRayMoves(moves, Vector2Int.right);
        return moves;
    }

    private List<Vector2Int> GetBishopMoves()
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        AddRayMoves(moves, new Vector2Int(1, 1));
        AddRayMoves(moves, new Vector2Int(1, -1));
        AddRayMoves(moves, new Vector2Int(-1, 1));
        AddRayMoves(moves, new Vector2Int(-1, -1));
        return moves;
    }

    private List<Vector2Int> GetKnightMoves()
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        Vector2Int[] offsets =
        {
            new(2, 1),
            new(2, -1),
            new(-2, 1),
            new(-2, -1),
            new(1, 2),
            new(1, -2),
            new(-1, 2),
            new(-1, -2)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int target = gridPosition.Value + offsets[i];
            if (CanMoveTo(target))
            {
                moves.Add(target);
            }
        }

        return moves;
    }

    private List<Vector2Int> GetAmazonMoves()
    {
        // 아마존: 퀸의 행마(8방향 슬라이딩) + 나이트의 행마(8방향 L자 점프)
        List<Vector2Int> moves = GetQueenMoves();
        List<Vector2Int> knightMoves = GetKnightMoves();
        foreach (var move in knightMoves)
        {
            if (!moves.Contains(move))
            {
                moves.Add(move);
            }
        }
        return moves;
    }

    private List<Vector2Int> GetChancellorMoves()
    {
        // 챈슬러: 룩의 행마(직선 4방향 슬라이딩) + 나이트의 행마(8방향 L자 점프)
        List<Vector2Int> moves = GetRookMoves();
        List<Vector2Int> knightMoves = GetKnightMoves();
        foreach (var move in knightMoves)
        {
            if (!moves.Contains(move))
            {
                moves.Add(move);
            }
        }
        return moves;
    }

    private List<Vector2Int> GetArchbishopMoves()
    {
        // 아치비숍: 비숍의 행마(대각선 4방향 슬라이딩) + 나이트의 행마(8방향 L자 점프)
        List<Vector2Int> moves = GetBishopMoves();
        List<Vector2Int> knightMoves = GetKnightMoves();
        foreach (var move in knightMoves)
        {
            if (!moves.Contains(move))
            {
                moves.Add(move);
            }
        }
        return moves;
    }

    private List<Vector2Int> GetCentaurMoves()
    {
        // 켄타우로스: 킹의 행마(8방향 1칸) + 나이트의 행마(8방향 L자 점프)
        List<Vector2Int> moves = GetKingMoves();
        List<Vector2Int> knightMoves = GetKnightMoves();
        foreach (var move in knightMoves)
        {
            if (!moves.Contains(move))
            {
                moves.Add(move);
            }
        }
        return moves;
    }

    private List<Vector2Int> GetCannonMoves()
    {
        // 캐논: 평상시에는 룩과 같이 빈 칸을 직선 슬라이딩 이동 (잡기는 불가)
        // 적을 잡을 때는 반드시 중간에 기물 1개(아군/적군 무관)를 뛰어넘어야 함 (뛰어넘을 기물이 없으면 포획 불가)
        List<Vector2Int> moves = new List<Vector2Int>();
        AddCannonRayMoves(moves, Vector2Int.up);
        AddCannonRayMoves(moves, Vector2Int.down);
        AddCannonRayMoves(moves, Vector2Int.left);
        AddCannonRayMoves(moves, Vector2Int.right);
        return moves;
    }

    private void AddCannonRayMoves(List<Vector2Int> moves, Vector2Int direction)
    {
        if (PieceManager.Instance == null || !gridPosition.HasValue)
            return;

        Vector2Int current = gridPosition.Value + direction;
        bool jumped = false;

        while (IsInBounds(current))
        {
            if (IsDestroyedCell(current))
                break;

            PieceController pieceAt = PieceManager.Instance.GetPieceAt(current);
            bool isHiddenPiece = IsHiddenEnemyDuringPrepare(pieceAt);

            if (isHiddenPiece)
            {
                pieceAt = null;
            }

            if (!jumped)
            {
                if (pieceAt == null)
                {
                    // 첫 번째 기물을 만나기 전: 빈 칸으로 일반 이동 가능 (잡기 아님)
                    if (isHiddenPiece || CanMoveTo(current))
                    {
                        moves.Add(current);
                    }
                    current += direction;
                }
                else
                {
                    // 첫 번째 기물을 만남 -> 이 기물을 '발판(도약대)'으로 삼고 뛰어넘음
                    jumped = true;
                    current += direction;
                }
            }
            else
            {
                // 기물 1개를 뛰어넘은 상태
                if (pieceAt == null)
                {
                    // 뛰어넘은 뒤 빈 칸으로는 이동 불가 (캐논은 잡을 때만 뛰어넘음)
                    current += direction;
                }
                else
                {
                    // 뛰어넘은 후 만난 첫 번째 기물이 적군이면 포획 가능!
                    if (pieceAt.IsEnemy != isEnemy && CanMoveTo(current))
                    {
                        moves.Add(current);
                    }
                    // 두 번째 기물을 만났으므로 이 방향 탐색 종료
                    break;
                }
            }
        }
    }

    private List<Vector2Int> GetGrasshopperMoves()
    {
        // 그래스호퍼: 퀸의 8방향으로 이동하되, 반드시 기물 1개를 뛰어넘어야 하며, 뛰어넘은 기물의 바로 다음 칸에만 착지(이동 또는 포획) 가능
        List<Vector2Int> moves = new List<Vector2Int>();
        AddGrasshopperRayMoves(moves, Vector2Int.up);
        AddGrasshopperRayMoves(moves, Vector2Int.down);
        AddGrasshopperRayMoves(moves, Vector2Int.left);
        AddGrasshopperRayMoves(moves, Vector2Int.right);
        AddGrasshopperRayMoves(moves, new Vector2Int(1, 1));
        AddGrasshopperRayMoves(moves, new Vector2Int(1, -1));
        AddGrasshopperRayMoves(moves, new Vector2Int(-1, 1));
        AddGrasshopperRayMoves(moves, new Vector2Int(-1, -1));
        return moves;
    }

    private void AddGrasshopperRayMoves(List<Vector2Int> moves, Vector2Int direction)
    {
        if (PieceManager.Instance == null || !gridPosition.HasValue)
            return;

        Vector2Int current = gridPosition.Value + direction;

        // 첫 번째 장애물(기물)을 만날 때까지 전진
        while (IsInBounds(current))
        {
            if (IsDestroyedCell(current))
                break;

            PieceController pieceAt = PieceManager.Instance.GetPieceAt(current);
            if (IsHiddenEnemyDuringPrepare(pieceAt))
            {
                pieceAt = null;
            }

            if (pieceAt != null)
            {
                // 기물을 발견함 (이 기물을 발판으로 뛰어넘음)
                Vector2Int landingPos = current + direction;
                if (IsInBounds(landingPos) && !IsDestroyedCell(landingPos))
                {
                    PieceController landingPiece = PieceManager.Instance.GetPieceAt(landingPos);
                    bool isHiddenLandingPiece = IsHiddenEnemyDuringPrepare(landingPiece);
                    if (isHiddenLandingPiece)
                    {
                        landingPiece = null;
                    }

                    if (landingPiece == null)
                    {
                        // 바로 다음 칸이 빈 칸이면 착지 이동 가능
                        if (isHiddenLandingPiece || CanMoveTo(landingPos))
                        {
                            moves.Add(landingPos);
                        }
                    }
                    else if (landingPiece.IsEnemy != isEnemy)
                    {
                        // 바로 다음 칸이 적 기물이면 포획 가능
                        if (CanMoveTo(landingPos))
                        {
                            moves.Add(landingPos);
                        }
                    }
                }
                // 기물 1개를 만났으므로 이 방향 탐색 종료
                break;
            }

            current += direction;
        }
    }

    private List<Vector2Int> GetPawnMoves()
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null)
        {
            return moves;
        }

        GridManager gridManager = PieceManager.Instance.gridManager;
        int dirY = isEnemy ? -1 : 1;
        Vector2Int forward = new Vector2Int(0, dirY);

        // 1. 전진 1칸 (빈 칸일 때만 이동 가능)
        Vector2Int oneStep = gridPosition.Value + forward;
        bool oneStepHasHiddenEnemy = IsInBounds(oneStep) &&
            IsHiddenEnemyDuringPrepare(PieceManager.Instance.GetPieceAt(oneStep));
        if (IsInBounds(oneStep) && !IsDestroyedCell(oneStep) &&
            (oneStepHasHiddenEnemy || !IsOccupied(oneStep)))
        {
            moves.Add(oneStep);

            // 2. 초기 2칸 전진 (시작 진영 1~2번째 줄에서 두 칸 모두 비어있을 때)
            int minY = gridManager.gridMinBounds.y;
            int maxY = minY + gridManager.boardHeight - 1;
            bool isStartRank = isEnemy
                ? (gridPosition.Value.y >= maxY - 1)
                : (gridPosition.Value.y <= minY + 1);

            Vector2Int twoStep = gridPosition.Value + (forward * 2);
            bool twoStepHasHiddenEnemy = IsInBounds(twoStep) &&
                IsHiddenEnemyDuringPrepare(PieceManager.Instance.GetPieceAt(twoStep));
            if (isStartRank && IsInBounds(twoStep) && !IsDestroyedCell(twoStep) &&
                (twoStepHasHiddenEnemy || !IsOccupied(twoStep)))
            {
                moves.Add(twoStep);
            }
        }

        // 3. 대각선 전방 포획 (적 기물이 있을 때만 공격 이동 가능, 오셀로의 성역 활성 시에는 직접 포획 불가)
        if (!OthelloArtifactSystem.IsActive)
        {
            Vector2Int[] captureOffsets =
            {
                new Vector2Int(-1, dirY),
                new Vector2Int(1, dirY)
            };

            for (int i = 0; i < captureOffsets.Length; i++)
            {
                Vector2Int diagTarget = gridPosition.Value + captureOffsets[i];
                if (!IsInBounds(diagTarget) || IsDestroyedCell(diagTarget))
                {
                    continue;
                }

                PieceController targetPiece = PieceManager.Instance.GetPieceAt(diagTarget);
                if (targetPiece != null && targetPiece.IsEnemy != isEnemy)
                {
                    moves.Add(diagTarget);
                }
            }
        }

        return moves;
    }

    private void AddRayMoves(List<Vector2Int> moves, Vector2Int direction)
    {
        Vector2Int current = gridPosition.Value + direction;
        while (IsInBounds(current))
        {
            if (PieceManager.Instance == null)
            {
                break;
            }

            // 파괴된 칸을 만나면 멈춤
            if (IsDestroyedCell(current))
            {
                break;
            }

            PieceController pieceAt = PieceManager.Instance.GetPieceAt(current);
            if (IsHiddenEnemyDuringPrepare(pieceAt))
            {
                moves.Add(current);
                current += direction;
                continue;
            }

            if (pieceAt == null)
            {
                if (CanMoveTo(current))
                {
                    moves.Add(current);
                }
                current += direction;
                continue;
            }

            if (pieceAt.IsEnemy != isEnemy && CanMoveTo(current))
            {
                moves.Add(current);
            }
            else if (pieceAt.IsEnemy == isEnemy && HasPhantomSeal())
            {
                current += direction;
                continue;
            }

            break;
        }
    }

    public bool HasPhantomSeal()
    {
        foreach (SealBase seal in equippedSeals)
        {
            if (seal != null && seal.Data != null && seal.Data.sealName == "환영의 인장")
            {
                return true;
            }
        }
        return false;
    }

    private bool IsInBounds(Vector2Int position)
    {
        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null)
        {
            throw new System.Exception("GridManager reference is required in PieceManager.");
        }

        GridManager gridManager = PieceManager.Instance.gridManager;
        Vector2Int min = gridManager.gridMinBounds;
        int width = gridManager.boardWidth;
        int height = gridManager.boardHeight;

        return position.x >= min.x && position.x < min.x + width &&
               position.y >= min.y && position.y < min.y + height;
    }

    public void SetThreatened(bool state)
    {
        if (isThreatened == state) return;
        isThreatened = state;

        if (isThreatened)
        {
            PlayStartleAnimation();
        }
        else
        {
            UpdateThreatenedVisuals();
        }
    }

    public void RefreshThreatenedVisuals()
    {
        if (isThreatened)
        {
            PlayStartleAnimation();
        }
        else
        {
            UpdateThreatenedVisuals();
        }
    }

    private void PlayStartleAnimation()
    {
        if (threatenedTween != null && threatenedTween.IsActive())
        {
            threatenedTween.Kill();
        }

        bool isGamePlay = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay;

        if (isGamePlay && currentLocation == PieceLocation.Board && !isDragging)
        {
            isStartling = true;

            Vector2 basePos = Vector2.zero;
            if (PieceManager.Instance != null && PieceManager.Instance.gridManager != null && gridPosition.HasValue)
            {
                basePos = PieceManager.Instance.gridManager.GridToUiPosition(gridPosition.Value);
            }
            else
            {
                basePos = rectTransform.anchoredPosition;
            }

            Sequence seq = DOTween.Sequence();
            // 화들짝 놀라서 점프
            seq.Append(rectTransform.DOJumpAnchorPos(basePos, startleJumpPower, 1, startleDuration));
            // 동시에 스케일 펀치 효과
            seq.Join(transform.DOPunchScale(new Vector3(0.2f, -0.2f, 0), startleDuration, 10, 1));

            seq.OnComplete(() =>
            {
                isStartling = false;
                if (!PieceFlickLauncher.IsPhysicsActive)
                {
                    rectTransform.anchoredPosition = basePos;
                }
                transform.localScale = originalScale;
            });
            seq.OnKill(() =>
            {
                isStartling = false;
            });

            threatenedTween = seq;
        }
        else
        {
            UpdateThreatenedVisuals();
        }
    }

    private void UpdateThreatenedVisuals()
    {
        if (threatenedTween != null && threatenedTween.IsActive())
        {
            threatenedTween.Kill();
        }

        if (!isThreatened) isStartling = false;

        bool isGamePlay = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay;
        bool shouldShake = isThreatened && !isDragging && currentLocation == PieceLocation.Board && isGamePlay;

        if (!isDragging)
        {
            rectTransform.localRotation = originalRotation;
        }

        if (!shouldShake && !isDragging)
        {
            // 위치 복구
            if (currentLocation == PieceLocation.Board)
            {
                if (PieceManager.Instance != null && PieceManager.Instance.gridManager != null)
                    UpdateUiPosition();
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    private void CheckInvalidZoneFeedback()
    {
        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null) return;

        Vector2Int? gridPos = PieceManager.Instance.gridManager.GetNearestGridPosition(rectTransform.anchoredPosition);

        // 보드 위이고, 인벤토리에서 온 기물이며, 기물 배치 한도를 초과했는지 확인
        bool isOverLimit = currentLocation == PieceLocation.Inventory &&
                           gridPos.HasValue &&
                           PieceManager.Instance.GetPlayerPieceCountOnBoard() >= PieceManager.Instance.MaxPlayerPiecesOnBoard;

        // 유효하지 않은 구역(적진)에 있거나, 기물 배치 한도를 초과한 경우
        if ((gridPos.HasValue && gridPos.Value.y > PieceManager.Instance.PlayerPrepareMaxY) || isOverLimit)
        {
            if (pieceImage != null) pieceImage.color = invalidZoneTint;
        }
        else
        {
            ResetPieceColor();
        }
    }

    private void ResetPieceColor()
    {
        if (pieceImage != null) pieceImage.color = originalColor;
    }

    // --- Seal System ---

    public bool EquipSeal(SealData data)
    {
        if (data == null) return false;
        if (!isEnemy && pieceType == PieceType.King)
        {
            data = ShotgunKingSystem.ConvertSeal(data);
        }

        if (CollectionManager.Instance != null)
        {
            if (isEnemy)
            {
                CollectionManager.Instance.RecordSealSeen(data);
            }
            else
            {
                CollectionManager.Instance.RecordSeal(data);
            }
        }

        // 프리팹이 없으면 기본 로직이나 에러 처리
        if (data.sealPrefab == null)
        {
            Debug.LogWarning($"[Seal] {data.sealName} 인장 프리팹이 없어 장착할 수 없습니다.");
            return false;
        }

        GameObject sealObj = CreateEquippedSealVisual(data);
        if (sealObj == null)
        {
            return false;
        }

        // 프리팹 인스턴스화 (로직이 담긴 컴포넌트 포함)
        GameObject logicObj = Instantiate(data.sealPrefab, sealObj.transform);
        logicObj.name = "Logic";

        SealBase sealComponent = logicObj.GetComponent<SealBase>();
        if (sealComponent != null)
        {
            sealComponent.Initialize(data, this);
            equippedSeals.Add(sealComponent);
            RefreshEquippedSealVisuals();
            ShotgunKingSystem.NotifyKingPartsChanged(this);
            return true;
        }
        else
        {
            Destroy(sealObj);
            Debug.LogWarning($"[Seal] {data.sealName} 프리팹에 SealBase 컴포넌트가 없습니다.");
            return false;
        }
    }

    private GameObject CreateEquippedSealVisual(SealData data)
    {
        GameObject sealObj;
        if (sealIconPrefab != null)
        {
            sealObj = Instantiate(sealIconPrefab, transform);
        }
        else
        {
            sealObj = new GameObject($"Seal_{data.sealName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(DraggableSeal));
        }

        sealObj.name = $"Seal_{data.sealName}";
        sealObj.transform.SetParent(transform, false);

        RectTransform rt = sealObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(sealIconSize, sealIconSize);
            Vector2 baseOffset = GetSealIconOffset();
            rt.anchoredPosition = new Vector2(baseOffset.x, baseOffset.y + (equippedSeals.Count * sealIconSpacing));
        }

        DraggableSeal draggable = sealObj.GetComponent<DraggableSeal>();
        if (draggable == null)
        {
            draggable = sealObj.AddComponent<DraggableSeal>();
        }
        draggable.enabled = true;
        draggable.Initialize(data);

        SealTooltipHandler tooltipHandler = sealObj.GetComponent<SealTooltipHandler>();
        if (tooltipHandler == null)
        {
            tooltipHandler = sealObj.AddComponent<SealTooltipHandler>();
        }
        tooltipHandler.enabled = true;
        tooltipHandler.Initialize(data);

        return sealObj;
    }

    public void UnEquipAllSeals()
    {
        foreach (var seal in equippedSeals)
        {
            seal.OnUnequip();
            if (seal.transform.parent != null) // Seal_Name 오브젝트 삭제
                Destroy(seal.transform.parent.gameObject);
            else
                Destroy(seal.gameObject);
        }
        equippedSeals.Clear();
        StopDuplicateSealCycle();
    }

    public bool UnEquipSeal(SealData sealData, bool destroyVisual = true)
    {
        SealBase sealToUnEquip = null;
        foreach (var seal in equippedSeals)
        {
            if (seal.Data == sealData)
            {
                sealToUnEquip = seal;
                break;
            }
        }

        if (sealToUnEquip != null)
        {
            equippedSeals.Remove(sealToUnEquip);
            sealToUnEquip.OnUnequip();
            // Seal_Name > Logic 구조이므로 부모 오브젝트를 삭제해야 아이콘까지 지워집니다.
            if (destroyVisual)
            {
                if (sealToUnEquip.transform.parent != null) Destroy(sealToUnEquip.transform.parent.gameObject);
                else Destroy(sealToUnEquip.gameObject);
            }
            RefreshEquippedSealVisuals();
            ShotgunKingSystem.NotifyKingPartsChanged(this);
            return true;
        }
        return false;
    }

    private bool HasSeal(SealData sealData)
    {
        if (sealData == null) return false;
        foreach (var seal in equippedSeals)
        {
            if (seal != null && seal.Data == sealData)
            {
                return true;
            }
        }
        return false;
    }

    private void RefreshEquippedSealVisuals()
    {
        StopDuplicateSealCycle();

        List<SealBase> visibleSeals = new List<SealBase>();
        foreach (var seal in equippedSeals)
        {
            if (seal == null || seal.Data == null) continue;
            RectTransform rt = seal.transform.parent as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = GetSealIconOffset();
            }
            visibleSeals.Add(seal);
        }

        for (int i = 0; i < visibleSeals.Count; i++)
        {
            SetSealVisualVisible(visibleSeals[i], i == 0);
        }

        if (visibleSeals.Count > 0 && visibleSeals[0] != null)
        {
            currentDisplaySealData = visibleSeals[0].Data;
            currentDisplaySealIndex = 0;
            OnEquippedSealVisualChanged?.Invoke(this, currentDisplaySealData, 0, visibleSeals.Count);
        }
        else
        {
            currentDisplaySealData = null;
            currentDisplaySealIndex = 0;
        }

        if (visibleSeals.Count > 1)
        {
            duplicateSealCycleCoroutine = StartCoroutine(CycleDuplicateSealVisuals(visibleSeals));
        }
    }

    private IEnumerator CycleDuplicateSealVisuals(List<SealBase> seals, int startIndex = 0)
    {
        int index = startIndex;
        while (true)
        {
            yield return new WaitForSeconds(duplicateSealCycleInterval);
            index++;
            int activeIndex = index % seals.Count;
            for (int i = 0; i < seals.Count; i++)
            {
                SetSealVisualVisible(seals[i], i == activeIndex, true);
            }

            if (activeIndex >= 0 && activeIndex < seals.Count && seals[activeIndex] != null)
            {
                currentDisplaySealData = seals[activeIndex].Data;
                currentDisplaySealIndex = activeIndex;
                OnEquippedSealVisualChanged?.Invoke(this, currentDisplaySealData, activeIndex, seals.Count);
            }
        }
    }

    public void SetDisplaySealByIndex(int targetIndex)
    {
        if (equippedSeals == null || equippedSeals.Count == 0) return;

        List<SealBase> visibleSeals = new List<SealBase>();
        foreach (var seal in equippedSeals)
        {
            if (seal != null && seal.Data != null)
            {
                visibleSeals.Add(seal);
            }
        }

        if (visibleSeals.Count == 0) return;
        int activeIndex = ((targetIndex % visibleSeals.Count) + visibleSeals.Count) % visibleSeals.Count;

        for (int i = 0; i < visibleSeals.Count; i++)
        {
            SetSealVisualVisible(visibleSeals[i], i == activeIndex, true);
        }

        if (visibleSeals[activeIndex] != null)
        {
            currentDisplaySealData = visibleSeals[activeIndex].Data;
            currentDisplaySealIndex = activeIndex;
        }
    }

    public void PauseDuplicateSealCycle()
    {
        StopDuplicateSealCycle();
    }

    public void ResumeDuplicateSealCycle()
    {
        if (duplicateSealCycleCoroutine != null || equippedSeals == null || equippedSeals.Count <= 1)
        {
            return;
        }

        List<SealBase> visibleSeals = new List<SealBase>();
        foreach (var seal in equippedSeals)
        {
            if (seal != null && seal.Data != null)
            {
                visibleSeals.Add(seal);
            }
        }

        if (visibleSeals.Count > 1)
        {
            duplicateSealCycleCoroutine = StartCoroutine(CycleDuplicateSealVisuals(visibleSeals, currentDisplaySealIndex));
        }
    }

    private void SetSealVisualVisible(SealBase seal, bool visible, bool animate = false)
    {
        if (seal == null || seal.transform.parent == null) return;
        GameObject visual = seal.transform.parent.gameObject;
        DraggableSeal draggable = visual.GetComponent<DraggableSeal>();
        Image image = draggable != null ? draggable.SealIconImage : visual.GetComponent<Image>();
        RectTransform rt = visual.GetComponent<RectTransform>();
        if (rt != null) rt.DOKill();
        if (image != null) image.DOKill();

        if (animate && rt != null && image != null)
        {
            if (visible)
            {
                image.enabled = true;
                image.color = new Color(image.color.r, image.color.g, image.color.b, 0f);
                rt.localScale = Vector3.one * 0.75f;
                image.DOFade(1f, duplicateSealSwitchDuration).SetEase(Ease.OutQuad);
                rt.DOScale(Vector3.one, duplicateSealSwitchDuration).SetEase(Ease.OutBack);
            }
            else
            {
                image.DOFade(0f, duplicateSealSwitchDuration).SetEase(Ease.InQuad)
                    .OnComplete(() => image.enabled = false);
                rt.DOScale(Vector3.one * 0.75f, duplicateSealSwitchDuration).SetEase(Ease.InQuad);
            }
        }
        else if (image != null)
        {
            image.enabled = visible;
            image.color = new Color(image.color.r, image.color.g, image.color.b, visible ? 1f : 0f);
            if (rt != null) rt.localScale = Vector3.one;
        }

        bool isConversion = seal is ConversionSeal || ConversionSeal.IsConversionSealData(seal.Data);
        if (draggable != null) draggable.enabled = visible && !isConversion;
        SealTooltipHandler tooltip = visual.GetComponent<SealTooltipHandler>();
        if (tooltip != null) tooltip.enabled = visible;
    }

    private void StopDuplicateSealCycle()
    {
        if (duplicateSealCycleCoroutine != null)
        {
            StopCoroutine(duplicateSealCycleCoroutine);
            duplicateSealCycleCoroutine = null;
        }
    }

    public bool HasConversionSeal()
    {
        if (convertedByOthelloThisBattle)
        {
            return true;
        }

        foreach (var seal in equippedSeals)
        {
            if (seal == null) continue;
            if (seal is ConversionSeal || ConversionSeal.IsConversionSealData(seal.Data))
            {
                return true;
            }
        }

        return false;
    }

    public bool AttachConversionSeal(SealData sealData = null)
    {
        if (sealData == null)
        {
            sealData = ConversionSeal.GetOrCreateSealData();
        }

        convertedByOthelloThisBattle = true;

        // 이미 전향의 인장 시각 오브젝트가 부착되어 있는지 확인
        foreach (var seal in equippedSeals)
        {
            if (seal != null && (seal is ConversionSeal || ConversionSeal.IsConversionSealData(seal.Data)))
            {
                return true;
            }
        }

        if (sealData != null && sealData.sealPrefab != null)
        {
            EquipSeal(sealData);
            foreach (var seal in equippedSeals)
            {
                if (seal != null && ConversionSeal.IsConversionSealData(seal.Data))
                {
                    return true;
                }
            }
        }

        AttachConversionSealFallbackVisual(sealData);
        return HasConversionSeal();
    }

    private void AttachConversionSealFallbackVisual(SealData sealData)
    {
        GameObject sealObj = CreateEquippedSealVisual(sealData);
        if (sealObj == null)
        {
            return;
        }

        DraggableSeal draggable = sealObj.GetComponent<DraggableSeal>();
        if (draggable != null)
        {
            draggable.enabled = false;
        }

        GameObject logicObj = new GameObject("Logic");
        logicObj.transform.SetParent(sealObj.transform, false);
        ConversionSeal conversionSeal = logicObj.AddComponent<ConversionSeal>();
        conversionSeal.Initialize(sealData, this);
        equippedSeals.Add(conversionSeal);
        RefreshEquippedSealVisuals();
    }

    /// <summary>
    /// 오셀로의 성역 효과로 적 기물을 아군으로 전향시키고 180도 뒤집기 연출 및 [전향의 인장]을 부여합니다.
    /// </summary>
    public void ConvertToAllyWithOthelloSeal()
    {
        ConvertToTeamWithOthelloSeal(false);
    }

    /// <summary>
    /// 오셀로의 성역 효과로 기물을 지정된 진영(toEnemy)으로 전향시키고 180도 뒤집기 연출 및 [전향의 인장]을 부여합니다.
    /// </summary>
    public void ConvertToTeamWithOthelloSeal(bool toEnemy)
    {
        convertedByOthelloThisBattle = true;
        isEnemy = toEnemy;
        SetHiddenDuringPrepare(false);

        EnemyPieceController enemyController = GetComponent<EnemyPieceController>();
        if (toEnemy)
        {
            if (enemyController != null)
            {
                enemyController.enabled = true;
            }
        }
        else
        {
            if (enemyController != null)
            {
                enemyController.enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(enemyController);
                }
                else
                {
                    DestroyImmediate(enemyController);
                }
            }
        }

        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.NotifyPieceTeamChanged(this);
        }

        if (!toEnemy && CollectionManager.Instance != null)
        {
            CollectionManager.Instance.RecordPiece(pieceType);
        }

        AnimateFlip();
    }

    /// <summary>
    /// 기물이 DOTween을 활용해 부드럽게 180도 뒤집히며(Flip) 진영 스프라이트로 변하고 상단에 [전향의 인장]이 부착됩니다.
    /// </summary>
    public void AnimateFlip()
    {
        if (rectTransform == null)
        {
            ApplySprite();
            AttachConversionSeal();
            return;
        }

        rotationTween?.Kill();
        scaleTween?.Kill();

        float halfFlipDuration = 0.16f;
        Sequence flipSequence = DOTween.Sequence();

        // 전반부 90도 회전 (옆면이 되어 두께가 0이 되는 지점까지)
        flipSequence.Append(rectTransform.DOLocalRotate(new Vector3(0f, 90f, 0f), halfFlipDuration).SetEase(Ease.InQuad));
        flipSequence.Join(rectTransform.DOScale(originalScale * 1.15f, halfFlipDuration).SetEase(Ease.OutQuad));

        // 90도 지점에서 스프라이트 교체 및 상단 [전향의 인장] 부착
        flipSequence.AppendCallback(() =>
        {
            ApplySprite();
            ResetPieceColor();
            AttachConversionSeal();

            if (Application.isPlaying && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(SFXType.Click);
            }

            // 좌우 반전이 남지 않도록 -90도에서 0도로 후반부 90도 회전 연결 (총 180도 동일 방향 플립)
            rectTransform.localEulerAngles = new Vector3(0f, -90f, 0f);
        });

        // 후반부 90도 회전 및 탄성 착지
        flipSequence.Append(rectTransform.DOLocalRotate(Vector3.zero, halfFlipDuration * 1.15f).SetEase(Ease.OutBack));
        flipSequence.Join(rectTransform.DOScale(originalScale, halfFlipDuration * 1.15f).SetEase(Ease.OutBack));
        flipSequence.OnComplete(() =>
        {
            if (rectTransform != null)
            {
                rectTransform.localRotation = originalRotation;
                rectTransform.localScale = originalScale;
            }
        });
    }

    public bool AttachPromotionSeal(SealData sealData)
    {
        if (HasPromotionSeal())
        {
            return true;
        }

        if (sealData != null)
        {
            EquipSeal(sealData);

            if (HasPromotionSeal())
            {
                return true;
            }
        }
        else
        {
        }

        AttachPromotionSealFallbackVisual(sealData);
        return HasPromotionSeal();
    }

    private void AttachPromotionSealFallbackVisual(SealData sealData)
    {
        GameObject sealObj = new GameObject("Seal_승급의 인장");
        sealObj.transform.SetParent(transform, false);

        GameObject logicObj = new GameObject("Logic");
        logicObj.transform.SetParent(sealObj.transform, false);
        EmptySeal emptySeal = logicObj.AddComponent<EmptySeal>();
        emptySeal.Initialize(sealData, this);
        equippedSeals.Add(emptySeal);

        Image img = sealObj.AddComponent<Image>();
        img.raycastTarget = true;
        if (sealData != null && sealData.icon != null)
        {
            img.sprite = sealData.icon;
            img.preserveAspect = true;
            img.color = Color.white;
        }
        else
        {
            // 폴백: 투명 단색 사각 블록으로 노란빛 표시
            img.color = new Color(1f, 0.85f, 0.2f, 1f);
        }

        RectTransform rt = sealObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(sealIconSize, sealIconSize);
        Vector2 baseOffset = GetSealIconOffset();
        rt.anchoredPosition = new Vector2(baseOffset.x, baseOffset.y + ((equippedSeals.Count - 1) * sealIconSpacing));

        if (sealData != null)
        {
            SealTooltipHandler tooltipHandler = sealObj.AddComponent<SealTooltipHandler>();
            tooltipHandler.Initialize(sealData);
        }
    }

    public void MarkPromotedThisStage()
    {
        promotedByMedalThisStage = true;
    }

    public void MarkPromotedByMedalThisStage()
    {
        MarkPromotedThisStage();
    }

    /// <summary>
    /// 프로모션 상태(메달 플래그 및 승급의 인장)를 제거합니다.
    /// 다른 기물을 잡아 새로운 기물로 변신했을 때 호출됩니다.
    /// </summary>
    public void RemovePromotionSeal()
    {
        promotedByMedalThisStage = false;

        SealBase promoSeal = null;
        foreach (var seal in equippedSeals)
        {
            if (seal != null && seal.Data != null &&
                (seal.Data.sealName == "승급의 인장" || seal.Data.sealName == "승급자의 인장"))
            {
                promoSeal = seal;
                break;
            }
        }

        if (promoSeal != null)
        {
            UnEquipSeal(promoSeal.Data);
        }
    }

    public bool HasPromotionSeal()
    {
        if (promotedByMedalThisStage)
        {
            return true;
        }

        foreach (var seal in equippedSeals)
        {
            if (seal == null)
            {
                continue;
            }

            if (seal.Data != null &&
                (seal.Data.sealName == "승급의 인장" ||
                 seal.Data.sealName == "승급자의 인장"))
            {
                return true;
            }
        }

        return false;
    }

    // 📌 Case B Hook: 파괴 전 거부권 확인
    public bool CanBeDestroyed()
    {
        foreach (var seal in equippedSeals)
        {
            if (!seal.OnBeforeDestroy()) return false; // 하나라도 거부하면 파괴 불가
        }
        return true;
    }

    // 📌 Case F Hook: 이동 시작 전 알림
    public void OnStartMove(Vector2Int fromPos, Vector2Int toPos)
    {
        foreach (var seal in equippedSeals)
        {
            if (seal != null)
            {
                seal.OnBeforeMove(fromPos, toPos);
            }
        }
    }

    // 📌 Case C Hook: 이동 후 알림
    public void OnMoveFinished(Vector2Int prevPos, Vector2Int newPos)
    {
        foreach (var seal in equippedSeals)
        {
            seal.OnAfterMove(prevPos, newPos);
        }
    }

    // 📌 Case E Hook: 적 기물 포획 후 알림
    public void OnCapturedPiece(PieceType capturedType)
    {
        foreach (var seal in new List<SealBase>(equippedSeals))
        {
            if (seal == null) continue;
            seal.OnAfterCapture(capturedType);
        }
    }

    /// <summary>
    /// 인장 등은 유지한 채 기물 종류만 즉시 변경합니다.
    /// </summary>
    public void ChangeType(PieceType newType)
    {
        if (pieceType == newType) return;

        pieceType = newType;
        if (CollectionManager.Instance != null && !isEnemy)
        {
            CollectionManager.Instance.RecordPiece(newType);
        }
        ApplySprite();
        UpdateUiPosition();
    }

    public void OnDestroyed(PieceController killer, Vector2Int ownerPosition)
    {
        foreach (var seal in equippedSeals)
        {
            if (seal == null)
            {
                continue;
            }

            seal.OnOwnerDestroyed(killer, ownerPosition);
        }
    }

    public int GetBuffStacks(PieceBuffType buffType)
    {
        return buffType switch
        {
            PieceBuffType.Protection => protectionStacks,
            PieceBuffType.Evasion => evasionStacks,
            PieceBuffType.Counterattack => counterattackStacks,
            PieceBuffType.Invincibility => invincibilityTurns,
            _ => 0
        };
    }

    public void AddBuff(PieceBuffType buffType, int stacks = 1)
    {
        if (buffType == PieceBuffType.Invincibility)
        {
            AddInvincibility(stacks);
            return;
        }

        if (stacks <= 0) return;

        switch (buffType)
        {
            case PieceBuffType.Protection:
                protectionStacks += stacks;
                break;
            case PieceBuffType.Evasion:
                evasionStacks += stacks;
                break;
            case PieceBuffType.Counterattack:
                counterattackStacks += stacks;
                break;
            case PieceBuffType.Invincibility:
                invincibilityTurns = stacks;
                break;
        }

        RefreshBuffAuraVisuals();
    }

    public bool ConsumeBuff(PieceBuffType buffType)
    {
        if (GetBuffStacks(buffType) <= 0) return false;

        switch (buffType)
        {
            case PieceBuffType.Protection:
                protectionStacks--;
                break;
            case PieceBuffType.Evasion:
                evasionStacks--;
                break;
            case PieceBuffType.Counterattack:
                counterattackStacks--;
                break;
            case PieceBuffType.Invincibility:
                if (invincibilityTurns > 0)
                {
                    invincibilityTurns--;
                }
                else
                {
                    return false;
                }
                break;
            default:
                return false;
        }

        RefreshBuffAuraVisuals();
        return true;
    }

    public void AddInvincibility(int turns)
    {
        if (turns == 0) return;

        if (turns < 0)
        {
            invincibilityTurns = -1;
        }
        else if (invincibilityTurns != -1)
        {
            invincibilityTurns = Mathf.Max(invincibilityTurns, turns);
        }

        RefreshBuffAuraVisuals();
    }

    public void AdvanceBuffTurn()
    {
        if (invincibilityTurns > 0)
        {
            invincibilityTurns--;
            RefreshBuffAuraVisuals();
        }
    }

    public void PlayBuffActivationEffect(PieceBuffType buffType)
    {
        if (pieceImage == null)
        {
            return;
        }

        // 버프 발동 순간 기물 이미지에 보여줄 플래시 색상입니다.
        // 보호/회피/역공의 발동 연출을 바꾸려면 이 색상만 수정하면 됩니다.
        Color effectColor = buffType switch
        {
            PieceBuffType.Protection => new Color(0.35f, 0.75f, 1f),
            PieceBuffType.Evasion => new Color(0.75f, 0.45f, 1f),
            PieceBuffType.Counterattack => new Color(1f, 0.35f, 0.2f),
            PieceBuffType.Invincibility => new Color(0.35f, 1f, 0.45f),
            _ => Color.white
        };

        // 플래시가 끝난 뒤 현재 기물 색상으로 복귀합니다.
        Color returnColor = pieceImage.color;
        pieceImage.DOKill();
        pieceImage.color = effectColor;
        pieceImage.DOColor(returnColor, 0.35f).SetEase(Ease.OutQuad);
    }

    public void SetBuffStacks(PieceBuffType buffType, int stacks)
    {
        stacks = Mathf.Max(0, stacks);
        switch (buffType)
        {
            case PieceBuffType.Protection:
                protectionStacks = stacks;
                break;
            case PieceBuffType.Evasion:
                evasionStacks = stacks;
                break;
            case PieceBuffType.Counterattack:
                counterattackStacks = stacks;
                break;
            case PieceBuffType.Invincibility:
                invincibilityTurns = stacks < 0 ? -1 : stacks;
                break;
        }

        RefreshBuffAuraVisuals();
    }

    /// <summary>
    /// 기물에 부여된 모든 전투 버프(보호, 회피, 역공, 무적)를 초기화하고 오라 연출을 갱신합니다.
    /// 스테이지 종료 시 호출됩니다.
    /// </summary>
    public void ClearAllBuffs()
    {
        protectionStacks = 0;
        evasionStacks = 0;
        counterattackStacks = 0;
        invincibilityTurns = 0;
        RefreshBuffAuraVisuals();
    }

    private void CreateBuffAuraVisuals()
    {
        if (buffAuraImages[0] != null)
        {
            return;
        }

        if (buffAuraSprite == null)
        {
            // 모든 기물이 공유하는 링 텍스처입니다.
            // outerRadius/innerRadius 차이로 링 두께를 조절할 수 있습니다.
            const int textureSize = 64;
            const float outerRadius = 30f;
            const float innerRadius = 24f;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.name = "BuffAuraRingTexture";
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[textureSize * textureSize];
            Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * textureSize + x] = distance >= innerRadius && distance <= outerRadius
                        ? Color.white
                        : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            buffAuraSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize);
        }

        // 배열 순서와 PieceBuffType 순서가 일치합니다.
        // 보호=노랑, 회피=파랑, 역공=빨강, 무적=초록입니다.
        Color[] auraColors =
        {
            new Color(1f, 0.85f, 0.1f, 0.9f),
            new Color(0.2f, 0.55f, 1f, 0.9f),
            new Color(1f, 0.2f, 0.15f, 0.9f),
            new Color(0.3f, 1f, 0.4f, 0.9f)
        };

        for (int i = 0; i < buffAuraImages.Length; i++)
        {
            GameObject auraObject = new GameObject($"BuffAura_{(PieceBuffType)i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            auraObject.transform.SetParent(transform, false);
            auraObject.transform.SetAsFirstSibling();

            Image auraImage = auraObject.GetComponent<Image>();
            auraImage.sprite = buffAuraSprite;
            auraImage.color = auraColors[i];
            auraImage.raycastTarget = false;
            auraImage.preserveAspect = true;

            RectTransform auraRect = auraImage.rectTransform;
            auraRect.anchorMin = new Vector2(0.5f, 0.5f);
            auraRect.anchorMax = new Vector2(0.5f, 0.5f);
            auraRect.pivot = new Vector2(0.5f, 0.5f);
            auraRect.anchoredPosition = Vector2.zero;
            // 여러 버프가 동시에 있을 때 링이 겹치지 않도록 버프별 크기를 다르게 합니다.
            auraRect.sizeDelta = new Vector2(92f + (i * 12f), 92f + (i * 12f));
            buffAuraImages[i] = auraImage;
        }
    }

    public void RefreshBuffAuraVisuals()
    {
        if (buffAuraImages[0] == null)
        {
            return;
        }

        buffAuraImages[0].gameObject.SetActive(protectionStacks > 0);
        buffAuraImages[1].gameObject.SetActive(evasionStacks > 0);
        buffAuraImages[2].gameObject.SetActive(counterattackStacks > 0);
        buffAuraImages[3].gameObject.SetActive(IsInvincible);
    }

    // -------------------

    public void SetLocation(PieceLocation location)
    {
        currentLocation = location;
    }

    public void ClearGridPosition()
    {
        gridPosition = null;
    }

    public void SetPhysicsGridPosition(Vector2Int target)
    {
        gridPosition = target;
        currentLocation = PieceLocation.Board;
        PieceManager.Instance?.MarkPiecePositionCacheDirty();
    }

    public void Die()
    {
        PieceManager.Instance?.RemovePiece(this);
    }

    public void Capture(PieceController killer = null)
    {
        PieceManager.Instance?.RemovePiece(this);
    }

    /// <summary>
    /// 이동범위 패턴 텍스트를 생성하여 반환합니다 (기본 · + 코드 인젝션 추가분 ★)
    /// </summary>
    public string GenerateMovementPatternText()
    {
        bool replacesMovement = HasMovementReplacementPreview();

        // 기본 패턴 오프셋
        List<Vector2Int> baseOffsets = replacesMovement ? new List<Vector2Int>() : GetBaseMovementOffsets();

        // 인장 추가 오프셋
        List<Vector2Int> additionalOffsets = GetTooltipAdditionalOffsets(replacesMovement);

        // 7x7 그리드 생성 (중심: 3,3)
        const int gridSize = 7;
        const int center = 3;
        char[,] grid = new char[gridSize, gridSize];

        // 빈 공간으로 초기화
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                grid[y, x] = ' ';
            }
        }

        // 중심에 현재 기물 표시
        grid[center, center] = 'W';

        // 기본 이동범위: 터미널 그린 □ (B = Basic)
        foreach (var offset in baseOffsets)
        {
            int gridX = center + offset.x;
            int gridY = center - offset.y; // Y축 반전 (화면 좌표)

            if (gridX >= 0 && gridX < gridSize && gridY >= 0 && gridY < gridSize)
            {
                if (grid[gridY, gridX] != 'W')
                    grid[gridY, gridX] = 'B';
            }
        }

        // 폰(Pawn)인 경우 대각선 포획 위치(C = Capture) 표시
        if (!replacesMovement && pieceType == PieceType.Pawn)
        {
            int dirY = isEnemy ? -1 : 1;
            Vector2Int[] capOffsets = { new Vector2Int(-1, dirY), new Vector2Int(1, dirY) };
            foreach (var cap in capOffsets)
            {
                int gx = center + cap.x;
                int gy = center - cap.y;
                if (gx >= 0 && gx < gridSize && gy >= 0 && gy < gridSize && grid[gy, gx] == ' ')
                {
                    grid[gy, gx] = 'C';
                }
            }
        }

        // 인장 추가 이동범위: 핑크 □ (S = Seal)
        foreach (var offset in additionalOffsets)
        {
            int gridX = center + offset.x;
            int gridY = center - offset.y;

            if (gridX >= 0 && gridX < gridSize && gridY >= 0 && gridY < gridSize)
            {
                if (grid[gridY, gridX] != 'W')
                    grid[gridY, gridX] = 'S';
            }
        }

        // 텍스트로 변환 (색상 코드 적용)
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[{GetPieceNameForType(pieceType)}]\n");

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                char c = grid[y, x];
                string symbol;

                if (c == ' ')
                    symbol = FormatTooltipCell("#FFFFFF", "□"); // 하얀색 빈칸
                else if (c == 'W')
                    symbol = FormatTooltipCell("#FFFFFF", "■"); // 현재 기물
                else if (c == 'B')
                    symbol = FormatTooltipCell("#80FF00", "□"); // 연두색 이동 가능
                else if (c == 'C')
                    symbol = FormatTooltipCell("#FF5555", "×"); // 대각선 포획 전용
                else if (c == 'S')
                    symbol = FormatTooltipCell("#FF80FF", "□"); // 핑크색 인장 추가
                else if (c == 'G')
                    symbol = FormatTooltipCell("#00CC00", "■"); // 초록색 다른 기물
                else
                    symbol = c.ToString();

                sb.Append(symbol);
                if (x < gridSize - 1) sb.Append(" ");
            }
            if (y < gridSize - 1) sb.Append("\n");
        }

        if (pieceType == PieceType.Pawn)
        {
            sb.Append("\n<size=85%><color=#88FF88>□:전진(초기2칸) / </color><color=#FF5555>×:대각선 포획</color></size>");
        }
        else if (pieceType == PieceType.Knight)
        {
            sb.Append("\n<size=85%><color=#88FF88>※ 장애물 점프(Jump) 이동 가능</color></size>");
        }
        else if (pieceType == PieceType.Amazon)
        {
            sb.Append("\n<size=85%><color=#FFD700>※ 퀸의 8방향 슬라이딩 + 나이트 점프(Jump)</color></size>");
        }
        else if (pieceType == PieceType.Chancellor)
        {
            sb.Append("\n<size=85%><color=#00FFFF>※ 룩의 4방향 직선 슬라이딩 + 나이트 점프(Jump)</color></size>");
        }
        else if (pieceType == PieceType.Archbishop)
        {
            sb.Append("\n<size=85%><color=#DA70D6>※ 비숍의 4방향 대각선 슬라이딩 + 나이트 점프(Jump)</color></size>");
        }
        else if (pieceType == PieceType.Centaur)
        {
            sb.Append("\n<size=85%><color=#FFA500>※ 킹의 8방향 1칸 이동 + 나이트 점프(Jump)</color></size>");
        }
        else if (pieceType == PieceType.Cannon)
        {
            sb.Append("\n<size=85%><color=#FF69B4>※ 룩의 4방향 이동 / 적 포획 시 기물 1개를 뛰어넘어야 함</color></size>");
        }
        else if (pieceType == PieceType.Grasshopper)
        {
            sb.Append("\n<size=85%><color=#98FB98>※ 퀸의 8방향 도약 / 기물 1개를 뛰어넘어 바로 뒤 칸으로만 이동 및 포획</color></size>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 기본 이동범위 오프셋을 반환합니다 (인장 적용 전)
    /// </summary>
    private List<Vector2Int> GetBaseMovementOffsets()
    {
        return GetBaseMovementOffsetsForType(pieceType, isEnemy);
    }

    /// <summary>
    /// 지정된 체스 기물 타입의 기본 이동범위 오프셋을 반환합니다
    /// </summary>
    public static List<Vector2Int> GetBaseMovementOffsetsForType(PieceType pieceType, bool isEnemy = false)
    {
        List<Vector2Int> offsets = new List<Vector2Int>();

        switch (pieceType)
        {
            case PieceType.King:
                // 킹: 8방향 1칸
                offsets.AddRange(new[] {
                    Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
                    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
                });
                break;

            case PieceType.Queen:
                // 퀸: 직선 + 대각선 8방향 전방위 슬라이딩
                for (int i = 1; i <= 3; i++)
                {
                    offsets.Add(new Vector2Int(0, i));
                    offsets.Add(new Vector2Int(0, -i));
                    offsets.Add(new Vector2Int(-i, 0));
                    offsets.Add(new Vector2Int(i, 0));
                    offsets.Add(new Vector2Int(i, i));
                    offsets.Add(new Vector2Int(i, -i));
                    offsets.Add(new Vector2Int(-i, i));
                    offsets.Add(new Vector2Int(-i, -i));
                }
                break;

            case PieceType.Rook:
                // 룩: 상하좌우 직선 슬라이딩
                for (int i = 1; i <= 3; i++)
                {
                    offsets.Add(new Vector2Int(0, i));
                    offsets.Add(new Vector2Int(0, -i));
                    offsets.Add(new Vector2Int(-i, 0));
                    offsets.Add(new Vector2Int(i, 0));
                }
                break;

            case PieceType.Bishop:
                // 비숍: 대각선 4방향 슬라이딩
                for (int i = 1; i <= 3; i++)
                {
                    offsets.Add(new Vector2Int(i, i));
                    offsets.Add(new Vector2Int(i, -i));
                    offsets.Add(new Vector2Int(-i, i));
                    offsets.Add(new Vector2Int(-i, -i));
                }
                break;

            case PieceType.Knight:
                // 나이트: L자 점프 8칸
                offsets.AddRange(new Vector2Int[] {
                    new(2, 1), new(2, -1), new(-2, 1), new(-2, -1),
                    new(1, 2), new(1, -2), new(-1, 2), new(-1, -2)
                });
                break;

            case PieceType.Amazon:
                // 아마존: 퀸(직선+대각선 8방향 슬라이딩) + 나이트(L자 점프 8칸)
                for (int i = 1; i <= 3; i++)
                {
                    offsets.Add(new Vector2Int(0, i));
                    offsets.Add(new Vector2Int(0, -i));
                    offsets.Add(new Vector2Int(-i, 0));
                    offsets.Add(new Vector2Int(i, 0));
                    offsets.Add(new Vector2Int(i, i));
                    offsets.Add(new Vector2Int(i, -i));
                    offsets.Add(new Vector2Int(-i, i));
                    offsets.Add(new Vector2Int(-i, -i));
                }
                offsets.AddRange(new Vector2Int[] {
                    new(2, 1), new(2, -1), new(-2, 1), new(-2, -1),
                    new(1, 2), new(1, -2), new(-1, 2), new(-1, -2)
                });
                break;

            case PieceType.Chancellor:
                // 챈슬러: 룩(직선 4방향 슬라이딩) + 나이트(L자 점프 8칸)
                for (int i = 1; i <= 3; i++)
                {
                    offsets.Add(new Vector2Int(0, i));
                    offsets.Add(new Vector2Int(0, -i));
                    offsets.Add(new Vector2Int(-i, 0));
                    offsets.Add(new Vector2Int(i, 0));
                }
                offsets.AddRange(new Vector2Int[] {
                    new(2, 1), new(2, -1), new(-2, 1), new(-2, -1),
                    new(1, 2), new(1, -2), new(-1, 2), new(-1, -2)
                });
                break;

            case PieceType.Archbishop:
                // 아치비숍: 비숍(대각선 4방향 슬라이딩) + 나이트(L자 점프 8칸)
                for (int i = 1; i <= 3; i++)
                {
                    offsets.Add(new Vector2Int(i, i));
                    offsets.Add(new Vector2Int(i, -i));
                    offsets.Add(new Vector2Int(-i, i));
                    offsets.Add(new Vector2Int(-i, -i));
                }
                offsets.AddRange(new Vector2Int[] {
                    new(2, 1), new(2, -1), new(-2, 1), new(-2, -1),
                    new(1, 2), new(1, -2), new(-1, 2), new(-1, -2)
                });
                break;

            case PieceType.Centaur:
                // 켄타우로스: 킹(8방향 1칸) + 나이트(L자 점프 8칸)
                offsets.AddRange(new[] {
                    Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
                    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
                });
                offsets.AddRange(new Vector2Int[] {
                    new(2, 1), new(2, -1), new(-2, 1), new(-2, -1),
                    new(1, 2), new(1, -2), new(-1, 2), new(-1, -2)
                });
                break;

            case PieceType.Cannon:
                // 캐논: 평상시 룩과 동일한 4방향 직선 이동
                for (int i = 1; i <= 3; i++)
                {
                    offsets.Add(new Vector2Int(0, i));
                    offsets.Add(new Vector2Int(0, -i));
                    offsets.Add(new Vector2Int(-i, 0));
                    offsets.Add(new Vector2Int(i, 0));
                }
                break;

            case PieceType.Grasshopper:
                // 그래스호퍼: 퀸의 8방향 도약 이동
                for (int i = 1; i <= 3; i++)
                {
                    offsets.Add(new Vector2Int(0, i));
                    offsets.Add(new Vector2Int(0, -i));
                    offsets.Add(new Vector2Int(-i, 0));
                    offsets.Add(new Vector2Int(i, 0));
                    offsets.Add(new Vector2Int(i, i));
                    offsets.Add(new Vector2Int(i, -i));
                    offsets.Add(new Vector2Int(-i, i));
                    offsets.Add(new Vector2Int(-i, -i));
                }
                break;

            case PieceType.Pawn:
            default:
                // 폰: 전진 1칸 (및 초기 2칸)
                if (isEnemy)
                {
                    offsets.Add(Vector2Int.down);
                    offsets.Add(new Vector2Int(0, -2));
                }
                else
                {
                    offsets.Add(Vector2Int.up);
                    offsets.Add(new Vector2Int(0, 2));
                }
                break;
        }

        return offsets;
    }

    /// <summary>
    /// 지정된 기물 타입의 이동범위 패턴 텍스트를 생성합니다 (상점 슬롯용)
    /// </summary>
    public static string GenerateMovementPatternForType(PieceType pieceType, SealData seal = null)
    {
        bool replacesMovement = false;
        List<Vector2Int> baseOffsets = GetBaseMovementOffsetsForType(pieceType, false);

        // 코드 인젝션의 추가 오프셋 계산
        List<Vector2Int> additionalOffsets = GetTooltipAdditionalOffsetsForType(pieceType, seal, out replacesMovement);
        if (replacesMovement)
        {
            baseOffsets = new List<Vector2Int>();
        }

        // 7x7 그리드 생성 (중심: 3,3)
        const int gridSize = 7;
        const int center = 3;
        char[,] grid = new char[gridSize, gridSize];

        // 빈 공간으로 초기화
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                grid[y, x] = ' ';
            }
        }

        // 중심에 현재 기물 표시
        grid[center, center] = 'W';

        // 기본 이동범위: 터미널 그린 □ (B = Basic)
        foreach (var offset in baseOffsets)
        {
            int gridX = center + offset.x;
            int gridY = center - offset.y; // Y축 반전 (화면 좌표)

            if (gridX >= 0 && gridX < gridSize && gridY >= 0 && gridY < gridSize)
            {
                if (grid[gridY, gridX] != 'W')
                    grid[gridY, gridX] = 'B';
            }
        }

        if (!replacesMovement && pieceType == PieceType.Pawn)
        {
            Vector2Int[] capOffsets = { new Vector2Int(-1, 1), new Vector2Int(1, 1) };
            foreach (var cap in capOffsets)
            {
                int gx = center + cap.x;
                int gy = center - cap.y;
                if (gx >= 0 && gx < gridSize && gy >= 0 && gy < gridSize && grid[gy, gx] == ' ')
                {
                    grid[gy, gx] = 'C';
                }
            }
        }

        // 인장의 추가 이동범위: 시안 ▣ (S = Seal)
        foreach (var offset in additionalOffsets)
        {
            int gridX = center + offset.x;
            int gridY = center - offset.y;

            if (gridX >= 0 && gridX < gridSize && gridY >= 0 && gridY < gridSize)
            {
                if (grid[gridY, gridX] != 'W')
                    grid[gridY, gridX] = 'S';
            }
        }

        // 텍스트로 변환 (색상 코드 적용)
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[{GetPieceNameForType(pieceType)}]\n");

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                char c = grid[y, x];
                string symbol;

                if (c == ' ')
                    symbol = FormatTooltipCell("#FFFFFF", "□"); // 하얀색 빈칸
                else if (c == 'W')
                    symbol = FormatTooltipCell("#FFFFFF", "■"); // 현재 기물
                else if (c == 'B')
                    symbol = FormatTooltipCell("#80FF00", "□"); // 연두색 이동 가능
                else if (c == 'C')
                    symbol = FormatTooltipCell("#FF5555", "×"); // 대각선 포획 전용
                else if (c == 'S')
                    symbol = FormatTooltipCell("#FF80FF", "□"); // 핑크색 인장 추가
                else if (c == 'G')
                    symbol = FormatTooltipCell("#00CC00", "■"); // 초록색 다른 기물
                else
                    symbol = c.ToString();

                sb.Append(symbol);
                if (x < gridSize - 1) sb.Append(" ");
            }
            if (y < gridSize - 1) sb.Append("\n");
        }

        if (pieceType == PieceType.Pawn)
        {
            sb.Append("\n<size=85%><color=#88FF88>□:전진(초기2칸) / </color><color=#FF5555>×:대각선 포획</color></size>");
        }
        else if (pieceType == PieceType.Knight)
        {
            sb.Append("\n<size=85%><color=#88FF88>※ 장애물 점프(Jump) 이동 가능</color></size>");
        }
        else if (pieceType == PieceType.Amazon)
        {
            sb.Append("\n<size=85%><color=#FFD700>※ 퀸의 8방향 슬라이딩 + 나이트 점프(Jump)</color></size>");
        }
        else if (pieceType == PieceType.Chancellor)
        {
            sb.Append("\n<size=85%><color=#00FFFF>※ 룩의 4방향 직선 슬라이딩 + 나이트 점프(Jump)</color></size>");
        }
        else if (pieceType == PieceType.Archbishop)
        {
            sb.Append("\n<size=85%><color=#DA70D6>※ 비숍의 4방향 대각선 슬라이딩 + 나이트 점프(Jump)</color></size>");
        }
        else if (pieceType == PieceType.Centaur)
        {
            sb.Append("\n<size=85%><color=#FFA500>※ 킹의 8방향 1칸 이동 + 나이트 점프(Jump)</color></size>");
        }
        else if (pieceType == PieceType.Cannon)
        {
            sb.Append("\n<size=85%><color=#FF69B4>※ 룩의 4방향 이동 / 적 포획 시 기물 1개를 뛰어넘어야 함</color></size>");
        }
        else if (pieceType == PieceType.Grasshopper)
        {
            sb.Append("\n<size=85%><color=#98FB98>※ 퀸의 8방향 도약 / 기물 1개를 뛰어넘어 바로 뒤 칸으로만 이동 및 포획</color></size>");
        }

        return sb.ToString();
    }

    private static string FormatTooltipCell(string colorHex, string symbol)
    {
        return $"<size={TooltipCellSizePercent}%><color={colorHex}>{symbol}</color></size>";
    }

    private bool HasMovementReplacementPreview()
    {
        if (equippedSeals == null)
        {
            return false;
        }

        foreach (var seal in equippedSeals)
        {
            if (seal != null && seal.ReplacesMovementPreview)
            {
                return true;
            }
        }

        return false;
    }

    private List<Vector2Int> GetTooltipAdditionalOffsets(bool replacesMovement)
    {
        if (equippedSeals == null || equippedSeals.Count == 0)
        {
            return new List<Vector2Int>();
        }

        List<Vector2Int> additionalOffsets = new List<Vector2Int>();
        List<Vector2Int> baseOffsets = GetBaseMovementOffsets();

        foreach (var seal in equippedSeals)
        {
            if (seal == null)
            {
                continue;
            }

            List<Vector2Int> previewOffsets = replacesMovement
                ? seal.GetPreviewMovementOffsets(pieceType, isEnemy)
                : seal.GetPreviewAdditionalMovementOffsets(pieceType, isEnemy);
            foreach (var offset in previewOffsets)
            {
                if ((replacesMovement || !baseOffsets.Contains(offset)) && !additionalOffsets.Contains(offset))
                {
                    additionalOffsets.Add(offset);
                }
            }
        }

        return additionalOffsets;
    }

    public static string GetPieceNameForType(PieceType type)
    {
        return type switch
        {
            PieceType.King => "King",
            PieceType.Queen => "Queen",
            PieceType.Rook => "Rook",
            PieceType.Bishop => "Bishop",
            PieceType.Knight => "Knight",
            PieceType.Pawn => "Pawn",
            PieceType.Amazon => "Amazon",
            PieceType.Chancellor => "Chancellor",
            PieceType.Archbishop => "Archbishop",
            PieceType.Centaur => "Centaur",
            PieceType.Cannon => "Cannon",
            PieceType.Grasshopper => "Grasshopper",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// 기물의 레어도를 반환합니다 (폰, 킹, 마이너 피스: 1 / 메이저 피스(퀸), 켄타우로스, 캐논, 그래스호퍼: 2 / 에픽(룩, 챈슬러, 아치비숍): 3 / 변형 체스 전설 기물(아마존 등): 5).
    /// </summary>
    public static int GetRarityForType(PieceType type)
    {
        return type switch
        {
            PieceType.Amazon => 5,
            PieceType.Chancellor or PieceType.Archbishop or PieceType.Rook => 3,
            PieceType.Queen or PieceType.Centaur or PieceType.Cannon or PieceType.Grasshopper => 2,
            _ => 1
        };
    }

    /// <summary>
    /// 체스 기물의 가치 점수를 반환합니다 (PieceData에 정의된 score 참조)
    /// </summary>
    public static int GetChessValueForType(PieceType type)
    {
        return PieceData.GetPieceScore(type);
    }

    /// <summary>
    /// 체스 기물의 핵심 특수 룰 요약 텍스트를 반환합니다
    /// </summary>
    public static string GetSpecialRuleTextForType(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => "※ 전방 1칸 이동(첫 이동 시 2칸) / 대각선 1칸 적 포획",
            PieceType.Knight => "※ 장애물 무시 L자 도약 (8방향 점프)",
            PieceType.Bishop => "※ 대각선 4방향 원거리 슬라이딩",
            PieceType.Rook => "※ 상하좌우 4방향 직선 원거리 슬라이딩",
            PieceType.Queen => "※ 가로·세로·대각선 8방향 전방위 슬라이딩",
            PieceType.King => "※ 가로·세로·대각선 모든 방향 1칸 이동",
            PieceType.Amazon => "※ 퀸의 전방위 슬라이딩 + 나이트의 L자 도약",
            PieceType.Chancellor => "※ 룩의 직선 슬라이딩 + 나이트의 L자 도약",
            PieceType.Archbishop => "※ 비숍의 대각선 슬라이딩 + 나이트의 L자 도약",
            PieceType.Centaur => "※ 킹의 8방향 1칸 이동 + 나이트의 L자 도약",
            PieceType.Cannon => "※ 룩처럼 직선 이동 / 적 포획 시 기물 1개를 뛰어넘어야 함",
            PieceType.Grasshopper => "※ 퀸 방향 도약 / 기물 1개를 뛰어넘어 바로 뒤 칸에 착지",
            _ => ""
        };
    }

    public static List<Vector2Int> GetTooltipAdditionalOffsetsForType(PieceType pieceType, SealData seal, out bool replacesMovement)
    {
        replacesMovement = false;

        if (seal == null)
        {
            return new List<Vector2Int>();
        }

        List<Vector2Int> baseOffsets = GetBaseMovementOffsetsForType(pieceType, false);
        SealBase previewSeal = null;

        if (seal.sealPrefab != null)
        {
            previewSeal = seal.sealPrefab.GetComponent<SealBase>();
        }

        if (previewSeal == null)
        {
            return new List<Vector2Int>();
        }

        replacesMovement = previewSeal.ReplacesMovementPreview;
        List<Vector2Int> previewOffsets = replacesMovement
            ? previewSeal.GetPreviewMovementOffsets(pieceType, false)
            : previewSeal.GetPreviewAdditionalMovementOffsets(pieceType, false);
        List<Vector2Int> additionalOffsets = new List<Vector2Int>();

        foreach (var offset in previewOffsets)
        {
            if ((replacesMovement || !baseOffsets.Contains(offset)) && !additionalOffsets.Contains(offset))
            {
                additionalOffsets.Add(offset);
            }
        }

        return additionalOffsets;
    }

    private void OnDestroy()
    {
        if (isDragging)
        {
            isAnyDragging = false;
        }

        rotationTween?.Kill();
        positionTween?.Kill();
        scaleTween?.Kill();
        threatenedTween?.Kill();
    }
    // Returns a list of active keyword strings for tooltip display (무적, 보호, 회피, 역습)
    public System.Collections.Generic.List<string> GetActiveKeywords()
    {
        var keywords = new System.Collections.Generic.List<string>();
        if (IsInvincible) keywords.Add("무적");
        if (ProtectionStacks > 0) keywords.Add("보호");
        if (EvasionStacks > 0) keywords.Add("회피");
        if (CounterattackStacks > 0) keywords.Add("역습");
        return keywords;
    }

    /// <summary>
    /// 기물에 활성화된 키워드/상태 효과의 상세 정보를 반환합니다 (무적, 보호, 회피, 역습)
    /// </summary>
    public System.Collections.Generic.List<KeywordInfo> GetActiveKeywordInfos()
    {
        var list = new System.Collections.Generic.List<KeywordInfo>();

        if (IsInvincible)
        {
            string countText = invincibilityTurns == -1
                ? "상시 지속"
                : (invincibilityTurns > 0 ? $"{invincibilityTurns}턴 지속" : "인장 지속");
            list.Add(new KeywordInfo(
                "무적 (Invincible)",
                countText,
                "지속 시간 동안 모든 공격과 파괴 효과로부터 피해를 받지 않습니다.",
                new Color(0.35f, 1f, 0.45f) // 라임그린
            ));
        }

        if (protectionStacks > 0)
        {
            list.Add(new KeywordInfo(
                "보호 (Shield)",
                $"×{protectionStacks}",
                "다음 1회의 적 공격을 완전히 무효화하고 보호 1개를 소모합니다.",
                new Color(1f, 0.85f, 0.1f) // 황금색
            ));
        }

        if (evasionStacks > 0)
        {
            list.Add(new KeywordInfo(
                "회피 (Evasion)",
                $"×{evasionStacks}",
                "적의 공격을 받을 때 피해를 입지 않고 인접한 빈 칸으로 도약합니다.",
                new Color(0.2f, 0.65f, 1f) // 스카이블루
            ));
        }

        if (counterattackStacks > 0)
        {
            list.Add(new KeywordInfo(
                "역습 (Counter)",
                $"×{counterattackStacks}",
                "적에게 공격을 받을 때 반격하여 공격한 적 기물을 즉시 처치합니다.",
                new Color(1f, 0.35f, 0.25f) // 선홍색
            ));
        }

        return list;
    }


}


