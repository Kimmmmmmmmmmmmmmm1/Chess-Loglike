using UnityEngine;
using System.Collections.Generic;

public interface ISkillEffect
{
    void ApplyEffect(Vector2Int gridPosition);

    void ApplyEffectInRange(Vector2Int centerPosition, int radius);

    void ApplyEffectToMultiplePositions(List<Vector2Int> positions);
}

public class SkillExecutionContext
{
    public GridManager gridManager;
    public PieceManager pieceManager;
    public int currentTurnCount;

    public SkillExecutionContext(GridManager grid, PieceManager pieces, int turnCount)
    {
        gridManager = grid;
        pieceManager = pieces;
        currentTurnCount = turnCount;
    }
}
