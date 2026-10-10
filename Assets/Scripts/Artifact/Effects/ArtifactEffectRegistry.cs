public static class ArtifactEffectRegistry
{
    // A001-A003: 상점 관련 유물
    public static readonly SlotExpansionArtifactEffect SlotExpansion = new SlotExpansionArtifactEffect();
    public static readonly RerollDiscountArtifactEffect RerollDiscount = new RerollDiscountArtifactEffect();
    public static readonly SealChanceBonusArtifactEffect SealChanceBonus = new SealChanceBonusArtifactEffect();

    // A004-A005: 스테이지 제한 유물
    public static readonly GourdArtifactEffect Gourd = new GourdArtifactEffect();
    public static readonly TombstoneArtifactEffect Tombstone = new TombstoneArtifactEffect();

    public static void ResetStageLimitedEffects()
    {
        // A001-A003은 스테이지 제한이 없으므로 리셋 불필요
        Gourd.ResetStageLimitedEffects();
        Tombstone.ResetStageLimitedEffects();
    }
}
