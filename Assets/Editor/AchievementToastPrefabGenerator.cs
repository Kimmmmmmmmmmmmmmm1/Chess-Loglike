#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// 업적 달성 토스트 알림 UI 프리팹을 자동으로 생성하고 관리하는 에디터 툴입니다.
/// 런타임 Resources.Load 경로인 Assets/Resources/UI/AchievementToast.prefab을 단일 관리합니다.
/// </summary>
[InitializeOnLoad]
public static class AchievementToastPrefabGenerator
{
    private const string ResourcesDir = "Assets/Resources/UI";
    private const string ResourcesPrefabPath = "Assets/Resources/UI/AchievementToast.prefab";
    private const string ObsoletePrefabPath = "Assets/Prefabs/UI/AchievementToast.prefab";

    [InitializeOnLoadMethod]
    private static void CheckAndGenerateOnInit()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return;
            }

            // 불필요한 중복 프리팹이 남아있다면 정리
            CleanupObsoletePrefab();

            bool needPrefab = !File.Exists(ResourcesPrefabPath);
            if (needPrefab)
            {
                GeneratePrefab();
            }
        };
    }

    [MenuItem("Tools/UI/Generate Achievement Toast Prefab")]
    public static void GeneratePrefab()
    {
        EnsureDirectories();
        CleanupObsoletePrefab();

        AchievementToastUI toastUI = AchievementToastUI.CreateDefault(null);
        GameObject go = toastUI.gameObject;
        go.name = "AchievementToast";

        // 프로젝트에 구비된 폰트 에셋 적용
        TMP_FontAsset targetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Pretendard-Medium SDF.asset");
        if (targetFont == null)
        {
            targetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF.asset");
        }
        if (targetFont == null)
        {
            targetFont = TMP_Settings.defaultFontAsset;
        }

        if (targetFont != null)
        {
            var tmps = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                t.font = targetFont;
            }
        }

        // 기본 상태 비활성화
        go.SetActive(false);

        // Resources/UI 경로에만 단일 저장하여 런타임 Resources.Load로 사용
        PrefabUtility.SaveAsPrefabAsset(go, ResourcesPrefabPath);

        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AchievementToastPrefabGenerator] 성공적으로 업적 토스트 프리팹을 생성했습니다:\n- {ResourcesPrefabPath}");
    }

    private static void CleanupObsoletePrefab()
    {
        if (File.Exists(ObsoletePrefabPath))
        {
            AssetDatabase.DeleteAsset(ObsoletePrefabPath);
        }
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        }
    }
}
#endif
