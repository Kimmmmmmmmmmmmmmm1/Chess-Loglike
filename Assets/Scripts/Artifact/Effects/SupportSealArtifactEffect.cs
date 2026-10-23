using UnityEngine;

/// <summary>
/// [AC11] 지원군의 훈장: 인벤토리 아군을 아군 진영 1~2랭크에 배치합니다.
/// </summary>
public sealed class SupportSealArtifactEffect : ActiveArtifactEffectBase
{
    public const string Id = "AC11";
    private const int BaseCooldownTurns = 4;
    private const int MinimumCooldownTurns = 2;

    private PieceController selectedPiece;
    private int cooldownTurnsRemaining;
    private int cooldownTurnsTotal;

    public override string ArtifactId => Id;
    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.InventoryPiecePlacement;
    public override bool ConsumesArtifact => false;
    public override bool IsOnCooldown => cooldownTurnsRemaining > 0;
    public override bool HasSelectedInventoryPiece => selectedPiece != null;
    public int CooldownTurnsRemaining => cooldownTurnsRemaining;
    public float CooldownRemainingRatio => cooldownTurnsTotal <= 0
        ? 0f
        : Mathf.Clamp01((float)cooldownTurnsRemaining / cooldownTurnsTotal);

    public override bool CanUse()
    {
        return cooldownTurnsRemaining <= 0 && PieceInventory.Instance != null && PieceInventory.Instance.OwnedPieces.Count > 0;
    }

    public override bool Execute(PieceController target) => false;

    public static int GetCooldownForLevel(int level)
    {
        return Mathf.Max(MinimumCooldownTurns, BaseCooldownTurns - Mathf.Max(0, level - 1));
    }

    public override bool TrySelectInventoryPiece(PieceController piece)
    {
        if (piece == null || piece.IsEnemy || piece.CurrentLocation != PieceLocation.Inventory ||
            piece.transform.parent == null || piece.transform.parent.GetComponent<InventorySlot>() == null)
        {
            return false;
        }

        selectedPiece = piece;
        return true;
    }

    public override bool IsValidPlacementPosition(Vector2Int position)
    {
        if (selectedPiece == null || selectedPiece.CurrentLocation != PieceLocation.Inventory ||
            PieceManager.Instance == null || PieceManager.Instance.gridManager == null)
        {
            return false;
        }

        GridManager grid = PieceManager.Instance.gridManager;
        int rankIndex = position.y - grid.gridMinBounds.y;
        if (!grid.IsInBounds(position) || rankIndex < 0 || rankIndex >= 2)
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(position);
        if (cell == null || cell.isDestroyed)
        {
            return false;
        }

        PieceManager.Instance.EnsurePositionCacheReady();
        return PieceManager.Instance.GetPieceAt(position) == null;
    }

    public override bool ExecuteAtPosition(Vector2Int position)
    {
        if (!IsValidPlacementPosition(position)) return false;

        PieceController piece = selectedPiece;
        selectedPiece = null;
        piece.MoveToGrid(position);
        PieceManager.Instance?.EnsurePositionCacheReady();
        PieceManager.Instance?.UpdateThreatenedStatus();

        int rank = position.y - PieceManager.Instance.gridManager.gridMinBounds.y + 1;
        TurnLogUIManager.Instance?.AddLog($"지원군의 훈장 발동: 아군 기물을 {rank}랭크에 배치했습니다.", false);
        return true;
    }

    public override void OnSuccessfulUse(ArtifactData artifact)
    {
        cooldownTurnsRemaining = GetCooldownForLevel(artifact != null ? artifact.Level : 1);
        cooldownTurnsTotal = cooldownTurnsRemaining;
    }

    public override void CancelSelection()
    {
        selectedPiece = null;
    }

    public bool OnPlayerTurnStarted()
    {
        if (cooldownTurnsRemaining <= 0) return false;

        cooldownTurnsRemaining--;
        return true;
    }

    public bool ResetForBattle()
    {
        bool changed = cooldownTurnsRemaining > 0;
        cooldownTurnsRemaining = 0;
        cooldownTurnsTotal = 0;
        selectedPiece = null;
        return changed;
    }
}
