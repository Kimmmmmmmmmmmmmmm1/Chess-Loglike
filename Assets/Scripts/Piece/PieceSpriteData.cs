using UnityEngine;

[CreateAssetMenu(fileName = "PieceSpriteData", menuName = "Chess/Piece Sprite Data")]
public class PieceSpriteData : ScriptableObject
{
    private static PieceSpriteData instance;
    public static PieceSpriteData Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<PieceSpriteData>("PieceSpriteData");
                if (instance == null)
                {
                    instance = Resources.Load<PieceSpriteData>("Piece/PieceSpriteData");
                }
#if UNITY_EDITOR
                if (instance == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:PieceSpriteData");
                    if (guids != null && guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        instance = UnityEditor.AssetDatabase.LoadAssetAtPath<PieceSpriteData>(path);
                    }
                }
#endif
            }
            return instance;
        }
    }

    [Header("Ally Piece Sprites (아군 기물)")]
    public Sprite kingSprite;
    public Sprite queenSprite;
    public Sprite rookSprite;
    public Sprite bishopSprite;
    public Sprite knightSprite;
    public Sprite pawnSprite;
    public Sprite amazonSprite;
    public Sprite chancellorSprite;
    public Sprite archbishopSprite;
    public Sprite centaurSprite;
    public Sprite cannonSprite;
    public Sprite grasshopperSprite;

    [Header("Enemy Piece Sprites (적군 기물)")]
    public Sprite enemyKingSprite;
    public Sprite enemyQueenSprite;
    public Sprite enemyRookSprite;
    public Sprite enemyBishopSprite;
    public Sprite enemyKnightSprite;
    public Sprite enemyPawnSprite;
    public Sprite enemyAmazonSprite;
    public Sprite enemyChancellorSprite;
    public Sprite enemyArchbishopSprite;
    public Sprite enemyCentaurSprite;
    public Sprite enemyCannonSprite;
    public Sprite enemyGrasshopperSprite;

    public Sprite GetSprite(PieceType type, bool isEnemy)
    {
        if (isEnemy)
        {
            return type switch
            {
                PieceType.King => enemyKingSprite,
                PieceType.Queen => enemyQueenSprite,
                PieceType.Rook => enemyRookSprite,
                PieceType.Bishop => enemyBishopSprite,
                PieceType.Knight => enemyKnightSprite,
                PieceType.Pawn => enemyPawnSprite,
                PieceType.Amazon => enemyAmazonSprite != null ? enemyAmazonSprite : enemyQueenSprite,
                PieceType.Chancellor => enemyChancellorSprite != null ? enemyChancellorSprite : enemyRookSprite,
                PieceType.Archbishop => enemyArchbishopSprite != null ? enemyArchbishopSprite : enemyBishopSprite,
                PieceType.Centaur => enemyCentaurSprite != null ? enemyCentaurSprite : enemyKnightSprite,
                PieceType.Cannon => enemyCannonSprite != null ? enemyCannonSprite : enemyRookSprite,
                PieceType.Grasshopper => enemyGrasshopperSprite != null ? enemyGrasshopperSprite : enemyQueenSprite,
                _ => enemyPawnSprite
            };
        }
        else
        {
            return type switch
            {
                PieceType.King => kingSprite,
                PieceType.Queen => queenSprite,
                PieceType.Rook => rookSprite,
                PieceType.Bishop => bishopSprite,
                PieceType.Knight => knightSprite,
                PieceType.Pawn => pawnSprite,
                PieceType.Amazon => amazonSprite != null ? amazonSprite : queenSprite,
                PieceType.Chancellor => chancellorSprite != null ? chancellorSprite : rookSprite,
                PieceType.Archbishop => archbishopSprite != null ? archbishopSprite : bishopSprite,
                PieceType.Centaur => centaurSprite != null ? centaurSprite : knightSprite,
                PieceType.Cannon => cannonSprite != null ? cannonSprite : rookSprite,
                PieceType.Grasshopper => grasshopperSprite != null ? grasshopperSprite : queenSprite,
                _ => pawnSprite
            };
        }
    }
}

