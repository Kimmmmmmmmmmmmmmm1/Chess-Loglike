using System.Collections.Generic;
using UnityEngine;

public class BishopSeal : SealBase
{
    public override bool ReplacesMovementPreview => true;

    public override void ModifyMoves(ref List<Vector2Int> moves, Vector2Int currentPos, bool isEnemy, System.Func<Vector2Int, bool> validator = null, System.Func<Vector2Int, bool> isOccupied = null)
    {
        // 기존 이동 경로 제거 (이동 방식 변경)
        moves.Clear();

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
            Vector2Int target = currentPos + dir;
            
            // 슬라이딩 이동 (장애물이나 맵 끝까지)
            while (true)
            {
                bool canMove = (validator != null) ? validator(target) : (owner != null && owner.CanMoveTo(target));
                if (!canMove) break; // 이동 불가(맵 밖이거나 아군)하면 중단

                moves.Add(target);

                // 적이거나 장애물이 있으면 이동 후 중단 (validator가 true여도 적일 수 있음)
                bool occupied = (isOccupied != null) ? isOccupied(target) : (owner != null && owner.IsOccupied(target));
                if (occupied) break;

                target += dir;
            }
        }
    }

    public override List<Vector2Int> GetPreviewAdditionalMovementOffsets(PieceType pieceType, bool isEnemy)
    {
        List<Vector2Int> offsets = new List<Vector2Int>();

        Vector2Int[] diagonals = new Vector2Int[]
        {
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        for (int i = 1; i <= 3; i++)
        {
            foreach (var dir in diagonals)
            {
                offsets.Add(dir * i);
            }
        }

        return offsets;
    }
}
