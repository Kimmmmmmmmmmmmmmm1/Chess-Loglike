/// <summary>
/// [AC04] 회피의 물약 (액티브, 대상 선택)
/// 선택한 아군 기물 1개에게 회피 1회를 부여합니다.
/// 회피: 공격받을 때 주변 8칸 중 빈 칸으로 이동합니다.
/// </summary>
public sealed class EvasionPotionArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC04";

    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.AllyPiece;

    public override bool Execute(PieceController target)
    {
        if (!IsValidTarget(target))
        {
            return false;
        }

        target.AddBuff(PieceBuffType.Evasion, 1);

        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.PlayCameraShake(0.1f, 0.3f);
        }

        return true;
    }
}

