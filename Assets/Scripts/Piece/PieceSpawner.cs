using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.SceneManagement;

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

    [Header("Spawn Data")]
    public List<PieceSpawnInfo> playerPieces;
    public List<PieceSpawnInfo> enemyPieces;

    private bool hasSpawned = false;
    private Coroutine spawnCoroutine;

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

    public void SpawnPieceAndFlyToInventory(PieceType pieceType, Vector3 startWorldPos, InventorySlot targetSlot, SealData attachedSeal = null, System.Action<PieceController> onComplete = null, Transform flyParentOverride = null)
    {
        if (piecePrefab == null) return;

        // 날아가는 동안 가려지지 않도록 최상위 캔버스(또는 루트)를 부모로 생성
        Canvas rootCanvas = targetSlot.GetComponentInParent<Canvas>();
        Transform spawnParent = rootCanvas != null ? rootCanvas.transform : targetSlot.transform.root;
        GameObject pieceObj = Instantiate(piecePrefab, spawnParent);
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
        if (piecePrefab == null) return;

        Transform parent = piecesParent != null ? piecesParent : transform;

        foreach (var info in pieces)
        {
            GameObject pieceObj = Instantiate(piecePrefab, parent);
            
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

                // 생성 애니메이션: 위에서 아래로 떨어짐
                if (piece.GetComponent<RectTransform>() is RectTransform rt)
                {
                    Vector2 targetPos = rt.anchoredPosition;
                    float dropHeight = PieceManager.Instance.gridManager.cellSize.y * spawnDropHeightMultiplier;
                    rt.anchoredPosition = targetPos + new Vector2(0, dropHeight); // 적당히 위쪽에서 시작
                    rt.DOAnchorPos(targetPos, 0.8f).SetEase(Ease.OutQuad).SetDelay(delayAccumulator)
                        .OnComplete(() => 
                        {
                            if (EffectManager.Instance != null)
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
