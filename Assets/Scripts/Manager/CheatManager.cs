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
