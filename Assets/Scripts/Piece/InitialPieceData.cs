using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewInitialPieceData", menuName = "Janggi/Initial Piece Data")]
public class InitialPieceData : ScriptableObject
{
    [System.Serializable]
    public struct InitialPieceInfo
    {
        public PieceType pieceType;
    }

    [Tooltip("게임 시작 시 인벤토리에 지급할 기물 목록")]
    public List<InitialPieceInfo> initialPieces = new List<InitialPieceInfo>();
}