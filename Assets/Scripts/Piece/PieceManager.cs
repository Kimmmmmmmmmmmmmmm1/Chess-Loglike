using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

public class PieceManager : MonoBehaviour
{
    public static PieceManager Instance { get; private set; }

    [Header("Grid Reference")]
    public GridManager gridManager;

    [Header("Prefabs")]
    public GameObject moveMarkerPrefab;
    public RectTransform markersParent;
    public GameObject destructionEffectPrefab;


    [Header("Piece Sprite Database")]
    public PieceSpriteData spriteData;

    public PieceSpriteData SpriteData
    {
        get
        {
            if (spriteData == null)
            {
                spriteData = PieceSpriteData.Instance;
            }
            return spriteData;
        }
    }

    // Properties for backwards compatibility and easy access
    public Sprite kingSprite => GetSpriteFor(PieceType.King);
    public Sprite queenSprite => GetSpriteFor(PieceType.Queen);
    public Sprite rookSprite => GetSpriteFor(PieceType.Rook);
    public Sprite bishopSprite => GetSpriteFor(PieceType.Bishop);
    public Sprite knightSprite => GetSpriteFor(PieceType.Knight);
    public Sprite pawnSprite => GetSpriteFor(PieceType.Pawn);

    public Sprite enemyKingSprite => GetEnemySpriteFor(PieceType.King);
    public Sprite enemyQueenSprite => GetEnemySpriteFor(PieceType.Queen);
    public Sprite enemyRookSprite => GetEnemySpriteFor(PieceType.Rook);
    public Sprite enemyBishopSprite => GetEnemySpriteFor(PieceType.Bishop);
    public Sprite enemyKnightSprite => GetEnemySpriteFor(PieceType.Knight);
    public Sprite enemyPawnSprite => GetEnemySpriteFor(PieceType.Pawn);

    // Backwards compatibility properties (deprecated Janggi names)
    [System.Obsolete("Use queenSprite instead.")]
    public Sprite cannonSprite => queenSprite;
    [System.Obsolete("Use rookSprite instead.")]
    public Sprite chariotSprite => rookSprite;
    [System.Obsolete("Use bishopSprite instead.")]
    public Sprite elephantSprite => bishopSprite;
    [System.Obsolete("Use knightSprite instead.")]
    public Sprite horseSprite => knightSprite;
    [System.Obsolete("Use pawnSprite instead.")]
    public Sprite soldierSprite => pawnSprite;
    [System.Obsolete("Use enemyKingSprite instead.")]
    public Sprite enemykingSprite => enemyKingSprite;
    [System.Obsolete("Use enemyQueenSprite instead.")]
    public Sprite enemycannonSprite => enemyQueenSprite;
    [System.Obsolete("Use enemyRookSprite instead.")]
    public Sprite enemychariotSprite => enemyRookSprite;
    [System.Obsolete("Use enemyBishopSprite instead.")]
    public Sprite enemyelephantSprite => enemyBishopSprite;
    [System.Obsolete("Use enemyKnightSprite instead.")]
    public Sprite enemyhorseSprite => enemyKnightSprite;
    [System.Obsolete("Use enemyPawnSprite instead.")]
    public Sprite enemysoldierSprite => enemyPawnSprite;

    [Header("Settings")]
    [SerializeField] private float captureDestroyDelay = 0.25f;
    [SerializeField] private int playerPrepareMaxY = -1;
    [SerializeField] private int maxPlayerPiecesOnBoard = 4;
    public int PlayerPrepareMaxY => playerPrepareMaxY;
    public int MaxPlayerPiecesOnBoard => maxPlayerPiecesOnBoard;
    public const int AbsoluteMaxPlayerPieces = 10;
    private bool hideEnemyTerritoryDuringPrepare;
    public bool IsEnemyTerritoryHiddenDuringPrepare => hideEnemyTerritoryDuringPrepare &&
        GameStateManager.Instance != null &&
        GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI pieceCountText;

    private readonly List<MoveMarker> activeMarkers = new();
    private readonly List<PieceController> pieces = new();
    private readonly List<PieceController> playerPieces = new();
    private readonly List<PieceController> enemyPieces = new();
    private readonly Dictionary<Vector2Int, PieceController> pieceByPosition = new();
    private readonly Dictionary<PieceController, Vector2Int> placementSnapshot = new();
    private bool hasPlacementSnapshot = false;
    private PieceController selectedPiece;
    private bool positionCacheDirty = true;
    private bool threatenedUpdateQueued = false;
    private bool threatenedUpdateRunning = false;
    private const int ThreatenedBatchSize = 6;
    private int currentTurnNumber = 1;
    private bool whiteMovedInTurn = false;
    private bool blackMovedInTurn = false;

