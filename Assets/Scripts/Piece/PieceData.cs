using UnityEngine;

[CreateAssetMenu(fileName = "NewPieceData", menuName = "Chess/Piece Data")]
public class PieceData : ScriptableObject
{
    public string id;
    public PieceType pieceType;
    public string pieceName;
    public Sprite icon;
    [Range(1, 5)] public int rarity = 1;
    [TextArea(2, 5)] public string description;
    [TextArea(2, 5)] public string flavorText;

    public int Rarity => rarity > 0 ? rarity : PieceController.GetRarityForType(pieceType);

    public string GetTooltipTitle()
    {
        return !string.IsNullOrEmpty(pieceName) ? pieceName : pieceType.ToString();
    }

    public string GetTooltipDescription()
    {
        return description ?? string.Empty;
    }
}

