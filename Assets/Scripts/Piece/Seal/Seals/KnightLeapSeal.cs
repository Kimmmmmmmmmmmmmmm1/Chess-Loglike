using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 나이트 도약 인장: 어떤 기물이든 나이트의 L자 점프(8방향) 이동 능력을 추가 부여합니다.
/// </summary>
public class KnightLeapSeal : AddMoveSeal
{
    protected override List<Vector2Int> GetAddedMoves(Vector2Int currentPos, bool isEnemy)
    {
        List<Vector2Int> addedMoves = new List<Vector2Int>();

        Vector2Int[] knightOffsets = new Vector2Int[]
        {
            new Vector2Int(2, 1),
            new Vector2Int(2, -1),
            new Vector2Int(-2, 1),
            new Vector2Int(-2, -1),
            new Vector2Int(1, 2),
            new Vector2Int(1, -2),
            new Vector2Int(-1, 2),
            new Vector2Int(-1, -2)
        };

        foreach (var offset in knightOffsets)
        {
            addedMoves.Add(currentPos + offset);
        }

        return addedMoves;
    }
}

