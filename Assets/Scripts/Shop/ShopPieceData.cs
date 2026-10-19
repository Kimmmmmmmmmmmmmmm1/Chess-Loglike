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

    // 가중치 기반 랜덤 기물 뽑기
    public PieceInfo GetRandomPiece(bool excludeLegendary = false)
    {
        if (pieceList == null || pieceList.Count == 0) return null;

        var pool = excludeLegendary
            ? pieceList.FindAll(p => p.pieceType != PieceType.Amazon)
            : pieceList;

        if (pool.Count == 0) return null;

        int totalWeight = 0;
        foreach (var piece in pool) totalWeight += piece.weight;

        if (totalWeight <= 0) return pool[Random.Range(0, pool.Count)];

        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var piece in pool)
        {
            currentWeight += piece.weight;
            if (randomValue < currentWeight)
            {
                return piece;
            }
        }

        return pool[pool.Count - 1];
    }
}
