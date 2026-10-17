using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;



/// <summary>
/// 인스펙터 드롭다운 선택 및 컨텍스트 메뉴/버튼을 통해
/// 원하는 유물, 기물, 인장, 무한 골드 치트를 간편하게 실행할 수 있는 컴포넌트입니다.
/// </summary>
public class CheatManager : MonoBehaviour
{
    public static CheatManager Instance { get; private set; }

    [Header("1. Gold Cheat")]
    [Tooltip("무한 골드 모드 활성화 (체크 시 골드가 99,999로 고정 유지됨)")]
    public bool infiniteGold = false;
    [Tooltip("지급할 골드 금액")]
    public int addGoldAmount = 1000;

    [Header("2. Artifact Cheat")]
    [Tooltip("지급할 유물 에셋")]
    public ArtifactData selectedArtifactData;

    [Header("3. Piece Cheat")]
    [Tooltip("인벤토리에 지급할 기물 종류를 선택하세요")]
    public PieceType selectedPiece = PieceType.Queen;

    [Header("4. Seal Cheat")]
    [Tooltip("지급할 인장 에셋")]
    public SealData selectedSealData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (infiniteGold && GameManager.Instance != null)
        {
            if (GameManager.Instance.Coin < 99999)
            {
                GameManager.Instance.Coin = 99999;
                GameManager.Instance.AddCoin(0); // UI 갱신 트리거
            }
        }
    }

    // ==========================================
    // Context Menu Actions (컴포넌트 우클릭 메뉴)
    // ==========================================

    [ContextMenu("골드 지급 (Add Gold)")]
    public void GiveGold()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddCoin(addGoldAmount);
            Debug.Log($"[Cheat] 골드 {addGoldAmount} 지급 완료 (현재 골드: {GameManager.Instance.Coin})");
        }
        else
        {
            Debug.LogWarning("[Cheat] GameManager 인스턴스를 찾을 수 없습니다.");
        }
    }

    [ContextMenu("무한 골드 토글 (Toggle Infinite Gold)")]
    public void ToggleInfiniteGold()
    {
        infiniteGold = !infiniteGold;
        if (infiniteGold && GameManager.Instance != null)
        {
            GameManager.Instance.Coin = 99999;
            GameManager.Instance.AddCoin(0);
        }
        Debug.Log($"[Cheat] 무한 골드 모드: {(infiniteGold ? "ON (99,999)" : "OFF")}");
    }

    [ContextMenu("선택한 유물 지급 (Give Selected Artifact)")]
    public void GiveArtifact()
    {
        if (ArtifactManager.Instance == null)
        {
            Debug.LogWarning("[Cheat] ArtifactManager 인스턴스를 찾을 수 없습니다.");
            return;
        }

        if (selectedArtifactData != null)
        {
            ArtifactManager.Instance.AddArtifact(selectedArtifactData);
            Debug.Log($"[Cheat] 유물 획득: {selectedArtifactData.artifactName} (ID: {selectedArtifactData.id})");
        }
        else
        {
            Debug.LogWarning("[Cheat] 선택된 유물 에셋이 없습니다.");
        }
    }

    [ContextMenu("선택한 기물 지급 (Give Selected Piece)")]
    public void GivePiece()
    {
        InventorySlot[] slots = FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);
        var emptySlot = slots
            .OrderBy(s => s.transform.GetSiblingIndex())
            .FirstOrDefault(s => s.GetComponentInChildren<PieceController>() == null && !s.IsReserved);

        if (emptySlot == null)
        {
            if (PieceInventory.Instance != null)
            {
                PieceInventory.Instance.AddPiece(selectedPiece);
                Debug.Log($"[Cheat] 빈 슬롯이 없어 데이터 목록에만 기물 추가: {selectedPiece}");
            }
            else
            {
                Debug.LogWarning("[Cheat] 빈 인벤토리 슬롯이 없습니다.");
            }
            return;
        }

        if (PieceSpawner.Instance != null && PieceSpawner.Instance.piecePrefab != null)
        {
            GameObject obj = Instantiate(PieceSpawner.Instance.piecePrefab, emptySlot.transform);
            obj.name = $"PiecePrefab(Clone)@CheatGivePiece[{selectedPiece}_{emptySlot.name}]";
            PieceController piece = obj.GetComponent<PieceController>();
            if (piece != null)
            {
                piece.Initialize(selectedPiece, false);
                piece.MoveToInventory(emptySlot.transform, true);
                Debug.Log($"[Cheat] 기물 생성 완료: {selectedPiece}");
            }
        }
        else if (PieceInventory.Instance != null)
        {
            PieceInventory.Instance.AddPiece(selectedPiece);
            Debug.Log($"[Cheat] 기물 데이터 추가: {selectedPiece}");
        }
    }

    [ContextMenu("남은 기물 목록 콘솔 출력 (Print Remaining Pieces)")]
    public void PrintRemainingPieces()
    {
        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);

        if (allPieces == null || allPieces.Length == 0)
        {
            Debug.Log("[Cheat] 씬에 남아있는 기물이 없습니다.");
            return;
        }

        // 보드 위 기물은 gridPosition.HasValue가 true인 실제 타일 위 기물만 분류
        var playerBoardPieces = allPieces
            .Where(p => !p.IsEnemy && p.CurrentLocation == PieceLocation.Board && p.gridPosition.HasValue)
            .OrderBy(p => p.gridPosition.Value.y * 10 + p.gridPosition.Value.x)
            .ToList();

        var enemyBoardPieces = allPieces
            .Where(p => p.IsEnemy && p.CurrentLocation == PieceLocation.Board && p.gridPosition.HasValue)
            .OrderBy(p => p.gridPosition.Value.y * 10 + p.gridPosition.Value.x)
            .ToList();

        var inventoryPieces = allPieces
            .Where(p => !p.IsEnemy && p.CurrentLocation == PieceLocation.Inventory)
            .ToList();

        var unplacedPieces = allPieces
            .Except(playerBoardPieces)
            .Except(enemyBoardPieces)
            .Except(inventoryPieces)
            .ToList();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[Cheat] ===== 현재 기물 목록 (총 {allPieces.Length}개) =====");

        // 1. 보드 위 플레이어 기물
        sb.AppendLine($"■ [보드 위 플레이어 기물] ({playerBoardPieces.Count}개)");
        if (playerBoardPieces.Count == 0)
        {
            sb.AppendLine("  (없음)");
        }
        else
        {
            foreach (var p in playerBoardPieces)
            {
                sb.AppendLine($"  - {FormatPieceInfo(p)}");
            }
        }

        // 2. 보드 위 적 기물
        sb.AppendLine($"■ [보드 위 적 기물] ({enemyBoardPieces.Count}개)");
        if (enemyBoardPieces.Count == 0)
        {
            sb.AppendLine("  (없음)");
        }
        else
        {
            foreach (var p in enemyBoardPieces)
            {
                sb.AppendLine($"  - {FormatPieceInfo(p)}");
            }
        }

        // 3. 인벤토리 대기 기물
        sb.AppendLine($"■ [인벤토리 대기 기물] ({inventoryPieces.Count}개)");
        if (inventoryPieces.Count == 0)
        {
            sb.AppendLine("  (없음)");
        }
        else
        {
            foreach (var p in inventoryPieces)
            {
                sb.AppendLine($"  - {FormatPieceInfo(p)}");
            }
        }

        // 4. 좌표 없는 기물
        if (unplacedPieces.Count > 0)
        {
            sb.AppendLine($"■ [미배치 / 좌표 없음 / 기타 기물] ({unplacedPieces.Count}개)");
            foreach (var p in unplacedPieces)
            {
                sb.AppendLine($"  - {FormatPieceInfo(p)}");
            }
        }

        sb.Append("==================================================");

        Debug.Log(sb.ToString());
    }

    [ContextMenu("좌표 없는 보드 기물 즉시 정리 (Cleanup Invalid Board Pieces)")]
    public void CleanupInvalidBoardPieces()
    {
        if (PieceManager.Instance != null)
        {
            int count = PieceManager.Instance.CleanupInvalidBoardPieces();
            Debug.Log($"[Cheat] 좌표 없는 보드 기물 정리 완료: 총 {count}개 삭제됨.");
        }
        else
        {
            Debug.LogWarning("[Cheat] PieceManager 인스턴스를 찾을 수 없습니다.");
        }
    }

    private string FormatPieceInfo(PieceController piece)
    {
        string posStr;
        if (piece.gridPosition.HasValue)
        {
            posStr = $"위치: ({piece.gridPosition.Value.x}, {piece.gridPosition.Value.y})";
        }
        else
        {
            string parentName = piece.transform.parent != null ? piece.transform.parent.name : "루트";
            posStr = $"위치: {piece.CurrentLocation} (좌표 없음, 부모: [{parentName}])";
        }

        string sealStr = "";
        if (piece.EquippedSeals != null && piece.EquippedSeals.Count > 0)
        {
            var sealNames = piece.EquippedSeals
                .Where(s => s != null && s.Data != null)
                .Select(s => s.Data.sealName);
            string joined = string.Join(", ", sealNames);
            if (!string.IsNullOrEmpty(joined))
            {
                sealStr = $" [인장: {joined}]";
            }
        }

        List<string> buffs = new List<string>();
        if (piece.ProtectionStacks > 0) buffs.Add($"보호x{piece.ProtectionStacks}");
        if (piece.EvasionStacks > 0) buffs.Add($"회피x{piece.EvasionStacks}");
        if (piece.CounterattackStacks > 0) buffs.Add($"역공x{piece.CounterattackStacks}");
        if (piece.IsInvincible) buffs.Add("무적");
        string buffStr = buffs.Count > 0 ? $" [버프: {string.Join(", ", buffs)}]" : "";

        string side = piece.IsEnemy ? "적" : "아군";
        return $"[{side}] {piece.gameObject.name} ({piece.Type}) | {posStr}{sealStr}{buffStr}";
    }

    [ContextMenu("선택한 인장 지급 (Give Selected Seal)")]
    public void GiveSeal()
    {
        if (SealInventory.Instance == null)
        {
            Debug.LogWarning("[Cheat] SealInventory 인스턴스를 찾을 수 없습니다.");
            return;
        }

        if (selectedSealData != null)
        {
            SealInventory.Instance.AddSeal(selectedSealData);

            SealInventoryPanel panel = FindAnyObjectByType<SealInventoryPanel>(FindObjectsInactive.Include);
            panel?.RefreshSealItems();

            Debug.Log($"[Cheat] 인장 획득: {selectedSealData.sealName} ({selectedSealData.name})");
        }
        else
        {
            Debug.LogWarning("[Cheat] 선택된 인장 에셋이 없습니다.");
        }
    }

}
