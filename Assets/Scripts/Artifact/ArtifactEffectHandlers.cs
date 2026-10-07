using UnityEngine;

/// <summary>
/// 레거시 호출부 호환을 위한 퍼사드 클래스.
/// 실제 유물별 로직은 Assets/Scripts/Artifact/Effects 하위 클래스로 분리됩니다.
/// </summary>
public static class ArtifactEffectHandlers
{
    public static void ResetStageLimitedEffects()
    {
        ArtifactEffectRegistry.ResetStageLimitedEffects();
        ArtifactManager.Instance?.UpdateInventoryUI();
    }

    // ========== A001: 상점 슬롯 확장 ==========
    public static int GetShopSlotBonus() => ArtifactEffectRegistry.SlotExpansion.GetSlotBonus();
    public static int GetSlotBonusForLevel(int level) => SlotExpansionArtifactEffect.GetBonusForLevel(level);

    // ========== A002: 리롤 비용 할인 ==========
    public static int GetRerollDiscount() => ArtifactEffectRegistry.RerollDiscount.GetDiscount();
    public static int GetRerollDiscountForLevel(int level) => RerollDiscountArtifactEffect.GetDiscountForLevel(level);

    // ========== A003: 인장 확률 보너스 ==========
    public static float GetSealChanceBonus() => ArtifactEffectRegistry.SealChanceBonus.GetChanceBonus();
    public static float GetSealChanceBonusForLevel(int level) => SealChanceBonusArtifactEffect.GetBonusForLevel(level);

    // ========== A004: 회복의 단지 ==========
    public static bool IsGourdRecoveryExhausted() => ArtifactEffectRegistry.Gourd.IsExhausted();
    public static bool TryPrepareGourdRecovery(PieceController piece, out InventorySlot targetSlot)
    {
        return ArtifactEffectRegistry.Gourd.TryPrepareRecovery(piece, out targetSlot);
    }
    public static int GetGourdRecoveryMaxForLevel(int level) => GourdArtifactEffect.GetMaxForLevel(level);
    public static int GetGourdRecoveryCount() => ArtifactEffectRegistry.Gourd.GetGaugeCurrentCount();
    public static int GetGourdRecoveryMax() => ArtifactEffectRegistry.Gourd.GetGaugeMaxCount();

    // ========== A005: 무한한 비석 ==========
    public static void OnTombstoneDestroyCount(PieceController piece)
    {
        ArtifactEffectRegistry.Tombstone.OnPieceDestroyed(piece);
    }
    public static int GetTombstoneThresholdForLevel(int level) => TombstoneArtifactEffect.GetThresholdForLevel(level);
    public static int GetTombstoneDestroyCount() => ArtifactEffectRegistry.Tombstone.GetGaugeCurrentCount();
    public static int GetTombstoneDestroyThreshold() => ArtifactEffectRegistry.Tombstone.GetGaugeMaxCount();

    // ========== A006: 용맹의 훈장 ==========
    public static bool IsMedalPromotionExhausted() => ArtifactEffectRegistry.Medal.IsExhausted();
    public static bool HasMedalPromotionRemaining() => ArtifactEffectRegistry.Medal.HasRemaining();
    public static bool TryMedalPromotion(PieceController soldier, PieceType promotedType)
    {
        return ArtifactEffectRegistry.Medal.TryPromote(soldier, promotedType);
    }
    public static int GetMedalPromotionMaxForLevel(int level) => MedalArtifactEffect.GetMaxForLevel(level);
    public static int GetMedalPromotionCount() => ArtifactEffectRegistry.Medal.GetGaugeCurrentCount();
    public static int GetMedalPromotionMax() => ArtifactEffectRegistry.Medal.GetGaugeMaxCount();

    // ========== 게이지 통합 접근자 ==========
    public static int GetArtifactGaugeCurrentCount(string artifactId)
    {
        if (ArtifactManager.Instance == null || string.IsNullOrEmpty(artifactId))
        {
            return 0;
        }

        return artifactId switch
        {
            "A004" => ArtifactManager.Instance.HasArtifact("A004", out _) ? GetGourdRecoveryCount() : 0,
            "A005" => ArtifactManager.Instance.HasArtifact("A005", out _) ? GetTombstoneDestroyCount() : 0,
            "A006" => ArtifactManager.Instance.HasArtifact("A006", out _) ? GetMedalPromotionCount() : 0,
            _ => 0
        };
    }

    public static int GetArtifactGaugeMaxCount(string artifactId)
    {
        if (ArtifactManager.Instance == null || string.IsNullOrEmpty(artifactId))
        {
            return 0;
        }

        return artifactId switch
        {
            "A004" => ArtifactManager.Instance.HasArtifact("A004", out _) ? GetGourdRecoveryMax() : 0,
            "A005" => ArtifactManager.Instance.HasArtifact("A005", out _) ? GetTombstoneDestroyThreshold() : 0,
            "A006" => ArtifactManager.Instance.HasArtifact("A006", out _) ? GetMedalPromotionMax() : 0,
            _ => 0
        };
    }
}

