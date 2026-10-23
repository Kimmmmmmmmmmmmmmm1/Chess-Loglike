using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PieceInventory : MonoBehaviour
{
    public static PieceInventory Instance { get; private set; }
    [System.Serializable]
    public struct PieceInfo
    {
        public PieceType pieceType;
    }

    [Header("Owned Pieces")]
    [SerializeField] private List<PieceInfo> ownedPieces = new List<PieceInfo>();
    [Header("Initial Pieces")]
    [SerializeField] private InitialPieceData initialPieceData;
    [Header("Initial Seals")]
    [SerializeField] private InitialSealData initialSealData;
    private bool hasStageSnapshot = false;
    private string lastSuppliedScene = null;

    public IReadOnlyList<PieceInfo> OwnedPieces => ownedPieces;
    public InitialPieceData InitialPieceData => initialPieceData;
    public InitialSealData InitialSealData => initialSealData;
    public bool HasStageSnapshot => hasStageSnapshot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Scene-bound: do not persist across scenes to avoid stale UI references
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
    }

    private void OnSceneUnloaded(Scene scene)
    {
    }

    private void Start()
    {
        // 이어하기로 복원 중인 경우 초기 기물 지급을 건너뜁니다.
        if (StageSaveManager.IsContinuePending || StageSaveManager.IsRestoring || StageSaveManager.HasRestoredThisSession)
        {
            return;
        }

        // 씬의 모든 오브젝트가 초기화된 후, 초기 기물과 인장을 지급합니다.
        // 이 시점에서 호출해야 다른 매니저(EventManager 등)가 준비되어 있습니다.
        SupplyInitialPieces();
    }

    public void SupplyInitialPieces()
    {
        // 이미 지급했다면 중복 실행 방지
        if (lastSuppliedScene == SceneManager.GetActiveScene().name) return;

        if (initialPieceData == null)
        {
            return;
        }

        ClearPieces();

        foreach (var pieceInfo in initialPieceData.initialPieces)
        {
            if (ShotgunKingSystem.IsActive && pieceInfo.pieceType != PieceType.King) continue;
            AddPiece(pieceInfo.pieceType);
        }

        // 게임 시작 시 설정된 초기 인장 또는 무작위 인장 1개 지급
        StartCoroutine(GrantInitialSealRoutine());

        // 데이터 지급이 끝난 후, ShopManager에게 인벤토리 복원을 요청합니다.
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.RestoreInventory();
        }
        lastSuppliedScene = SceneManager.GetActiveScene().name;
    }

    private IEnumerator GrantInitialSealRoutine()
    {
        // EventManager와 SealInventory가 준비될 때까지 최대 1초 대기
        float waitTime = 0f;
        while ((EventManager.Instance == null || SealInventory.Instance == null) && waitTime < 1f)
        {
            yield return null;
            waitTime += Time.deltaTime;
        }

        if (EventManager.Instance != null && SealInventory.Instance != null)
        {
            if (initialSealData != null && initialSealData.initialSeals != null && initialSealData.initialSeals.Count > 0)
            {
                foreach (SealData seal in initialSealData.initialSeals)
                {
                    if (seal != null)
                    {
                        SealInventory.Instance.AddSeal(seal);
                    }
                }
            }
        }
    }

    public void AddPiece(PieceType pieceType)
    {
        if (ShotgunKingSystem.IsActive && pieceType != PieceType.King)
        {
            GameManager.Instance?.AddCoin(ShotgunKingSystem.GetSellPrice(pieceType));
            return;
        }
        ownedPieces.Add(new PieceInfo { pieceType = pieceType });
        CollectionManager.EnsureInstance()?.RecordPiece(pieceType);
    }

    public bool RemovePiece(PieceType pieceType)
    {
        PieceInfo pieceToRemove = ownedPieces.Find(p => p.pieceType == pieceType);
        if (pieceToRemove.pieceType == pieceType)
        {
            ownedPieces.Remove(pieceToRemove);

            return true;
        }
        return false;
    }

    public void ClearPieces()
    {
        ownedPieces.Clear();
        // Clear the last supplied scene marker so a subsequent scene load
        // (even the same scene name) will re-supply initial pieces.
        lastSuppliedScene = null;
    }

    public void SaveBoardPieces()
    {
        if (PieceManager.Instance == null) return;

        foreach (var piece in PieceManager.Instance.Pieces)
        {
            // 적 기물이 아니고, 전향된 임시 기물이 아니며, 보드(Board) 상태인 기물만 저장
            if (piece != null && !piece.IsEnemy && !piece.HasConversionSeal() && piece.CurrentLocation == PieceLocation.Board)
            {
                // 장착된 인장은 SealInventory로 반환
                foreach (var sealBase in piece.EquippedSeals)
                {
                    if (sealBase != null && sealBase.Data != null && !ConversionSeal.IsConversionSealData(sealBase.Data) && SealInventory.Instance != null)
                    {
                        SealInventory.Instance.AddSeal(sealBase.Data);
                    }
                }

                AddPiece(piece.HasPromotionSeal() ? PieceType.Pawn : piece.Type);
            }
        }
    }

    public void SavePiecesForNextStage()
    {
        if (hasStageSnapshot)
        {
            return;
        }

        ClearPieces();

        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        foreach (PieceController piece in allPieces)
        {
            if (piece == null || piece.IsEnemy || piece.HasConversionSeal())
            {
                continue;
            }

            if (piece.CurrentLocation != PieceLocation.Board && piece.CurrentLocation != PieceLocation.Inventory)
            {
                continue;
            }

            AddPiece(piece.HasPromotionSeal() ? PieceType.Pawn : piece.Type);
        }

        hasStageSnapshot = true;
    }

    public void ResetStageSnapshot()
    {
        hasStageSnapshot = false;
    }

    private void AddDefaultPieces()
    {
        // 기본 체스 기물 세트 예시 (로그라이크 특성에 맞춰 조절 가능)
        AddPiece(PieceType.King);
        AddPiece(PieceType.Queen);
        AddPiece(PieceType.Rook);
        AddPiece(PieceType.Bishop);
        AddPiece(PieceType.Knight);
        AddPiece(PieceType.Pawn);
        AddPiece(PieceType.Pawn);
        AddPiece(PieceType.Pawn);
    }
}
