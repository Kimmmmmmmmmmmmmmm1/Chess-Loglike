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


    [Header("Piece Sprites")]
    public Sprite kingSprite;
    public Sprite chariotSprite;
    public Sprite horseSprite;
    public Sprite elephantSprite;
    public Sprite cannonSprite;
    public Sprite soldierSprite;


    [Header("Enemy Piece Sprites")]
    public Sprite enemykingSprite;
    public Sprite enemychariotSprite;
    public Sprite enemyhorseSprite;
    public Sprite enemyelephantSprite;
    public Sprite enemycannonSprite;
    public Sprite enemysoldierSprite;

    [Header("Settings")]
    [SerializeField] private float captureDestroyDelay = 0.25f;
    [SerializeField] private int playerPrepareMaxY = 0;
    [SerializeField] private int maxPlayerPiecesOnBoard = 3;
    public int PlayerPrepareMaxY => playerPrepareMaxY;
    public int MaxPlayerPiecesOnBoard => maxPlayerPiecesOnBoard;
    public const int AbsoluteMaxPlayerPieces = 10;

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

    public PieceController SelectedPiece => selectedPiece;
    public IReadOnlyList<PieceController> Pieces => pieces;
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
        SceneManager.sceneLoaded -= OnSceneLoaded;
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
            if (!hasPlacementSnapshot)
            {
                CapturePlacementSnapshot();
            }
            MovePlayerPiecesToSafety();
        }

        foreach (var piece in pieces)
        {
            if (piece != null)
            {
                piece.RefreshThreatenedVisuals();
            }
        }
    }

    public void SelectPiece(PieceController piece)
    {
        if (piece == null || (selectedPiece == piece && activeMarkers.Count > 0))
        {
            return;
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

            if (!piece.HasPromotionSeal() || piece.Type == PieceType.Soldier)
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
            newPiece.Initialize(PieceType.Soldier, false);
            newPiece.MoveToGrid(position);
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
            newPiece.Initialize(PieceType.Soldier, false);
            newPiece.MoveToInventory(slotTransform);
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
                    scale = 1.2f;
                    break;
                case PieceType.Soldier:
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
        switch (pieceType)
        {
            case PieceType.King:
                return kingSprite;
            case PieceType.Chariot:
                return chariotSprite;
            case PieceType.Horse:
                return horseSprite;
            case PieceType.Elephant:
                return elephantSprite;
            case PieceType.Cannon:
                return cannonSprite;
            case PieceType.Soldier:
            default:
                return soldierSprite;
        }
    }

    public Sprite GetEnemySpriteFor(PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.King:
                return enemykingSprite;
            case PieceType.Chariot:
                return enemychariotSprite;
            case PieceType.Horse:
                return enemyhorseSprite;
            case PieceType.Elephant:
                return enemyelephantSprite;
            case PieceType.Cannon:
                return enemycannonSprite;
            case PieceType.Soldier:
            default:
                return enemysoldierSprite;
        }
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

    public void MoveSelectedTo(Vector2Int gridPosition)
    {
        if (selectedPiece == null)
        {
            return;
        }

        if (TryMovePiece(selectedPiece, gridPosition))
        {
            ClearMarkers();
            selectedPiece = null;
        }
    }

    public bool TryMovePiece(PieceController piece, Vector2Int gridPosition)
    {
        if (piece == null) return false;
        if (!piece.gridPosition.HasValue) return false;

        Vector2Int fromPosition = piece.gridPosition.Value;

        PieceController targetPiece = GetPieceAt(gridPosition);
        PieceType? capturedType = targetPiece != null ? targetPiece.Type : null;
        bool? capturedIsEnemy = targetPiece != null ? targetPiece.IsEnemy : null;
        if (targetPiece != null)
        {
            if (targetPiece.IsEnemy == piece.IsEnemy) return false;

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
                EffectManager.Instance.PlayCameraShake(0.2f, 0.5f);
            }
        }

        Vector2Int prevPos = piece.gridPosition.Value;
        piece.MoveToGrid(gridPosition);

        LogTurnAction(piece, prevPos, gridPosition, capturedType, capturedIsEnemy);
        
        piece.OnMoveFinished(prevPos, gridPosition);

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

    private void LogTurnAction(PieceController mover, Vector2Int from, Vector2Int to, PieceType? capturedType, bool? capturedIsEnemy)
    {
        if (mover == null)
        {
            return;
        }

        string actorTeam = mover.IsEnemy ? "적" : "플레이어";
        string moverTypeName = GetPieceTypeKoreanName(mover.Type);
        string moverPositionFrom = FormatDisplayGridPosition(from);
        string moverPositionTo = FormatDisplayGridPosition(to);
        bool useSimpleTurnLog = SettingsManager.Instance != null
            && SettingsManager.Instance.Settings != null
            && SettingsManager.Instance.Settings.simpleTurnLog;

        string logStr = null;
        if (useSimpleTurnLog)
        {
            logStr = $"{moverTypeName} {moverPositionTo}";
            TurnLogUIManager.Instance?.AddLog(logStr, mover.IsEnemy);
            return;
        }

        if (capturedType.HasValue && capturedIsEnemy.HasValue)
        {
            string targetTypeName = GetPieceTypeKoreanName(capturedType.Value);
            string moverSubjectParticle = GetKoreanParticle(moverTypeName, ParticleKind.Subject);
            string targetObjectParticle = GetKoreanParticle(targetTypeName, ParticleKind.Object);
            logStr = $"{moverTypeName}{moverSubjectParticle} {moverPositionFrom}에서 {moverPositionTo}로 이동하며 {targetTypeName}{targetObjectParticle} 포획";
            TurnLogUIManager.Instance?.AddLog(logStr, mover.IsEnemy);
            return;
        }

        string moverSubject = GetKoreanParticle(moverTypeName, ParticleKind.Subject);
        logStr = $"{moverTypeName}{moverSubject} {moverPositionFrom}에서 {moverPositionTo}로 이동";
        TurnLogUIManager.Instance?.AddLog(logStr, mover.IsEnemy);
    }

    private string FormatDisplayGridPosition(Vector2Int gridPos)
    {
        if (gridManager == null)
        {
            return $"({gridPos.x},{gridPos.y})";
        }

        int minX = gridManager.gridMinBounds.x;
        int maxY = gridManager.gridMinBounds.y + gridManager.boardHeight - 1;

        int displayX = (gridPos.x - minX) + 1;
        int displayY = (maxY - gridPos.y) + 1;
        return $"({displayX},{displayY})";
    }

    private enum ParticleKind { Subject, Object, Topic }

    private string GetKoreanParticle(string word, ParticleKind kind)
    {
        if (string.IsNullOrEmpty(word))
        {
            return kind == ParticleKind.Object ? "를" : (kind == ParticleKind.Subject ? "가" : "는");
        }

        char lastChar = word[word.Length - 1];

        if (lastChar >= 0xAC00 && lastChar <= 0xD7A3)
        {
            int finalConsonantIndex = (lastChar - 0xAC00) % 28;
            bool hasBatchim = finalConsonantIndex != 0;
            return kind switch
            {
                ParticleKind.Subject => hasBatchim ? "이" : "가",
                ParticleKind.Object => hasBatchim ? "을" : "를",
                ParticleKind.Topic => hasBatchim ? "은" : "는",
                _ => ""
            };
        }

        return kind == ParticleKind.Object ? "를" : (kind == ParticleKind.Subject ? "가" : "는");
    }

    private string GetPieceTypeKoreanName(PieceType type)
    {
        return type switch
        {
            PieceType.King => "궁",
            PieceType.Chariot => "차",
            PieceType.Horse => "마",
            PieceType.Elephant => "상",
            PieceType.Cannon => "포",
            PieceType.Soldier => "졸",
            _ => type.ToString()
        };
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
        foreach (var piece in pieces)
        {
            if (piece != null && !piece.IsEnemy && piece.CurrentLocation == PieceLocation.Board)
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
            }

            if (marker != null)
            {
                marker.Initialize(this, gridPos, isEnemy);
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

    private void CheckPieceCounts()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            return;
        }

        int enemyOnBoardCount = CountPiecesOnBoard(enemyPieces);
        int playerOnBoardCount = CountPiecesOnBoard(playerPieces);

        if (enemyOnBoardCount == 0)
        {
            OnAllEnemyPiecesRemoved();
        }

        if (playerOnBoardCount == 0)
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
