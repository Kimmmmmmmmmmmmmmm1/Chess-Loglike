#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CheatManager))]
public class CheatManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CheatManager cheat = (CheatManager)target;

        // 1. Gold
        EditorGUILayout.LabelField("1. Gold", EditorStyles.boldLabel);
        cheat.infiniteGold = EditorGUILayout.Toggle("무한 골드 고정 (99,999)", cheat.infiniteGold);
        cheat.addGoldAmount = EditorGUILayout.IntField("지급할 골드 액수", cheat.addGoldAmount);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("골드 지급", GUILayout.Height(24)))
        {
            cheat.GiveGold();
        }
        if (GUILayout.Button(cheat.infiniteGold ? "무한 골드 끄기" : "무한 골드 켜기", GUILayout.Height(24)))
        {
            cheat.ToggleInfiniteGold();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // 2. Artifact
        EditorGUILayout.LabelField("2. Artifact (유물)", EditorStyles.boldLabel);
        cheat.selectedArtifact = (CheatArtifactChoice)EditorGUILayout.EnumPopup("유물 선택", cheat.selectedArtifact);
        if (GUILayout.Button("선택한 유물 지급", GUILayout.Height(26)))
        {
            cheat.GiveArtifact();
        }

        EditorGUILayout.Space(10);

        // 3. Piece
        EditorGUILayout.LabelField("3. Piece (기물)", EditorStyles.boldLabel);
        cheat.selectedPiece = (PieceType)EditorGUILayout.EnumPopup("기물 선택", cheat.selectedPiece);
        if (GUILayout.Button("선택한 기물 지급", GUILayout.Height(26)))
        {
            cheat.GivePiece();
        }

        EditorGUILayout.Space(10);

        // 4. Seal
        EditorGUILayout.LabelField("4. Seal (인장)", EditorStyles.boldLabel);
        cheat.selectedSeal = (CheatSealChoice)EditorGUILayout.EnumPopup("인장 선택", cheat.selectedSeal);
        if (GUILayout.Button("선택한 인장 지급", GUILayout.Height(26)))
        {
            cheat.GiveSeal();
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(cheat);
        }
    }
}
#endif
