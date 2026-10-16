#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CheatManager))]
public class CheatManagerEditor : Editor
{
    private List<ArtifactData> artifacts = new List<ArtifactData>();
    private string[] artifactLabels = new string[0];
    private int selectedArtifactIndex = 0;

    private List<SealData> seals = new List<SealData>();
    private string[] sealLabels = new string[0];
    private int selectedSealIndex = 0;

    private void OnEnable()
    {
        RefreshAssetLists();
    }

    public void RefreshAssetLists()
    {
        CheatManager cheat = (CheatManager)target;

        // 1. ArtifactData 로드 (Assets/Data/Artifact 등 프로젝트 내 전체)
        artifacts.Clear();
        string[] artifactGuids = AssetDatabase.FindAssets("t:ArtifactData");
        foreach (string guid in artifactGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ArtifactData data = AssetDatabase.LoadAssetAtPath<ArtifactData>(path);
            if (data != null && !artifacts.Contains(data))
            {
                artifacts.Add(data);
            }
        }

        // ID 또는 이름순 정렬
        artifacts.Sort((a, b) =>
        {
            if (!string.IsNullOrEmpty(a.id) && !string.IsNullOrEmpty(b.id))
                return string.Compare(a.id, b.id, System.StringComparison.Ordinal);
            return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
        });

        artifactLabels = new string[artifacts.Count];
        for (int i = 0; i < artifacts.Count; i++)
        {
            var a = artifacts[i];
            string typeTag = a.IsActive ? "[액티브]" : "[패시브]";
            string idTag = !string.IsNullOrEmpty(a.id) ? $"[{a.id}] " : "";
            string nameTag = !string.IsNullOrEmpty(a.artifactName) ? a.artifactName : a.name;
            artifactLabels[i] = $"{idTag}{nameTag} {typeTag}";
        }

        if (cheat.selectedArtifactData != null)
        {
            selectedArtifactIndex = artifacts.IndexOf(cheat.selectedArtifactData);
            if (selectedArtifactIndex < 0) selectedArtifactIndex = 0;
        }
        else if (artifacts.Count > 0)
        {
            cheat.selectedArtifactData = artifacts[0];
            selectedArtifactIndex = 0;
        }

        // 2. SealData 로드 (Assets/Resources/Seal 등 프로젝트 내 전체)
        seals.Clear();
        string[] sealGuids = AssetDatabase.FindAssets("t:SealData");
        foreach (string guid in sealGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SealData data = AssetDatabase.LoadAssetAtPath<SealData>(path);
            if (data != null && !seals.Contains(data))
            {
                seals.Add(data);
            }
        }

        // 이름순 정렬
        seals.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        sealLabels = new string[seals.Count];
        for (int i = 0; i < seals.Count; i++)
        {
            var s = seals[i];
            string nameTag = !string.IsNullOrEmpty(s.sealName) ? $"{s.sealName} ({s.name})" : s.name;
            sealLabels[i] = nameTag;
        }

        if (cheat.selectedSealData != null)
        {
            selectedSealIndex = seals.IndexOf(cheat.selectedSealData);
            if (selectedSealIndex < 0) selectedSealIndex = 0;
        }
        else if (seals.Count > 0)
        {
            cheat.selectedSealData = seals[0];
            selectedSealIndex = 0;
        }
    }

    public override void OnInspectorGUI()
    {
        CheatManager cheat = (CheatManager)target;

        // 상단 새로고침 버튼
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("치트 컨트롤러", EditorStyles.boldLabel);
        if (GUILayout.Button("🔄 에셋 새로고침", GUILayout.Width(110), GUILayout.Height(20)))
        {
            RefreshAssetLists();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

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
        if (artifacts.Count > 0)
        {
            int newIdx = EditorGUILayout.Popup("유물 선택 (드롭다운)", selectedArtifactIndex, artifactLabels);
            if (newIdx != selectedArtifactIndex)
            {
                selectedArtifactIndex = newIdx;
                cheat.selectedArtifactData = artifacts[selectedArtifactIndex];
                EditorUtility.SetDirty(cheat);
            }
            else if (cheat.selectedArtifactData != artifacts[selectedArtifactIndex])
            {
                // 인스펙터 필드로 직접 변경되었을 경우 인덱스 동기화
                selectedArtifactIndex = artifacts.IndexOf(cheat.selectedArtifactData);
                if (selectedArtifactIndex < 0) selectedArtifactIndex = 0;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("프로젝트에서 ArtifactData 에셋을 찾을 수 없습니다.", MessageType.Warning);
        }

        var prevArtifact = cheat.selectedArtifactData;
        cheat.selectedArtifactData = (ArtifactData)EditorGUILayout.ObjectField("선택된 유물 에셋", cheat.selectedArtifactData, typeof(ArtifactData), false);
        if (prevArtifact != cheat.selectedArtifactData && cheat.selectedArtifactData != null)
        {
            selectedArtifactIndex = artifacts.IndexOf(cheat.selectedArtifactData);
        }

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

        if (GUILayout.Button("🔍 현재 남은 기물 목록 콘솔 출력", GUILayout.Height(26)))
        {
            cheat.PrintRemainingPieces();
        }

        if (GUILayout.Button("🧹 좌표 없는 보드 기물 즉시 정리", GUILayout.Height(24)))
        {
            cheat.CleanupInvalidBoardPieces();
        }

        EditorGUILayout.Space(10);

        // 4. Seal
        EditorGUILayout.LabelField("4. Seal (인장)", EditorStyles.boldLabel);
        if (seals.Count > 0)
        {
            int newIdx = EditorGUILayout.Popup("인장 선택 (드롭다운)", selectedSealIndex, sealLabels);
            if (newIdx != selectedSealIndex)
            {
                selectedSealIndex = newIdx;
                cheat.selectedSealData = seals[selectedSealIndex];
                EditorUtility.SetDirty(cheat);
            }
            else if (cheat.selectedSealData != seals[selectedSealIndex])
            {
                // 인스펙터 필드로 직접 변경되었을 경우 인덱스 동기화
                selectedSealIndex = seals.IndexOf(cheat.selectedSealData);
                if (selectedSealIndex < 0) selectedSealIndex = 0;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("프로젝트에서 SealData 에셋을 찾을 수 없습니다.", MessageType.Warning);
        }

        var prevSeal = cheat.selectedSealData;
        cheat.selectedSealData = (SealData)EditorGUILayout.ObjectField("선택된 인장 에셋", cheat.selectedSealData, typeof(SealData), false);
        if (prevSeal != cheat.selectedSealData && cheat.selectedSealData != null)
        {
            selectedSealIndex = seals.IndexOf(cheat.selectedSealData);
        }

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
