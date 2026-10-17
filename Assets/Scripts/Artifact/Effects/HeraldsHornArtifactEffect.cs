/// <summary>
/// [AP04] 전령의 나팔: 전투 시작 시 가장 전방의 아군에게 보호 1회를 부여합니다.
/// </summary>
public static class HeraldsHornArtifactEffect
{
    public const string ArtifactId = "AP04";

    public static void ApplyAtBattleStart()
    {
        if (ArtifactManager.Instance == null || !ArtifactManager.Instance.HasArtifact(ArtifactId) ||
            PieceManager.Instance == null)
        {
            return;
        }

        PieceController forward = null;
        foreach (PieceController piece in PieceManager.Instance.PlayerPieces)
        {
            if (piece == null || piece.CurrentLocation != PieceLocation.Board || piece.GridPosition == null) continue;
            if (forward == null || piece.GridPosition.Value.y > forward.GridPosition.Value.y)
            {
                forward = piece;
            }
        }

        if (forward == null) return;
        forward.AddBuff(PieceBuffType.Protection, 1);
        forward.PlayBuffActivationEffect(PieceBuffType.Protection);
        ArtifactPopupEffect.Play(ArtifactId);
        TurnLogUIManager.Instance?.AddLog("전령의 나팔: 선봉장에게 보호 부여", false);
    }
}
