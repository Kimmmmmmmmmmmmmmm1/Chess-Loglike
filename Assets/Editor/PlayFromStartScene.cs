using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayFromStartScene
{
    private const string MenuPath = "Tools/Always Play From Start Scene";
    // 프로젝트의 실제 시작 씬 경로로 수정하세요.
    private const string StartScenePath = "Assets/Scenes/TitleScene.unity";

    static PlayFromStartScene()
    {
        EditorApplication.delayCall += ApplySceneSetting;
    }

    [MenuItem(MenuPath)]
    private static void ToggleMenu()
    {
        bool isEnabled = EditorPrefs.GetBool(MenuPath, false);
        EditorPrefs.SetBool(MenuPath, !isEnabled);
        ApplySceneSetting();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleMenuValidate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(MenuPath, false));
        return true;
    }

    private static void ApplySceneSetting()
    {
        bool isEnabled = EditorPrefs.GetBool(MenuPath, false);
        if (isEnabled)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScenePath);
            EditorSceneManager.playModeStartScene = sceneAsset;
        }
        else
        {
            EditorSceneManager.playModeStartScene = null;
        }
    }
}