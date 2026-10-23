#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// 튜토리얼 다이얼로그 UI 프리팹을 자동으로 생성하고 갱신하는 에디터 툴입니다.
/// 반투명 검정색 박스와 흰색 텍스트, 역삼각형(▼) 마커로 구성된 프리팹을 생성합니다.
/// </summary>
//[InitializeOnLoad]
public static class TutorialPrefabGenerator
{
    private const string ResourcesDir = "Assets/Resources/UI";
    private const string ResourcesPrefabPath = "Assets/Resources/UI/TutorialDialog.prefab";
    private const string PrefabsDir = "Assets/Prefabs/UI";
    private const string PrefabsPrefabPath = "Assets/Prefabs/UI/TutorialDialog.prefab";

    //[InitializeOnLoadMethod]
    private static void CheckAndGenerateOnInit()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return;
            }

            GeneratePrefab();
        };
    }

    [MenuItem("Tools/UI/Generate Tutorial Dialog Prefab")]
    public static void GeneratePrefab()
    {
        EnsureDirectories();

        TutorialDialogUI dialogUI = TutorialDialogUI.CreateDefault(null);
        GameObject go = dialogUI.gameObject;
        go.name = "TutorialDialog";

        // 프로젝트 폰트 에셋 적용
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

        dialogUI.ResolveReferences();

        // 비활성화 기본 상태로 저장
        go.SetActive(false);

        // Resources 및 Prefabs 경로에 프리팹 에셋 저장
        PrefabUtility.SaveAsPrefabAsset(go, ResourcesPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(go, PrefabsPrefabPath);

        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TutorialPrefabGenerator] 성공적으로 튜토리얼 프리팹을 생성했습니다:\n- {ResourcesPrefabPath}\n- {PrefabsPrefabPath}");
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
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(PrefabsDir))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }
    }
}
#endif

