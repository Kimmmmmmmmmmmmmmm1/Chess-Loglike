using System.Linq;
using UnityEngine;

public enum CheatArtifactChoice
{
    [InspectorName("A001: 상인의 모자 (상점 기물 수 증가)")]
    A001_MerchantHat,

    [InspectorName("A002: 상인의 머플러 (리롤 비용 할인)")]
    A002_MerchantMuffler,

    [InspectorName("A003: 상인의 가족사진 (인장 확률 증가)")]
    A003_MerchantFamilyPhoto,

    [InspectorName("A004: 망자의 호리병 (기물 파괴 방지)")]
    A004_GourdOfTheFallen,

    [InspectorName("A005: 끝없는 묘비 (기물 파괴 시 보상)")]
    A005_EndlessTombstone,

    [InspectorName("AC01: [액티브] 시간의 모래 (적 턴 스킵)")]
    AC01_HourglassOfTime,

    [InspectorName("AC02: [액티브] 낙뢰의 부적 (적 기물 즉시 파괴)")]
    AC02_LightningTalisman,

    [InspectorName("AC03: [액티브] 보호의 물약 (아군 기물에 보호 1회)")]
    AC03_ProtectionPotion,

    [InspectorName("AC04: [액티브] 회피의 물약 (아군 기물에 회피 1회)")]
    AC04_EvasionPotion,

    [InspectorName("AC05: [액티브] 역공의 물약 (아군 기물에 역공 1회)")]
    AC05_CounterattackPotion
}

public enum CheatSealChoice
{
    [InspectorName("비숍의 인장 (대각선 이동 추가)")]
    Bishop,

    [InspectorName("도약의 인장 (나이트 도약 이동 추가)")]
    KnightLeap,

    [InspectorName("반사의 인장 (벽 반사 이동)")]
    Reflection,

    [InspectorName("카멜레온의 인장 (적 기물 처치 시 형태 복제)")]
    Chameleon,

    [InspectorName("승급의 인장 (퀸/스페셜 기물 승급)")]
    Promotion,

    [InspectorName("자폭의 인장 (파괴 시 주변 폭발)")]
    Destruction,

    [InspectorName("붕괴의 인장 (공격 시 타일 붕괴)")]
    Collapse,

    [InspectorName("측면의 인장 (좌우 수평 이동 추가)")]
    LateralAdvance,

    [InspectorName("후방 대각선의 인장 (후방 대각선 이동)")]
    DiagonalRetreat,

    [InspectorName("부서진 비숍 인장 (전방 대각선만)")]
    Diagnal
}

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
    [Tooltip("지급할 유물을 선택하세요")]
    public CheatArtifactChoice selectedArtifact = CheatArtifactChoice.AC03_ProtectionPotion;

    [Header("3. Piece Cheat")]
    [Tooltip("인벤토리에 지급할 기물 종류를 선택하세요")]
    public PieceType selectedPiece = PieceType.Queen;

    [Header("4. Seal Cheat")]
    [Tooltip("지급할 인장을 선택하세요")]
    public CheatSealChoice selectedSeal = CheatSealChoice.Reflection;

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

        string targetId = GetArtifactId(selectedArtifact);
        ArtifactData target = null;

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ArtifactData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var a = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtifactData>(path);
            if (a != null && a.id == targetId)
            {
                target = a;
                break;
            }
        }
#endif
        if (target == null)
        {
            ArtifactData[] loaded = Resources.LoadAll<ArtifactData>("Artifact");
            target = loaded.FirstOrDefault(a => a != null && a.id == targetId);
        }

        if (target != null)
        {
            ArtifactManager.Instance.AddArtifact(target);
            Debug.Log($"[Cheat] 유물 획득: {target.artifactName} (ID: {target.id})");
        }
        else
        {
            Debug.LogWarning($"[Cheat] 유물(ID: {targetId})을 찾을 수 없습니다.");
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

        string sealFileName = selectedSeal.ToString();
        SealData target = Resources.Load<SealData>($"Seal/{sealFileName}");

#if UNITY_EDITOR
        if (target == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SealData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var s = UnityEditor.AssetDatabase.LoadAssetAtPath<SealData>(path);
                if (s != null && s.name == sealFileName)
                {
                    target = s;
                    break;
                }
            }
        }
#endif

        if (target != null)
        {
            SealInventory.Instance.AddSeal(target);

            SealInventoryPanel panel = FindAnyObjectByType<SealInventoryPanel>(FindObjectsInactive.Include);
            panel?.RefreshSealItems();

            Debug.Log($"[Cheat] 인장 획득: {target.sealName} ({target.name})");
        }
        else
        {
            Debug.LogWarning($"[Cheat] 인장 에셋({sealFileName})을 찾을 수 없습니다.");
        }
    }

    public static string GetArtifactId(CheatArtifactChoice choice)
    {
        return choice switch
        {
            CheatArtifactChoice.A001_MerchantHat => "A001",
            CheatArtifactChoice.A002_MerchantMuffler => "A002",
            CheatArtifactChoice.A003_MerchantFamilyPhoto => "A003",
            CheatArtifactChoice.A004_GourdOfTheFallen => "A004",
            CheatArtifactChoice.A005_EndlessTombstone => "A005",
            CheatArtifactChoice.AC01_HourglassOfTime => "AC01",
            CheatArtifactChoice.AC02_LightningTalisman => "AC02",
            CheatArtifactChoice.AC03_ProtectionPotion => "AC03",
            CheatArtifactChoice.AC04_EvasionPotion => "AC04",
            CheatArtifactChoice.AC05_CounterattackPotion => "AC05",
            _ => "A001"
        };
    }
}
