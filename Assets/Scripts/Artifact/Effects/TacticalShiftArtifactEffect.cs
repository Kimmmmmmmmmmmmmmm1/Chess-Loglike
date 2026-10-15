/// <summary>
/// [AC08] 전술적 교대: 두 아군 기물의 위치를 맞바꿉니다.
/// </summary>
public sealed class TacticalShiftArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC08";
    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.AllyPiece;
    private PieceController firstPiece;

    public override bool ContinueTargetingAfterFailedExecution => firstPiece != null;

    public override bool Execute(PieceController target)
    {
        if (!IsValidTarget(target)) return false;

        if (firstPiece == null)
        {
            firstPiece = target;
            TurnLogUIManager.Instance?.AddLog("전술적 교대: 두 번째 아군 기물을 선택하세요.", false);
            return false;
        }

        if (target == firstPiece || target.GridPosition == null || firstPiece.GridPosition == null)
        {
            return false;
        }

        UnityEngine.Vector2Int firstPosition = firstPiece.GridPosition.Value;
        UnityEngine.Vector2Int secondPosition = target.GridPosition.Value;
        firstPiece.MoveToGrid(secondPosition);
        target.MoveToGrid(firstPosition);
        PieceManager.Instance?.EnsurePositionCacheReady();
        PieceManager.Instance?.UpdateThreatenedStatus();
        TurnLogUIManager.Instance?.AddLog("전술적 교대 발동", false);
        firstPiece = null;
        return true;
    }

    public override void CancelSelection()
    {
        firstPiece = null;
    }
}
