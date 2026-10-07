using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewStageData", menuName = "Janggi/Stage Data")]
public class StageData : ScriptableObject
{
    [Tooltip("스테이지 난이도")]
    public int difficulty;

    [Header("Map Settings")]
    [Tooltip("맵의 가로 크기")]
    public int mapWidth = 5;
    [Tooltip("맵의 세로 크기")]
    public int mapHeight = 7;
    
    [Tooltip("그리드 시작 X 좌표 (GridManager의 gridMinBounds.x와 일치시켜주세요)")]
    public int minX = -2;
    [Tooltip("그리드 시작 Y 좌표 (GridManager의 gridMinBounds.y와 일치시켜주세요)")]
    public int minY = -3;

    [Tooltip("적 기물 배치 정보")]
    public List<PieceSpawner.PieceSpawnInfo> enemyPieces;

    private void OnValidate()
    {
        // 맵 크기 최소값 보정
        if (mapWidth < 1) mapWidth = 1;
        if (mapHeight < 1) mapHeight = 1;

        // 적 기물 위치 유효성 검사
        if (enemyPieces != null)
        {
            int maxX = minX + mapWidth - 1;
            int maxY = minY + mapHeight - 1;

            foreach (var piece in enemyPieces)
            {
                // 범위를 벗어난 기물 경고
                if (piece.gridCoordinate.x < minX || piece.gridCoordinate.x > maxX ||
                    piece.gridCoordinate.y < minY || piece.gridCoordinate.y > maxY)
                {
                }
                
                if (piece.gridCoordinate.y != 3 && piece.gridCoordinate.y != 2)
                {
                }

                // 인장 호환성 검사
                if (piece.seals != null && piece.seals.Count > 0)
                {
                    foreach (var seal in piece.seals)
                    {
                        if (seal != null && seal.compatiblePieces != null && seal.compatiblePieces.Count > 0 && !seal.compatiblePieces.Contains(piece.pieceType))
                        {
                        }
                    }
                }
            }
        }
    }
}
