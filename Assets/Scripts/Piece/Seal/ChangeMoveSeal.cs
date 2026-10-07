using System.Collections.Generic;
using UnityEngine;

public abstract class ChangeMoveSeal : SealBase
{
    public override bool ReplacesMovementPreview => true;

    public override void ModifyMoves(ref List<Vector2Int> moves, Vector2Int currentPos, bool isEnemy, System.Func<Vector2Int, bool> validator = null, System.Func<Vector2Int, bool> isOccupied = null)
    {
        // 기존 이동 경로를 모두 제거 (대체)
        moves.Clear();

        List<Vector2Int> newMoves = GetNewMoves(currentPos, isEnemy);
        
        foreach (var target in newMoves)
        {
            // 기물의 기본 이동 규칙(보드 범위, 아군 충돌 등)을 체크
            bool canMove = false;
            if (validator != null)
            {
                canMove = validator(target);
            }
            else if (owner != null)
            {
                canMove = owner.CanMoveTo(target);
            }

            if (canMove)
            {
                moves.Add(target);
            }
        }
    }

    public override List<Vector2Int> GetPreviewMovementOffsets(PieceType pieceType, bool isEnemy)
    {
        return GetNewMoves(Vector2Int.zero, isEnemy);
    }

    
    protected abstract List<Vector2Int> GetNewMoves(Vector2Int currentPos, bool isEnemy);
}
