using UnityEngine;

public sealed class MedalArtifactEffect : ArtifactEffectBase
{
    private int promotionCount;

    public override string ArtifactId => "A006";

    public override void ResetStageLimitedEffects()
    {
        promotionCount = 0;
    }

    public override bool IsExhausted()
    {
        if (!TryGetArtifactLevel(out _))
        {
            return false;
        }

        return promotionCount >= GetGaugeMaxCount();
    }

    public override bool HasRemaining()
    {
        if (!TryGetArtifactLevel(out _))
        {
            return false;
        }

        return promotionCount < GetGaugeMaxCount();
    }

    public bool TryPromote(PieceController soldier, PieceType promotedType)
    {
        if (soldier == null)
        {
            return false;
        }

        if (!TryGetArtifactLevel(out _))
        {
            return false;
        }

        if (soldier.Type != PieceType.Soldier)
        {
            return false;
        }

        if (promotedType == PieceType.King || promotedType == PieceType.Soldier)
        {
            return false;
        }

        if (promotionCount >= GetGaugeMaxCount())
        {
            return false;
        }

        promotionCount++;
        ArtifactManager.Instance?.UpdateInventoryUI();

        return true;
    }

    public override int GetGaugeCurrentCount() => promotionCount;

    public override int GetGaugeMaxCount()
    {
        if (TryGetArtifactLevel(out int level))
        {
            return GetMaxForLevel(level);
        }

        return GetMaxForLevel(1);
    }

    public static int GetMaxForLevel(int level)
    {
        return level switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            _ => 1
        };
    }
}