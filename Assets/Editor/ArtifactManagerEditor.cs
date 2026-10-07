#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ArtifactManagerEditor
{
    [MenuItem("Assets/Load All Artifacts to Manager")]
    public static void LoadAllArtifactsToManager()
    {
        // ArtifactManager 찾기
        ArtifactManager manager = Object.FindFirstObjectByType<ArtifactManager>();
        if (manager == null)
        {
            return;
        }

        // Assets/Data/Artifact 폴더에서 모든 ArtifactData 찾기
        string folderPath = "Assets/Data/Artifact";
        string[] guids = AssetDatabase.FindAssets("t:ArtifactData", new[] { folderPath });
        
        List<ArtifactData> allArtifacts = new List<ArtifactData>();
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ArtifactData artifact = AssetDatabase.LoadAssetAtPath<ArtifactData>(assetPath);
            if (artifact != null)
            {
                allArtifacts.Add(artifact);
            }
        }

        // 리플렉션으로 private 필드 설정
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty allArtifactsProperty = so.FindProperty("allArtifacts");
        
        allArtifactsProperty.ClearArray();
        for (int i = 0; i < allArtifacts.Count; i++)
        {
            allArtifactsProperty.InsertArrayElementAtIndex(i);
            allArtifactsProperty.GetArrayElementAtIndex(i).objectReferenceValue = allArtifacts[i];
        }
        
        so.ApplyModifiedProperties();
        
    }
}
#endif
