/// <summary>
/// [AC05] 역공의 물약 (액티브, 대상 선택)
/// 선택한 아군 기물 1개에게 역공 1회를 부여합니다.
/// 역공: 공격받을 때 스택 1을 소모하여 공격자를 즉시 반격 제거합니다.
/// </summary>
public sealed class CounterattackPotionArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC05";

    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.AllyPiece;

    public override bool Execute(PieceController target)
    {
        if (!IsValidTarget(target))
        {
            return false;
        }

        target.AddBuff(PieceBuffType.Counterattack, 1);

        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.PlayCameraShake(0.15f, 0.4f);
        }

        return true;
    }
}

