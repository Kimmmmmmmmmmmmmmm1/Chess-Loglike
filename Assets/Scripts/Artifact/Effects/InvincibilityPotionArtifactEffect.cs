/// <summary>
/// [AC06] 무적의 물약 (액티브, 대상 선택)
/// 선택한 아군 기물에게 2턴 동안 무적을 부여합니다.
/// </summary>
public sealed class InvincibilityPotionArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC06";

    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.AllyPiece;

    public override bool Execute(PieceController target)
    {
        if (!IsValidTarget(target))
        {
            return false;
        }

        target.AddInvincibility(2);
        target.PlayBuffActivationEffect(PieceBuffType.Invincibility);
        TurnLogUIManager.Instance?.AddLog("무적 발동: 2턴 동안 공격 대상이 되지 않음", target.IsEnemy);
        return true;
    }
}
