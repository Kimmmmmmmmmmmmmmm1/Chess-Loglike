using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 기물 합성용 슬롯: IDropHandler를 구현하여 드래그된 기물을 받을 수 있습니다.
/// </summary>
public class SynthesisSlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private int slotIndex = 0; // 0 = Slot1, 1 = Slot2
    [SerializeField] private Image slotImage;
    [SerializeField] private Color emptyColor = Color.gray;
    [SerializeField] private Color filledColor = Color.white;

    private PiecesSynthManager manager;

    private void Start()
    {
        EnsureManager();

        if (slotImage == null)
        {
            slotImage = GetComponent<Image>();
        }

        UpdateVisuals(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        // 드래그된 오브젝트 찾기
        GameObject draggedObject = eventData.pointerDrag;
        if (draggedObject == null) return;

        PieceController piece = draggedObject.GetComponent<PieceController>();
        if (piece == null) return;

        piece.MarkSynthesisDropHandled();

        // 적 기물은 드래그할 수 없음
        if (piece.IsEnemy)
        {
            return;
        }

        // 합성 슬롯은 인벤토리 기물만 받도록 제한합니다.
        // 보드 기물을 직접 꽂아넣으면 데이터/부모 관계가 꼬여 중복 저장이 발생할 수 있습니다.
        if (piece.CurrentLocation != PieceLocation.Inventory)
        {
            return;
        }

        if (!EnsureManager()) return;

        PieceController currentPiece = manager.GetPieceInSlot(slotIndex);
        if (currentPiece == piece)
        {
            return;
        }

        SynthesisSlot sourceSlot = piece.OriginalParent != null ? piece.OriginalParent.GetComponent<SynthesisSlot>() : null;

        // 슬롯이 비어있으면 드래그한 기물을 합성 슬롯으로 날려 보냄
        if (currentPiece == null)
        {
            SetPieceDirect(piece);
            if (!manager.TryRestorePieceToSynthesisSlot(piece, slotIndex, false))
            {
                SetPieceDirect(piece);
            }

            UpdateVisuals(true);
            return;
        }

        // 슬롯이 차있으면: 같은 작업장 슬롯 교환 or 인벤토리 기물과 교체
        if (sourceSlot != null && sourceSlot != this)
        {
            // 드래그한 기물은 즉시 배치, 밀려나는 기물만 애니메이션 이동
            manager.AssignPieceToSynthesisSlot(slotIndex, piece);
            manager.AssignPieceToSynthesisSlot(sourceSlot.SlotIndex, currentPiece);

            bool draggedPlaced = manager.TryRestorePieceToSynthesisSlot(piece, slotIndex, false);
            bool displacedMoved = manager.TryRestorePieceToSynthesisSlot(currentPiece, sourceSlot.SlotIndex, true);

            if (!draggedPlaced || !displacedMoved)
            {
                return;
            }
        }
        else
        {
            // 드래그한 기물은 즉시 배치, 기존(밀려나는) 기물만 인벤토리로 애니메이션 이동
            bool moved = manager.TryMovePieceToInventory(currentPiece, piece.OriginalParent, true);
            if (!moved)
            {
                return;
            }

            SetPieceDirect(piece);
            if (!manager.TryRestorePieceToSynthesisSlot(piece, slotIndex, false))
            {
                return;
            }
        }

        UpdateVisuals(true);
    }
    /// <summary>
    /// 슬롯에서 기물을 제거합니다 (기물이 드래그로 빠져나갈 때 호출)
    /// </summary>
    public void RemovePiece()
    {
        if (EnsureManager())
        {
            SetPieceToSlot(null);

            UpdateVisuals(false);
        }
    }

    /// <summary>
    /// 실패한 드래그에서 기물이 다시 돌아왔을 때 슬롯에 기물을 복구합니다
    /// </summary>
    public void RestorePiece(PieceController piece, bool animateFly = false)
    {
        if (piece == null) return;

        if (EnsureManager())
        {
            if (animateFly)
            {
                if (!manager.TryRestorePieceToSynthesisSlot(piece, slotIndex, true))
                {
                    SetPieceToSlot(piece);
                }
            }
            else
            {
                SetPieceToSlot(piece);
            }

            UpdateVisuals(true);
        }
    }

    private void UpdateVisuals(bool isFilled)
    {
        if (slotImage != null)
        {
            slotImage.color = isFilled ? filledColor : emptyColor;
        }
    }

    public void SetVisualFilled(bool isFilled)
    {
        UpdateVisuals(isFilled);
    }

    public void SetPieceDirect(PieceController piece)
    {
        SetPieceToSlot(piece);
        UpdateVisuals(piece != null);
    }

    private bool EnsureManager()
    {
        if (manager != null)
        {
            return true;
        }

        manager = PiecesSynthManager.Instance;
        if (manager == null)
        {
            manager = FindFirstObjectByType<PiecesSynthManager>();
        }

        return manager != null;
    }

    public int SlotIndex => slotIndex;

    private void SetPieceToSlot(PieceController piece)
    {
        if (slotIndex == 0)
        {
            manager.SetSlot1Piece(piece);
        }
        else if (slotIndex == 1)
        {
            manager.SetSlot2Piece(piece);
        }
    }
}
