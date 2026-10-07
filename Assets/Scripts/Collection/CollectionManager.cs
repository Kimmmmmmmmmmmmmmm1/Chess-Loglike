using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class CollectionManager : PersistentManagerBase
{
    private static class AchievementIds
    {
        public const string CollectFirstArtifact = "collect_first_artifact";
        public const string CollectArtifact3 = "collect_artifact_3";
        public const string CollectArtifact5 = "collect_artifact_5";
        public const string CollectArtifact10 = "collect_artifact_10";
        public const string CollectAllArtifacts = "collect_all_artifacts";

        public const string CollectFirstSeal = "collect_first_seal";
        public const string CollectSeal3 = "collect_seal_3";
        public const string CollectSeal5 = "collect_seal_5";
    }

    public static CollectionManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private List<ArtifactData> allArtifacts = new List<ArtifactData>();
    [SerializeField] private List<SealData> allSeals = new List<SealData>();
    [SerializeField] private string artifactEditorFolderPath = "Assets/Data/Artifact";
    [SerializeField] private string sealEditorFolderPath = "Assets/Data/Seal";
    [SerializeField] private string artifactRuntimeResourcesPath = "Artifact";
    [SerializeField] private string sealRuntimeResourcesPath = "Seal";

    [Header("Save")]
    [SerializeField] private string saveFileName = "collection_save.json";

    public event Action OnCollectionChanged;

    private CollectionSaveData saveData = new CollectionSaveData();
    private readonly HashSet<string> unlockedArtifactLookup = new HashSet<string>();
    private readonly HashSet<string> unlockedSealLookup = new HashSet<string>();

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    protected override void Awake()
    {
        base.Awake();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadAllCollectionData();
        LoadSaveData();
        RebuildLookup();
    }

    public static CollectionManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        CollectionManager existing = FindFirstObjectByType<CollectionManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject managerObject = new GameObject("CollectionManager");
        return managerObject.AddComponent<CollectionManager>();
    }

    public override void ResetForNewRun()
    {
        // Collection progress persists across runs.
    }

    public IReadOnlyList<ArtifactData> GetAllArtifacts()
    {
        return allArtifacts
            .Where(artifact => artifact != null && !string.IsNullOrEmpty(GetArtifactId(artifact)))
            .OrderBy(artifact => IsArtifactUnlocked(artifact) ? 0 : 1)
            .ThenBy(artifact => artifact.rarity)
            .ThenBy(artifact => artifact.artifactName)
            .ToList();
    }

    public IReadOnlyList<SealData> GetAllSeals()
    {
        return allSeals
            .Where(seal => seal != null && !string.IsNullOrEmpty(GetSealId(seal)))
            .OrderBy(seal => IsSealUnlocked(seal) ? 0 : 1)
            .ThenBy(seal => seal.rarity)
            .ThenBy(seal => seal.sealName)
            .ToList();
    }

    public bool RecordArtifact(ArtifactData artifact)
    {
        string artifactId = GetArtifactId(artifact);
        if (string.IsNullOrEmpty(artifactId) || unlockedArtifactLookup.Contains(artifactId))
        {
            return false;
        }

        unlockedArtifactLookup.Add(artifactId);
        saveData.unlockedArtifactIDs.Add(artifactId);
        TryAddCollectionAchievementProgress(AchievementIds.CollectFirstArtifact);
        TryAddCollectionAchievementProgress(AchievementIds.CollectArtifact3);
        TryAddCollectionAchievementProgress(AchievementIds.CollectArtifact5);
        TryAddCollectionAchievementProgress(AchievementIds.CollectArtifact10);
        TryAddCollectionAchievementProgress(AchievementIds.CollectAllArtifacts);
        Save();
        OnCollectionChanged?.Invoke();
        return true;
    }

    public bool RecordSeal(SealData seal)
    {
        string sealId = GetSealId(seal);
        if (string.IsNullOrEmpty(sealId) || unlockedSealLookup.Contains(sealId))
        {
            return false;
        }

        unlockedSealLookup.Add(sealId);
        saveData.unlockedSealIDs.Add(sealId);
        TryAddCollectionAchievementProgress(AchievementIds.CollectFirstSeal);
        TryAddCollectionAchievementProgress(AchievementIds.CollectSeal3);
        TryAddCollectionAchievementProgress(AchievementIds.CollectSeal5);
        Save();
        OnCollectionChanged?.Invoke();
        return true;
    }

    public bool RecordSeals(IEnumerable<SealData> seals)
    {
        if (seals == null)
        {
            return false;
        }

        bool changed = false;
        foreach (SealData seal in seals)
        {
            string sealId = GetSealId(seal);
            if (string.IsNullOrEmpty(sealId) || unlockedSealLookup.Contains(sealId))
            {
                continue;
            }

            unlockedSealLookup.Add(sealId);
            saveData.unlockedSealIDs.Add(sealId);
            TryAddCollectionAchievementProgress(AchievementIds.CollectFirstSeal);
            TryAddCollectionAchievementProgress(AchievementIds.CollectSeal3);
            TryAddCollectionAchievementProgress(AchievementIds.CollectSeal5);
            changed = true;
        }

        if (changed)
        {
            Save();
            OnCollectionChanged?.Invoke();
        }

        return changed;
    }

    public bool IsArtifactUnlocked(ArtifactData artifact)
    {
        string artifactId = GetArtifactId(artifact);
        return !string.IsNullOrEmpty(artifactId) && unlockedArtifactLookup.Contains(artifactId);
    }

    public bool IsSealUnlocked(SealData seal)
    {
        string sealId = GetSealId(seal);
        return !string.IsNullOrEmpty(sealId) && unlockedSealLookup.Contains(sealId);
    }

    public int GetUnlockedArtifactCount()
    {
        return GetAllArtifacts().Count(IsArtifactUnlocked);
    }

    public int GetUnlockedSealCount()
    {
        return GetAllSeals().Count(IsSealUnlocked);
    }

    public int GetTotalArtifactCount()
    {
        return GetAllArtifacts().Count;
    }

    public int GetTotalSealCount()
    {
        return GetAllSeals().Count;
    }

    public int GetUnlockedTotalCount()
    {
        return GetUnlockedArtifactCount() + GetUnlockedSealCount();
    }

    public int GetTotalCount()
    {
        return GetTotalArtifactCount() + GetTotalSealCount();
    }

    public float GetOverallCompletionRatio()
    {
        int totalCount = GetTotalCount();
        return totalCount <= 0 ? 0f : Mathf.Clamp01((float)GetUnlockedTotalCount() / totalCount);
    }

    public static string GetArtifactId(ArtifactData artifact)
    {
        if (artifact == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrEmpty(artifact.id) ? artifact.id : artifact.name;
    }

    public static string GetSealId(SealData seal)
    {
        if (seal == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrEmpty(seal.name) ? seal.name : seal.sealName;
    }

    public void Save()
    {
        try
        {
            NormalizeSaveData();
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception)
        {
        }
    }

    public void ResetAllProgress()
    {
        saveData = new CollectionSaveData();
        RebuildLookup();
        Save();
        OnCollectionChanged?.Invoke();
    }

    private void LoadAllCollectionData()
    {
        if (allArtifacts == null)
        {
            allArtifacts = new List<ArtifactData>();
        }

        if (allSeals == null)
        {
            allSeals = new List<SealData>();
        }

        allArtifacts.Clear();
        allSeals.Clear();

#if UNITY_EDITOR
        string[] artifactGuids = UnityEditor.AssetDatabase.FindAssets("t:ArtifactData", new[] { artifactEditorFolderPath });
        foreach (string guid in artifactGuids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ArtifactData artifact = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtifactData>(assetPath);
            if (artifact != null)
            {
                allArtifacts.Add(artifact);
            }
        }

        string[] sealGuids = UnityEditor.AssetDatabase.FindAssets("t:SealData", new[] { sealEditorFolderPath });
        foreach (string guid in sealGuids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            SealData seal = UnityEditor.AssetDatabase.LoadAssetAtPath<SealData>(assetPath);
            if (seal != null)
            {
                allSeals.Add(seal);
            }
        }

        if (allArtifacts.Count == 0)
        {
            allArtifacts.AddRange(Resources.LoadAll<ArtifactData>(artifactRuntimeResourcesPath));
        }

        if (allSeals.Count == 0)
        {
            allSeals.AddRange(Resources.LoadAll<SealData>(sealRuntimeResourcesPath));
        }
#else
        allArtifacts.AddRange(Resources.LoadAll<ArtifactData>(artifactRuntimeResourcesPath));
        allSeals.AddRange(Resources.LoadAll<SealData>(sealRuntimeResourcesPath));
#endif
    }

    private void LoadSaveData()
    {
        if (!File.Exists(SavePath))
        {
            saveData = new CollectionSaveData();
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            saveData = JsonUtility.FromJson<CollectionSaveData>(json) ?? new CollectionSaveData();
        }
        catch (Exception)
        {
            saveData = new CollectionSaveData();
        }
    }

    private void RebuildLookup()
    {
        NormalizeSaveData();

        unlockedArtifactLookup.Clear();
        unlockedSealLookup.Clear();

        foreach (string artifactId in saveData.unlockedArtifactIDs)
        {
            if (!string.IsNullOrEmpty(artifactId))
            {
                unlockedArtifactLookup.Add(artifactId);
            }
        }

        foreach (string sealId in saveData.unlockedSealIDs)
        {
            if (!string.IsNullOrEmpty(sealId))
            {
                unlockedSealLookup.Add(sealId);
            }
        }
    }

    private void NormalizeSaveData()
    {
        if (saveData == null)
        {
            saveData = new CollectionSaveData();
        }

        saveData.unlockedArtifactIDs = saveData.unlockedArtifactIDs?
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList() ?? new List<string>();

        saveData.unlockedSealIDs = saveData.unlockedSealIDs?
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList() ?? new List<string>();
    }

    [ContextMenu("Debug/Reset All Collection Progress")]
    private void DebugResetAllProgress()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResetAllProgress();
    }

    private void TryAddCollectionAchievementProgress(string achievementId, int amount = 1)
    {
        if (string.IsNullOrEmpty(achievementId) || amount <= 0)
        {
            return;
        }

        AchievementManager.Instance?.AddProgress(achievementId, amount);
    }
}
