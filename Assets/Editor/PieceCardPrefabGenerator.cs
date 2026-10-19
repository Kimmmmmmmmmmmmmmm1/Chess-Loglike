#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PieceCardPrefabGenerator
{
    private const string PrefabDir = "Assets/Prefabs/UI";
    private const string PieceCardPrefabPath = "Assets/Prefabs/UI/PieceCardTooltip.prefab";
    private const string KeywordCardPrefabPath = "Assets/Prefabs/UI/KeywordCardTooltip.prefab";

    private const string ResourcesDir = "Assets/Resources/UI";
    private const string PieceCardResourcesPrefabPath = "Assets/Resources/UI/PieceCardTooltip.prefab";
    private const string KeywordCardResourcesPrefabPath = "Assets/Resources/UI/KeywordCardTooltip.prefab";

    [InitializeOnLoadMethod]
    private static void CheckAndGenerateOnInit()
    {
        EditorApplication.delayCall += () =>
        {
            bool needPieceCard = !System.IO.File.Exists(PieceCardPrefabPath) || !System.IO.File.Exists(PieceCardResourcesPrefabPath);
            bool needKeywordCard = !System.IO.File.Exists(KeywordCardPrefabPath) || !System.IO.File.Exists(KeywordCardResourcesPrefabPath);

            if (needPieceCard) GeneratePieceCardPrefab();
            if (needKeywordCard) GenerateKeywordCardPrefab();
        };
    }

    [MenuItem("Tools/UI/Generate All Tooltip Card Prefabs")]
    public static void GenerateAllPrefabs()
    {
        EnsureDirectories();
        GeneratePieceCardPrefab();
        GenerateKeywordCardPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PieceCardPrefabGenerator] Successfully generated all tooltip card prefabs!");
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }

        if (!AssetDatabase.IsValidFolder(ResourcesDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        }
    }

    [MenuItem("Tools/UI/Generate Piece Card Tooltip Prefab")]
    public static void GeneratePieceCardPrefab()
    {
        EnsureDirectories();

        GameObject go = new GameObject("PieceCardTooltip", typeof(RectTransform));
        PieceCardTooltipView view = go.AddComponent<PieceCardTooltipView>();
        view.EnsureUIHierarchy();

        // 폰트: 네오둥근모(NeoDunggeunmoPro-Regular SDF) 적용
        TMPro.TMP_FontAsset neoFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF.asset");
        if (neoFont != null)
        {
            var tmps = go.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                t.font = neoFont;
            }
        }

        // 기본 상태 비활성화
        go.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(go, PieceCardPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(go, PieceCardResourcesPrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PieceCardPrefabGenerator] Successfully generated PieceCardTooltip at:\n- {PieceCardPrefabPath}\n- {PieceCardResourcesPrefabPath}");
    }

    [MenuItem("Tools/UI/Generate Keyword Card Tooltip Prefab")]
    public static void GenerateKeywordCardPrefab()
    {
        EnsureDirectories();

        GameObject go = new GameObject("KeywordCardTooltip", typeof(RectTransform));
        KeywordTooltipView view = go.AddComponent<KeywordTooltipView>();
        view.EnsureUIHierarchy();

        // 폰트: 네오둥근모(NeoDunggeunmoPro-Regular SDF) 적용
        TMPro.TMP_FontAsset neoFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF.asset");
        if (neoFont != null)
        {
            var tmps = go.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                t.font = neoFont;
            }
        }

        // 기본 상태 비활성화
        go.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(go, KeywordCardPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(go, KeywordCardResourcesPrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PieceCardPrefabGenerator] Successfully generated KeywordCardTooltip at:\n- {KeywordCardPrefabPath}\n- {KeywordCardResourcesPrefabPath}");
    }
}
#endif
