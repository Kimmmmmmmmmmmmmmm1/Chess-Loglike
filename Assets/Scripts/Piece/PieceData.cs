using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPieceData", menuName = "Chess/Piece Data")]
public class PieceData : ScriptableObject
{
    public string id;
    public PieceType pieceType;
    public string pieceName;
    public Sprite icon;
    [Range(1, 5)] public int rarity = 1;
    [Tooltip("기물 가치 점수 (전투 보상 계산, AI 가치 평가 등)")]
    public int score = 1;
    [TextArea(2, 5)] public string description;
    [TextArea(2, 5)] public string flavorText;

    public int Rarity => rarity > 0 ? rarity : PieceController.GetRarityForType(pieceType);

    public string GetTooltipTitle()
    {
        return !string.IsNullOrEmpty(pieceName) ? pieceName : pieceType.ToString();
    }

    public string GetTooltipDescription()
    {
        return description ?? string.Empty;
    }

    #region Static Registry & Score Lookup

    private static readonly Dictionary<PieceType, PieceData> pieceDataCache = new();
    private static bool isCacheInitialized = false;

    public static void ClearCache()
    {
        pieceDataCache.Clear();
        isCacheInitialized = false;
    }

    private static void EnsureCache()
    {
        if (isCacheInitialized) return;
        isCacheInitialized = true;
        pieceDataCache.Clear();

#if UNITY_EDITOR
        string[] searchFolders = { "Assets/Resources/Piece", "Assets/Data/Piece" };
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:PieceData", searchFolders);
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            PieceData data = UnityEditor.AssetDatabase.LoadAssetAtPath<PieceData>(path);
            if (data != null && !pieceDataCache.ContainsKey(data.pieceType))
            {
                pieceDataCache[data.pieceType] = data;
            }
        }
#endif

        PieceData[] loaded = Resources.LoadAll<PieceData>("Piece");
        if (loaded != null)
        {
            foreach (PieceData data in loaded)
            {
                if (data != null && !pieceDataCache.ContainsKey(data.pieceType))
                {
                    pieceDataCache[data.pieceType] = data;
                }
            }
        }
    }

    public static PieceData GetData(PieceType type)
    {
        EnsureCache();
        if (pieceDataCache.TryGetValue(type, out PieceData data))
        {
            return data;
        }

        PieceData singleLoad = Resources.Load<PieceData>($"Piece/Piece_{type}");
        if (singleLoad != null)
        {
            pieceDataCache[type] = singleLoad;
            return singleLoad;
        }

        return null;
    }

    public static int GetPieceScore(PieceType type)
    {
        PieceData data = GetData(type);
        if (data != null && data.score > 0)
        {
            return data.score;
        }
        return GetDefaultScore(type);
    }

    public static int GetDefaultScore(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => 1,
            PieceType.Knight => 3,
            PieceType.Bishop => 3,
            PieceType.Rook => 5,
            PieceType.Queen => 9,
            PieceType.King => 4,
            PieceType.Centaur => 5,
            PieceType.Archbishop => 6,
            PieceType.Chancellor => 7,
            PieceType.Amazon => 13,
            PieceType.Grasshopper => 3,
            PieceType.Cannon => 5,
            _ => 1
        };
    }

    #endregion
}

