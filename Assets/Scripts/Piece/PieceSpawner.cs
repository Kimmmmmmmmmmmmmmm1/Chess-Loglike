using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PieceSpawner : MonoBehaviour
{
    public static PieceSpawner Instance { get; private set; }
    [System.Serializable]
    public struct PieceSpawnInfo
    {
        public PieceType pieceType;
        public Vector2Int gridCoordinate;
        public List<SealData> seals;
    }

    [Header("Settings")]
    public GameObject piecePrefab;
    public Transform piecesParent;
    [SerializeField] private float spawnDropHeightMultiplier = 3.0f;
    [SerializeField] private float enemyRevealDuration = 0.9f;
    [SerializeField] private float enemyRevealInterval = 0.12f;

    [Header("Spawn Data")]
    public List<PieceSpawnInfo> playerPieces;
    public List<PieceSpawnInfo> enemyPieces;

    private bool hasSpawned = false;
    private Coroutine spawnCoroutine;
    private bool isStartingGame;

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
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
            {
                OnStateChanged(GameStateManager.GameState.Prepare);
            }
        }
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnStateChanged;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (piecesParent == null)
        {
            GameObject parentObj = GameObject.Find("PiecesParent");
            if (parentObj != null)
            {
                piecesParent = parentObj.transform;
            }
        }
    }

    private void OnStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Prepare && !hasSpawned)
        {
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
            spawnCoroutine = StartCoroutine(SpawnRoutine());
        }
        else if (newState == GameStateManager.GameState.None)
        {
            hasSpawned = false;
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);

            ArtifactEffectHandlers.ResetStageLimitedEffects();
        }
    }

    private IEnumerator SpawnRoutine()
    {
        hasSpawned = true;

        if (PieceInventory.Instance != null)
        {
            PieceInventory.Instance.ResetStageSnapshot();
        }

        // GridManager가 존재하면 그리드 생성 애니메이션이 끝날 때까지 대기
        if (PieceManager.Instance != null && PieceManager.Instance.gridManager != null)
        {
            float waitTime = PieceManager.Instance.gridManager.GetTotalAnimationDuration();
            yield return new WaitForSeconds(waitTime);
        }

        // 유물 효과 스테이지별 리셋
        ArtifactEffectHandlers.ResetStageLimitedEffects();

        SpawnPlayerPieces();
        SpawnEnemyPieces();

        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.UpdateThreatenedStatus();
        }
    }

    public void SpawnAllPieces()
    {
        SpawnPlayerPieces();
        SpawnEnemyPieces();
    }
    public void SpawnPlayerPieces()
    {
        float currentDelay = 0f;
        SpawnGroup(playerPieces, false, ref currentDelay);
    }
    public void SpawnEnemyPieces()
    {
        if (enemyPieces == null || enemyPieces.Count == 0)
        {
            return;
        }

        float currentDelay = 0f;
        SpawnGroup(enemyPieces, true, ref currentDelay);
    }

    public bool TryStartGameWithEnemyReveal()
    {
        if (isStartingGame)
        {
            return true;
        }

        if (PieceManager.Instance == null ||
            !PieceManager.Instance.IsEnemyTerritoryHiddenDuringPrepare)
        {
            return false;
        }

        isStartingGame = true;
        Sequence revealSequence = DOTween.Sequence().SetId(this);

        int revealIndex = 0;
        foreach (PieceController piece in PieceManager.Instance.EnemyPieces)
        {
            if (piece == null) continue;

            CanvasGroup canvasGroup = piece.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = piece.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            revealSequence.Insert(
                revealIndex * enemyRevealInterval,
                CreateEnemyRevealSequence(piece, canvasGroup));
            revealIndex++;
        }

        revealSequence.OnComplete(() =>
        {
            isStartingGame = false;
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
            {
                GameStateManager.Instance.ChangeState(GameStateManager.GameState.GamePlay);
            }
        });

        if (revealSequence.Duration() <= 0f)
        {
            revealSequence.Complete();
        }

        return true;
    }

    private Sequence CreateEnemyRevealSequence(PieceController piece, CanvasGroup canvasGroup)
    {
        RectTransform pieceRect = piece.transform as RectTransform;
        if (pieceRect == null)
        {
            return DOTween.Sequence()
                .Append(canvasGroup.DOFade(1f, enemyRevealDuration))
                .OnComplete(() =>
                {
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                });
        }

        Transform parent = pieceRect.parent;
        GameObject pillarObject = new GameObject(
            "EnemyRevealLightPillar",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        pillarObject.transform.SetParent(parent, false);

        RectTransform pillarRect = pillarObject.GetComponent<RectTransform>();
        Image pillarImage = pillarObject.GetComponent<Image>();
        pillarImage.sprite = GetRevealSprite();
        pillarImage.color = new Color(1f, 1f, 1f, 0f);
        pillarImage.raycastTarget = false;

        Vector2 cellSize = PieceManager.Instance.gridManager != null
            ? PieceManager.Instance.gridManager.cellSize
            : pieceRect.rect.size;
        float pillarWidth = Mathf.Max(cellSize.x * 0.72f, pieceRect.rect.width * 0.72f);
        // 기물 아래쪽 끝은 유지하면서 반대쪽 끝이 보이지 않도록 충분히 길게 확장한다.
        float pillarHeight = Mathf.Max(cellSize.y * 5f, pieceRect.rect.height * 5f);
        Vector2 pieceTargetPosition = pieceRect.anchoredPosition;
        Vector2 pillarPosition = pieceTargetPosition;
        pillarPosition.y -= cellSize.y * 0.45f;
        Vector2 startPosition = pieceTargetPosition + new Vector2(
            0f,
            cellSize.y * spawnDropHeightMultiplier);
        pieceRect.anchoredPosition = startPosition;
        pillarRect.anchoredPosition = pillarPosition;
        pillarRect.pivot = new Vector2(0.5f, 0f);
        pillarRect.sizeDelta = new Vector2(0f, pillarHeight);
        pillarRect.SetSiblingIndex(pieceRect.GetSiblingIndex());

        Sequence sequence = DOTween.Sequence();
        sequence.Append(pillarImage.DOFade(0.9f, 0.12f).SetEase(Ease.OutQuad));
        sequence.Join(DOTween.To(
            () => pillarRect.sizeDelta.x,
            width => pillarRect.sizeDelta = new Vector2(width, pillarHeight),
            pillarWidth,
            0.18f).SetEase(Ease.OutBack));
        sequence.Join(pieceRect.DOAnchorPos(pieceTargetPosition, enemyRevealDuration * 0.75f)
            .SetEase(Ease.OutCubic));
        sequence.Join(canvasGroup.DOFade(1f, enemyRevealDuration * 0.75f)
            .SetEase(Ease.OutQuad));
        sequence.Append(pillarImage.DOFade(0f, 0.18f));
        sequence.OnComplete(() =>
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            Destroy(pillarObject);
        });

        return sequence;
    }

    private static Sprite revealSprite;

    private static Sprite GetRevealSprite()
    {
        if (revealSprite != null) return revealSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        revealSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        revealSprite.name = "EnemyRevealLightPillarSprite";
        return revealSprite;
    }

    public void SpawnPieceAndFlyToInventory(PieceType pieceType, Vector3 startWorldPos, InventorySlot targetSlot, SealData attachedSeal = null, System.Action<PieceController> onComplete = null, Transform flyParentOverride = null, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        if (piecePrefab == null) return;

        // 날아가는 동안 가려지지 않도록 최상위 캔버스(또는 루트)를 부모로 생성
        Canvas rootCanvas = targetSlot.GetComponentInParent<Canvas>();
        Transform spawnParent = rootCanvas != null ? rootCanvas.transform : targetSlot.transform.root;
        GameObject pieceObj = Instantiate(piecePrefab, spawnParent);
        pieceObj.name = $"PiecePrefab(Clone)@FlyToInventory[{caller}_{pieceType}]";
        PieceController piece = pieceObj.GetComponent<PieceController>();
        
        if (piece != null)
        {
            piece.Initialize(pieceType, false); // 아군 기물로 초기화 [데이터]
            
            // 비행 중에는 보드 로직(Grid 0,0 등)에 간섭하지 않도록 매니저에서 임시 해제 [데이터]
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.UnregisterPiece(piece);
            }

            // 시작 위치 설정
            piece.transform.position = startWorldPos;
            piece.transform.localScale = Vector3.zero;

            PieceManager.PlayJumpAnimation(
                piece,
                targetSlot.transform,
                5f,
                0.8f,
                () =>
            {
                if (piece != null)
                {
                    if (attachedSeal != null)
                    {
                        piece.EquipSeal(attachedSeal);
                    }
                    piece.MoveToInventory(targetSlot.transform);
                }
                onComplete?.Invoke(piece);
            },
                Ease.OutQuad,
                0.3f,
                flyParentOverride);
        }
    }

    private void SpawnGroup(List<PieceSpawnInfo> pieces, bool isEnemy, ref float delayAccumulator)
    {
        if (piecePrefab == null || pieces == null) return;

        List<PieceSpawnInfo> spawnList = isEnemy ? StageManager.SanitizeEnemyPieces(pieces) : pieces;

        Transform parent = piecesParent != null ? piecesParent : transform;

        foreach (var info in spawnList)
        {
            GameObject pieceObj = Instantiate(piecePrefab, parent);
            pieceObj.name = $"PiecePrefab(Clone)@SpawnGroup[{(isEnemy ? "Enemy" : "Ally")}_{info.pieceType}_{info.gridCoordinate}]";
            
            if (pieceObj.TryGetComponent<PieceController>(out var piece))
            {
                piece.gridPosition = info.gridCoordinate;
                piece.Initialize(info.pieceType, isEnemy);

                if (info.seals != null)
                {
                    foreach (var sealData in info.seals)
                    {
                        piece.EquipSeal(sealData);
                    }
                }

                bool hideEnemyDuringPrepare = isEnemy &&
                    PieceManager.Instance != null &&
                    PieceManager.Instance.IsEnemyTerritoryHiddenDuringPrepare;

                if (isEnemy && hideEnemyDuringPrepare)
                {
                    // 준비 단계에서 숨겨지는 적 기물: 낙하 애니메이션 및 파편 효과 없이 대기
                    // (게임 시작 시 TryStartGameWithEnemyReveal을 통해 등장)
                }
                else if (isEnemy)
                {
                    CanvasGroup canvasGroup = piece.GetComponent<CanvasGroup>();
                    if (canvasGroup == null)
                    {
                        canvasGroup = piece.gameObject.AddComponent<CanvasGroup>();
                    }

                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                    CreateEnemyRevealSequence(piece, canvasGroup)
                        .SetDelay(delayAccumulator)
                        .SetId(this);
                    delayAccumulator += enemyRevealInterval;
                }
                else
                {
                    // 생성 애니메이션: 위에서 아래로 떨어짐
                    if (piece.GetComponent<RectTransform>() is RectTransform rt)
                    {
                        Vector2 targetPos = rt.anchoredPosition;
                        float dropHeight = PieceManager.Instance.gridManager.cellSize.y * spawnDropHeightMultiplier;
                        rt.anchoredPosition = targetPos + new Vector2(0, dropHeight); // 적당히 위쪽에서 시작
                        rt.DOAnchorPos(targetPos, 0.8f).SetEase(Ease.OutQuad).SetDelay(delayAccumulator)
                            .OnComplete(() =>
                            {
                                if (!isEnemy && EffectManager.Instance != null)
                                    EffectManager.Instance.PlayLandingEffect(rt.position);
                            });

                        if (piece.PieceImage != null)
                        {
                            Color c = piece.PieceImage.color;
                            piece.PieceImage.color = new Color(c.r, c.g, c.b, 0f); // 투명하게 시작
                            piece.PieceImage.DOFade(1f, 0.8f).SetEase(Ease.OutQuad).SetDelay(delayAccumulator);
                        }

                        delayAccumulator += 0.1f; // 다음 기물은 0.1초 뒤에 생성
                    }
                }

            }
        }
    }
}
