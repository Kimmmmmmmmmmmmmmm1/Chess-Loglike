using UnityEngine;

public class InventorySlot : MonoBehaviour
{
    public bool IsReserved { get; set; }

    public bool TryAddPiece(PieceController piece)
    {
        if (IsReserved) return false;

        if (piece == null) return false;

        // 현재 슬롯에 있는 기물 확인
        PieceController currentPiece = GetComponentInChildren<PieceController>();

        // 같은 기물을 같은 슬롯에 다시 놓는 경우는 아무 것도 하지 않음
        if (currentPiece == piece)
        {
            return true;
        }

        // 슬롯이 비어있으면 바로 이동
        if (currentPiece == null)
        {
            piece.MoveToInventory(transform);
            return true;
        }

        // 슬롯이 차있고, 드래그한 기물도 인벤토리에 있던 것이라면 서로 교체 (Swap)
        if (piece.CurrentLocation == PieceLocation.Inventory)
        {
            // 드래그 중에는 부모가 바뀌어 있으므로 저장해둔 원래 부모(슬롯)를 사용
            Transform originalSlot = piece.OriginalParent;
            SynthesisSlot originalSynthesisSlot = originalSlot != null ? originalSlot.GetComponent<SynthesisSlot>() : null;
            
            // 밀려나는 기물은 원래 슬롯으로 점프 연출 후 복귀
            if (originalSlot != null)
            {
                PieceManager.MovePieceToTransformWithJump(currentPiece, originalSlot, 5f, 0.35f, () =>
                {
                    if (currentPiece != null)
                    {
                        if (originalSynthesisSlot != null)
                        {
                            originalSynthesisSlot.SetPieceDirect(currentPiece);
                        }
                    }
                });
            }

            piece.MoveToInventory(transform);
            return true;
        }

        // 2. 드래그한 기물이 장기판(Board)에 있던 것이라면 교체 (Board <-> Inventory)
        if (piece.CurrentLocation == PieceLocation.Board && piece.gridPosition.HasValue)
        {
            // 장기판에 있던 기물의 원래 위치
            Vector2Int boardPos = piece.gridPosition.Value;

            // 장기판 기물 -> 인벤토리 (현재 슬롯)
            piece.MoveToInventory(transform);

            // 인벤토리 기물 -> 장기판 (원래 위치)
            currentPiece.MoveToGrid(boardPos);
            
            return true;
        }

        return false;
    }
}
