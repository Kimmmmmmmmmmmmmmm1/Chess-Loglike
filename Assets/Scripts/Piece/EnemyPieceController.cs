using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PieceController))]
public class EnemyPieceController : MonoBehaviour
{
    private PieceController piece;

    public PieceController Piece => piece;
    public PieceType PieceType => piece != null ? piece.Type : PieceType.Pawn;

    private void Awake()
    {
        piece = GetComponent<PieceController>();
        if (piece != null)
        {
            piece.MarkAsEnemy();
        }
    }

    public List<Vector2Int> GetCandidateMoves()
    {
        return piece != null ? piece.GetCandidateMoves() : new List<Vector2Int>();
    }
}
