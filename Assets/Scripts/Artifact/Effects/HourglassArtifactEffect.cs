/// <summary>
/// [AC01] 시간의 모래 (액티브, 즉시 발동)
/// 상대의 다음 턴을 건너뜁니다.
/// </summary>
public sealed class HourglassArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC01";

    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.None;

    // 이미 건너뛰기가 예약되어 있으면 중복 사용 불가
    public override bool CanUse()
    {
        return TurnManager.Instance != null && !TurnManager.Instance.IsOpponentTurnSkipPending;
    }

    public override bool Execute(PieceController target)
    {
        if (TurnManager.Instance == null)
        {
            return false;
        }

        return TurnManager.Instance.RequestSkipNextOpponentTurn();
    }
}

