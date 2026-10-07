using System.Collections.Generic;
using UnityEngine;

public class DiagonalMoveSeal : AddMoveSeal
{
    protected override List<Vector2Int> GetAddedMoves(Vector2Int currentPos, bool isEnemy)
    {
        List<Vector2Int> addedMoves = new List<Vector2Int>();

        // 대각선 4방향 정의
        Vector2Int[] diagonals = new Vector2Int[]
        {
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        foreach (var dir in diagonals)
        {
            addedMoves.Add(currentPos + dir);
        }
        
        return addedMoves;
    }
}
