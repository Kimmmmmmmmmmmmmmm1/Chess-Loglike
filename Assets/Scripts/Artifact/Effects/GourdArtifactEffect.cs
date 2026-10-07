using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class GourdArtifactEffect : ArtifactEffectBase
{
    private int recoveryCount;

    public override string ArtifactId => "A004";

    public override void ResetStageLimitedEffects()
    {
        recoveryCount = 0;
    }

    public override bool IsExhausted()
    {
        if (!TryGetArtifactLevel(out _))
        {
            return false;
        }

        return recoveryCount >= GetGaugeMaxCount();
    }

    public override bool HasRemaining()
    {
        if (!TryGetArtifactLevel(out _))
        {
            return false;
        }

        return recoveryCount < GetGaugeMaxCount();
    }

    public bool TryPrepareRecovery(PieceController piece, out InventorySlot targetSlot)
    {
        targetSlot = null;

        if (piece == null || piece.IsEnemy)
        {
            return false;
        }

        if (!TryGetArtifactLevel(out _))
        {
            return false;
        }

        if (recoveryCount >= GetGaugeMaxCount())
        {
            return false;
        }

        targetSlot = FindEmptyInventorySlot();
        if (targetSlot == null)
        {
            return false;
        }

        recoveryCount++;
        ArtifactManager.Instance?.UpdateInventoryUI();

        return true;
    }

    public override int GetGaugeCurrentCount() => recoveryCount;

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
            1 => 2,
            2 => 3,
            3 => 4,
            4 => 5,
            _ => 2
        };
    }

    private static InventorySlot FindEmptyInventorySlot()
    {
        InventorySlot[] slots = Object.FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);
        IEnumerable<InventorySlot> sortedSlots = slots
            .Where(slot => slot != null)
            .OrderByDescending(slot => slot.transform.position.y)
            .ThenBy(slot => slot.transform.position.x);

        foreach (InventorySlot slot in sortedSlots)
        {
            if (slot.IsReserved)
            {
                continue;
            }

            PieceController currentPiece = slot.GetComponentInChildren<PieceController>();
            if (currentPiece == null)
            {
                return slot;
            }
        }

        return null;
    }
}