/// <summary>
/// [AC03] 보호의 물약 (액티브, 대상 선택)
/// 선택한 아군 기물 1개에게 보호 1회를 부여합니다.
/// 보호: 공격받을 때 스택 1을 소모하여 공격과 공격자의 이동을 모두 무효화합니다.
/// </summary>
public sealed class ProtectionPotionArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC03";

    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.AllyPiece;

    public override bool Execute(PieceController target)
    {
        if (!IsValidTarget(target))
        {
            return false;
        }

        target.AddBuff(PieceBuffType.Protection, 1);

        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.PlayCameraShake(0.1f, 0.3f);
        }

        return true;
    }
}

