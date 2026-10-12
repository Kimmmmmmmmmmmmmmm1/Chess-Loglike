using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ArtifactManager : MonoBehaviour
{
    public static ArtifactManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private List<ArtifactData> allArtifacts; // 전체 아티팩트 목록
    [SerializeField] private List<ArtifactData> ownedArtifacts = new List<ArtifactData>(); // 소유중인 아티팩트

    public IReadOnlyList<ArtifactData> OwnedArtifacts => ownedArtifacts;

    public event Action<ArtifactData> OnArtifactAdded;
    public event Action<ArtifactData> OnArtifactEnhanced;
    public event Action<ArtifactData> OnArtifactConsumed;

    [Header("UI References")]
    public List<ArtifactSlot> artifactSlots; // 미리 생성된 슬롯들을 여기에 할당하세요
    
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
        // 자동으로 폴더에서 모든 유물 데이터 로드
        if (allArtifacts == null || allArtifacts.Count == 0)
        {
            LoadAllArtifactsFromFolder();
        }

        UpdateInventoryUI();
    }

    /// <summary>
    /// Assets/Data/Artifact 폴더에서 모든 ArtifactData 자동 로드
    /// </summary>
    private void LoadAllArtifactsFromFolder()
    {
        allArtifacts = new List<ArtifactData>();
        
        #if UNITY_EDITOR
        // 에디터 환경: AssetDatabase 사용
        string folderPath = "Assets/Data/Artifact";
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ArtifactData", new[] { folderPath });
        
        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ArtifactData artifact = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtifactData>(assetPath);
            if (artifact != null)
            {
                allArtifacts.Add(artifact);
            }
        }
        #else
        // 런타임 환경: Resources 폴더 사용 (Data 폴더를 Resources로 이동 필요)
        ArtifactData[] loadedArtifacts = Resources.LoadAll<ArtifactData>("Artifact");
        allArtifacts.AddRange(loadedArtifacts);
        #endif

    }

    public void AddArtifact(ArtifactData artifact)
    {
        if (artifact == null) return;
        
        ArtifactData existing = ownedArtifacts.FirstOrDefault(a => a != null && a.id == artifact.id);
        if (existing != null)
        {
            // 액티브 유물은 같은 칸에 개수만 증가
            if (existing.IsActive)
            {
                existing.AddStack(1);
                UpdateInventoryUI();
                OnArtifactAdded?.Invoke(existing);
            }
            // 패시브 유물은 중복 획득 방지
            return;
        }

        ArtifactData runtimeArtifact = Instantiate(artifact);
        runtimeArtifact.name = artifact.name;
        runtimeArtifact.ResetLevel();
        runtimeArtifact.ResetStack();

        ownedArtifacts.Add(runtimeArtifact);
        UpdateInventoryUI();
        CollectionManager.EnsureInstance().RecordArtifact(runtimeArtifact);
        OnArtifactAdded?.Invoke(runtimeArtifact);
    }

    /// <summary>
    /// 유물을 제거합니다. 여러 개 보유한 액티브 유물은 1개만 감소합니다.
    /// </summary>
    public void RemoveArtifact(ArtifactData artifact)
    {
        if (artifact == null || !ownedArtifacts.Contains(artifact)) return;

        if (artifact.IsActive && artifact.RemoveStack())
        {
            UpdateInventoryUI();
            return;
        }

        if (ownedArtifacts.Remove(artifact))
        {
            UpdateInventoryUI();
        }
    }

    /// <summary>
    /// 액티브 유물 1개를 사용(소모)합니다.
    /// </summary>
    public void ConsumeArtifact(ArtifactData artifact)
    {
        if (artifact == null || !artifact.IsActive) return;

        RemoveArtifact(artifact);
        OnArtifactConsumed?.Invoke(artifact);
    }

    /// <summary>
    /// 새로 획득할 유물이 들어갈 인벤토리 슬롯을 반환합니다. (이미 보유한 액티브 유물이면 기존 칸)
    /// </summary>
    public ArtifactSlot GetIncomingSlot(ArtifactData artifact)
    {
        if (artifactSlots == null) return null;

        int targetIndex = ownedArtifacts.Count;
        if (artifact != null && artifact.IsActive)
        {
            int existingIndex = ownedArtifacts.FindIndex(a => a != null && a.id == artifact.id);
            if (existingIndex >= 0) targetIndex = existingIndex;
        }

        return targetIndex < artifactSlots.Count ? artifactSlots[targetIndex] : null;
    }

    public ArtifactSlot GetSlotOf(ArtifactData artifact)
    {
        if (artifactSlots == null || artifact == null) return null;

        int index = ownedArtifacts.IndexOf(artifact);
        return index >= 0 && index < artifactSlots.Count ? artifactSlots[index] : null;
    }

    public void ClearArtifacts()
    {
        ActiveArtifactController.CancelIfTargeting();
        ownedArtifacts.Clear();
        UpdateInventoryUI();
    }

    public bool HasArtifact(string artifactId)
    {
        if (string.IsNullOrEmpty(artifactId)) return false;
        return ownedArtifacts.Any(a => a.id == artifactId);
    }

    public bool HasArtifact(string artifactId, out int level)
    {
        level = 0;

        ArtifactData artifact = GetOwnedArtifact(artifactId);
        if (artifact == null)
        {
            return false;
        }

        level = artifact.Level;
        return true;
    }

    public ArtifactData GetOwnedArtifact(string artifactId)
    {
        if (string.IsNullOrEmpty(artifactId))
        {
            return null;
        }

        return ownedArtifacts.FirstOrDefault(a => a != null && a.id == artifactId);
    }

    public int GetArtifactLevel(string artifactId, int defaultLevel = 0)
    {
        ArtifactData artifact = GetOwnedArtifact(artifactId);
        return artifact != null ? artifact.Level : defaultLevel;
    }

    public bool ApplyArtifactWithLevel(string artifactId, Action<int> applyAction)
    {
        if (applyAction == null)
        {
            return false;
        }

        ArtifactData artifact = GetOwnedArtifact(artifactId);
        if (artifact == null)
        {
            return false;
        }

        applyAction(artifact.Level);
        return true;
    }

    public bool TryEnhanceArtifact(string artifactId, out ArtifactData enhancedArtifact, out int newLevel)
    {
        enhancedArtifact = null;
        newLevel = 0;

        ArtifactData artifact = GetOwnedArtifact(artifactId);
        if (artifact == null)
        {
            return false;
        }

        if (!artifact.TryEnhance())
        {
            return false;
        }

        enhancedArtifact = artifact;
        newLevel = artifact.Level;
        OnArtifactEnhanced?.Invoke(artifact);
        GameManager.Instance?.RecordArtifactEnhanced();
        UpdateInventoryUI();
        return true;
    }

    public bool TryEnhanceAnyArtifact(out ArtifactData enhancedArtifact, out int newLevel)
    {
        enhancedArtifact = ownedArtifacts.FirstOrDefault(a => a != null && a.CanEnhance);
        newLevel = 0;

        if (enhancedArtifact == null)
        {
            return false;
        }

        if (!enhancedArtifact.TryEnhance())
        {
            enhancedArtifact = null;
            return false;
        }

        newLevel = enhancedArtifact.Level;
        OnArtifactEnhanced?.Invoke(enhancedArtifact);
        GameManager.Instance?.RecordArtifactEnhanced();
        UpdateInventoryUI();
        return true;
    }

    /// <summary>
    /// 획득 후보 여부: 패시브는 미보유일 때만, 액티브는 중복 보유 가능하므로 항상 후보.
    /// </summary>
    private bool IsObtainable(ArtifactData candidate)
    {
        if (candidate == null) return false;
        if (candidate.IsActive) return true;
        return !ownedArtifacts.Any(owned => owned != null && owned.id == candidate.id);
    }

    public ArtifactData GetRandomArtifact()
    {
        if (allArtifacts == null || allArtifacts.Count == 0) return null;

        // 획득 가능한 아티팩트 중에서 랜덤 선택
        var availableArtifacts = allArtifacts
            .Where(IsObtainable)
            .ToList();
        
        if (availableArtifacts.Count == 0) return null; // 모든 아티팩트 획득함

        return availableArtifacts[UnityEngine.Random.Range(0, availableArtifacts.Count)];
    }

    /// <summary>
    /// 상점에서 판매 가능한 유물 중에서 랜덤으로 선택
    /// </summary>
    public ArtifactData GetRandomArtifactForShop()
    {
        if (allArtifacts == null || allArtifacts.Count == 0) return null;

        // 상점에서 판매 가능하고, 소유하지 않은 아티팩트 중에서 랜덤 선택
        var shopAvailableArtifacts = allArtifacts
            .Where(candidate => candidate != null && 
                   candidate.isSoldInShop && 
                   IsObtainable(candidate))
            .ToList();
        
        if (shopAvailableArtifacts.Count == 0) return null;

        return shopAvailableArtifacts[UnityEngine.Random.Range(0, shopAvailableArtifacts.Count)];
    }

    public void UpdateInventoryUI()
    {
        if (artifactSlots == null) return;

        for (int i = 0; i < artifactSlots.Count; i++)
        {
            var slot = artifactSlots[i];
            if (slot == null) continue; // skip empty slot references

            if (i < ownedArtifacts.Count)
            {
                slot.Initialize(ownedArtifacts[i]);

                bool isGourd = ownedArtifacts[i] != null && ownedArtifacts[i].id == "A004";
                bool isExhausted = isGourd && ArtifactEffectHandlers.IsGourdRecoveryExhausted();
                slot.SetDimmed(isExhausted);
            }
            else
            {
                slot.Initialize(null);
            }
        }
    }
    public void UpdateEnhanceUI()
    {
        if (artifactSlots == null) return;

        foreach (var slot in artifactSlots)
        {
            slot?.RefreshUI();
        }
    }
}
