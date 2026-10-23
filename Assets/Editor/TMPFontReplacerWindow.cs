#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TMPFontReplacerWindow : EditorWindow
{
    private TMP_FontAsset sourceFont;
    private TMP_FontAsset targetFont;
    private bool changeOnlySourceFont = true; // false면 소스 폰트와 무관하게 모든 TMP_Text 교체
    private bool includePrefabs = true;
    private bool includeScenes = true;
    private bool updateTMPSettings = true;

    private const string DEFAULT_SOURCE_PATH = "Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF.asset";
    private const string DEFAULT_TARGET_PATH = "Assets/TextMesh Pro/Fonts/Pretendard-Medium SDF.asset";
    private const string TTF_PATH = "Assets/TextMesh Pro/Fonts/Pretendard-Medium.ttf";

    [MenuItem("Tools/TextMeshPro/TMP Font Replacer (폰트 일괄 교체)")]
    public static void Open()
    {
        GetWindow<TMPFontReplacerWindow>("TMP 폰트 교체 도구");
    }

    private void OnEnable()
    {
        if (sourceFont == null)
        {
            sourceFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DEFAULT_SOURCE_PATH);
        }

        if (targetFont == null)
        {
            targetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DEFAULT_TARGET_PATH);
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("TMP 폰트 일괄 교체 도구", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("프로젝트 내의 프리팹, 씬, TMP Settings의 폰트를 일괄 교체합니다.", MessageType.Info);
        GUILayout.Space(10);

        sourceFont = (TMP_FontAsset)EditorGUILayout.ObjectField("기존 폰트 (Source)", sourceFont, typeof(TMP_FontAsset), false);
        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("변경할 폰트 (Target)", targetFont, typeof(TMP_FontAsset), false);

        if (targetFont == null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Pretendard-Medium SDF 폰트 에셋이 아직 생성되지 않았습니다.\n아래 버튼을 눌러 에셋을 생성하거나, Project 창의 Pretendard-Medium.ttf 우클릭 > Create > TextMeshPro > Font Asset을 눌러주세요.", MessageType.Warning);

            if (GUILayout.Button("Pretendard-Medium SDF 에셋 생성 시도", GUILayout.Height(30)))
            {
                CreatePretendardFontAsset();
            }
        }

        GUILayout.Space(15);
        EditorGUILayout.LabelField("교체 옵션", EditorStyles.boldLabel);
        changeOnlySourceFont = EditorGUILayout.Toggle("기존 폰트와 일치하는 것만 교체", changeOnlySourceFont);
        includePrefabs = EditorGUILayout.Toggle("프리팹 포함 (Assets/Prefabs, Resources)", includePrefabs);
        includeScenes = EditorGUILayout.Toggle("모든 씬 포함 (Assets/Scenes)", includeScenes);
        updateTMPSettings = EditorGUILayout.Toggle("TMP Settings 기본 폰트 갱신", updateTMPSettings);

        GUILayout.Space(20);

        EditorGUI.BeginDisabledGroup(targetFont == null);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("프로젝트 전체 폰트 일괄 교체 실행", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("폰트 일괄 교체 확인",
                $"정말로 폰트를 {(targetFont != null ? targetFont.name : "null")}(으)로 교체하시겠습니까?\n작업 전 Git 변경사항을 커밋해두는 것을 권장합니다.", "교체 진행", "취소"))
            {
                ExecuteReplace();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();
    }

    private void CreatePretendardFontAsset()
    {
        Object ttfObj = AssetDatabase.LoadAssetAtPath<Object>(TTF_PATH);
        if (ttfObj == null)
        {
            EditorUtility.DisplayDialog("오류", $"경로에 폰트 파일이 없습니다:\n{TTF_PATH}", "확인");
            return;
        }

        Selection.activeObject = ttfObj;
        EditorGUIUtility.PingObject(ttfObj);

        // 유니티 내장 메뉴 명령 시도
        bool executed = EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Font Asset");
        if (!executed)
        {
            Font font = ttfObj as Font;
            if (font != null)
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
                if (asset != null)
                {
                    AssetDatabase.CreateAsset(asset, DEFAULT_TARGET_PATH);
                    if (asset.material != null)
                        AssetDatabase.AddObjectToAsset(asset.material, asset);

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    targetFont = asset;
                    EditorUtility.DisplayDialog("성공", "Pretendard-Medium SDF 에셋이 생성되었습니다!", "확인");
                    return;
                }
            }

            EditorUtility.DisplayDialog("안내", "유니티 Project 창에서 'Pretendard-Medium.ttf' 파일을 우클릭하고\n[Create > TextMeshPro > Font Asset]을 클릭해주세요.", "확인");
        }
        else
        {
            AssetDatabase.Refresh();
            targetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DEFAULT_TARGET_PATH);
        }
    }

    private void ExecuteReplace()
    {
        int totalReplacedCount = 0;
        int prefabCount = 0;
        int sceneCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            // 1. TMP Settings 기본 폰트 변경
            if (updateTMPSettings && TMP_Settings.instance != null)
            {
                TMP_Settings.defaultFontAsset = targetFont;
                EditorUtility.SetDirty(TMP_Settings.instance);
                Debug.Log("[FontReplacer] TMP Settings 기본 폰트를 교체했습니다.");
            }

            // 2. Prefabs 교체
            if (includePrefabs)
            {
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
                for (int i = 0; i < prefabGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    EditorUtility.DisplayProgressBar("프리팹 폰트 교체 중...", path, (float)i / prefabGuids.Length);

                    GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                    if (prefab == null) continue;

                    TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);
                    bool modified = false;

                    foreach (var text in texts)
                    {
                        if (!changeOnlySourceFont || text.font == sourceFont)
                        {
                            Undo.RecordObject(text, "Replace Font");
                            text.font = targetFont;
                            text.fontSharedMaterial = targetFont.material;
                            EditorUtility.SetDirty(text);
                            modified = true;
                            totalReplacedCount++;
                        }
                    }

                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefab, path);
                        prefabCount++;
                    }

                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        // 3. Scenes 교체
        if (includeScenes)
        {
            string currentScenePath = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }

            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                EditorUtility.DisplayProgressBar("씬 폰트 교체 중...", path, (float)i / sceneGuids.Length);

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                bool modified = false;
                foreach (var text in texts)
                {
                    if (text.gameObject.scene != scene) continue;

                    if (!changeOnlySourceFont || text.font == sourceFont)
                    {
                        Undo.RecordObject(text, "Replace Font");
                        text.font = targetFont;
                        text.fontSharedMaterial = targetFont.material;
                        EditorUtility.SetDirty(text);
                        modified = true;
                        totalReplacedCount++;
                    }
                }

                if (modified)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    sceneCount++;
                }
            }

            EditorUtility.ClearProgressBar();

            // 작업 전 활성화되어 있던 씬으로 복귀
            if (!string.IsNullOrEmpty(currentScenePath) && File.Exists(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = $"폰트 일괄 교체 완료!\n- 교체된 TMP_Text 개수: {totalReplacedCount}개\n- 수정된 프리팹: {prefabCount}개\n- 수정된 씬: {sceneCount}개";
        Debug.Log($"[FontReplacer] {summary}");
        EditorUtility.DisplayDialog("교체 완료", summary, "확인");
    }
}
#endif

