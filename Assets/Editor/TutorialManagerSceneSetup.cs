#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// GameScene의 Managers 하위에 TutorialManager 오브젝트를 구성하는 에디터 툴입니다.
/// </summary>
[InitializeOnLoad]
public static class TutorialManagerSceneSetup
{
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";

    [InitializeOnLoadMethod]
    private static void OnInit()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return;
            }

            EnsureTutorialManagerInScene();
        };
    }

    [MenuItem("Tools/Scene/Setup TutorialManager in GameScene")]
    public static void EnsureTutorialManagerInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            return;
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        bool isGameSceneActive = activeScene.IsValid() && activeScene.path == GameScenePath;

        if (isGameSceneActive)
        {
            SetupInLoadedScene(activeScene);
        }
        else
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            if (scene.IsValid())
            {
                SetupInLoadedScene(scene);
            }
        }
    }

    private static void SetupInLoadedScene(UnityEngine.SceneManagement.Scene scene)
    {
        TutorialManager existing = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        GameObject managersRoot = GameObject.Find("Managers");

        if (existing != null)
        {
            if (managersRoot != null && existing.transform.parent != managersRoot.transform)
            {
                existing.transform.SetParent(managersRoot.transform, false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return;
        }

        GameObject tmObj = new GameObject("TutorialManager");
        if (managersRoot != null)
        {
            tmObj.transform.SetParent(managersRoot.transform, false);
        }

        TutorialManager tm = tmObj.AddComponent<TutorialManager>();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/TutorialDialog.prefab");
        if (prefab != null)
        {
            SerializedObject so = new SerializedObject(tm);
            SerializedProperty prop = so.FindProperty("tutorialPrefab");
            if (prop != null)
            {
                prop.objectReferenceValue = prefab;
                so.ApplyModifiedProperties();
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TutorialManagerSceneSetup] GameScene의 Managers 자식으로 TutorialManager를 성공적으로 생성하고 씬을 저장했습니다.");
    }
}
#endif

