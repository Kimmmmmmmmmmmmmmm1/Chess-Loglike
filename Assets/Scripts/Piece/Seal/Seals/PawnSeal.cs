using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 폰의 인장: 폰의 고유 포획 능력처럼 전방 좌우 대각선(1칸)에 위치한 적 기물을 포획(공격 이동)할 수 있게 합니다.
/// (적이 없는 빈 칸으로는 이동할 수 없으며, 오직 적 기물이 있을 때만 포획 가능)
/// </summary>
public class PawnSeal : SealBase
{
    public override void ModifyMoves(ref List<Vector2Int> moves, Vector2Int currentPos, bool isEnemy, System.Func<Vector2Int, bool> validator = null, System.Func<Vector2Int, bool> isOccupied = null)
    {
        if (PieceManager.Instance == null) return;
        if (OthelloArtifactSystem.IsActive) return;

        int dirY = isEnemy ? -1 : 1;
        Vector2Int[] diagOffsets = new Vector2Int[]
        {
            new Vector2Int(-1, dirY),
            new Vector2Int(1, dirY)
        };

        foreach (var offset in diagOffsets)
        {
            Vector2Int target = currentPos + offset;

            // 보드 범위, 파괴된 타일, 이동 가능 여부 체크
            bool canMove = false;
            if (validator != null)
            {
                canMove = validator(target);
            }
            else if (owner != null)
            {
                canMove = owner.CanMoveTo(target);
            }
            else
            {
                canMove = PieceManager.Instance.gridManager != null && PieceManager.Instance.gridManager.IsInBounds(target);
            }

            if (!canMove) continue;

            // 적 기물이 존재하는 경우에만 포획 공격 이동 추가 (빈 칸 이동 불가)
            PieceController pieceAt = PieceManager.Instance.GetPieceAt(target);
            if (pieceAt != null && pieceAt.IsEnemy != isEnemy)
            {
                if (!moves.Contains(target))
                {
                    moves.Add(target);
                }
            }
        }
    }
}

