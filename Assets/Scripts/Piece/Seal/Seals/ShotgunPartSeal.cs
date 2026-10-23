using System.Collections.Generic;
using UnityEngine;

/// <summary>샷건 킹 전용 무기 파츠 인장입니다.</summary>
public sealed class ShotgunPartSeal : SealBase
{
    private static readonly Vector2Int[] KnightOffsets =
    {
        new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(-2, 1), new Vector2Int(-2, -1),
        new Vector2Int(1, 2), new Vector2Int(1, -2), new Vector2Int(-1, 2), new Vector2Int(-1, -2)
    };

    public override void ModifyMoves(ref List<Vector2Int> moves, Vector2Int currentPos, bool isEnemy,
        System.Func<Vector2Int, bool> validator = null, System.Func<Vector2Int, bool> isOccupied = null)
    {
        if (isEnemy || owner == null || owner.Type != PieceType.King || !ShotgunKingSystem.IsActive || Data == null) return;

        if (Data.sealName == ShotgunKingSystem.JumpShot)
        {
            foreach (Vector2Int offset in KnightOffsets) AddMove(moves, currentPos + offset, validator);
        }
        else if (Data.sealName == ShotgunKingSystem.ScavengerBarrel)
        {
            foreach (Vector2Int target in ShotgunKingSystem.GetScavengedMoveTargets(owner, currentPos))
                AddMove(moves, target, validator);
        }
    }

    private void AddMove(List<Vector2Int> moves, Vector2Int target, System.Func<Vector2Int, bool> validator)
    {
        bool canMove = validator != null ? validator(target) : owner != null && owner.CanMoveTo(target);
        if (canMove && !moves.Contains(target)) moves.Add(target);
    }
}
