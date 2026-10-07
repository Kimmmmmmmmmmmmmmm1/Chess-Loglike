using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public class AchievementManager : PersistentManagerBase
{
    public static AchievementManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private List<AchievementData> allAchievements = new List<AchievementData>();
    [SerializeField] private string editorFolderPath = "Assets/Resources/Achievement";
    [SerializeField] private string runtimeResourcesPath = "Achievement";

    [Header("Save")]
    [SerializeField] private string saveFileName = "achievement_save.json";

    [Header("Debug")]
    [SerializeField] private string debugAchievementId = "test_first_capture";

    public event Action<AchievementData> OnAchievementUnlocked;
    public event Action<AchievementData, int, int> OnAchievementProgressChanged;

    private readonly Dictionary<string, AchievementData> achievementLookup = new Dictionary<string, AchievementData>();
    private readonly Dictionary<string, AchievementProgressData> progressLookup = new Dictionary<string, AchievementProgressData>();
    private AchievementSaveData saveData = new AchievementSaveData();

    [Serializable]
    private class LegacyAchievementSaveData
    {
        public List<LegacyAchievementProgressData> achievements = new List<LegacyAchievementProgressData>();
    }

    [Serializable]
    private class LegacyAchievementProgressData
    {
        public string achievementId = string.Empty;
        public int currentCount = 0;
        public bool isUnlocked = false;
    }

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

        LoadAllAchievements();
        LoadSaveData();
        RebuildProgressLookup();
    }

    public override void ResetForNewRun()
    {
        // Achievements persist across runs; nothing to reset per-run by default.
    }

    /// <summary>
    /// 런타임에 씬에 AchievementManager가 없을 경우 자동으로 생성합니다.
    /// </summary>
    // NOTE: Scene creation/bootstrap responsibilities are handled manually.
    // Removed automatic EnsureInstance to keep lifecycle explicit per project preferences.

    private void LoadAllAchievements()
    {
        if (allAchievements == null)
        {
            allAchievements = new List<AchievementData>();
        }
        else
        {
            allAchievements.Clear();
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AchievementData", new[] { editorFolderPath });
        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            AchievementData achievement = UnityEditor.AssetDatabase.LoadAssetAtPath<AchievementData>(assetPath);
            if (achievement != null)
            {
                allAchievements.Add(achievement);
            }
        }

        if (allAchievements.Count == 0)
        {
            AchievementData[] loadedAchievements = Resources.LoadAll<AchievementData>(runtimeResourcesPath);
            allAchievements.AddRange(loadedAchievements);
        }
#else
        AchievementData[] loadedAchievements = Resources.LoadAll<AchievementData>(runtimeResourcesPath);
        allAchievements.AddRange(loadedAchievements);
#endif

        achievementLookup.Clear();
        foreach (AchievementData achievement in allAchievements)
        {
            if (achievement == null || string.IsNullOrEmpty(achievement.id))
            {
                continue;
            }

            if (!achievementLookup.ContainsKey(achievement.id))
            {
                achievementLookup.Add(achievement.id, achievement);
            }
        }
    }

    private void LoadSaveData()
    {
        if (!File.Exists(SavePath))
        {
            saveData = new AchievementSaveData();
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            if (!string.IsNullOrEmpty(json) && json.Contains("\"isUnlocked\""))
            {
                LegacyAchievementSaveData legacySaveData = JsonUtility.FromJson<LegacyAchievementSaveData>(json);
                saveData = ConvertLegacySaveData(legacySaveData);
                Save();
                return;
            }

            saveData = JsonUtility.FromJson<AchievementSaveData>(json) ?? new AchievementSaveData();
        }
        catch (Exception)
        {
            saveData = new AchievementSaveData();
        }
    }

    private AchievementSaveData ConvertLegacySaveData(LegacyAchievementSaveData legacySaveData)
    {
        AchievementSaveData convertedSaveData = new AchievementSaveData();

        if (legacySaveData == null || legacySaveData.achievements == null)
        {
            return convertedSaveData;
        }

        foreach (LegacyAchievementProgressData legacyProgress in legacySaveData.achievements)
        {
            if (legacyProgress == null || string.IsNullOrEmpty(legacyProgress.achievementId))
            {
                continue;
            }

            convertedSaveData.achievements.Add(new AchievementProgressData
            {
                achievementId = legacyProgress.achievementId,
                currentCount = legacyProgress.currentCount,
                clearState = legacyProgress.isUnlocked ? GetCurrentClearStateStamp() : 0L
            });
        }

        return convertedSaveData;
    }

    private void RebuildProgressLookup()
    {
        progressLookup.Clear();

        if (saveData == null)
        {
            saveData = new AchievementSaveData();
        }

        if (saveData.achievements == null)
        {
            saveData.achievements = new List<AchievementProgressData>();
        }

        foreach (AchievementData achievement in allAchievements)
        {
            if (achievement == null || string.IsNullOrEmpty(achievement.id))
            {
                continue;
            }

            AchievementProgressData progress = saveData.achievements.FirstOrDefault(item => item != null && item.achievementId == achievement.id);
            if (progress == null)
            {
                progress = new AchievementProgressData
                {
                    achievementId = achievement.id,
                    currentCount = 0,
                    clearState = 0L
                };
                saveData.achievements.Add(progress);
            }

            progressLookup[achievement.id] = progress;
        }

        saveData.achievements = progressLookup.Values.ToList();
        Save();
    }

    public IReadOnlyList<AchievementData> GetAllAchievements()
    {
        return allAchievements;
    }

    public IReadOnlyList<AchievementData> GetSortedAchievements(AchievementCategory? categoryFilter = null)
    {
        IEnumerable<AchievementData> filteredAchievements = allAchievements
            .Where(achievement => achievement != null && !string.IsNullOrEmpty(achievement.id));

        if (categoryFilter.HasValue)
        {
            filteredAchievements = filteredAchievements.Where(achievement => achievement.category == categoryFilter.Value);
        }

        var sorted = filteredAchievements
            .OrderBy(achievement => IsUnlocked(achievement.id) ? 0 : 1)
            .ThenBy(achievement => categoryFilter.HasValue ? 0 : (int)achievement.category)
            .ThenBy(achievement => achievement.difficulty)
            .ThenBy(achievement => achievement.achievementName)
            .ToList();

        return sorted;
    }

    public IReadOnlyList<AchievementData> GetVisibleAchievements(AchievementCategory? categoryFilter = null)
    {
        HashSet<AchievementData> chainedChildren = new HashSet<AchievementData>();

        foreach (AchievementData achievement in allAchievements)
        {
            if (achievement != null && achievement.nextAchievement != null)
            {
                chainedChildren.Add(achievement.nextAchievement);
            }
        }

        IEnumerable<AchievementData> visibleAchievements = allAchievements
            .Where(achievement => achievement != null && !string.IsNullOrEmpty(achievement.id))
            .Where(achievement => !chainedChildren.Contains(achievement));

        if (categoryFilter.HasValue)
        {
            visibleAchievements = visibleAchievements.Where(achievement => achievement.category == categoryFilter.Value);
        }

        return visibleAchievements
            .OrderBy(achievement => IsUnlocked(achievement.id) ? 0 : 1)
            .ThenBy(achievement => categoryFilter.HasValue ? 0 : (int)achievement.category)
            .ThenBy(achievement => achievement.difficulty)
            .ThenBy(achievement => achievement.achievementName)
            .ToList();
    }

    public bool IsChainedAchievement(AchievementData achievement)
    {
        if (achievement == null)
        {
            return false;
        }

        foreach (AchievementData candidate in allAchievements)
        {
            if (candidate != null && candidate.nextAchievement == achievement)
            {
                return true;
            }
        }

        return false;
    }

    public AchievementData GetChainRoot(AchievementData achievement)
    {
        if (achievement == null)
        {
            return null;
        }

        AchievementData current = achievement;
        AchievementData previous = GetPreviousChainStep(current);
        while (previous != null)
        {
            current = previous;
            previous = GetPreviousChainStep(current);
        }

        return current;
    }

    public int GetChainLength(AchievementData achievement)
    {
        AchievementData root = GetChainRoot(achievement);
        if (root == null)
        {
            return 0;
        }

        int count = 0;
        AchievementData current = root;
        while (current != null)
        {
            count++;
            current = current.nextAchievement;
        }

        return count;
    }

    public int GetChainCurrentStepIndex(AchievementData achievement)
    {
        AchievementData root = GetChainRoot(achievement);
        if (root == null)
        {
            return 1;
        }

        int stepIndex = 1;
        AchievementData current = root;
        while (current != null)
        {
            if (!IsUnlocked(current.id))
            {
                return stepIndex;
            }

            if (current.nextAchievement == null)
            {
                return stepIndex;
            }

            current = current.nextAchievement;
            stepIndex++;
        }

        return stepIndex;
    }

    public string GetDisplayProgressText(string achievementId)
    {
        AchievementData achievement = GetAchievement(achievementId);
        if (achievement == null)
        {
            return "0/1";
        }

        return achievement.GetProgressText(GetCurrentCount(achievementId));
    }

    private AchievementData GetPreviousChainStep(AchievementData target)
    {
        if (target == null)
        {
            return null;
        }

        foreach (AchievementData candidate in allAchievements)
        {
            if (candidate != null && candidate.nextAchievement == target)
            {
                return candidate;
            }
        }

        return null;
    }

    public AchievementData GetAchievement(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId))
        {
            return null;
        }

        return achievementLookup.TryGetValue(achievementId, out AchievementData achievement) ? achievement : null;
    }

    public bool IsUnlocked(string achievementId)
    {
        AchievementProgressData progress = GetProgressData(achievementId);
        return progress != null && progress.clearState > 0;
    }

    public int GetCurrentCount(string achievementId)
    {
        AchievementProgressData progress = GetProgressData(achievementId);
        return progress != null ? Mathf.Max(0, progress.currentCount) : 0;
    }

    public long GetClearState(string achievementId)
    {
        AchievementProgressData progress = GetProgressData(achievementId);
        return progress != null ? progress.clearState : 0L;
    }

    public string GetClearTimeText(string achievementId)
    {
        long clearState = GetClearState(achievementId);
        if (clearState <= 0)
        {
            return string.Empty;
        }

        string stamp = clearState.ToString();
        if (!DateTime.TryParseExact(stamp, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            return string.Empty;
        }

        return parsed.ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture);
    }

    public int GetTargetCount(string achievementId)
    {
        AchievementData achievement = GetAchievement(achievementId);
        if (achievement == null)
        {
            return 1;
        }

        int currentCount = GetCurrentCount(achievementId);
        return achievement.GetCurrentDisplayTarget(currentCount);
    }

    public string GetProgressText(string achievementId)
    {
        AchievementData achievement = GetAchievement(achievementId);
        if (achievement == null)
        {
            return "0/1";
        }

        return achievement.GetProgressText(GetCurrentCount(achievementId));
    }

    public int GetTotalAchievementCount()
    {
        int totalCount = 0;

        for (int i = 0; i < allAchievements.Count; i++)
        {
            AchievementData achievement = allAchievements[i];
            if (achievement != null && !string.IsNullOrEmpty(achievement.id))
            {
                totalCount++;
            }
        }

        return totalCount;
    }

    public int GetUnlockedAchievementCount()
    {
        int unlockedCount = 0;

        for (int i = 0; i < allAchievements.Count; i++)
        {
            AchievementData achievement = allAchievements[i];
            if (achievement != null && !string.IsNullOrEmpty(achievement.id) && IsUnlocked(achievement.id))
            {
                unlockedCount++;
            }
        }

        return unlockedCount;
    }

    public float GetOverallCompletionRatio()
    {
        int totalCount = GetTotalAchievementCount();
        if (totalCount <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)GetUnlockedAchievementCount() / totalCount);
    }

    public bool AddProgress(string achievementId, int amount = 1)
    {
        if (string.IsNullOrEmpty(achievementId) || amount <= 0)
        {
            return false;
        }

        AchievementData achievement = GetAchievement(achievementId);
        if (achievement == null)
        {
            return false;
        }

        AchievementProgressData progress = GetOrCreateProgressData(achievementId);
        if (progress.clearState > 0)
        {
            return false;
        }

        int previousCount = progress.currentCount;
        int finalTargetCount = achievement.GetFinalTargetCount();

        progress.currentCount = Mathf.Clamp(progress.currentCount + amount, 0, finalTargetCount);
        int displayTarget = achievement.GetCurrentDisplayTarget(progress.currentCount);
        OnAchievementProgressChanged?.Invoke(achievement, progress.currentCount, displayTarget);

        if (progress.currentCount >= finalTargetCount)
        {
            progress.clearState = GetCurrentClearStateStamp();
            OnAchievementUnlocked?.Invoke(achievement);
        }

        if (progress.currentCount != previousCount || progress.clearState > 0)
        {
            Save();
        }

        return true;
    }

    public bool SetProgress(string achievementId, int currentCount)
    {
        if (string.IsNullOrEmpty(achievementId))
        {
            return false;
        }

        AchievementData achievement = GetAchievement(achievementId);
        if (achievement == null)
        {
            return false;
        }

        AchievementProgressData progress = GetOrCreateProgressData(achievementId);
        int finalTargetCount = achievement.GetFinalTargetCount();
        progress.currentCount = Mathf.Clamp(currentCount, 0, finalTargetCount);
        progress.clearState = progress.currentCount >= finalTargetCount ? GetCurrentClearStateStamp() : 0L;

        int displayTarget = achievement.GetCurrentDisplayTarget(progress.currentCount);
        OnAchievementProgressChanged?.Invoke(achievement, progress.currentCount, displayTarget);
        if (progress.clearState > 0)
        {
            OnAchievementUnlocked?.Invoke(achievement);
        }

        Save();
        return true;
    }

    public bool UnlockAchievement(string achievementId)
    {
        AchievementData achievement = GetAchievement(achievementId);
        if (achievement == null)
        {
            return false;
        }

        AchievementProgressData progress = GetOrCreateProgressData(achievementId);
        if (progress.clearState > 0)
        {
            return false;
        }

        int finalTargetCount = achievement.GetFinalTargetCount();
        progress.currentCount = Mathf.Max(progress.currentCount, finalTargetCount);
        progress.clearState = GetCurrentClearStateStamp();
        OnAchievementProgressChanged?.Invoke(achievement, progress.currentCount, achievement.GetCurrentDisplayTarget(progress.currentCount));
        OnAchievementUnlocked?.Invoke(achievement);
        Save();
        return true;
    }

    private AchievementProgressData GetProgressData(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId))
        {
            return null;
        }

        if (progressLookup.TryGetValue(achievementId, out AchievementProgressData progress))
        {
            return progress;
        }

        return null;
    }

    private AchievementProgressData GetOrCreateProgressData(string achievementId)
    {
        if (progressLookup.TryGetValue(achievementId, out AchievementProgressData progress))
        {
            return progress;
        }

        progress = new AchievementProgressData
        {
            achievementId = achievementId,
            currentCount = 0,
            clearState = 0L
        };

        progressLookup[achievementId] = progress;
        if (saveData.achievements == null)
        {
            saveData.achievements = new List<AchievementProgressData>();
        }

        saveData.achievements.Add(progress);
        return progress;
    }

    public void Save()
    {
        try
        {
            if (saveData == null)
            {
                saveData = new AchievementSaveData();
            }

            if (saveData.achievements == null)
            {
                saveData.achievements = new List<AchievementProgressData>();
            }

            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception)
        {
        }
    }

    public void ResetAllProgress()
    {
        saveData = new AchievementSaveData();
        progressLookup.Clear();

        foreach (AchievementData achievement in allAchievements)
        {
            if (achievement == null || string.IsNullOrEmpty(achievement.id))
            {
                continue;
            }

            AchievementProgressData progress = new AchievementProgressData
            {
                achievementId = achievement.id,
                currentCount = 0,
                clearState = 0L
            };

            progressLookup[achievement.id] = progress;
            saveData.achievements.Add(progress);
        }

        Save();
    }

    [ContextMenu("Debug/Unlock Debug Achievement")]
    private void DebugUnlockAchievement()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (string.IsNullOrEmpty(debugAchievementId))
        {
            return;
        }

        bool result = UnlockAchievement(debugAchievementId);
    }

    [ContextMenu("Debug/Reset All Achievement Progress")]
    private void DebugResetAllProgress()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResetAllProgress();
    }

    private long GetCurrentClearStateStamp()
    {
        return long.Parse(DateTime.Now.ToString("yyyyMMddHHmm"));
    }
}
