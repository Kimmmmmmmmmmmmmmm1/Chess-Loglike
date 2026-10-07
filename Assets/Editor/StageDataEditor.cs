using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

[CustomEditor(typeof(StageData))]
public class StageDataEditor : Editor
{
    private StageData stageData;
    private const float CellSize = 40f;

    private void OnEnable()
    {
        stageData = (StageData)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. 기본 필드 그리기
        EditorGUILayout.PropertyField(serializedObject.FindProperty("difficulty"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Map Settings", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Sync from Scene GridManager"))
        {
            SyncWithGridManager();
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("mapWidth"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mapHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minX"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minY"));

        // 맵 크기 제한
        if (stageData.mapWidth < 1) stageData.mapWidth = 1;
        if (stageData.mapHeight < 1) stageData.mapHeight = 1;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Enemy Placement (Visual Editor)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox($"Grid Range: X[{stageData.minX} ~ {stageData.minX + stageData.mapWidth - 1}], Y[{stageData.minY} ~ {stageData.minY + stageData.mapHeight - 1}]\nLeft Click: Cycle Piece\nRight Click: Clear Piece", MessageType.Info);

        // 2. 그리드 그리기
        DrawGrid();

        // 3. 리스트 데이터 동기화
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enemyPieces"), true);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(stageData);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawGrid()
    {
        if (stageData.enemyPieces == null)
            stageData.enemyPieces = new List<PieceSpawner.PieceSpawnInfo>();

        // 현재 데이터를 딕셔너리로 변환하여 빠른 조회
        Dictionary<Vector2Int, PieceType> pieceMap = new Dictionary<Vector2Int, PieceType>();
        foreach (var info in stageData.enemyPieces)
        {
            if (!pieceMap.ContainsKey(info.gridCoordinate))
                pieceMap.Add(info.gridCoordinate, info.pieceType);
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // y축을 위에서 아래로 그리기 (Visual: Top -> Bottom)
        // 실제 좌표는 minY + y
        for (int y = stageData.mapHeight - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int x = 0; x < stageData.mapWidth; x++)
            {
                // 실제 게임 내 좌표 계산 (Offset 적용)
                int realX = stageData.minX + x;
                int realY = stageData.minY + y;
                Vector2Int currentPos = new Vector2Int(realX, realY);

                PieceType? currentPiece = pieceMap.ContainsKey(currentPos) ? pieceMap[currentPos] : (PieceType?)null;

                // 버튼 스타일
                GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
                btnStyle.fontSize = 10;
                btnStyle.fontStyle = FontStyle.Bold;

                if (realY == 3 || realY == 2)
                {
                    GUI.backgroundColor = new Color(0.8f, 1f, 0.8f); // 연한 초록색
                }
                else
                {
                    GUI.backgroundColor = Color.white;
                }

                // 기물이 있으면 색상 및 텍스트 변경
                string btnText = $"{realX},{realY}"; // 좌표 표시
                if (currentPiece.HasValue)
                {
                    btnText = $"{GetPieceShortName(currentPiece.Value)}\n{realX},{realY}";
                    GUI.backgroundColor = GetPieceColor(currentPiece.Value);
                }

                if (GUILayout.Button(btnText, btnStyle, GUILayout.Width(CellSize), GUILayout.Height(CellSize)))
                {
                    if (Event.current.button == 0) // 좌클릭
                    {
                        CyclePieceAt(currentPos, currentPiece);
                    }
                }

                // 우클릭 처리
                if (Event.current.type == EventType.MouseUp && Event.current.button == 1 && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    RemovePieceAt(currentPos);
                    Event.current.Use();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();
    }

    private void SyncWithGridManager()
    {
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            Undo.RecordObject(stageData, "Sync Grid Settings");
            
            stageData.mapWidth = gridManager.boardWidth;
            stageData.mapHeight = gridManager.boardHeight;
            stageData.minX = gridManager.gridMinBounds.x;
            stageData.minY = gridManager.gridMinBounds.y;
            
        }
        else
        {
        }
    }

    private void CyclePieceAt(Vector2Int pos, PieceType? currentType)
    {
        Undo.RecordObject(stageData, "Modify Stage Data");

        stageData.enemyPieces.RemoveAll(p => p.gridCoordinate == pos);

        PieceType nextType;
        if (currentType == null)
        {
            nextType = PieceType.King;
        }
        else
        {
            int nextIndex = (int)currentType.Value + 1;
            int maxIndex = Enum.GetValues(typeof(PieceType)).Length;
            
            if (nextIndex >= maxIndex) return; // 마지막이면 제거
            nextType = (PieceType)nextIndex;
        }

        stageData.enemyPieces.Add(new PieceSpawner.PieceSpawnInfo
        {
            gridCoordinate = pos,
            pieceType = nextType,
            seals = new List<SealData>()
        });
    }

    private void RemovePieceAt(Vector2Int pos)
    {
        Undo.RecordObject(stageData, "Remove Piece");
        stageData.enemyPieces.RemoveAll(p => p.gridCoordinate == pos);
    }

    private string GetPieceShortName(PieceType type)
    {
        switch (type)
        {
            case PieceType.King: return "궁";
            case PieceType.Chariot: return "차";
            case PieceType.Horse: return "마";
            case PieceType.Elephant: return "상";
            case PieceType.Cannon: return "포";
            case PieceType.Soldier: return "졸";
            default: return "?";
        }
    }

    private Color GetPieceColor(PieceType type)
    {
        switch (type)
        {
            case PieceType.King: return new Color(1f, 0.5f, 0.5f);
            case PieceType.Chariot: return new Color(1f, 0.7f, 0.7f);
            case PieceType.Cannon: return new Color(1f, 0.8f, 0.6f);
            default: return new Color(1f, 0.9f, 0.9f);
        }
    }
}
