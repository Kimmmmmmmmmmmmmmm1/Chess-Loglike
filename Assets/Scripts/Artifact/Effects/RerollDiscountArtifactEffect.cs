using UnityEngine;

/// <summary>
/// A002 "리롤 비용 할인" 효과 구현
/// 상점 리롤 비용을 레벨에 따라 할인합니다 (Lv.1: -2, Lv.2: -3, Lv.3: -4...).
/// </summary>
public class RerollDiscountArtifactEffect : ArtifactEffectBase
{
    private const string ARTIFACT_ID = "A002";
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
    /// 리롤 비용 할인 금액을 반환합니다.
    /// </summary>
    public int GetDiscount()
    {
        if (ArtifactManager.Instance == null) return 0;

        int discount = 0;
        ArtifactManager.Instance.ApplyArtifactWithLevel(ARTIFACT_ID, level =>
        {
            discount = 2 + (level - 1); // Lv.1: -2, Lv.2: -3, Lv.3: -4...
        });

        return discount;
    }

    /// <summary>
    /// 레벨별 할인 금액을 반환합니다 (툴팁용).
    /// </summary>
    public static int GetDiscountForLevel(int level)
    {
        return 2 + (level - 1);
    }
}
