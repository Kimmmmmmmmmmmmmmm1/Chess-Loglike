using UnityEngine;

[RequireComponent(typeof(PieceController))]
public class EnemyPieceController : MonoBehaviour
{
    private PieceController piece;

    public PieceController Piece => piece;

    private void Awake()
    {
        piece = GetComponent<PieceController>();
        if (piece != null)
        {
            piece.MarkAsEnemy();
        }
    }
}
