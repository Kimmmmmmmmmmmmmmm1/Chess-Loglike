using System.Collections.Generic;
using UnityEngine;

public class TramplingSeal : SealBase
{
    private static readonly Vector2Int[] HorseOffsets =
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

    private static readonly Vector2Int[] ElephantOffsets =
    {
        new Vector2Int(3, 2),
        new Vector2Int(3, -2),
        new Vector2Int(-3, 2),
        new Vector2Int(-3, -2),
        new Vector2Int(2, 3),
        new Vector2Int(2, -3),
        new Vector2Int(-2, 3),
        new Vector2Int(-2, -3)
    };

    public override void OnEquip()
    {
        if (owner != null && owner.Type != PieceType.Horse && owner.Type != PieceType.Elephant)
        {
        }
    }

    public override void ModifyMoves(ref List<Vector2Int> moves, Vector2Int currentPos, bool isEnemy, System.Func<Vector2Int, bool> validator = null, System.Func<Vector2Int, bool> isOccupied = null)
    {
        if (owner == null || isOccupied == null)
        {
            return;
        }

        if (owner.Type == PieceType.Horse)
        {
            AddBlockedMoves(ref moves, currentPos, HorseOffsets, validator, isOccupied, isElephant: false);
            return;
        }

        if (owner.Type == PieceType.Elephant)
        {
            AddBlockedMoves(ref moves, currentPos, ElephantOffsets, validator, isOccupied, isElephant: true);
        }
    }

    public override List<Vector2Int> GetPreviewAdditionalMovementOffsets(PieceType pieceType, bool isEnemy)
    {
        List<Vector2Int> offsets = new List<Vector2Int>();

        if (pieceType == PieceType.Horse)
        {
            offsets.AddRange(HorseOffsets);
            return offsets;
        }

        if (pieceType == PieceType.Elephant)
        {
            offsets.AddRange(ElephantOffsets);
        }

        return offsets;
    }

    public override void OnAfterMove(Vector2Int prevPos, Vector2Int newPos)
    {
        if (owner == null || PieceManager.Instance == null)
        {
            return;
        }

        Vector2Int offset = newPos - prevPos;

        if (owner.Type == PieceType.Horse)
        {
            if (TryGetHorseBlock(offset, out Vector2Int blockRel))
            {
                DestroyEnemyAt(prevPos + blockRel);
            }

            return;
        }

        if (owner.Type == PieceType.Elephant)
        {
            if (TryGetElephantBlocks(offset, out Vector2Int step1Rel, out Vector2Int step2Rel))
            {
                DestroyEnemyAt(prevPos + step1Rel);
                DestroyEnemyAt(prevPos + step2Rel);
            }
        }
    }

    private void AddBlockedMoves(
        ref List<Vector2Int> moves,
        Vector2Int currentPos,
        Vector2Int[] offsets,
        System.Func<Vector2Int, bool> validator,
        System.Func<Vector2Int, bool> isOccupied,
        bool isElephant)
    {
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int offset = offsets[i];
            Vector2Int target = currentPos + offset;

            bool hasBlockingPiece;
            if (isElephant)
            {
                if (!TryGetElephantBlocks(offset, out Vector2Int step1Rel, out Vector2Int step2Rel))
                {
                    continue;
                }

                hasBlockingPiece = isOccupied(currentPos + step1Rel) || isOccupied(currentPos + step2Rel);
            }
            else
            {
                if (!TryGetHorseBlock(offset, out Vector2Int blockRel))
                {
                    continue;
                }

                hasBlockingPiece = isOccupied(currentPos + blockRel);
            }

            if (!hasBlockingPiece)
            {
                continue;
            }

            bool canMove = validator != null ? validator(target) : owner.CanMoveTo(target);
            if (canMove && !moves.Contains(target))
            {
                moves.Add(target);
            }
        }
    }

    private void DestroyEnemyAt(Vector2Int position)
    {
        PieceController piece = PieceManager.Instance.GetPieceAt(position);
        if (piece != null && piece.IsEnemy != owner.IsEnemy)
        {
            PieceManager.Instance.RemovePiece(piece);
        }
    }

    private bool TryGetHorseBlock(Vector2Int offset, out Vector2Int blockRel)
    {
        blockRel = Vector2Int.zero;

        int absX = Mathf.Abs(offset.x);
        int absY = Mathf.Abs(offset.y);
        if (!((absX == 2 && absY == 1) || (absX == 1 && absY == 2)))
        {
            return false;
        }

        int stepX = offset.x == 0 ? 0 : (int)Mathf.Sign(offset.x);
        int stepY = offset.y == 0 ? 0 : (int)Mathf.Sign(offset.y);

        blockRel = absX == 2 ? new Vector2Int(stepX, 0) : new Vector2Int(0, stepY);
        return true;
    }

    private bool TryGetElephantBlocks(Vector2Int offset, out Vector2Int step1Rel, out Vector2Int step2Rel)
    {
        step1Rel = Vector2Int.zero;
        step2Rel = Vector2Int.zero;

        int absX = Mathf.Abs(offset.x);
        int absY = Mathf.Abs(offset.y);
        if (!((absX == 3 && absY == 2) || (absX == 2 && absY == 3)))
        {
            return false;
        }

        int stepX = offset.x == 0 ? 0 : (int)Mathf.Sign(offset.x);
        int stepY = offset.y == 0 ? 0 : (int)Mathf.Sign(offset.y);

        step1Rel = absX == 3 ? new Vector2Int(stepX, 0) : new Vector2Int(0, stepY);
        step2Rel = step1Rel + new Vector2Int(stepX, stepY);
        return true;
    }
}
