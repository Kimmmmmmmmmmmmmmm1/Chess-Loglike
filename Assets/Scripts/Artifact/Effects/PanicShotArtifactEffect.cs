/// <summary>
/// [AC07] 공포탄: 선택한 적 기물의 Rank에 있는 적 기물을 모두 한 칸 후퇴시킵니다.
/// </summary>
public sealed class PanicShotArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC07";
    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.EnemyPiece;

    public override bool Execute(PieceController target)
    {
        if (!IsValidTarget(target) || PieceManager.Instance == null)
        {
            return false;
        }

        int rank = target.GridPosition.Value.y;
        PieceController[] targets = new System.Collections.Generic.List<PieceController>(PieceManager.Instance.EnemyPieces).ToArray();
        foreach (PieceController piece in targets)
        {
            if (piece == null || piece.GridPosition == null || piece.GridPosition.Value.y != rank) continue;
            PieceManager.Instance.TryForceMovePiece(piece, piece.GridPosition.Value + UnityEngine.Vector2Int.up);
        }

        TurnLogUIManager.Instance?.AddLog($"공포탄 발동: Rank {rank}의 적 기물이 후퇴", false);
        return true;
    }
}
