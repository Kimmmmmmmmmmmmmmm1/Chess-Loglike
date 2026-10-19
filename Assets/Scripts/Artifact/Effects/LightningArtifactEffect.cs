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

        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.PlayCameraShake(0.2f, 0.5f);
        }

        // 1. 무적: 낙뢰 피해 완전 무시
        if (target.IsInvincible)
        {
            TurnLogUIManager.Instance?.AddLog(
                $"[{target.PieceType}] 무적으로 낙뢰 피해를 무시했습니다!",
                target.IsEnemy);
            PieceManager.Instance.UpdateThreatenedStatus();
            return true;
        }

        // 2. 보호 / 회피 / 역공 판정 처리
        // - 보호: 스택 차감 & 피해 무효화
        // - 회피: 스택 차감 & 인접 빈 칸으로 회피 이동
        // - 역공: 스택 차감 & 낙뢰 차단
        PieceManager.AttackResponse response = PieceManager.Instance.ResolveAttackResponse(null, target);
        if (response != PieceManager.AttackResponse.None)
        {
            PieceManager.Instance.UpdateThreatenedStatus();
            return true;
        }

        // 3. 파괴 전 거부권 확인
        if (target.CanBeDestroyed())
        {
            PieceManager.Instance.RemovePiece(target);
        }

        PieceManager.Instance.UpdateThreatenedStatus();
        return true;
    }
}

