using UnityEngine;

/// <summary>
/// A003 "인장 확률 보너스" 효과 구현
/// 상점에서 인장이 등장할 확률을 레벨당 +10% 증가시킵니다.
/// </summary>
public class SealChanceBonusArtifactEffect : ArtifactEffectBase
{
    private const string ARTIFACT_ID = "A003";
    public override string ArtifactId => ARTIFACT_ID;
    private const float BONUS_PER_LEVEL = 10f;

    public override void ResetStageLimitedEffects()
    {
        // 이 효과는 스테이지 제한이 없으므로 아무 작업도 하지 않습니다.
    }

    public override bool IsExhausted()
    {
        return false; // 소진 개념이 없음
    }

    public override bool HasRemaining()
    {
        return true; // 항상 사용 가능
    }

    public override int GetGaugeCurrentCount()
    {
        return 0; // 게이지 없음
    }

    public override int GetGaugeMaxCount()
    {
        return 0; // 게이지 없음
    }

    /// <summary>
    /// 인장 확률 보너스를 반환합니다 (퍼센트 단위).
    /// </summary>
    public float GetChanceBonus()
    {
        if (ArtifactManager.Instance == null) return 0f;

        float bonus = 0f;
        ArtifactManager.Instance.ApplyArtifactWithLevel(ARTIFACT_ID, level =>
        {
            bonus = BONUS_PER_LEVEL * level; // 레벨당 +10%
        });

        return bonus;
    }

    /// <summary>
    /// 레벨별 확률 보너스를 반환합니다 (툴팁용).
    /// </summary>
    public static float GetBonusForLevel(int level)
    {
        return BONUS_PER_LEVEL * level;
    }
}
