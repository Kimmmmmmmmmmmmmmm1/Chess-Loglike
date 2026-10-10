using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 폰 전용 인장: 대각선 후퇴 능력 부여
/// 전방 대각선 공격뿐만 아니라 후방 대각선 2방향(좌후방, 우후방)으로도 이동/공격이 가능하도록 이동 경로를 확장합니다.
/// </summary>
public class DiagonalRetreatSeal : AddMoveSeal
{
    protected override List<Vector2Int> GetAddedMoves(Vector2Int currentPos, bool isEnemy)
    {
        List<Vector2Int> addedMoves = new List<Vector2Int>();

        // 플레이어 기준 후퇴는 down (-1), 적 기준 후퇴는 up (+1)
        int retreatY = isEnemy ? 1 : -1;

        Vector2Int[] retreatDiagonals = new Vector2Int[]
        {
            new Vector2Int(-1, retreatY),
            new Vector2Int(1, retreatY)
        };

        foreach (var offset in retreatDiagonals)
        {
            addedMoves.Add(currentPos + offset);
        }

        return addedMoves;
    }
}

