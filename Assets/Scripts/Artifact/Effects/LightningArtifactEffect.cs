/// <summary>
/// [AC02] 낙뢰의 부적 (액티브, 대상 선택)
/// 선택한 적 기물 1개를 파괴합니다.
/// </summary>
public sealed class LightningArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC02";

    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.EnemyPiece;

    public override bool IsValidTarget(PieceController piece)
    {
        return base.IsValidTarget(piece);
    }

    public override bool Execute(PieceController target)
    {
        if (!IsValidTarget(target) || PieceManager.Instance == null)
        {
            return false;
        }

        // 보호 인장 등이 파괴를 막으면 유물은 소모되지만 기물은 살아남음
        if (target.CanBeDestroyed())
        {
            if (EffectManager.Instance != null)
            {
                EffectManager.Instance.PlayCameraShake(0.2f, 0.5f);
            }

            PieceManager.Instance.RemovePiece(target);
        }

        PieceManager.Instance.UpdateThreatenedStatus();
        return true;
    }
}

