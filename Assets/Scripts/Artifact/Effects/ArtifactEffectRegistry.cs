using System.Collections.Generic;

public static class ArtifactEffectRegistry
{
    // A001-A003: 상점 관련 유물
    public static readonly SlotExpansionArtifactEffect SlotExpansion = new SlotExpansionArtifactEffect();
    public static readonly RerollDiscountArtifactEffect RerollDiscount = new RerollDiscountArtifactEffect();
    public static readonly SealChanceBonusArtifactEffect SealChanceBonus = new SealChanceBonusArtifactEffect();

    // A004-A005: 스테이지 제한 유물
    public static readonly GourdArtifactEffect Gourd = new GourdArtifactEffect();
    public static readonly TombstoneArtifactEffect Tombstone = new TombstoneArtifactEffect();
    public static readonly TyrantsBarrageArtifactEffect TyrantsBarrage = new TyrantsBarrageArtifactEffect();

    // AC01~: 액티브(소모형) 유물
    public static readonly HourglassArtifactEffect Hourglass = new HourglassArtifactEffect();
    public static readonly LightningArtifactEffect Lightning = new LightningArtifactEffect();
    public static readonly ProtectionPotionArtifactEffect ProtectionPotion = new ProtectionPotionArtifactEffect();
    public static readonly EvasionPotionArtifactEffect EvasionPotion = new EvasionPotionArtifactEffect();
    public static readonly CounterattackPotionArtifactEffect CounterattackPotion = new CounterattackPotionArtifactEffect();
    public static readonly InvincibilityPotionArtifactEffect InvincibilityPotion = new InvincibilityPotionArtifactEffect();
    public static readonly PanicShotArtifactEffect PanicShot = new PanicShotArtifactEffect();
    public static readonly TacticalShiftArtifactEffect TacticalShift = new TacticalShiftArtifactEffect();
    public static readonly BlindnessPotionArtifactEffect BlindnessPotion = new BlindnessPotionArtifactEffect();
    public static readonly ConfusionPotionArtifactEffect ConfusionPotion = new ConfusionPotionArtifactEffect();

    private static readonly Dictionary<string, ActiveArtifactEffectBase> activeEffects = BuildActiveEffects(
        Hourglass,
        Lightning,
        ProtectionPotion,
        EvasionPotion,
        CounterattackPotion,
        InvincibilityPotion,
        PanicShot,
        TacticalShift,
        BlindnessPotion,
        ConfusionPotion
    );

    private static Dictionary<string, ActiveArtifactEffectBase> BuildActiveEffects(params ActiveArtifactEffectBase[] effects)
    {
        var map = new Dictionary<string, ActiveArtifactEffectBase>();
        foreach (var effect in effects)
        {
            if (effect != null && !string.IsNullOrEmpty(effect.ArtifactId))
            {
                map[effect.ArtifactId] = effect;
            }
        }
        return map;
    }

    public static ActiveArtifactEffectBase GetActiveEffect(string artifactId)
    {
        if (string.IsNullOrEmpty(artifactId)) return null;
        return activeEffects.TryGetValue(artifactId, out var effect) ? effect : null;
    }

    public static void ResetStageLimitedEffects()
    {
        // A001-A003은 스테이지 제한이 없으므로 리셋 불필요
        Gourd.ResetStageLimitedEffects();
        Tombstone.ResetStageLimitedEffects();
    }

    public static void OnBattleStarted()
    {
        HeraldsHornArtifactEffect.ApplyAtBattleStart();
        TyrantsBarrage.OnBattleStarted();
    }
}
