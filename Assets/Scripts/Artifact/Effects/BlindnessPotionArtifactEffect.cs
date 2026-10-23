using UnityEngine;

/// <summary>
/// [AC09] 실명의 물약 (액티브, 대상 선택)
/// 선택한 적 기물 1개에게 2턴 동안 실명 상태를 부여합니다.
/// 실명: 적 기물이 공격 및 아군 기물 위협을 할 수 없게 됩니다.
/// </summary>
public sealed class BlindnessPotionArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC09";

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
            TurnLogUIManager.Instance?.AddLog($"[{target.PieceType}] 무적으로 인해 실명을 무시했습니다!", target.IsEnemy);
            return true;
        }

        target.AddBuff(PieceBuffType.Blindness, 2);
        target.PlayBuffActivationEffect(PieceBuffType.Blindness);
        PieceManager.Instance.UpdateThreatenedStatus();

        TurnLogUIManager.Instance?.AddLog($"[{target.PieceType}] 2턴 동안 실명 부여 (공격 불가)", target.IsEnemy);
        return true;
    }
}

