using UnityEngine;

/// <summary>
/// [AC10] 혼란의 물약 (액티브, 대상 선택)
/// 선택한 적 기물 1개에게 2턴 동안 혼란 상태를 부여합니다.
/// 혼란: 적 기물의 이동 및 공격 범위가 폰(Pawn)으로 제한되며, 인장 이동 경로 수정이 무효화됩니다.
/// </summary>
public sealed class ConfusionPotionArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC10";

    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.EnemyPiece;

    public override bool Execute(PieceController target)
    {
        if (!IsValidTarget(target) || PieceManager.Instance == null)
        {
            return false;
        }

        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.PlayCameraShake(0.1f, 0.3f);
        }

        if (target.IsInvincible)
        {
            TurnLogUIManager.Instance?.AddLog($"[{target.PieceType}] 무적으로 인해 혼란을 무시했습니다!", target.IsEnemy);
            return true;
        }

        target.AddBuff(PieceBuffType.Confusion, 2);
        target.PlayBuffActivationEffect(PieceBuffType.Confusion);
        PieceManager.Instance.UpdateThreatenedStatus();

        TurnLogUIManager.Instance?.AddLog($"[{target.PieceType}] 2턴 동안 혼란 부여 (폰 행마로 제한)", target.IsEnemy);
        return true;
    }
}

