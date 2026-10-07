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
    public PieceInfo GetRandomPiece()
    {
        if (pieceList == null || pieceList.Count == 0) return null;

        int totalWeight = 0;
        foreach (var piece in pieceList) totalWeight += piece.weight;

        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var piece in pieceList)
        {
            currentWeight += piece.weight;
            if (randomValue < currentWeight)
            {
                return piece;
            }
        }

        return pieceList[pieceList.Count - 1];
    }
}
