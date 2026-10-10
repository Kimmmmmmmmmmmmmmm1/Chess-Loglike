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
    [SerializeField] private List<PieceData> allPieces = new List<PieceData>();
    [SerializeField] private string artifactEditorFolderPath = "Assets/Data/Artifact";
    [SerializeField] private string sealEditorFolderPath = "Assets/Data/Seal";
    [SerializeField] private string pieceEditorFolderPath = "Assets/Data/Piece";
    [SerializeField] private string artifactRuntimeResourcesPath = "Artifact";
    [SerializeField] private string sealRuntimeResourcesPath = "Seal";
    [SerializeField] private string pieceRuntimeResourcesPath = "Piece";

    [Header("Save")]
    [SerializeField] private string saveFileName = "collection_save.json";

    public event Action OnCollectionChanged;

    private CollectionSaveData saveData = new CollectionSaveData();
    private readonly HashSet<string> unlockedArtifactLookup = new HashSet<string>();
    private readonly HashSet<string> unlockedSealLookup = new HashSet<string>();
    private readonly HashSet<string> unlockedPieceLookup = new HashSet<string>();
    private readonly HashSet<string> seenArtifactLookup = new HashSet<string>();
    private readonly HashSet<string> seenSealLookup = new HashSet<string>();
    private readonly HashSet<string> seenPieceLookup = new HashSet<string>();

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

        CollectionManager existing = FindFirstObjectByType<CollectionManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            if (!existing.gameObject.activeSelf)
            {
                existing.gameObject.SetActive(true);
            }
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
            .OrderByDescending(artifact => (int)GetArtifactState(artifact))
            .ThenBy(artifact => artifact.rarity)
            .ThenBy(artifact => artifact.artifactName)
            .ToList();
    }

    public IReadOnlyList<SealData> GetAllSeals()
    {
        return allSeals
            .Where(seal => seal != null && !string.IsNullOrEmpty(GetSealId(seal)))
            .OrderByDescending(seal => (int)GetSealState(seal))
            .ThenBy(seal => seal.rarity)
            .ThenBy(seal => seal.sealName)
            .ToList();
    }

    public IReadOnlyList<PieceData> GetAllPieces()
    {
        return allPieces
            .Where(piece => piece != null && !string.IsNullOrEmpty(GetPieceId(piece)))
            .OrderByDescending(piece => (int)GetPieceState(piece))
            .ThenBy(piece => GetPieceSortOrder(piece.pieceType))
            .ThenBy(piece => piece.pieceName)
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
        seenArtifactLookup.Add(artifactId);
        saveData.unlockedArtifactIDs.Add(artifactId);
        if (!saveData.seenArtifactIDs.Contains(artifactId))
        {
            saveData.seenArtifactIDs.Add(artifactId);
        }

        TryAddCollectionAchievementProgress(AchievementIds.CollectFirstArtifact);
        TryAddCollectionAchievementProgress(AchievementIds.CollectArtifact3);
        TryAddCollectionAchievementProgress(AchievementIds.CollectArtifact5);
        TryAddCollectionAchievementProgress(AchievementIds.CollectArtifact10);
        TryAddCollectionAchievementProgress(AchievementIds.CollectAllArtifacts);
        Save();
        OnCollectionChanged?.Invoke();
        return true;
    }

    public bool RecordArtifactSeen(ArtifactData artifact)
    {
        string artifactId = GetArtifactId(artifact);
        if (string.IsNullOrEmpty(artifactId) || seenArtifactLookup.Contains(artifactId))
        {
            return false;
        }

        seenArtifactLookup.Add(artifactId);
        saveData.seenArtifactIDs.Add(artifactId);
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
        seenSealLookup.Add(sealId);
        saveData.unlockedSealIDs.Add(sealId);
        if (!saveData.seenSealIDs.Contains(sealId))
        {
            saveData.seenSealIDs.Add(sealId);
        }

        TryAddCollectionAchievementProgress(AchievementIds.CollectFirstSeal);
        TryAddCollectionAchievementProgress(AchievementIds.CollectSeal3);
        TryAddCollectionAchievementProgress(AchievementIds.CollectSeal5);
        Save();
        OnCollectionChanged?.Invoke();
        return true;
    }

    public bool RecordSealSeen(SealData seal)
    {
        string sealId = GetSealId(seal);
        if (string.IsNullOrEmpty(sealId) || seenSealLookup.Contains(sealId))
        {
            return false;
        }

        seenSealLookup.Add(sealId);
        saveData.seenSealIDs.Add(sealId);
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
            seenSealLookup.Add(sealId);
            saveData.unlockedSealIDs.Add(sealId);
            if (!saveData.seenSealIDs.Contains(sealId))
            {
                saveData.seenSealIDs.Add(sealId);
            }

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

    public bool RecordSealsSeen(IEnumerable<SealData> seals)
    {
        if (seals == null)
        {
            return false;
        }

        bool changed = false;
        foreach (SealData seal in seals)
        {
            string sealId = GetSealId(seal);
            if (string.IsNullOrEmpty(sealId) || seenSealLookup.Contains(sealId))
            {
                continue;
            }

            seenSealLookup.Add(sealId);
            saveData.seenSealIDs.Add(sealId);
            changed = true;
        }

        if (changed)
        {
            Save();
            OnCollectionChanged?.Invoke();
        }

        return changed;
    }

    public bool RecordPiece(PieceData piece)
    {
        return RecordPiece(GetPieceId(piece));
    }

    public bool RecordPiece(PieceType pieceType)
    {
        return RecordPiece(GetPieceId(pieceType));
    }

    public bool RecordPiece(string pieceId)
    {
        if (string.IsNullOrEmpty(pieceId) || unlockedPieceLookup.Contains(pieceId))
        {
            return false;
        }

        unlockedPieceLookup.Add(pieceId);
        seenPieceLookup.Add(pieceId);
        saveData.unlockedPieceIDs.Add(pieceId);
        if (!saveData.seenPieceIDs.Contains(pieceId))
        {
            saveData.seenPieceIDs.Add(pieceId);
        }

        Save();
        OnCollectionChanged?.Invoke();
        return true;
    }

    public bool RecordPieceSeen(PieceData piece)
    {
        return RecordPieceSeen(GetPieceId(piece));
    }

    public bool RecordPieceSeen(PieceType pieceType)
    {
        return RecordPieceSeen(GetPieceId(pieceType));
    }

    public bool RecordPieceSeen(string pieceId)
    {
        if (string.IsNullOrEmpty(pieceId) || seenPieceLookup.Contains(pieceId))
        {
            return false;
        }

        seenPieceLookup.Add(pieceId);
        saveData.seenPieceIDs.Add(pieceId);
        Save();
        OnCollectionChanged?.Invoke();
        return true;
    }

    public CollectionDiscoveryState GetArtifactState(ArtifactData artifact)
    {
        string artifactId = GetArtifactId(artifact);
        if (string.IsNullOrEmpty(artifactId))
        {
            return CollectionDiscoveryState.Unseen;
        }

        if (unlockedArtifactLookup.Contains(artifactId))
        {
            return CollectionDiscoveryState.Acquired;
        }

        if (seenArtifactLookup.Contains(artifactId))
        {
            return CollectionDiscoveryState.Seen;
        }

        return CollectionDiscoveryState.Unseen;
    }

    public CollectionDiscoveryState GetSealState(SealData seal)
    {
        string sealId = GetSealId(seal);
        if (string.IsNullOrEmpty(sealId))
        {
            return CollectionDiscoveryState.Unseen;
        }

        if (unlockedSealLookup.Contains(sealId))
        {
            return CollectionDiscoveryState.Acquired;
        }

        if (seenSealLookup.Contains(sealId))
        {
            return CollectionDiscoveryState.Seen;
        }

        return CollectionDiscoveryState.Unseen;
    }

    public CollectionDiscoveryState GetPieceState(PieceData piece)
    {
        return GetPieceState(GetPieceId(piece));
    }

    public CollectionDiscoveryState GetPieceState(PieceType pieceType)
    {
        return GetPieceState(GetPieceId(pieceType));
    }

    public CollectionDiscoveryState GetPieceState(string pieceId)
    {
        if (string.IsNullOrEmpty(pieceId))
        {
            return CollectionDiscoveryState.Unseen;
        }

        if (unlockedPieceLookup.Contains(pieceId))
        {
            return CollectionDiscoveryState.Acquired;
        }

        if (seenPieceLookup.Contains(pieceId))
        {
            return CollectionDiscoveryState.Seen;
        }

        return CollectionDiscoveryState.Unseen;
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

    public bool IsPieceUnlocked(PieceData piece)
    {
        string pieceId = GetPieceId(piece);
        return !string.IsNullOrEmpty(pieceId) && unlockedPieceLookup.Contains(pieceId);
    }

    public bool IsPieceUnlocked(PieceType pieceType)
    {
        string pieceId = GetPieceId(pieceType);
        return !string.IsNullOrEmpty(pieceId) && unlockedPieceLookup.Contains(pieceId);
    }

    public bool IsArtifactSeen(ArtifactData artifact)
    {
        return GetArtifactState(artifact) >= CollectionDiscoveryState.Seen;
    }

    public bool IsSealSeen(SealData seal)
    {
        return GetSealState(seal) >= CollectionDiscoveryState.Seen;
    }

    public bool IsPieceSeen(PieceData piece)
    {
        return GetPieceState(piece) >= CollectionDiscoveryState.Seen;
    }

    public bool IsPieceSeen(PieceType pieceType)
    {
        return GetPieceState(pieceType) >= CollectionDiscoveryState.Seen;
    }

    public int GetUnlockedArtifactCount()
    {
        return GetAllArtifacts().Count(IsArtifactUnlocked);
    }

    public int GetUnlockedSealCount()
    {
        return GetAllSeals().Count(IsSealUnlocked);
    }

    public int GetUnlockedPieceCount()
    {
        return GetAllPieces().Count(IsPieceUnlocked);
    }

    public int GetTotalArtifactCount()
    {
        return GetAllArtifacts().Count;
    }

    public int GetTotalSealCount()
    {
        return GetAllSeals().Count;
    }

    public int GetTotalPieceCount()
    {
        return GetAllPieces().Count;
    }

    public int GetUnlockedTotalCount()
    {
        return GetUnlockedArtifactCount() + GetUnlockedSealCount() + GetUnlockedPieceCount();
    }

    public int GetTotalCount()
    {
        return GetTotalArtifactCount() + GetTotalSealCount() + GetTotalPieceCount();
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

    public static string GetPieceId(PieceData piece)
    {
        if (piece == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrEmpty(piece.id) ? piece.id : piece.pieceType.ToString();
    }

    public static string GetPieceId(PieceType type)
    {
        return type.ToString();
    }

    private static int GetPieceSortOrder(PieceType type)
    {
        return type switch
        {
            PieceType.King => 0,
            PieceType.Queen => 1,
            PieceType.Rook => 2,
            PieceType.Bishop => 3,
            PieceType.Knight => 4,
            PieceType.Pawn => 5,
            _ => 99
        };
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

        if (allPieces == null)
        {
            allPieces = new List<PieceData>();
        }

        allArtifacts.Clear();
        allSeals.Clear();
        allPieces.Clear();

        HashSet<string> addedArtifactIds = new HashSet<string>();
        HashSet<string> addedSealIds = new HashSet<string>();
        HashSet<string> addedPieceIds = new HashSet<string>();

        void TryAddArtifact(ArtifactData artifact)
        {
            string id = GetArtifactId(artifact);
            if (artifact != null && !string.IsNullOrEmpty(id) && addedArtifactIds.Add(id))
            {
                allArtifacts.Add(artifact);
            }
        }

        void TryAddSeal(SealData seal)
        {
            string id = GetSealId(seal);
            if (seal != null && !string.IsNullOrEmpty(id) && addedSealIds.Add(id))
            {
                allSeals.Add(seal);
            }
        }

        void TryAddPiece(PieceData piece)
        {
            string id = GetPieceId(piece);
            if (piece != null && !string.IsNullOrEmpty(id) && addedPieceIds.Add(id))
            {
                allPieces.Add(piece);
            }
        }

#if UNITY_EDITOR
        List<string> artifactFolders = new List<string>();
        if (!string.IsNullOrEmpty(artifactEditorFolderPath) && UnityEditor.AssetDatabase.IsValidFolder(artifactEditorFolderPath))
        {
            artifactFolders.Add(artifactEditorFolderPath);
        }
        if (!artifactFolders.Contains("Assets/Data/Artifact") && UnityEditor.AssetDatabase.IsValidFolder("Assets/Data/Artifact"))
        {
            artifactFolders.Add("Assets/Data/Artifact");
        }
        if (!artifactFolders.Contains("Assets/Resources/Artifact") && UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Artifact"))
        {
            artifactFolders.Add("Assets/Resources/Artifact");
        }

        if (artifactFolders.Count > 0)
        {
            string[] artifactGuids = UnityEditor.AssetDatabase.FindAssets("t:ArtifactData", artifactFolders.ToArray());
            foreach (string guid in artifactGuids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ArtifactData artifact = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtifactData>(assetPath);
                TryAddArtifact(artifact);
            }
        }

        List<string> sealFolders = new List<string>();
        if (!string.IsNullOrEmpty(sealEditorFolderPath) && UnityEditor.AssetDatabase.IsValidFolder(sealEditorFolderPath))
        {
            sealFolders.Add(sealEditorFolderPath);
        }
        if (!sealFolders.Contains("Assets/Data/Seal") && UnityEditor.AssetDatabase.IsValidFolder("Assets/Data/Seal"))
        {
            sealFolders.Add("Assets/Data/Seal");
        }
        if (!sealFolders.Contains("Assets/Resources/Seal") && UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Seal"))
        {
            sealFolders.Add("Assets/Resources/Seal");
        }

        if (sealFolders.Count > 0)
        {
            string[] sealGuids = UnityEditor.AssetDatabase.FindAssets("t:SealData", sealFolders.ToArray());
            foreach (string guid in sealGuids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                SealData seal = UnityEditor.AssetDatabase.LoadAssetAtPath<SealData>(assetPath);
                TryAddSeal(seal);
            }
        }

        List<string> pieceFolders = new List<string>();
        if (!string.IsNullOrEmpty(pieceEditorFolderPath) && UnityEditor.AssetDatabase.IsValidFolder(pieceEditorFolderPath))
        {
            pieceFolders.Add(pieceEditorFolderPath);
        }
        if (!pieceFolders.Contains("Assets/Data/Piece") && UnityEditor.AssetDatabase.IsValidFolder("Assets/Data/Piece"))
        {
            pieceFolders.Add("Assets/Data/Piece");
        }
        if (!pieceFolders.Contains("Assets/Resources/Piece") && UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Piece"))
        {
            pieceFolders.Add("Assets/Resources/Piece");
        }

        if (pieceFolders.Count > 0)
        {
            string[] pieceGuids = UnityEditor.AssetDatabase.FindAssets("t:PieceData", pieceFolders.ToArray());
            foreach (string guid in pieceGuids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                PieceData piece = UnityEditor.AssetDatabase.LoadAssetAtPath<PieceData>(assetPath);
                TryAddPiece(piece);
            }
        }
#endif

        ArtifactData[] runtimeArtifacts = Resources.LoadAll<ArtifactData>(artifactRuntimeResourcesPath);
        if (runtimeArtifacts != null)
        {
            for (int i = 0; i < runtimeArtifacts.Length; i++)
            {
                TryAddArtifact(runtimeArtifacts[i]);
            }
        }

        SealData[] runtimeSeals = Resources.LoadAll<SealData>(sealRuntimeResourcesPath);
        if (runtimeSeals != null)
        {
            for (int i = 0; i < runtimeSeals.Length; i++)
            {
                TryAddSeal(runtimeSeals[i]);
            }
        }

        PieceData[] runtimePieces = Resources.LoadAll<PieceData>(pieceRuntimeResourcesPath);
        if (runtimePieces != null)
        {
            for (int i = 0; i < runtimePieces.Length; i++)
            {
                TryAddPiece(runtimePieces[i]);
            }
        }

        EnsureDefaultPieces();
    }

    private void EnsureDefaultPieces()
    {
        PieceType[] standardTypes = new[]
        {
            PieceType.King,
            PieceType.Queen,
            PieceType.Rook,
            PieceType.Bishop,
            PieceType.Knight,
            PieceType.Pawn
        };

        foreach (PieceType type in standardTypes)
        {
            string id = type.ToString();
            PieceData existing = allPieces.FirstOrDefault(p => p != null && (p.id == id || p.pieceType == type));
            if (existing == null)
            {
                PieceData piece = ScriptableObject.CreateInstance<PieceData>();
                piece.id = id;
                piece.pieceType = type;
                piece.pieceName = type switch
                {
                    PieceType.King => "킹",
                    PieceType.Queen => "퀸",
                    PieceType.Rook => "룩",
                    PieceType.Bishop => "비숍",
                    PieceType.Knight => "나이트",
                    PieceType.Pawn => "폰",
                    _ => type.ToString()
                };
                piece.description = type switch
                {
                    PieceType.King => "모든 방향으로 1칸씩 이동할 수 있습니다.\n킹이 파괴되면 게임에서 패배합니다.",
                    PieceType.Queen => "가로, 세로, 대각선 모든 방향으로 거리 제한 없이 이동할 수 있습니다.",
                    PieceType.Rook => "가로와 세로 방향으로 거리 제한 없이 이동할 수 있습니다.",
                    PieceType.Bishop => "대각선 방향으로 거리 제한 없이 이동할 수 있습니다.",
                    PieceType.Knight => "L자 모양으로 이동합니다(2칸 직진 후 1칸 옆).\n경로상의 장애물을 무시하고 뛰어넘을 수 있습니다.",
                    PieceType.Pawn => "앞으로 1칸 전진(첫 이동 시 2칸 가능)하며, 대각선 앞 1칸의 적을 포획합니다.\n적진 끝에 도달하면 승급합니다.",
                    _ => string.Empty
                };
                piece.flavorText = type switch
                {
                    PieceType.King => "체스 진영의 심장이자 최우선 보호 대상인 왕.",
                    PieceType.Queen => "전 방향으로 이동할 수 있는 체스판 위 최강의 기물.",
                    PieceType.Rook => "직선 경로를 장악하는 웅장한 성채.",
                    PieceType.Bishop => "대각선을 가로지르는 성직자.",
                    PieceType.Knight => "장애물을 뛰어넘는 기동력의 기사.",
                    PieceType.Pawn => "가장 앞장서 전진하며, 끝에 도달하면 승급을 이뤄내는 보병.",
                    _ => string.Empty
                };
                piece.icon = GetPieceSprite(type);
                allPieces.Add(piece);
            }
            else if (existing.icon == null)
            {
                existing.icon = GetPieceSprite(type);
            }
        }
    }

    private Sprite GetPieceSprite(PieceType type)
    {
        if (PieceManager.Instance != null)
        {
            return PieceManager.Instance.GetSpriteFor(type);
        }

#if UNITY_EDITOR
        Sprite[] sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Game/Piece-Sheet.png").OfType<Sprite>().ToArray();
        string targetSpriteName = type switch
        {
            PieceType.King => "Piece-Sheet_1",
            PieceType.Queen => "Piece-Sheet_3",
            PieceType.Rook => "Piece-Sheet_2",
            PieceType.Bishop => "Piece-Sheet_5",
            PieceType.Knight => "Piece-Sheet_4",
            PieceType.Pawn => "Piece-Sheet_6",
            _ => null
        };
        if (targetSpriteName != null)
        {
            Sprite found = sprites.FirstOrDefault(s => s.name == targetSpriteName);
            if (found != null)
            {
                return found;
            }
        }
#endif
        return null;
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
        unlockedPieceLookup.Clear();
        seenArtifactLookup.Clear();
        seenSealLookup.Clear();
        seenPieceLookup.Clear();

        foreach (string artifactId in saveData.unlockedArtifactIDs)
        {
            if (!string.IsNullOrEmpty(artifactId))
            {
                unlockedArtifactLookup.Add(artifactId);
                seenArtifactLookup.Add(artifactId);
            }
        }

        foreach (string sealId in saveData.unlockedSealIDs)
        {
            if (!string.IsNullOrEmpty(sealId))
            {
                unlockedSealLookup.Add(sealId);
                seenSealLookup.Add(sealId);
            }
        }

        foreach (string pieceId in saveData.unlockedPieceIDs)
        {
            if (!string.IsNullOrEmpty(pieceId))
            {
                unlockedPieceLookup.Add(pieceId);
                seenPieceLookup.Add(pieceId);
            }
        }

        foreach (string artifactId in saveData.seenArtifactIDs)
        {
            if (!string.IsNullOrEmpty(artifactId))
            {
                seenArtifactLookup.Add(artifactId);
            }
        }

        foreach (string sealId in saveData.seenSealIDs)
        {
            if (!string.IsNullOrEmpty(sealId))
            {
                seenSealLookup.Add(sealId);
            }
        }

        foreach (string pieceId in saveData.seenPieceIDs)
        {
            if (!string.IsNullOrEmpty(pieceId))
            {
                seenPieceLookup.Add(pieceId);
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

        saveData.unlockedPieceIDs = saveData.unlockedPieceIDs?
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList() ?? new List<string>();

        saveData.seenArtifactIDs = saveData.seenArtifactIDs?
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList() ?? new List<string>();

        saveData.seenSealIDs = saveData.seenSealIDs?
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList() ?? new List<string>();

        saveData.seenPieceIDs = saveData.seenPieceIDs?
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList() ?? new List<string>();

        // Ensure acquired items are also marked as seen
        foreach (string id in saveData.unlockedArtifactIDs)
        {
            if (!saveData.seenArtifactIDs.Contains(id))
            {
                saveData.seenArtifactIDs.Add(id);
            }
        }
        foreach (string id in saveData.unlockedSealIDs)
        {
            if (!saveData.seenSealIDs.Contains(id))
            {
                saveData.seenSealIDs.Add(id);
            }
        }
        foreach (string id in saveData.unlockedPieceIDs)
        {
            if (!saveData.seenPieceIDs.Contains(id))
            {
                saveData.seenPieceIDs.Add(id);
            }
        }

        // 기존 플레이 기록이 있으나 기물 컬렉션 데이터가 처음 추가된 경우 기본 기물(킹, 폰, 나이트)을 해금
        if (saveData.unlockedPieceIDs.Count == 0 &&
            (saveData.unlockedArtifactIDs.Count > 0 || saveData.unlockedSealIDs.Count > 0))
        {
            string kingId = GetPieceId(PieceType.King);
            string pawnId = GetPieceId(PieceType.Pawn);
            string knightId = GetPieceId(PieceType.Knight);

            saveData.unlockedPieceIDs.Add(kingId);
            saveData.unlockedPieceIDs.Add(pawnId);
            saveData.unlockedPieceIDs.Add(knightId);

            if (!saveData.seenPieceIDs.Contains(kingId)) saveData.seenPieceIDs.Add(kingId);
            if (!saveData.seenPieceIDs.Contains(pawnId)) saveData.seenPieceIDs.Add(pawnId);
            if (!saveData.seenPieceIDs.Contains(knightId)) saveData.seenPieceIDs.Add(knightId);
        }
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

        AchievementManager manager = AchievementManager.Instance != null
            ? AchievementManager.Instance
            : AchievementManager.EnsureInstance();
        manager?.AddProgress(achievementId, amount);
    }
}
