using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBossData", menuName = "Janggi/Boss Data")]
public class BossData : ScriptableObject
{
    [Tooltip("보스 난이도")]
    public int difficulty;

    [Header("Boss Presentation")]
    [Tooltip("보스 이명")]
    public string bossTitle;
    [Tooltip("보스 이름")]
    public string bossName;
    [Tooltip("보스 스프라이트")]
    public Sprite bossSprite;

    [Header("Map Settings")]
    [Tooltip("맵의 가로 크기")]
    public int mapWidth = 5;
    [Tooltip("맵의 세로 크기")]
    public int mapHeight = 7;
    
    [Tooltip("그리드 시작 X 좌표 (GridManager의 gridMinBounds.x와 일치시켜주세요)")]
    public int minX = -2;
    [Tooltip("그리드 시작 Y 좌표 (GridManager의 gridMinBounds.y와 일치시켜주세요)")]
    public int minY = -3;

    [Tooltip("보스 기물 배치 정보")]
    public List<PieceSpawner.PieceSpawnInfo> bossPieces;

    [Header("Boss Skills")]
    [Tooltip("보스가 사용할 스킬들")]
    public List<BaseSkill> skills = new List<BaseSkill>();

    private void OnValidate()
    {
        // 맵 크기 최소값 보정
        if (mapWidth < 1) mapWidth = 1;
        if (mapHeight < 1) mapHeight = 1;

        // 보스 기물 위치 유효성 검사
        if (bossPieces != null)
        {
            int maxX = minX + mapWidth - 1;
            int maxY = minY + mapHeight - 1;

            foreach (var piece in bossPieces)
            {
                // 범위를 벗어난 기물 경고
                if (piece.gridCoordinate.x < minX || piece.gridCoordinate.x > maxX ||
                    piece.gridCoordinate.y < minY || piece.gridCoordinate.y > maxY)
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

        // 스킬 유효성 검사
        if (skills != null)
        {
            foreach (var skill in skills)
            {
                if (skill == null)
                {
                }
            }
        }
    }
}
