using System.Collections.Generic;
using UnityEngine;

public abstract class AddMoveSeal : SealBase
{
    public override void ModifyMoves(ref List<Vector2Int> moves, Vector2Int currentPos, bool isEnemy, System.Func<Vector2Int, bool> validator = null, System.Func<Vector2Int, bool> isOccupied = null)
    {
        List<Vector2Int> addedMoves = GetAddedMoves(currentPos, isEnemy);
        
        foreach (var target in addedMoves)
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
                // 이미 리스트에 없다면 추가
                if (!moves.Contains(target))
                {
                    moves.Add(target);
                }
            }
        }
    }

    /// <summary>
    /// 추가할 이동 좌표들을 계산하여 반환합니다.
    /// </summary>
    protected abstract List<Vector2Int> GetAddedMoves(Vector2Int currentPos, bool isEnemy);
}
