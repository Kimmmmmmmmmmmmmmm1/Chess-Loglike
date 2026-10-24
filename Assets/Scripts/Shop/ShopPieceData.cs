using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopPieceData", menuName = "Janggi/Shop Piece Data")]
public class ShopPieceData : ScriptableObject
{
    [System.Serializable]
    public class PieceInfo
    {
        public PieceType pieceType;
        [Tooltip("기물 점수 (가격 = 점수 * 10)")]
        public int Price;
        [Tooltip("상점 등장 확률 가중치 (높을수록 자주 등장)")]
        public int weight;

        // 가격은 점수 * 10
        public int score => Price / 10;
    }

    public List<PieceInfo> pieceList;

    public static bool IsRarePiece(PieceType type)
    {
        return type == PieceType.Rook ||
               type == PieceType.Queen ||
               type == PieceType.Chancellor ||
               type == PieceType.Archbishop ||
               type == PieceType.Centaur ||
               type == PieceType.Amazon;
    }

    // 가중치 기반 랜덤 기물 뽑기
    public PieceInfo GetRandomPiece(bool excludeLegendary = false)
    {
        if (pieceList == null || pieceList.Count == 0) return null;

        var pool = excludeLegendary
            ? pieceList.FindAll(p => p.pieceType != PieceType.Amazon)
            : pieceList;

        if (pool.Count == 0) return null;

        float rareBonus = ArtifactEffectHandlers.GetRareChanceBonus();
        float rareMult = 1f + (rareBonus / 100f);

        float totalWeight = 0f;
        List<float> weights = new List<float>(pool.Count);
        foreach (var piece in pool)
        {
            float w = piece.weight;
            if (IsRarePiece(piece.pieceType))
            {
                w *= rareMult;
            }
            weights.Add(w);
            totalWeight += w;
        }

        if (totalWeight <= 0f) return pool[Random.Range(0, pool.Count)];

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < pool.Count; i++)
        {
            currentWeight += weights[i];
            if (randomValue < currentWeight)
            {
                return pool[i];
            }
        }

        return pool[pool.Count - 1];
    }
}
