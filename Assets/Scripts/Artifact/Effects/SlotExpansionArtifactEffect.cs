using UnityEngine;

/// <summary>
/// A001 "상점 슬롯 확장" 효과 구현
/// 상점에 표시되는 기물 슬롯 개수를 레벨당 +1 증가시킵니다.
/// </summary>
public class SlotExpansionArtifactEffect : ArtifactEffectBase
{
    private const string ARTIFACT_ID = "A001";
    public override string ArtifactId => ARTIFACT_ID;

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
    /// 상점 슬롯 확장 보너스를 반환합니다.
    /// </summary>
    public int GetSlotBonus()
    {
        if (ArtifactManager.Instance == null) return 0;

        int bonus = 0;
        ArtifactManager.Instance.ApplyArtifactWithLevel(ARTIFACT_ID, level =>
        {
            bonus = level; // 레벨당 +1 슬롯
        });

        return bonus;
    }

    /// <summary>
    /// 레벨별 최대 슬롯 보너스를 반환합니다 (툴팁용).
    /// </summary>
    public static int GetBonusForLevel(int level)
    {
        return level; // 레벨당 +1
    }
}