    public PieceController SelectedPiece => selectedPiece;
    public IReadOnlyList<PieceController> Pieces => pieces;
    public IReadOnlyList<PieceController> PlayerPieces => playerPieces;
    public IReadOnlyList<PieceController> EnemyPieces => enemyPieces;
    public bool HasPlacementSnapshot => hasPlacementSnapshot && placementSnapshot.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnStateChanged;
        }
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced += HandleTurnAdvanced;
        }
        UpdatePieceCountUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnStateChanged;
        }
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= HandleTurnAdvanced;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void HandleTurnAdvanced(TurnOwner newTurn)
    {
        foreach (PieceController piece in pieces)
        {
            if (piece != null)
            {
                piece.AdvanceBuffTurn();
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (markersParent == null)
        {
            GameObject parentObj = GameObject.Find("MarkersParent");
            if (parentObj != null)
            {
                markersParent = parentObj.GetComponent<RectTransform>();
            }
        }
    }

    private void OnStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Prepare)
        {
            ResetTurnNumber();
            TurnLogUIManager.Instance?.ClearAllLogs();

            if (!hasPlacementSnapshot)
            {
                CapturePlacementSnapshot();
            }
            MovePlayerPiecesToSafety();
        }
        else if (newState == GameStateManager.GameState.GamePlay)
        {
            ResetTurnNumber();
        }

        foreach (var piece in pieces)
        {
            if (piece != null)
            {
                piece.RefreshThreatenedVisuals();
                piece.SetHiddenDuringPrepare(newState == GameStateManager.GameState.Prepare &&
                                             hideEnemyTerritoryDuringPrepare &&
                                             piece.IsEnemy);
            }
        }
    }

    public void SetHideEnemyTerritoryDuringPrepare(bool hide)
    {
        hideEnemyTerritoryDuringPrepare = hide;
        bool isPrepare = GameStateManager.Instance != null &&
                         GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;

        foreach (var piece in pieces)
        {
            if (piece != null && piece.IsEnemy)
            {
                piece.SetHiddenDuringPrepare(isPrepare && hide);
            }
        }
    }

    public void SelectPiece(PieceController piece)
    {
        if (piece == null || (selectedPiece == piece && activeMarkers.Count > 0))
        {
            return;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay)
        {
            if (PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsPromoting)
            {
                return;
            }

            if (piece != null && !piece.IsEnemy && TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn)
            {
                return;
            }
        }

        selectedPiece = piece;
        
        if (GameStateManager.Instance != null && 
            (GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay || 
             GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare))
        {
            ShowMoveMarkers(piece.GetCandidateMoves());
        }
    }

    public bool IsSelected(PieceController piece)
    {
        return selectedPiece == piece;
    }

    public void RegisterPiece(PieceController piece)
    {
        if (piece == null || pieces.Contains(piece))
        {
            return;
        }

        pieces.Add(piece);
        if (piece.IsEnemy)
        {
            enemyPieces.Add(piece);
        }
        else
        {
            playerPieces.Add(piece);
        }
        MarkPiecePositionCacheDirty();
        UpdatePieceCountUI();
    }

    public void UnregisterPiece(PieceController piece)
    {
        if (piece == null)
        {
            return;
        }

        pieces.Remove(piece);
        if (piece.IsEnemy)
        {
            enemyPieces.Remove(piece);
        }
        else
        {
            playerPieces.Remove(piece);
        }
        MarkPiecePositionCacheDirty();
        UpdatePieceCountUI();
        CheckPieceCounts();
    }

    public PieceController GetPieceAt(Vector2Int gridPosition)
    {
        if (positionCacheDirty)
        {
            RebuildPositionCache();
        }

        return pieceByPosition.TryGetValue(gridPosition, out PieceController piece) ? piece : null;
    }

    public bool TryForceMovePiece(PieceController piece, Vector2Int target)
    {
        if (piece == null || piece.CurrentLocation != PieceLocation.Board || !piece.gridPosition.HasValue ||
            GetPieceAt(target) != null || gridManager == null || !gridManager.IsInBounds(target))
        {
            return false;
        }

        GridCell cell = gridManager.GetGridCell(target);
        if (cell != null && cell.isDestroyed) return false;

        Vector2Int previous = piece.gridPosition.Value;
        piece.OnStartMove(previous, target);
        piece.MoveToGrid(target);
        piece.OnMoveFinished(previous, target);
        UpdateThreatenedStatus();
        return true;
    }

    public void MarkPiecePositionCacheDirty()
    {
        positionCacheDirty = true;
    }

    public void EnsurePositionCacheReady()
    {
        if (positionCacheDirty)
        {
            RebuildPositionCache();
        }
    }

    private void RebuildPositionCache()
    {
        pieceByPosition.Clear();

        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            PieceController piece = pieces[i];
            if (piece == null)
            {
                pieces.RemoveAt(i);
                continue;
            }

            if (piece.CurrentLocation != PieceLocation.Board || !piece.gridPosition.HasValue)
            {
                continue;
            }

            pieceByPosition[piece.gridPosition.Value] = piece;
        }

        positionCacheDirty = false;
    }

    public List<PieceController> GetPiecesAtGridPoint(Vector2Int gridPointPosition)
    {
        List<PieceController> affectedPieces = new List<PieceController>();

        Vector2Int[] surroundingCells = new Vector2Int[]
        {
            new Vector2Int(gridPointPosition.x - 1, gridPointPosition.y - 1),
            new Vector2Int(gridPointPosition.x, gridPointPosition.y - 1),
            new Vector2Int(gridPointPosition.x - 1, gridPointPosition.y),
            new Vector2Int(gridPointPosition.x, gridPointPosition.y)
        };

        foreach (var cellPos in surroundingCells)
        {
            PieceController piece = GetPieceAt(cellPos);
            if (piece != null && !affectedPieces.Contains(piece))
            {
                affectedPieces.Add(piece);
            }
        }

        return affectedPieces;
    }

    public void RemovePieceAtPosition(Vector2Int gridPosition)
    {
        PieceController piece = GetPieceAt(gridPosition);
        if (piece != null)
        {
            DestroyPieceWithEffects(piece, false, false, null);
        }
    }

    public void RemovePiece(PieceController piece)
    {
        if (piece == null)
        {
            return;
        }

        DestroyPieceWithEffects(piece, false, false, null);
    }

    public void RevertPromotionSealedPiecesToSoldier()
    {
        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        bool hasBoardReplacement = false;

        foreach (PieceController piece in allPieces)
        {
            if (piece == null || piece.IsEnemy)
            {
                continue;
            }

            if (!piece.HasPromotionSeal() || piece.Type == PieceType.Pawn)
            {
                continue;
            }

            if (piece.CurrentLocation == PieceLocation.Board && piece.gridPosition.HasValue)
            {
                ReplaceBoardPieceWithSoldier(piece, piece.gridPosition.Value);
                hasBoardReplacement = true;
                continue;
            }

            if (piece.CurrentLocation == PieceLocation.Inventory)
            {
                ReplaceInventoryPieceWithSoldier(piece);
            }
        }

        if (hasBoardReplacement)
        {
            UpdateThreatenedStatus();
        }
    }

    private void ReplaceBoardPieceWithSoldier(PieceController piece, Vector2Int position)
    {
        if (PieceSpawner.Instance == null || PieceSpawner.Instance.piecePrefab == null)
        {
            return;
        }

        // 승급의 인장을 제외한 기존 인장들(카멜레온의 인장 등) 보존
        List<SealData> retainedSeals = new List<SealData>();
        if (piece.EquippedSeals != null)
        {
            foreach (var seal in piece.EquippedSeals)
            {
                if (seal != null && seal.Data != null &&
                    seal.Data.sealName != "승급의 인장" && seal.Data.sealName != "승급자의 인장")
                {
                    retainedSeals.Add(seal.Data);
                }
            }
        }

        Vector3 spawnPos = piece.transform.position;
        UnregisterPiece(piece);
        piece.gridPosition = null;

        if (Application.isPlaying) Destroy(piece.gameObject);
        else DestroyImmediate(piece.gameObject);

        GameObject newPieceObj = Instantiate(
            PieceSpawner.Instance.piecePrefab,
            spawnPos,
            Quaternion.identity,
            PieceSpawner.Instance.piecesParent
        );

        PieceController newPiece = newPieceObj != null ? newPieceObj.GetComponent<PieceController>() : null;
        if (newPiece != null)
        {
            newPiece.Initialize(PieceType.Pawn, false);
            newPiece.MoveToGrid(position);

            foreach (var sData in retainedSeals)
            {
                if (sData != null)
                {
                    newPiece.EquipSeal(sData);
                }
            }
        }
    }

    private void ReplaceInventoryPieceWithSoldier(PieceController piece)
    {
        if (PieceSpawner.Instance == null || PieceSpawner.Instance.piecePrefab == null)
        {
            return;
        }

        Transform slotTransform = piece.transform.parent;
        if (slotTransform == null)
        {
            return;
        }

        // 승급의 인장을 제외한 기존 인장들 보존
        List<SealData> retainedSeals = new List<SealData>();
        if (piece.EquippedSeals != null)
        {
            foreach (var seal in piece.EquippedSeals)
            {
                if (seal != null && seal.Data != null &&
                    seal.Data.sealName != "승급의 인장" && seal.Data.sealName != "승급자의 인장")
                {
                    retainedSeals.Add(seal.Data);
                }
            }
        }

        if (PieceInventory.Instance != null)
        {
            PieceInventory.Instance.RemovePiece(piece.Type);
        }

        if (Application.isPlaying) Destroy(piece.gameObject);
        else DestroyImmediate(piece.gameObject);

        GameObject newPieceObj = Instantiate(PieceSpawner.Instance.piecePrefab, slotTransform);
        PieceController newPiece = newPieceObj != null ? newPieceObj.GetComponent<PieceController>() : null;
        if (newPiece != null)
        {
            newPiece.Initialize(PieceType.Pawn, false);
            newPiece.MoveToInventory(slotTransform);

            foreach (var sData in retainedSeals)
            {
                if (sData != null)
                {
                    newPiece.EquipSeal(sData);
                }
            }
        }
    }

    public void RemovePieceAtPositionByCollapse(Vector2Int gridPosition)
    {
        PieceController piece = GetPieceAt(gridPosition);
        if (piece != null)
        {
            CollapsePieceAndDestroy(piece);
        }
    }

    private void CollapsePieceAndDestroy(PieceController piece)
    {
        if (piece == null)
        {
            return;
        }

        PlayDestroySfx();

        RectTransform pieceRect = piece.GetComponent<RectTransform>();
        if (pieceRect == null)
        {
            piece.gridPosition = null;
            UnregisterPiece(piece);
            if (Application.isPlaying) Destroy(piece.gameObject);
            else DestroyImmediate(piece.gameObject);
            return;
        }

        if (EffectManager.Instance != null)
        {
            Color debrisColor = piece.PieceImage != null ? piece.PieceImage.color : Color.white;
            EffectManager.Instance.PlayCollapseExplosion(pieceRect.position, debrisColor, piece.IsEnemy, 1f);
        }

        piece.gridPosition = null;
        UnregisterPiece(piece);

        CanvasGroup canvasGroup = piece.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = piece.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float baseDistance = (gridManager != null) ? gridManager.cellSize.x * 2.4f : 220f;
        Vector2 targetPos = pieceRect.anchoredPosition + (Vector2.down * baseDistance);
        float duration = 0.55f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(pieceRect.DOAnchorPos(targetPos, duration).SetEase(Ease.InQuad));
        sequence.Join(pieceRect.DOScale(0.75f, duration).SetEase(Ease.InQuad));
        sequence.Join(canvasGroup.DOFade(0f, duration).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            if (piece != null)
            {
                Destroy(piece.gameObject);
            }
        });
    }

    private void DestroyPieceWithEffects(PieceController piece, bool notifyCapture, bool recordCapture, PieceController killer)
    {
        if (piece == null)
        {
            return;
        }

        PlayDestroySfx();

        ArtifactEffectHandlers.OnTombstoneDestroyCount(piece);

        if (ArtifactEffectHandlers.TryPrepareGourdRecovery(piece, out InventorySlot recoverySlot))
        {
            PlayPieceDestroyVfx(piece);
            PlayGourdRecoveryAnimation(piece, recoverySlot);
            return;
        }

        PlayPieceDestroyVfx(piece);

        if (piece.gridPosition.HasValue)
        {
            piece.OnDestroyed(killer, piece.gridPosition.Value);
        }

        if (notifyCapture && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPieceCaptured();
        }

        if (recordCapture && GameManager.Instance != null)
        {
            GameManager.Instance.RecordCapture(piece.IsEnemy, piece.Type);
        }

        piece.gridPosition = null;
        UnregisterPiece(piece);

        if (Application.isPlaying) Destroy(piece.gameObject, captureDestroyDelay);
        else DestroyImmediate(piece.gameObject);
    }

    private void PlayGourdRecoveryAnimation(PieceController piece, InventorySlot targetSlot)
    {
        if (piece == null || targetSlot == null)
        {
            return;
        }

        RectTransform pieceRect = piece.GetComponent<RectTransform>();
        if (pieceRect == null)
        {
            piece.MoveToInventory(targetSlot.transform);
            return;
        }

        CanvasGroup canvasGroup = piece.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = piece.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        pieceRect.DOKill();
        piece.transform.DOKill();

        float shardLeadTime = 0.12f;
        float flyDuration = 0.35f;

        Sequence recoverySequence = DOTween.Sequence();
        recoverySequence.AppendInterval(shardLeadTime);
        recoverySequence.Append(piece.transform.DOMove(targetSlot.transform.position, flyDuration).SetEase(Ease.InOutQuad));
        recoverySequence.Join(piece.transform.DOScale(0.82f, flyDuration).SetEase(Ease.InQuad));
        recoverySequence.OnComplete(() =>
        {
            piece.transform.localScale = Vector3.one;
            piece.MoveToInventory(targetSlot.transform);
        });
    }

    private void PlayPieceDestroyVfx(PieceController piece)
    {
        if (piece == null)
        {
            return;
        }

        if (EffectManager.Instance != null)
        {
            Color targetColor = piece.PieceImage != null ? piece.PieceImage.color : Color.white;

            float scale = 1f;
            switch (piece.Type)
            {
                case PieceType.King:
                case PieceType.Queen:
                case PieceType.Amazon:
                case PieceType.Chancellor:
                case PieceType.Archbishop:
                case PieceType.Centaur:
                case PieceType.Cannon:
                case PieceType.Grasshopper:
                    scale = 1.2f;
                    break;
                case PieceType.Pawn:
                    scale = 0.8f;
                    break;
                default:
                    scale = 1f;
                    break;
            }

            EffectManager.Instance.PlayExplosion(piece.transform.position, targetColor, piece.IsEnemy, scale);
        }

        if (destructionEffectPrefab != null)
        {
            Instantiate(destructionEffectPrefab, piece.GetComponent<RectTransform>().position, Quaternion.identity);
        }
    }

    private void PlayDestroySfx()
    {
        if (Application.isPlaying && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SFXType.Destroy);
        }
    }

    public Sprite GetSpriteFor(PieceType pieceType)
    {
        if (SpriteData != null)
        {
            return SpriteData.GetSprite(pieceType, false);
        }
        return null;
    }

    public Sprite GetEnemySpriteFor(PieceType pieceType)
    {
        if (SpriteData != null)
        {
            return SpriteData.GetSprite(pieceType, true);
        }
        return null;
    }

    public int GetPlayerPieceCountOnBoard()
    {
        int count = 0;
        foreach (var piece in playerPieces)
        {
            if (piece != null && piece.CurrentLocation == PieceLocation.Board && piece.gridPosition.HasValue)
            {
                count++;
            }
        }
        return count;
    }

    public void IncreaseMaxPlayerPieces(int amount)
    {
        maxPlayerPiecesOnBoard += amount;
        UpdatePieceCountUI();
    }

    public void UpdatePieceCountUI()
    {
        if (pieceCountText != null)
        {
            pieceCountText.text = $"배치 가능 기물: {GetPlayerPieceCountOnBoard()}/{maxPlayerPiecesOnBoard}";
        }
    }

    public static Sequence PlayJumpAnimation(
        PieceController piece,
        Transform targetTransform,
        float jumpPower,
        float duration,
        System.Action onComplete = null,
        Ease ease = Ease.OutQuad,
        float scaleFromZeroDuration = 0f,
        Transform flyParentOverride = null)
    {
        if (piece == null || targetTransform == null)
        {
            return null;
        }

        Canvas rootCanvas = targetTransform.GetComponentInParent<Canvas>();
        Transform flyParent = flyParentOverride != null
            ? flyParentOverride
            : (rootCanvas != null ? rootCanvas.transform : targetTransform.root);

        piece.transform.SetParent(flyParent, true);
        piece.transform.SetAsLastSibling();

        CanvasGroup canvasGroup = piece.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        piece.transform.DOKill();

        Sequence sequence = DOTween.Sequence();

        if (scaleFromZeroDuration > 0f)
        {
            sequence.Join(piece.transform.DOScale(Vector3.one, scaleFromZeroDuration).SetEase(Ease.OutBack));
        }

        sequence.Join(piece.transform.DOJump(targetTransform.position, jumpPower, 1, duration).SetEase(ease));
        sequence.OnComplete(() =>
        {
            if (piece != null && canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
            }

            onComplete?.Invoke();
        });

        return sequence;
    }

    public static Sequence MovePieceToInventoryWithJump(
        PieceController piece,
        InventorySlot targetSlot,
        float jumpPower,
        float duration,
        System.Action onComplete = null,
        bool addToInventoryData = true,
        Ease ease = Ease.OutQuad,
        float scaleFromZeroDuration = 0f,
        Transform flyParentOverride = null)
    {
        if (piece == null || targetSlot == null)
        {
            onComplete?.Invoke();
            return null;
        }

        return PlayJumpAnimation(
            piece,
            targetSlot.transform,
            jumpPower,
            duration,
            () =>
            {
                if (piece != null)
                {
                    piece.MoveToInventory(targetSlot.transform, addToInventoryData);
                }
                onComplete?.Invoke();
            },
            ease,
            scaleFromZeroDuration,
            flyParentOverride);
    }

    public static Sequence MovePieceToTransformWithJump(
        PieceController piece,
        Transform targetTransform,
        float jumpPower,
        float duration,
        System.Action onComplete = null,
        bool addToInventoryData = true,
        Ease ease = Ease.OutQuad,
        float scaleFromZeroDuration = 0f,
        Transform flyParentOverride = null)
    {
        if (piece == null || targetTransform == null)
        {
            onComplete?.Invoke();
            return null;
        }

        return PlayJumpAnimation(
            piece,
            targetTransform,
            jumpPower,
            duration,
            () =>
            {
                if (piece != null)
                {
                    piece.MoveToInventory(targetTransform, addToInventoryData);
                }
                onComplete?.Invoke();
            },
            ease,
            scaleFromZeroDuration,
            flyParentOverride);
    }

    public bool MoveSelectedTo(Vector2Int gridPosition)
    {
        if (selectedPiece == null)
        {
            return false;
        }

        if (TryMovePiece(selectedPiece, gridPosition))
        {
            ClearMarkers();
            selectedPiece = null;
            return true;
        }

        return false;
    }

    public bool TryMovePiece(PieceController piece, Vector2Int gridPosition)
    {
        if (piece == null) return false;
        if (!piece.gridPosition.HasValue) return false;

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay)
        {
            if (PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsPromoting)
            {
                return false;
            }

            if (!piece.IsEnemy && TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn)
            {
                return false;
            }

            // 액티브 유물 대상 선택 중에는 플레이어 이동 불가
            if (!piece.IsEnemy && ActiveArtifactController.IsTargeting)
            {
                return false;
            }
        }

        Vector2Int fromPosition = piece.gridPosition.Value;

        PieceController targetPiece = GetPieceAt(gridPosition);
        PieceType? capturedType = targetPiece != null ? targetPiece.Type : null;
        bool? capturedIsEnemy = targetPiece != null ? targetPiece.IsEnemy : null;
        if (targetPiece != null)
        {
            if (targetPiece.IsEnemy == piece.IsEnemy) return false;
            if (targetPiece.IsInvincible) return false;

            AttackResponse response = ResolveAttackResponse(piece, targetPiece);
            if (response != AttackResponse.None)
            {
                UpdateThreatenedStatus();
                AdvanceTurnAfterMove();
                return true;
            }

            if (!targetPiece.CanBeDestroyed())
            {
                return false;
            }

            DestroyPieceWithEffects(targetPiece, true, true, piece);

            if (piece == null || !piece.gridPosition.HasValue)
            {
                UpdateThreatenedStatus();

                if (TurnManager.Instance != null)
                {
                    if (PiecePromotionManager.Instance == null || !PiecePromotionManager.Instance.IsPromotionPanelOpen())
                    {
                        TurnManager.Instance.AdvanceTurn();
                    }
                }

                return true;
            }

            if (EffectManager.Instance != null)
            {
                EffectManager.Instance.PlaySlowMotion();
            }
        }

        Vector2Int prevPos = piece.gridPosition.Value;
        piece.OnStartMove(prevPos, gridPosition);
        piece.MoveToGrid(gridPosition);

        // 체스닷컴 스타일: 이동 전 칸과 이동 후 칸 하이라이트
        if (gridManager != null)
        {
            gridManager.HighlightLastMove(prevPos, gridPosition);
        }

        LogTurnAction(piece, prevPos, gridPosition, capturedType, capturedIsEnemy);
        
        piece.OnMoveFinished(prevPos, gridPosition);

        if (capturedType.HasValue)
        {
            piece.OnCapturedPiece(capturedType.Value);
        }

        UpdateThreatenedStatus();

        AdvanceTurnAfterMove();
        return true;
    }

    private enum AttackResponse
    {
        None,
        Protected,
        Evaded,
        Counterattacked
    }

    private AttackResponse ResolveAttackResponse(PieceController attacker, PieceController defender)
    {
        // 여러 버프가 동시에 있으면 보호 -> 회피 -> 역공 순서로 하나만 발동합니다.
        if (defender.ConsumeBuff(PieceBuffType.Protection))
        {
            ActivateBuffEffect(defender, PieceBuffType.Protection, "공격 무효화");
            return AttackResponse.Protected;
        }

        Vector2Int? evasionPosition = FindEvasionPosition(defender);
        if (evasionPosition.HasValue)
        {
            defender.ConsumeBuff(PieceBuffType.Evasion);
            defender.OnStartMove(defender.gridPosition.Value, evasionPosition.Value);
            Vector2Int previousPosition = defender.gridPosition.Value;
            defender.MoveToGrid(evasionPosition.Value);
            defender.OnMoveFinished(previousPosition, evasionPosition.Value);
            ActivateBuffEffect(defender, PieceBuffType.Evasion, "주변 빈 칸으로 회피");
            return AttackResponse.Evaded;
        }

        if (defender.ConsumeBuff(PieceBuffType.Counterattack))
        {
            if (attacker.CanBeDestroyed())
            {
                DestroyPieceWithEffects(attacker, true, true, defender);
                defender.OnCapturedPiece(attacker.Type);
                ActivateBuffEffect(defender, PieceBuffType.Counterattack, "공격자를 역공");
                return AttackResponse.Counterattacked;
            }

            // The counterattack stack is refunded when another destruction veto prevents it.
            defender.AddBuff(PieceBuffType.Counterattack);
        }

        return AttackResponse.None;
    }

    private void ActivateBuffEffect(PieceController defender, PieceBuffType buffType, string effectDescription)
    {
        // 공통 발동 연출은 PieceController에서 관리하고, 이곳에서는 로그만 조합합니다.
        defender.PlayBuffActivationEffect(buffType);

        string buffName = buffType switch
        {
            PieceBuffType.Protection => "보호",
            PieceBuffType.Evasion => "회피",
            PieceBuffType.Counterattack => "역공",
            _ => "버프"
        };

        TurnLogUIManager.Instance?.AddLog(
            $"{buffName} 발동: {effectDescription} (남은 스택 {defender.GetBuffStacks(buffType)})",
            defender.IsEnemy);
    }

    private Vector2Int? FindEvasionPosition(PieceController defender)
    {
        if (defender.GetBuffStacks(PieceBuffType.Evasion) <= 0 ||
            defender.gridPosition == null ||
            gridManager == null)
        {
            return null;
        }

        Vector2Int origin = defender.gridPosition.Value;
        Vector2Int[] adjacentOffsets =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        foreach (Vector2Int offset in adjacentOffsets)
        {
            Vector2Int candidate = origin + offset;
            GridCell cell = gridManager.GetGridCell(candidate);
            if (cell != null && !cell.isDestroyed && GetPieceAt(candidate) == null)
            {
                return candidate;
            }
        }

        return null;
    }

    private void AdvanceTurnAfterMove()
    {
        if (TurnManager.Instance != null &&
            (PiecePromotionManager.Instance == null || !PiecePromotionManager.Instance.IsPromotionPanelOpen()))
        {
            TurnManager.Instance.AdvanceTurn();
        }
    }

    public void ResetTurnNumber()
    {
        currentTurnNumber = 1;
        whiteMovedInTurn = false;
        blackMovedInTurn = false;
        if (gridManager != null)
        {
            gridManager.ClearLastMoveHighlight();
        }
    }

    private string GetTurnPrefix(bool isEnemy)
    {
        string prefix = isEnemy ? $"{currentTurnNumber}... " : $"{currentTurnNumber}. ";
        if (isEnemy)
        {
            blackMovedInTurn = true;
        }
        else
        {
            whiteMovedInTurn = true;
        }

        if (whiteMovedInTurn && blackMovedInTurn)
        {
            currentTurnNumber++;
            whiteMovedInTurn = false;
            blackMovedInTurn = false;
        }

        return prefix;
    }

    public static string GetPieceAlgebraicSymbol(PieceType type)
    {
        return type switch
        {
            PieceType.King => "K",
            PieceType.Queen => "Q",
            PieceType.Rook => "R",
            PieceType.Bishop => "B",
            PieceType.Knight => "N",
            PieceType.Pawn => "",
            PieceType.Amazon => "A",
            PieceType.Chancellor => "C",
            PieceType.Archbishop => "H",
            PieceType.Centaur => "T",
            PieceType.Cannon => "X",
            PieceType.Grasshopper => "G",
            _ => ""
        };
    }

    public static string GetPieceTypeKoreanName(PieceType type)
    {
        return type switch
        {
            PieceType.King => "킹",
            PieceType.Queen => "퀸",
            PieceType.Rook => "룩",
            PieceType.Bishop => "비숍",
            PieceType.Knight => "나이트",
            PieceType.Pawn => "폰",
            PieceType.Amazon => "아마존",
            PieceType.Chancellor => "챈슬러",
            PieceType.Archbishop => "아치비숍",
            PieceType.Centaur => "켄타우로스",
            PieceType.Cannon => "캐논",
            PieceType.Grasshopper => "그래스호퍼",
            _ => type.ToString()
        };
    }

    private void LogTurnAction(PieceController mover, Vector2Int from, Vector2Int to, PieceType? capturedType, bool? capturedIsEnemy)
    {
        if (mover == null)
        {
            return;
        }

        MarkPiecePositionCacheDirty();

        string fromPos = FormatDisplayGridPosition(from);
        string toPos = FormatDisplayGridPosition(to);
        bool isCapture = capturedType.HasValue;

        // 1. 체스 기보(Algebraic Notation) 기본 문자열 생성
        string notation = "";
        if (mover.Type == PieceType.Pawn)
        {
            if (isCapture)
            {
                notation = $"{fromPos[0]}x{toPos}";
            }
            else
            {
                notation = toPos;
            }

            // 승급선 도달 시 적군은 즉시 퀸(=Q)으로 표기
            if (gridManager != null)
            {
                int promoRank = mover.IsEnemy ? gridManager.gridMinBounds.y : (gridManager.gridMinBounds.y + gridManager.boardHeight - 1);
                if (to.y == promoRank && mover.IsEnemy)
                {
                    notation += "=Q";
                }
            }
        }
        else
        {
            string symbol = GetPieceAlgebraicSymbol(mover.Type);

            // 동일 기물 출발 위치 모호성(Disambiguation) 해소
            string disambiguation = "";
            List<PieceController> others = new List<PieceController>();
            for (int i = 0; i < pieces.Count; i++)
            {
                PieceController p = pieces[i];
                if (p != null && p != mover && p.IsEnemy == mover.IsEnemy && p.Type == mover.Type && p.CurrentLocation == PieceLocation.Board && p.gridPosition.HasValue)
                {
                    var validMoves = p.GetCandidateMoves();
                    if (validMoves != null && validMoves.Contains(to))
                    {
                        others.Add(p);
                    }
                }
            }

            if (others.Count > 0)
            {
                bool sameFile = false;
                bool sameRank = false;
                for (int i = 0; i < others.Count; i++)
                {
                    if (others[i].gridPosition.Value.x == from.x) sameFile = true;
                    if (others[i].gridPosition.Value.y == from.y) sameRank = true;
                }

                if (!sameFile)
                {
                    disambiguation = fromPos[0].ToString();
                }
                else if (!sameRank)
                {
                    disambiguation = fromPos[1].ToString();
                }
                else
                {
                    disambiguation = fromPos;
                }
            }

            if (isCapture)
            {
                notation = $"{symbol}{disambiguation}x{toPos}";
            }
            else
            {
                notation = $"{symbol}{disambiguation}{toPos}";
            }
        }

        // 2. 체크(+) 및 체크메이트(#) 판별
        if (capturedType == PieceType.King)
        {
            notation += "#";
        }
        else
        {
            PieceController oppKing = null;
            for (int i = 0; i < pieces.Count; i++)
            {
                PieceController p = pieces[i];
                if (p != null && p.IsEnemy != mover.IsEnemy && p.Type == PieceType.King && p.CurrentLocation == PieceLocation.Board && p.gridPosition.HasValue)
                {
                    oppKing = p;
                    break;
                }
            }

            if (oppKing != null && oppKing.gridPosition.HasValue)
            {
                Vector2Int kingPos = oppKing.gridPosition.Value;
                bool isCheck = false;

                var moverMoves = mover.GetCandidateMoves();
                if (moverMoves != null && moverMoves.Contains(kingPos))
                {
                    isCheck = true;
                }
                else
                {
                    for (int i = 0; i < pieces.Count; i++)
                    {
                        PieceController p = pieces[i];
                        if (p != null && p != mover && p.IsEnemy == mover.IsEnemy && p.CurrentLocation == PieceLocation.Board && p.gridPosition.HasValue)
                        {
                            var pm = p.GetCandidateMoves();
                            if (pm != null && pm.Contains(kingPos))
                            {
                                isCheck = true;
                                break;
                            }
                        }
                    }
                }

                if (isCheck)
                {
                    notation += "+";
                }
            }
        }

        // 3. 턴 번호 접두사
        string turnPrefix = GetTurnPrefix(mover.IsEnemy);

        // 4. 로그 문자열 포맷팅
        bool useSimpleTurnLog = SettingsManager.Instance != null
            && SettingsManager.Instance.Settings != null
            && SettingsManager.Instance.Settings.simpleTurnLog;

        string logStr;
        if (useSimpleTurnLog)
        {
            logStr = $"{turnPrefix}{notation}";
        }
        else
        {
            string moverTypeName = GetPieceTypeKoreanName(mover.Type);
            if (isCapture && capturedType.HasValue)
            {
                string targetTypeName = GetPieceTypeKoreanName(capturedType.Value);
                logStr = $"{turnPrefix}{notation} ({moverTypeName} {fromPos} → {toPos}, {targetTypeName} 포획)";
            }
            else
            {
                logStr = $"{turnPrefix}{notation} ({moverTypeName} {fromPos} → {toPos})";
            }
        }

        TurnLogUIManager.Instance?.AddLog(logStr, mover.IsEnemy);
    }

    private string FormatDisplayGridPosition(Vector2Int gridPos)
    {
        if (gridManager == null)
        {
            return $"({gridPos.x},{gridPos.y})";
        }

        int fileIndex = Mathf.Clamp(gridPos.x - gridManager.gridMinBounds.x, 0, 25);
        int rankNumber = gridPos.y - gridManager.gridMinBounds.y + 1;
        char fileChar = (char)('a' + fileIndex);
        return $"{fileChar}{rankNumber}";
    }

    public void UpdateThreatenedStatus()
    {
        MarkPiecePositionCacheDirty();
        threatenedUpdateQueued = true;

        if (!threatenedUpdateRunning)
        {
            StartCoroutine(UpdateThreatenedStatusRoutine());
        }
    }

    private IEnumerator UpdateThreatenedStatusRoutine()
    {
        threatenedUpdateRunning = true;

        while (threatenedUpdateQueued)
        {
            threatenedUpdateQueued = false;

            HashSet<PieceController> threatenedSet = new HashSet<PieceController>();

            int enemyBatchSize = 4;

            int processedEnemies = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                PieceController piece = pieces[i];
                if (piece == null || piece.CurrentLocation != PieceLocation.Board || !piece.IsEnemy)
                {
                    continue;
                }

                List<Vector2Int> moves = piece.GetCandidateMoves();
                for (int m = 0; m < moves.Count; m++)
                {
                    PieceController target = GetPieceAt(moves[m]);
                    if (target != null && !target.IsEnemy)
                    {
                        threatenedSet.Add(target);
                    }
                }

                processedEnemies++;
                if (processedEnemies % enemyBatchSize == 0)
                {
                    yield return null;
                }
            }

            int appliedCount = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                PieceController piece = pieces[i];
                if (piece == null)
                {
                    continue;
                }

                piece.SetThreatened(threatenedSet.Contains(piece));

                appliedCount++;
                if (appliedCount % 8 == 0)
                {
                    yield return null;
                }
            }
        }

        threatenedUpdateRunning = false;
    }

    public bool HasAnyPlayerMoves()
    {
        return HasAnyMovesForSide(false);
    }

    public bool HasAnyEnemyMoves()
    {
        return HasAnyMovesForSide(true);
    }

    public bool HasAnyMovesForSide(bool isEnemySide)
    {
        var targetList = isEnemySide ? enemyPieces : playerPieces;
        foreach (var piece in targetList)
        {
            if (piece != null && piece.IsEnemy == isEnemySide && piece.CurrentLocation == PieceLocation.Board)
            {
                if (piece.GetCandidateMoves().Count > 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void MovePlayerPiecesToSafety()
    {
        StartCoroutine(MovePlayerPiecesToSafetyRoutine());
    }

    private IEnumerator MovePlayerPiecesToSafetyRoutine()
    {
        if (gridManager == null) yield break;
        if (hasPlacementSnapshot)
        {
            foreach (var kv in placementSnapshot)
            {
                var piece = kv.Key;
                var pos = kv.Value;
                if (piece != null && !piece.IsEnemy)
                {
                    piece.MoveToGrid(pos);
                    yield return new WaitForSeconds(0.05f);
                }
            }

            yield break;
        }

        List<PieceController> piecesToMove = new List<PieceController>();
        HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();

        int safeMaxY = playerPrepareMaxY;

        foreach (var piece in pieces)
        {
            if (piece == null || piece.CurrentLocation != PieceLocation.Board) continue;

            if (piece.IsEnemy)
            {
                if (piece.gridPosition.HasValue)
                {
                    occupiedPositions.Add(piece.gridPosition.Value);
                }
            }
            else
            {
                if (piece.gridPosition.HasValue && piece.gridPosition.Value.y > safeMaxY)
                {
                    piecesToMove.Add(piece);
                }
                else if (piece.gridPosition.HasValue)
                {
                    occupiedPositions.Add(piece.gridPosition.Value);
                }
            }
        }

        foreach (var piece in piecesToMove)
        {
            Vector2Int? targetPos = FindEmptySafePosition(occupiedPositions, safeMaxY);
            if (targetPos.HasValue)
            {
                occupiedPositions.Add(targetPos.Value);
                piece.MoveToGrid(targetPos.Value);
                yield return new WaitForSeconds(0.15f);
            }
        }
    }

    private Vector2Int? FindEmptySafePosition(HashSet<Vector2Int> occupied, int maxY)
    {
        int minX = gridManager.gridMinBounds.x;
        int width = gridManager.boardWidth;
        int minY = gridManager.gridMinBounds.y;
        
        List<int> xCoords = new List<int>();
        for (int x = minX; x < minX + width; x++) xCoords.Add(x);
        float centerX = minX + (width - 1) / 2f;
        xCoords.Sort((a, b) => Mathf.Abs(a - centerX).CompareTo(Mathf.Abs(b - centerX)));
        
        for (int y = maxY; y >= minY; y--)
        {
            foreach (int x in xCoords)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!occupied.Contains(pos))
                {
                    return pos;
                }
            }
        }
        return null;
    }

    public void CapturePlacementSnapshot()
    {
        placementSnapshot.Clear();
        foreach (var piece in pieces)
        {
            if (piece == null || piece.IsEnemy) continue;
            if (piece.CurrentLocation == PieceLocation.Board && piece.gridPosition.HasValue)
            {
                placementSnapshot[piece] = piece.gridPosition.Value;
            }
        }
        hasPlacementSnapshot = placementSnapshot.Count > 0;
    }

    public void ClearPlacementSnapshot()
    {
        placementSnapshot.Clear();
        hasPlacementSnapshot = false;
    }

    public bool RestorePlacementPositions(bool logIfEmpty = true)
    {
        if (!HasPlacementSnapshot)
        {
            return false;
        }
        var items = new List<System.Collections.Generic.KeyValuePair<PieceController, Vector2Int>>(placementSnapshot);
        int restoredByReference = 0;
        int missingReference = 0;
        HashSet<PieceController> restoredPieces = new HashSet<PieceController>();
        List<Vector2Int> unresolvedTargets = new List<Vector2Int>();

        foreach (var kv in items)
        {
            var piece = kv.Key;
            var pos = kv.Value;
            if (piece != null && !piece.IsEnemy)
            {
                piece.MoveToGrid(pos);
                restoredPieces.Add(piece);
                restoredByReference++;
            }
            else
            {
                unresolvedTargets.Add(pos);
                missingReference++;
            }
        }

        if (unresolvedTargets.Count > 0)
        {
            List<PieceController> candidates = new List<PieceController>();
            foreach (var piece in pieces)
            {
                if (piece == null || piece.IsEnemy)
                {
                    continue;
                }

                if (piece.CurrentLocation != PieceLocation.Board || !piece.gridPosition.HasValue)
                {
                    continue;
                }

                if (restoredPieces.Contains(piece))
                {
                    continue;
                }

                candidates.Add(piece);
            }

            int assignCount = Mathf.Min(unresolvedTargets.Count, candidates.Count);
            for (int i = 0; i < assignCount; i++)
            {
                candidates[i].MoveToGrid(unresolvedTargets[i]);
            }

        }

        ClearPlacementSnapshot();
        return true;
    }

    public void ClearSelection()
    {
        ClearMarkers();
        selectedPiece = null;
    }

    public void HideMoveMarkers()
    {
        ClearMarkers();
    }

    public void ShowEnemyMoveMarkers()
    {
        ClearMarkers();

        if (moveMarkerPrefab == null)
        {
            return;
        }

        List<Vector2Int> all = new List<Vector2Int>();
        for (int i = 0; i < enemyPieces.Count; i++)
        {
            var enemy = enemyPieces[i];
            if (enemy == null) continue;
            if (enemy.CurrentLocation != PieceLocation.Board || !enemy.gridPosition.HasValue) continue;
            all.AddRange(enemy.GetCandidateMoves());
        }

        SpawnMarkersFromPositions(all, true, true);
    }

    private void SpawnMarkersFromPositions(IEnumerable<Vector2Int> positions, bool isEnemy, bool dedupe = true)
    {
        if (moveMarkerPrefab == null) return;

        HashSet<Vector2Int> added = dedupe ? new HashSet<Vector2Int>() : null;

        foreach (var gridPos in positions)
        {
            if (!IsInBounds(gridPos)) continue;
            if (dedupe && added.Contains(gridPos)) continue;

            GameObject markerObject = Instantiate(moveMarkerPrefab, markersParent);
            MoveMarker marker = markerObject.GetComponent<MoveMarker>();

            RectTransform markerTransform = markerObject.GetComponent<RectTransform>();
            if (markerTransform != null)
            {
                markerTransform.anchoredPosition = GridToUiPosition(gridPos);
                Vector2 cellSize = gridManager != null ? gridManager.cellSize : new Vector2(42f, 42f);
                markerTransform.sizeDelta = cellSize;
            }

            if (marker != null)
            {
                PieceController pieceAt = GetPieceAt(gridPos);
                bool isCapture = pieceAt != null && pieceAt.IsEnemy != isEnemy && !pieceAt.IsHidden;
                marker.Initialize(this, gridPos, isEnemy, isCapture);
                activeMarkers.Add(marker);
                added?.Add(gridPos);
            }
        }
    }

    public MoveMarker GetMarkerAtPosition(Vector2 screenPosition)
    {
        PointerEventData pointerData = new(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            MoveMarker marker = result.gameObject.GetComponent<MoveMarker>();
            if (marker != null && activeMarkers.Contains(marker))
            {
                return marker;
            }
        }

        return null;
    }

    public Vector2 GridToUiPosition(Vector2Int gridPosition)
    {
        if (gridManager != null)
            return gridManager.GridToUiPosition(gridPosition);
        return Vector2.zero;
    }

    private void ShowMoveMarkers(List<Vector2Int> moves)
    {
        ClearMarkers();
        SpawnMarkersFromPositions(moves, selectedPiece != null && selectedPiece.IsEnemy, true);
    }

    private bool IsInBounds(Vector2Int gridPosition)
    {
        if (gridManager != null)
            return gridManager.IsInBounds(gridPosition);
        return false;
    }

    private void ClearMarkers()
    {
        for (int i = 0; i < activeMarkers.Count; i++)
        {
            if (activeMarkers[i] != null)
            {
                Destroy(activeMarkers[i].gameObject);
            }
        }

        activeMarkers.Clear();
    }

    public void CheckPieceCounts()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            return;
        }

        bool isEnemyPromoting = PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsEnemyPromoting;
        bool isPlayerPromoting = PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsPlayerPromoting;

        int enemyOnBoardCount = CountPiecesOnBoard(enemyPieces);
        int playerOnBoardCount = CountPiecesOnBoard(playerPieces);

        if (enemyOnBoardCount == 0 && !isEnemyPromoting)
        {
            OnAllEnemyPiecesRemoved();
        }

        if (playerOnBoardCount == 0 && !isPlayerPromoting)
        {
            OnAllPlayerPiecesRemoved();
        }
    }

    private int CountPiecesOnBoard(List<PieceController> targetList)
    {
        int count = 0;
        for (int i = targetList.Count - 1; i >= 0; i--)
        {
            PieceController piece = targetList[i];
            if (piece == null)
            {
                targetList.RemoveAt(i);
                continue;
            }

            if (piece.CurrentLocation == PieceLocation.Board && piece.gridPosition.HasValue)
            {
                count++;
            }
        }

        return count;
    }

    private void OnAllEnemyPiecesRemoved()
    {
        GameStateManager.Instance?.ChangeState(GameStateManager.GameState.Win);
    }

    private void OnAllPlayerPiecesRemoved()
    {
        GameStateManager.Instance?.ChangeState(GameStateManager.GameState.GameOver);
    }
}
