using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 횡이동 인장: 좌우 1칸 및 대각선 전방 이동 등 횡단 기동 능력을 부여합니다.
/// (폰 등 직선 전진만 가능한 기물에 좌/우 횡이동 능력을 주어 기동성을 대폭 확장)
/// </summary>
public class LateralAdvanceSeal : AddMoveSeal
{
    protected override List<Vector2Int> GetAddedMoves(Vector2Int currentPos, bool isEnemy)
    {
        List<Vector2Int> addedMoves = new List<Vector2Int>();

        Vector2Int[] lateralOffsets = new Vector2Int[]
        {
            Vector2Int.left,
            Vector2Int.right
        };

        foreach (var offset in lateralOffsets)
        {
            addedMoves.Add(currentPos + offset);
        }

        return addedMoves;
    }
}

