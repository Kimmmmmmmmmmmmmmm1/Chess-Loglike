using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Linq;

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
    private readonly List<MoveMarker> selfDestructRangeMarkers = new();
    private readonly List<MoveMarker> shotgunAimMarkers = new();
    private readonly HashSet<Vector2Int> shotgunAimMarkerPositions = new();
    private readonly HashSet<Vector2Int> shotgunAimHighlightedPositions = new();
    private PieceController activeMovingAttacker = null;

    private float lastDestructionTime = -10f;
    private int pendingDestructionCount = 0;

    public void IncrementPendingDestruction() => pendingDestructionCount++;
    public void DecrementPendingDestruction() => pendingDestructionCount = Mathf.Max(0, pendingDestructionCount - 1);
    public bool HasPendingDestruction => pendingDestructionCount > 0;

    public bool IsBusyWithAnimations()
    {
        if (pieces != null)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                PieceController piece = pieces[i];
                if (piece != null && piece.IsAnimatingMove)
                {
                    return true;
                }
            }
        }

        if (pendingDestructionCount > 0)
        {
            return true;
        }

        if (Time.unscaledTime - lastDestructionTime < 0.25f)
        {
            return true;
        }

        if (EffectManager.Instance != null && EffectManager.Instance.IsPlayingAnyDestructionEffect())
        {
            return true;
        }

        return false;
    }

    public bool HasDestructionEffectInProgress()
    {
        if (pendingDestructionCount > 0)
        {
            return true;
        }

        if (Time.unscaledTime - lastDestructionTime < 0.25f)
        {
            return true;
        }

        if (EffectManager.Instance != null && EffectManager.Instance.IsPlayingAnyDestructionEffect())
        {
            return true;
        }

        return false;
    }

    public bool IsPieceMovingToCapture(PieceController piece) => piece != null && activeMovingAttacker == piece;
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

    public event System.Action<PieceController> OnPieceRegistered;
    public event System.Action<PieceController> OnPieceUnregistered;

    public PieceController SelectedPiece => selectedPiece;
    public IReadOnlyList<PieceController> Pieces => pieces;
    public IReadOnlyList<PieceController> PlayerPieces => playerPieces;
    public IReadOnlyList<PieceController> EnemyPieces => enemyPieces;
    public bool HasPlacementSnapshot => hasPlacementSnapshot && placementSnapshot.Count > 0;
    public IReadOnlyDictionary<PieceController, Vector2Int> PlacementSnapshot => placementSnapshot;

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
        TurnOwner endedTurn = newTurn == TurnOwner.Player ? TurnOwner.Opponent : TurnOwner.Player;
        foreach (PieceController piece in pieces)
        {
            if (piece != null)
            {
                piece.AdvanceBuffTurn();
                piece.AdvanceStatusTurn(endedTurn);
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

        // 1. 기물이 날아가는 중(물리 시뮬레이션/동기화 중)에는 기물 선택 무시
        if (PieceFlickLauncher.IsPhysicsActive)
        {
            return;
        }

        // 2. 알까기 유물을 보유 중일 때, 내 기물(아군 기물)은 체스 이동 마커를 띄우지 않음 (상대 턴이든 내 턴이든 항상)
        if (piece != null && !piece.IsEnemy && PieceFlickLauncher.HasAlkkagiArtifact)
        {
            selectedPiece = piece;
            ClearMarkers();
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

    public void RefreshSelectedPieceMoves(PieceController piece)
    {
        if (piece == null || selectedPiece != piece) return;
        ClearMarkers();
        if (piece.CurrentLocation == PieceLocation.Board && piece.GridPosition.HasValue)
            ShowMoveMarkers(piece.GetCandidateMoves());
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
        OnPieceRegistered?.Invoke(piece);
    }

    public void UnregisterPiece(PieceController piece)
    {
        if (piece == null)
        {
            return;
        }

        pieces.Remove(piece);
        enemyPieces.Remove(piece);
        playerPieces.Remove(piece);
        MarkPiecePositionCacheDirty();
        UpdatePieceCountUI();
        CheckPieceCounts();
        OnPieceUnregistered?.Invoke(piece);
    }

    public void NotifyPieceTeamChanged(PieceController piece)
    {
        if (piece == null)
        {
            return;
        }

        if (!pieces.Contains(piece) && piece.CurrentLocation == PieceLocation.Board)
        {
            pieces.Add(piece);
        }

        enemyPieces.Remove(piece);
        playerPieces.Remove(piece);

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

    /// <summary>폭군의 난사 획득 시 아군 기물을 한 번에 숙청합니다.</summary>
    public void PurgePlayerArmyExceptKing(PieceController keptKing, PieceController replacedKing = null)
    {
        if (selectedPiece != null && selectedPiece != keptKing) ClearSelection();
        ClearMarkers();

        Vector2Int kingStartPosition = default;
        bool hasKingStartPosition = keptKing != null && placementSnapshot.TryGetValue(keptKing, out kingStartPosition);
        if (!hasKingStartPosition && replacedKing != null)
            hasKingStartPosition = placementSnapshot.TryGetValue(replacedKing, out kingStartPosition);

        List<PieceController> removedPieces = new List<PieceController>();
        foreach (PieceController piece in playerPieces)
        {
            if (piece != null && piece != keptKing) removedPieces.Add(piece);
        }

        foreach (PieceController piece in removedPieces)
        {
            pieces.Remove(piece);
            playerPieces.Remove(piece);
            if (piece.GridPosition.HasValue) pieceByPosition.Remove(piece.GridPosition.Value);
            piece.gridPosition = null;
            OnPieceUnregistered?.Invoke(piece);
        }

        MarkPiecePositionCacheDirty();
        placementSnapshot.Clear();
        if (hasKingStartPosition) placementSnapshot[keptKing] = kingStartPosition;
        hasPlacementSnapshot = hasKingStartPosition;
        UpdatePieceCountUI();
        CheckPieceCounts();
    }

    /// <summary>원거리 공격에서 이동 없이 포획 처리합니다.</summary>
    public bool TryCapturePieceWithAttack(PieceController attacker, PieceController target, out PieceType capturedType, out Vector2Int capturedPosition)
    {
        capturedType = PieceType.Pawn;
        capturedPosition = Vector2Int.zero;
        if (attacker == null || attacker.IsBlind || target == null || !target.GridPosition.HasValue || attacker.IsEnemy == target.IsEnemy || target.IsInvincible)
            return false;

        capturedType = target.Type;
        capturedPosition = target.GridPosition.Value;
        AttackResponse response = ResolveAttackResponse(attacker, target);
        if (response != AttackResponse.None) return false;
        if (!target.CanBeDestroyed()) return false;

        DestroyPieceWithEffects(target, true, true, attacker);
        if (target != null && target.CurrentLocation == PieceLocation.Board && target.GridPosition.HasValue) return false;
        if (attacker != null && attacker.CurrentLocation == PieceLocation.Board)
        {
            attacker.OnCapturedPiece(capturedType);
        }
        return true;
    }

    /// <summary>보호 파괴 후 효과를 위한 턴 소모 없는 인접 회피입니다.</summary>
    public bool TryEvadePieceImmediately(PieceController piece)
    {
        if (piece == null || !piece.GridPosition.HasValue || gridManager == null) return false;
        Vector2Int[] offsets =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        Vector2Int from = piece.GridPosition.Value;
        foreach (Vector2Int offset in offsets)
        {
            Vector2Int target = from + offset;
            if (!gridManager.IsInBounds(target) || GetPieceAt(target) != null) continue;
            GridCell cell = gridManager.GetGridCell(target);
            if (cell == null || cell.isDestroyed) continue;
            piece.OnStartMove(from, target);
            piece.MoveToGrid(target);
            piece.OnMoveFinished(from, target);
            gridManager.HighlightLastMove(from, target);
            UpdateThreatenedStatus();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 보드 상태(PieceLocation.Board)이지만 실제 타일 좌표(gridPosition)가 없거나,
    /// 보드 컨테이너(BoardContainer)의 자식이 아닌 기물(및 인벤토리 슬롯에 속하지 않는 고아 기물)을
    /// 모든 데이터 및 씬에서 완전히 제거합니다. (상대 턴 시작 전 AI 이동 전에 호출)
    /// </summary>
    public int CleanupInvalidBoardPieces()
    {
        Transform boardContainer = null;
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (gridManager != null && gridManager.boardContainer != null)
        {
            boardContainer = gridManager.boardContainer;
        }
        else
        {
            GameObject bcObj = GameObject.Find("BoardContainer");
            if (bcObj != null)
            {
                boardContainer = bcObj.transform;
            }
            else if (PieceSpawner.Instance != null && PieceSpawner.Instance.piecesParent != null)
            {
                boardContainer = PieceSpawner.Instance.piecesParent;
            }
        }

        List<PieceController> toRemove = new List<PieceController>();

        // 씬 내의 모든 PieceController 검사
        PieceController[] allScenePieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        foreach (var p in allScenePieces)
        {
            if (p == null) continue;

            bool isChildOfBoard = boardContainer != null && p.transform.IsChildOf(boardContainer);
            bool isUnderInventorySlot = p.GetComponentInParent<InventorySlot>() != null;

            bool shouldRemove = false;

            // 1. 보드 기물인데 타일 좌표가 없거나 보드 컨테이너 자식이 아닌 경우
            if (p.CurrentLocation == PieceLocation.Board)
            {
                if (!p.gridPosition.HasValue || (boardContainer != null && !isChildOfBoard))
                {
                    shouldRemove = true;
                }
            }
            // 2. 보드 컨테이너 자식이 아닌데, 정상 인벤토리 슬롯에도 들어있지 않은 경우 (상점/임시/고아 기물)
            else if (boardContainer != null && !isChildOfBoard && !isUnderInventorySlot)
            {
                shouldRemove = true;
            }

            if (shouldRemove && !toRemove.Contains(p))
            {
                toRemove.Add(p);
            }
        }

        // PieceManager 등록 목록에서도 이중 검사
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            PieceController p = pieces[i];
            if (p == null)
            {
                pieces.RemoveAt(i);
                continue;
            }

            bool isChildOfBoard = boardContainer != null && p.transform.IsChildOf(boardContainer);
            bool isUnderInventorySlot = p.GetComponentInParent<InventorySlot>() != null;

            if (p.CurrentLocation == PieceLocation.Board)
            {
                if (!p.gridPosition.HasValue || (boardContainer != null && !isChildOfBoard))
                {
                    if (!toRemove.Contains(p)) toRemove.Add(p);
                }
            }
            else if (boardContainer != null && !isChildOfBoard && !isUnderInventorySlot)
            {
                if (!toRemove.Contains(p)) toRemove.Add(p);
            }
        }

        if (toRemove.Count == 0)
        {
            return 0;
        }

        int removedCount = toRemove.Count;
        foreach (var piece in toRemove)
        {
            if (piece == null) continue;

            string parentName = piece.transform.parent != null ? piece.transform.parent.name : "루트";
            Debug.LogWarning($"[PieceManager] 보드 컨테이너 자식이 아니거나 좌표 없는 비정상 기물 정리: {piece.name} ({piece.Type}, Enemy: {piece.IsEnemy}, Location: {piece.CurrentLocation}, Parent: [{parentName}])");

            UnregisterPiece(piece);

            if (selectedPiece == piece)
            {
                ClearSelection();
            }

            if (Application.isPlaying)
            {
                Destroy(piece.gameObject);
            }
            else
            {
                DestroyImmediate(piece.gameObject);
            }
        }

        MarkPiecePositionCacheDirty();
        UpdatePieceCountUI();
        CheckPieceCounts();

        return removedCount;
    }

    public void RemovePieceFromPhysics(PieceController piece)
    {
        if (piece == null)
        {
            return;
        }

        Rigidbody body = piece.GetComponent<Rigidbody>();
        if (body != null)
        {
            Destroy(body);
        }

        Collider collider = piece.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        piece.gameObject.SetActive(false);
        DestroyPieceWithEffects(piece, true, true, null);
    }

    public IEnumerator WaitForThreatenedStatusReady()
    {
        while (threatenedUpdateQueued || threatenedUpdateRunning)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 전투 승리 후 [전향의 인장]이 장착된 임시 전향 기물을 소멸시켜 원래 덱 밸런스를 보존합니다.
    /// </summary>
    public void RemoveConversionSealedPieces()
    {
        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        bool removedAny = false;

        foreach (PieceController piece in allPieces)
        {
            if (piece == null || !piece.HasConversionSeal())
            {
                continue;
            }

            pieces.Remove(piece);
            enemyPieces.Remove(piece);
            playerPieces.Remove(piece);
            placementSnapshot.Remove(piece);

            if (selectedPiece == piece)
            {
                ClearSelection();
            }

            piece.gridPosition = null;
            OnPieceUnregistered?.Invoke(piece);

            if (Application.isPlaying)
            {
                Destroy(piece.gameObject);
            }
            else
            {
                DestroyImmediate(piece.gameObject);
            }

            removedAny = true;
        }

        if (removedAny)
        {
            MarkPiecePositionCacheDirty();
            UpdatePieceCountUI();
            UpdateThreatenedStatus();
        }
    }

    /// <summary>
    /// 15턴 제한 또는 다수결 판정 등으로 전투가 종료될 때 보드에 남아있는 모든 적 기물을 제거합니다.
    /// </summary>
    public void RemoveAllEnemyPieces(bool playVfx = true)
    {
        ClearSelection();
        ClearMarkers();
        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        bool removedAny = false;

        foreach (PieceController piece in allPieces)
        {
            if (piece == null || !piece.IsEnemy)
            {
                continue;
            }

            if (playVfx && Application.isPlaying)
            {
                PlayPieceDestroyVfx(piece);
            }

            pieces.Remove(piece);
            enemyPieces.Remove(piece);
            playerPieces.Remove(piece);
            placementSnapshot.Remove(piece);

            if (selectedPiece == piece)
            {
                ClearSelection();
            }

            piece.gridPosition = null;
            OnPieceUnregistered?.Invoke(piece);

            if (Application.isPlaying)
            {
                Destroy(piece.gameObject);
            }
            else
            {
                DestroyImmediate(piece.gameObject);
            }

            removedAny = true;
        }

        if (removedAny)
        {
            MarkPiecePositionCacheDirty();
            UpdatePieceCountUI();
            UpdateThreatenedStatus();
        }
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
        if (newPieceObj != null) newPieceObj.name = $"PiecePrefab(Clone)@ReplaceBoardSoldier[{position}]";

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
        if (newPieceObj != null) newPieceObj.name = $"PiecePrefab(Clone)@ReplaceInvSoldier[{slotTransform.name}]";
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

        lastDestructionTime = Time.unscaledTime;

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

        piece.SetLocation(PieceLocation.Inventory);
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
            // 씬 하이어라키 오브젝트(예: Pieces 컨테이너)가 잘못 할당된 경우 클론 생성을 차단합니다.
            if (destructionEffectPrefab.scene.IsValid())
            {
                Debug.LogWarning($"[PieceManager] destructionEffectPrefab에 프리팹이 아닌 씬 오브젝트({destructionEffectPrefab.name})가 할당되어 있어 해제합니다.");
                destructionEffectPrefab = null;
            }
            else
            {
                Instantiate(destructionEffectPrefab, piece.GetComponent<RectTransform>().position, Quaternion.identity);
            }
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

    public int GetEnemyPieceCountOnBoard()
    {
        int count = 0;
        foreach (var piece in enemyPieces)
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
        sequence.SetUpdate(true);

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
        if (piece.IsConfused && !piece.GetCandidateMoves().Contains(gridPosition)) return false;
        if (targetPiece != null && ShotgunKingSystem.IsShotgunKing(piece)) return false;
        PieceType? capturedType = targetPiece != null ? targetPiece.Type : null;
        bool? capturedIsEnemy = targetPiece != null ? targetPiece.IsEnemy : null;
        bool usedScavengedMove = ShotgunKingSystem.IsScavengedMove(piece, gridPosition);
        if (targetPiece != null)
        {
            if (piece.IsBlind) return false;
            // 오셀로의 성역 활성 시에는 빈 타일로만 이동 가능하며 직접 포획 금지
            if (OthelloArtifactSystem.IsActive) return false;
            if (targetPiece.IsEnemy == piece.IsEnemy) return false;
            if (targetPiece.IsInvincible) return false;

            Vector2Int attackerFromPos = piece.gridPosition.Value;
            bool isSniperAttack = piece.CanSnipeWithoutMoving;

            AttackResponse response = ResolveAttackResponse(piece, targetPiece);
            if (response != AttackResponse.None)
            {
                if (isSniperAttack)
                {
                    PlaySniperProjectile(attackerFromPos, gridPosition);
                    piece.ReturnToOriginalPosition();
                }
                UpdateThreatenedStatus();
                AdvanceTurnAfterMove();
                return true;
            }

            if (!targetPiece.CanBeDestroyed())
            {
                return false;
            }

            if (isSniperAttack)
            {
                PlaySniperProjectile(attackerFromPos, gridPosition);
            }

            try
            {
                activeMovingAttacker = isSniperAttack ? null : piece;
                DestroyPieceWithEffects(targetPiece, true, true, piece);
            }
            finally
            {
                activeMovingAttacker = null;
            }

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

            // 저격의 인장: 적 기물을 포획했을 때 이동하지 않고 제자리에서 포획 완료
            if (isSniperAttack)
            {
                // 드래그 중이었던 기물의 시각적 위치를 원래 위치로 복귀
                piece.ReturnToOriginalPosition();

                if (gridManager != null)
                {
                    gridManager.HighlightLastMove(attackerFromPos, gridPosition);
                }

                LogTurnAction(piece, attackerFromPos, gridPosition, capturedType, capturedIsEnemy);

                TurnLogUIManager.Instance?.AddLog(
                    $"[{piece.PieceType}] 저격! 이동하지 않고 [{targetPiece.PieceType}]을(를) 처치했습니다.",
                    piece.IsEnemy);

                if (capturedType.HasValue)
                {
                    piece.OnCapturedPiece(capturedType.Value);
                }

                ShotgunKingSystem.OnKingMoveFinished(piece, attackerFromPos, attackerFromPos, capturedType, usedScavengedMove);

                UpdateThreatenedStatus();
                AdvanceTurnAfterMove();
                return true;
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
        
        piece.OnMoveFinished(prevPos, gridPosition, capturedType.HasValue);

        if (capturedType.HasValue)
        {
            piece.OnCapturedPiece(capturedType.Value);
        }

        ShotgunKingSystem.OnKingMoveFinished(piece, prevPos, gridPosition, capturedType, usedScavengedMove);

        OthelloArtifactSystem.OnPieceLanded(piece);

        if (OthelloArtifactSystem.IsActive && piece != null && piece.gridPosition.HasValue)
        {
            piece.CheckSoldierPromotion();
        }

        UpdateThreatenedStatus();

        AdvanceTurnAfterMove();
        return true;
    }

    public enum AttackResponse
    {
        None,
        Protected,
        Evaded,
        Counterattacked
    }

    public AttackResponse ResolveAttackResponse(PieceController attacker, PieceController defender)
    {
        // 여러 버프가 동시에 있으면 보호 -> 회피 -> 역공 순서로 하나만 발동합니다.
        if (defender.ConsumeBuff(PieceBuffType.Protection))
        {
            ActivateBuffEffect(defender, PieceBuffType.Protection, "공격 무효화");
            ShotgunKingSystem.OnKingProtectionBroken(defender);
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

        if (!defender.IsBlind && defender.ConsumeBuff(PieceBuffType.Counterattack))
        {
            if (attacker != null && attacker.CanBeDestroyed())
            {
                DestroyPieceWithEffects(attacker, true, true, defender);
                defender.OnCapturedPiece(attacker.Type);
                ActivateBuffEffect(defender, PieceBuffType.Counterattack, "공격자를 역공");
                return AttackResponse.Counterattacked;
            }

            // 역공 대상(공격자)이 없거나 파괴 불가인 경우 역공 스택을 소모하고 공격은 무효화
            ActivateBuffEffect(defender, PieceBuffType.Counterattack, "역공으로 공격 차단");
            return AttackResponse.Counterattacked;
        }

        return AttackResponse.None;
    }

    public void ActivateBuffEffect(PieceController defender, PieceBuffType buffType, string effectDescription)
    {
        // 공통 발동 연출은 PieceController에서 관리하고, 이곳에서는 로그만 조합합니다.
        defender.PlayBuffActivationEffect(buffType);

        string buffName = buffType switch
        {
            PieceBuffType.Protection => "보호",
            PieceBuffType.Evasion => "회피",
            PieceBuffType.Counterattack => "역공",
            PieceBuffType.Blindness => "실명",
            PieceBuffType.Confusion => "혼란",
            _ => "버프"
        };

        bool isTimedStatus = buffType == PieceBuffType.Blindness || buffType == PieceBuffType.Confusion;
        string remainingText = isTimedStatus
            ? $"남은 턴 {defender.GetBuffStacks(buffType)}"
            : $"남은 스택 {defender.GetBuffStacks(buffType)}";
        TurnLogUIManager.Instance?.AddLog(
            $"{buffName} 발동: {effectDescription} ({remainingText})",
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
            PieceType.Archbishop => "AB",
            PieceType.Centaur => "T",
            PieceType.Cannon => "CN",
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

    private Coroutine moveSafetyCoroutine;

    public void MovePlayerPiecesToSafety()
    {
        if (moveSafetyCoroutine != null)
        {
            StopCoroutine(moveSafetyCoroutine);
        }
        moveSafetyCoroutine = StartCoroutine(MovePlayerPiecesToSafetyRoutine());
    }

    private IEnumerator MovePlayerPiecesToSafetyRoutine()
    {
        RemoveConversionSealedPieces();
        if (gridManager == null) yield break;

        // 1. 적 기물 위치 수집 (적 기물 위치는 아군이 배치될 수 없음)
        HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();
        foreach (var piece in pieces)
        {
            if (piece == null || piece.CurrentLocation != PieceLocation.Board) continue;
            if (piece.IsEnemy && piece.gridPosition.HasValue)
            {
                occupiedPositions.Add(piece.gridPosition.Value);
            }
        }

        // 2. 보드 위 아군 기물 목록 수집
        List<PieceController> allyBoardPieces = new List<PieceController>();
        foreach (var piece in pieces)
        {
            if (piece == null || piece.IsEnemy || piece.CurrentLocation != PieceLocation.Board || piece.HasConversionSeal())
            {
                continue;
            }
            allyBoardPieces.Add(piece);
        }

        // 3. 기물별 의도된 위치(스냅샷 위치 또는 현재 보드 위치) 산출
        Dictionary<PieceController, Vector2Int> intendedPositions = new Dictionary<PieceController, Vector2Int>();
        foreach (var piece in allyBoardPieces)
        {
            if (hasPlacementSnapshot && placementSnapshot.TryGetValue(piece, out Vector2Int snapPos))
            {
                intendedPositions[piece] = snapPos;
            }
            else if (piece.gridPosition.HasValue)
            {
                intendedPositions[piece] = piece.gridPosition.Value;
            }
        }

        List<PieceController> piecesToMove = new List<PieceController>();

        // 4. 이미 적절한 위치(playerPrepareMaxY 이하, 보드 내, 미점유)에 있는 기물 우선 고정
        foreach (var piece in allyBoardPieces)
        {
            if (intendedPositions.TryGetValue(piece, out Vector2Int pos))
            {
                bool isAppropriate = gridManager.IsInBounds(pos) &&
                                    gridManager.GetGridCell(pos) != null &&
                                    pos.y <= playerPrepareMaxY &&
                                    !occupiedPositions.Contains(pos);

                if (isAppropriate)
                {
                    occupiedPositions.Add(pos);
                    if (!piece.gridPosition.HasValue || piece.gridPosition.Value != pos)
                    {
                        piece.MoveToGrid(pos);
                    }
                }
                else
                {
                    piecesToMove.Add(piece);
                }
            }
            else
            {
                piecesToMove.Add(piece);
            }
        }

        // 5. 이동이 필요한 기물 정렬:
        // 안전선(playerPrepareMaxY)에 가까운 기물부터 차례대로 가장 가까운 유효 위치를 선점하도록 정렬
        piecesToMove.Sort((a, b) =>
        {
            int yA = intendedPositions.TryGetValue(a, out var pA) ? pA.y : (a.gridPosition.HasValue ? a.gridPosition.Value.y : int.MaxValue);
            int yB = intendedPositions.TryGetValue(b, out var pB) ? pB.y : (b.gridPosition.HasValue ? b.gridPosition.Value.y : int.MaxValue);
            int distA = Mathf.Abs(yA - playerPrepareMaxY);
            int distB = Mathf.Abs(yB - playerPrepareMaxY);
            return distA.CompareTo(distB);
        });

        // 6. 각 부적절한 위치의 기물을 '가장 가까운 적절한 위치'로 이동
        foreach (var piece in piecesToMove)
        {
            Vector2Int fromPos = intendedPositions.TryGetValue(piece, out var p)
                ? p
                : (piece.gridPosition.HasValue ? piece.gridPosition.Value : new Vector2Int(gridManager.gridMinBounds.x, playerPrepareMaxY));

            Vector2Int? targetPos = FindNearestValidPreparePosition(fromPos, occupiedPositions);

            if (targetPos.HasValue)
            {
                occupiedPositions.Add(targetPos.Value);
                piece.MoveToGrid(targetPos.Value);
                if (hasPlacementSnapshot)
                {
                    placementSnapshot[piece] = targetPos.Value;
                }
                yield return new WaitForSeconds(0.08f);
            }
            else
            {
                // 보드 내에 빈 유효 칸이 전혀 없는 경우 인벤토리 빈 슬롯으로 대피
                InventorySlot emptySlot = FindEmptyInventorySlot();
                if (emptySlot != null)
                {
                    piece.MoveToInventory(emptySlot.transform);
                }
            }
        }

        // 7. 스냅샷 갱신 및 위협/하이라이트 상태 갱신
        CapturePlacementSnapshot();
        UpdateThreatenedStatus();
        moveSafetyCoroutine = null;
    }

    private InventorySlot FindEmptyInventorySlot()
    {
        var slots = FindObjectsByType<InventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sortedSlots = slots.OrderBy(s => s.transform.GetSiblingIndex()).ToArray();
        return sortedSlots.FirstOrDefault(slot => slot.GetComponentInChildren<PieceController>() == null && !slot.IsReserved);
    }

    /// <summary>
    /// 지정된 기준 위치(fromPos)에서 가장 가까운 유효한 아군 배치 위치를 찾습니다.
    /// 조건: 보드 범위 내, 플레이어 진영(y <= playerPrepareMaxY), 미점유(적 및 다른 아군 없음).
    /// </summary>
    public Vector2Int? FindNearestValidPreparePosition(Vector2Int fromPos, HashSet<Vector2Int> occupied)
    {
        if (gridManager == null) return null;

        int minX = gridManager.gridMinBounds.x;
        int maxX = gridManager.gridMinBounds.x + gridManager.boardWidth - 1;
        int minY = gridManager.gridMinBounds.y;
        int maxY = playerPrepareMaxY;

        Vector2Int? bestPos = null;
        float bestScore = float.MaxValue;

        for (int y = maxY; y >= minY; y--)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2Int candidate = new Vector2Int(x, y);

                if (!gridManager.IsInBounds(candidate) || gridManager.GetGridCell(candidate) == null)
                {
                    continue;
                }

                if (occupied != null && occupied.Contains(candidate))
                {
                    continue;
                }

                PieceController pieceAt = GetPieceAt(candidate);
                if (pieceAt != null && pieceAt.IsEnemy)
                {
                    continue;
                }

                int dx = candidate.x - fromPos.x;
                int dy = candidate.y - fromPos.y;
                int manhattan = Mathf.Abs(dx) + Mathf.Abs(dy);
                int euclideanSq = dx * dx + dy * dy;

                // 유클리드 거리 최소화 우선, 동일 거리일 경우 X축 열 유지(수직 하강) 선호, 그 다음 전선(높은 Y) 선호
                float score = euclideanSq * 100f + manhattan * 10f + Mathf.Abs(dx) * 2f - candidate.y * 0.1f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestPos = candidate;
                }
            }
        }

        return bestPos;
    }

    public void CapturePlacementSnapshot()
    {
        placementSnapshot.Clear();
        foreach (var piece in pieces)
        {
            if (piece == null || piece.IsEnemy || piece.HasConversionSeal()) continue;
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
        RemoveConversionSealedPieces();

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
            if (piece != null && !piece.IsEnemy && !piece.HasConversionSeal())
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
                if (piece == null || piece.IsEnemy || piece.HasConversionSeal())
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
        ClearSelfDestructRangeMarkers();
        selectedPiece = null;
    }

    public void HideMoveMarkers()
    {
        ClearMarkers();
    }

    public void ShowEnemyMoveMarkers()
    {
        ClearMarkers();

        if (PieceFlickLauncher.IsPhysicsActive)
        {
            return;
        }

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

    public void ShowEnemyPieceMoveMarkers(PieceController enemy)
    {
        ClearMarkers();

        if (PieceFlickLauncher.IsPhysicsActive)
        {
            return;
        }

        if (moveMarkerPrefab == null ||
            enemy == null ||
            !enemy.IsEnemy ||
            enemy.CurrentLocation != PieceLocation.Board ||
            !enemy.GridPosition.HasValue)
        {
            return;
        }

        SpawnMarkersFromPositions(enemy.GetCandidateMoves(), true, true);
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

        // 1. 기물이 날아가는 중(물리 활성/동기화 중)이면 어떤 마커도 표시하지 않음
        if (PieceFlickLauncher.IsPhysicsActive)
        {
            return;
        }

        // 2. 알까기 유물을 보유 중일 때, 내 기물(아군 기물)의 이동 마커는 절대로 표시하지 않음 (상대 턴이든 내 턴이든 항상)
        if (selectedPiece != null && !selectedPiece.IsEnemy && PieceFlickLauncher.HasAlkkagiArtifact)
        {
            return;
        }

        SpawnMarkersFromPositions(moves, selectedPiece != null && selectedPiece.IsEnemy, true);
    }

    private bool IsInBounds(Vector2Int gridPosition)
    {
        if (gridManager != null)
            return gridManager.IsInBounds(gridPosition);
        return false;
    }

    public void ClearMarkers()
    {
        ClearShotgunAimMarkers();
        for (int i = 0; i < activeMarkers.Count; i++)
        {
            if (activeMarkers[i] != null)
            {
                Destroy(activeMarkers[i].gameObject);
            }
        }
        activeMarkers.Clear();

        if (markersParent != null)
        {
            for (int i = markersParent.childCount - 1; i >= 0; i--)
            {
                Transform child = markersParent.GetChild(i);
                if (child != null && child.name != "ShotgunProjectile" && child.name != "SniperProjectile" && !child.name.StartsWith("SelfDestructMarker"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        MoveMarker[] strayMarkers = Object.FindObjectsByType<MoveMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < strayMarkers.Length; i++)
        {
            if (strayMarkers[i] != null && !selfDestructRangeMarkers.Contains(strayMarkers[i]))
            {
                Destroy(strayMarkers[i].gameObject);
            }
        }
    }

    public void ShowSelfDestructRangeMarkers(Vector2Int centerPosition)
    {
        ClearSelfDestructRangeMarkers();
        if (moveMarkerPrefab == null || markersParent == null || gridManager == null) return;

        Vector2Int[] offsets = new Vector2Int[]
        {
            new Vector2Int(-1,  1), new Vector2Int(0,  1), new Vector2Int(1,  1),
            new Vector2Int(-1,  0),                         new Vector2Int(1,  0),
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1)
        };

        Color redSemiTransparent = new Color(1f, 0.2f, 0.2f, 0.55f);

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int pos = centerPosition + offsets[i];
            if (!IsInBounds(pos)) continue;

            GameObject markerObject = Instantiate(moveMarkerPrefab, markersParent);
            markerObject.name = "SelfDestructMarker";
            markerObject.transform.SetAsFirstSibling();

            RectTransform markerTransform = markerObject.GetComponent<RectTransform>();
            if (markerTransform != null)
            {
                markerTransform.anchoredPosition = GridToUiPosition(pos);
                markerTransform.sizeDelta = gridManager.cellSize;
            }

            MoveMarker marker = markerObject.GetComponent<MoveMarker>();
            if (marker == null)
            {
                Destroy(markerObject);
                continue;
            }

            marker.SetSelfDestructRangeAppearance(redSemiTransparent);
            selfDestructRangeMarkers.Add(marker);
        }
    }

    public void ClearSelfDestructRangeMarkers()
    {
        for (int i = 0; i < selfDestructRangeMarkers.Count; i++)
        {
            if (selfDestructRangeMarkers[i] != null)
            {
                Destroy(selfDestructRangeMarkers[i].gameObject);
            }
        }
        selfDestructRangeMarkers.Clear();
    }

    /// <summary>
    /// 필드, 대기석 및 씬의 모든 기물에 남아있는 버프와 상태를 제거합니다.
    /// 스테이지가 끝날 때 호출됩니다.
    /// </summary>
    public void ClearAllPieceBuffs()
    {
        PieceController[] allPieces = Object.FindObjectsByType<PieceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allPieces.Length; i++)
        {
            if (allPieces[i] != null)
            {
                allPieces[i].ClearAllBuffs();
            }
        }
    }

    public void UpdateShotgunAimMarkers(IEnumerable<Vector2Int> positions, IEnumerable<Vector2Int> highlightedPositions)
    {
        HashSet<Vector2Int> highlights = new HashSet<Vector2Int>(highlightedPositions);
        highlights.RemoveWhere(position => !IsInBounds(position));
        HashSet<Vector2Int> desiredPositions = new HashSet<Vector2Int>(positions);
        desiredPositions.UnionWith(highlights);
        desiredPositions.RemoveWhere(position => !IsInBounds(position));

        bool sameMarkers = shotgunAimMarkerPositions.SetEquals(desiredPositions) &&
                           shotgunAimHighlightedPositions.SetEquals(highlights);
        if (sameMarkers) return;

        ClearShotgunAimMarkers();
        if (moveMarkerPrefab == null || markersParent == null) return;

        foreach (Vector2Int position in desiredPositions)
        {
            if (!IsInBounds(position)) continue;
            GameObject markerObject = Instantiate(moveMarkerPrefab, markersParent);
            RectTransform markerTransform = markerObject.GetComponent<RectTransform>();
            if (markerTransform != null)
            {
                markerTransform.anchoredPosition = GridToUiPosition(position);
                markerTransform.sizeDelta = gridManager != null ? gridManager.cellSize : new Vector2(42f, 42f);
            }

            MoveMarker marker = markerObject.GetComponent<MoveMarker>();
            if (marker == null)
            {
                Destroy(markerObject);
                continue;
            }

            bool highlighted = highlights.Contains(position);
            marker.Initialize(this, position, false, true);
            marker.SetShotgunAimAppearance(highlighted);
            shotgunAimMarkers.Add(marker);
            shotgunAimMarkerPositions.Add(position);
            if (highlighted) shotgunAimHighlightedPositions.Add(position);
        }
    }

    public void ClearShotgunAimMarkers()
    {
        foreach (MoveMarker marker in shotgunAimMarkers)
        {
            if (marker != null) Destroy(marker.gameObject);
        }
        shotgunAimMarkers.Clear();
        shotgunAimMarkerPositions.Clear();
        shotgunAimHighlightedPositions.Clear();
    }

    public void PlayShotgunProjectile(Vector2Int fromPosition, Vector2Int targetPosition)
    {
        if (markersParent == null || gridManager == null) return;

        GameObject projectile = new GameObject("ShotgunProjectile", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform projectileRect = projectile.GetComponent<RectTransform>();
        projectileRect.SetParent(markersParent, false);
        projectileRect.anchorMin = new Vector2(0.5f, 0.5f);
        projectileRect.anchorMax = new Vector2(0.5f, 0.5f);
        projectileRect.pivot = new Vector2(0.5f, 0.5f);
        projectileRect.sizeDelta = new Vector2(14f, 5f);

        Vector2 start = GridToUiPosition(fromPosition);
        Vector2 end = GridToUiPosition(targetPosition);
        Vector2 direction = end - start;
        projectileRect.anchoredPosition = start;
        projectileRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        projectileRect.SetAsLastSibling();

        Image image = projectile.GetComponent<Image>();
        image.color = new Color(1f, 0.72f, 0.18f, 1f);
        image.raycastTarget = false;

        float duration = Mathf.Clamp(direction.magnitude / 900f, 0.08f, 0.18f);
        projectileRect.DOAnchorPos(end, duration).SetEase(Ease.InQuad).OnComplete(() =>
        {
            if (projectile != null) Destroy(projectile);
        });
    }

    public void PlaySniperProjectile(Vector2Int fromPosition, Vector2Int targetPosition)
    {
        if (markersParent == null || gridManager == null) return;

        GameObject projectile = new GameObject("SniperProjectile", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform projectileRect = projectile.GetComponent<RectTransform>();
        projectileRect.SetParent(markersParent, false);
        projectileRect.anchorMin = new Vector2(0.5f, 0.5f);
        projectileRect.anchorMax = new Vector2(0.5f, 0.5f);
        projectileRect.pivot = new Vector2(0.5f, 0.5f);
        projectileRect.sizeDelta = new Vector2(26f, 4.5f);

        Vector2 start = GridToUiPosition(fromPosition);
        Vector2 end = GridToUiPosition(targetPosition);
        Vector2 direction = end - start;
        projectileRect.anchoredPosition = start;
        projectileRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        projectileRect.SetAsLastSibling();

        Image image = projectile.GetComponent<Image>();
        image.color = new Color(0.25f, 0.88f, 1f, 0.95f); // 네온 시안 광선 궤적
        image.raycastTarget = false;

        float duration = Mathf.Clamp(direction.magnitude / 1400f, 0.05f, 0.12f);
        projectileRect.DOAnchorPos(end, duration).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            if (projectile != null) Destroy(projectile);
        });
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

        // 1. 아군 기물이 전멸한 경우: 적 기물 전멸 여부와 무관하게 무조건 탈락 (GameOver)
        if (playerOnBoardCount == 0 && !isPlayerPromoting)
        {
            OnAllPlayerPiecesRemoved();
            return;
        }

        // 2. 적 기물이 모두 제거되었고 아군 기물이 1개 이상 남아있는 경우에만 스테이지 승리 (Win)
        if (enemyOnBoardCount == 0 && !isEnemyPromoting)
        {
            OnAllEnemyPiecesRemoved();
            return;
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
        // 안전 검증: 아군 기물이 0개이면 승리가 아닌 무조건 탈락 (GameOver)
        int playerOnBoardCount = CountPiecesOnBoard(playerPieces);
        if (playerOnBoardCount == 0)
        {
            OnAllPlayerPiecesRemoved();
            return;
        }

        GameStateManager.Instance?.ChangeState(GameStateManager.GameState.Win);
    }

    private void OnAllPlayerPiecesRemoved()
    {
        GameStateManager.Instance?.ChangeState(GameStateManager.GameState.GameOver);
    }
}
