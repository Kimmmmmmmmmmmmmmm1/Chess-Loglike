using System.Collections.Generic;
using UnityEngine;

public sealed class TombstoneArtifactEffect : ArtifactEffectBase
{
    private int destroyCount;

    public override string ArtifactId => "A005";

    public override void ResetStageLimitedEffects()
    {
        destroyCount = 0;
    }

    public void OnPieceDestroyed(PieceController piece)
    {
        if (piece == null || piece.IsEnemy)
        {
            return;
        }

        if (!TryGetArtifactLevel(out _))
        {
            return;
        }

        int threshold = GetGaugeMaxCount();
        destroyCount++;
        if (destroyCount >= threshold)
        {
            destroyCount = 0;
            SpawnPieceFromTombstone();
        }

        ArtifactManager.Instance?.UpdateInventoryUI();
    }

    public override int GetGaugeCurrentCount() => destroyCount;

    public override int GetGaugeMaxCount()
    {
        if (TryGetArtifactLevel(out int level))
        {
            return GetThresholdForLevel(level);
        }

        return GetThresholdForLevel(1);
    }

    public static int GetThresholdForLevel(int level)
    {
        return level switch
        {
            1 => 6,
            2 => 4,
            3 => 2,
            _ => 6
        };
    }

    private static void SpawnPieceFromTombstone()
    {
        InventorySlot[] slots = Object.FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);
        List<PieceController> availablePieces = new List<PieceController>();

        foreach (InventorySlot slot in slots)
        {
            if (slot == null || slot.IsReserved)
            {
                continue;
            }

            PieceController piece = slot.GetComponentInChildren<PieceController>();
            if (piece != null && !piece.IsEnemy)
            {
                availablePieces.Add(piece);
            }
        }

        if (availablePieces.Count == 0)
        {
            return;
        }

        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null)
        {
            return;
        }

        GridManager gridMgr = PieceManager.Instance.gridManager;
        List<Vector2Int> validPositions = new List<Vector2Int>();

        int minX = gridMgr.gridMinBounds.x;
        int maxXExclusive = minX + gridMgr.boardWidth;
        int minY = gridMgr.gridMinBounds.y;
        int maxYExclusive = minY + gridMgr.boardHeight;
        int prepareMaxY = PieceManager.Instance.PlayerPrepareMaxY;

        for (int y = minY; y < maxYExclusive; y++)
        {
            if (y > prepareMaxY)
            {
                continue;
            }

            for (int x = minX; x < maxXExclusive; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (PieceManager.Instance.GetPieceAt(pos) == null)
                {
                    validPositions.Add(pos);
                }
            }
        }

        if (validPositions.Count == 0)
        {
            return;
        }

        PieceController randomPiece = availablePieces[Random.Range(0, availablePieces.Count)];
        Vector2Int spawnPos = validPositions[Random.Range(0, validPositions.Count)];

        randomPiece.MoveToGrid(spawnPos);
    }
}