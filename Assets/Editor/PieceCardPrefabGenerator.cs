#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PieceCardPrefabGenerator
{
    private const string PrefabDir = "Assets/Prefabs/UI";
    private const string PrefabPath = "Assets/Prefabs/UI/PieceCardTooltip.prefab";
    private const string ResourcesDir = "Assets/Resources/UI";
    private const string ResourcesPrefabPath = "Assets/Resources/UI/PieceCardTooltip.prefab";

    [InitializeOnLoadMethod]
    private static void CheckAndGenerateOnInit()
    {
        EditorApplication.delayCall += () =>
        {
            if (!System.IO.File.Exists(PrefabPath) || !System.IO.File.Exists(ResourcesPrefabPath))
            {
                GeneratePrefab();
            }
        };
    }

    [MenuItem("Tools/UI/Generate Piece Card Tooltip Prefab")]
    public static void GeneratePrefab()
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

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(go, ResourcesPrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PieceCardPrefabGenerator] Successfully generated prefabs at:\n- {PrefabPath}\n- {ResourcesPrefabPath}");
    }
}
#endif
