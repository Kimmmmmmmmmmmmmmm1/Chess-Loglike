using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 인장 인벤토리의 개별 슬롯. 인장을 표시하고 드롭 타겟 역할을 합니다.
/// </summary>
public class SealInventorySlot : MonoBehaviour, IDropHandler, ISealDropTarget, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private float hoverLiftAmount = 8f;
    [SerializeField] private float hoverDuration = 0.18f;

    private DraggableSeal currentSeal;
    private RectTransform currentSealIconRect;
    private Shadow currentSealShadow;
    private Vector2 currentSealBasePosition;
    private int slotIndex = -1;
    private const float SwapJumpPower = 5f;
    private const float SwapDuration = 0.35f;

    /// <summary>
    /// 슬롯에 인장을 설정하고 UI를 생성합니다.
    /// </summary>
    public void SetSeal(SealData sealData, GameObject draggableSealPrefab, int index)
    {
        slotIndex = index;

        if (sealData == null || draggableSealPrefab == null)
        {
            ClearSeal();
            return;
        }

        // 기존 인장이 있다면 제거
        if (currentSeal != null)
        {
            Destroy(currentSeal.gameObject);
        }

        GameObject sealObj = Instantiate(draggableSealPrefab, transform);
        currentSeal = sealObj.GetComponent<DraggableSeal>();
        if (currentSeal != null)
        {
            currentSeal.Initialize(sealData);
            currentSeal.SetInventoryIndex(index);
            if (currentSeal.SealIconImage != null)
            {
                currentSealIconRect = currentSeal.SealIconImage.GetComponent<RectTransform>();
                currentSealBasePosition = currentSealIconRect.anchoredPosition;
            }

            currentSealShadow = currentSeal.GetComponent<Shadow>();
            if (currentSealShadow == null) currentSealShadow = currentSeal.gameObject.AddComponent<Shadow>();
            currentSealShadow.effectColor = new Color(0, 0, 0, 0.45f);
            currentSealShadow.effectDistance = new Vector2(0, -5f);
            currentSealShadow.enabled = false;
        }
    }

    /// <summary>
    /// 슬롯을 비웁니다.
    /// </summary>
    public void ClearSeal()
    {
        ClearSeal(transform.GetSiblingIndex());
    }

    public void ClearSeal(int index)
    {
        slotIndex = index;

        if (currentSeal != null)
        {
            Destroy(currentSeal.gameObject);
            currentSeal = null;
            currentSealIconRect = null;
            currentSealShadow = null;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentSealIconRect == null) return;
        currentSealIconRect.DOKill();
        currentSealIconRect.DOAnchorPos(currentSealBasePosition + Vector2.up * hoverLiftAmount, hoverDuration).SetEase(Ease.OutQuad);
        if (currentSealShadow != null) currentSealShadow.enabled = true;
        Debug.Log($"Pointer entered slot with seal: {currentSeal?.SealData?.sealName}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentSealIconRect == null) return;
        currentSealIconRect.DOKill();
        currentSealIconRect.DOAnchorPos(currentSealBasePosition, hoverDuration).SetEase(Ease.OutQuad);
        if (currentSealShadow != null) currentSealShadow.enabled = false;
    }

    /// <summary>
    /// 이 슬롯에 UI 요소가 드롭되었을 때 Unity EventSystem에 의해 호출됩니다.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        // DraggableSeal.OnEndDrag에서 드롭 대상 판정과 처리를 한 번만 수행합니다.
    }
    /// <summary>
    /// 다른 인장이 이 슬롯에 드롭되었을 때 처리합니다.
    /// </summary>
    public void HandleSealDrop(DraggableSeal droppedSeal)
    {
        if (droppedSeal == null) return;

        SealInventory inventory = SealInventory.Instance;
        if (inventory == null) return;

        int targetIndex = slotIndex >= 0 ? slotIndex : transform.GetSiblingIndex();
        int sourceIndex = droppedSeal.OriginalInventoryIndex;
        PieceController sourcePiece = droppedSeal.SourcePiece;

        // 인벤토리에서 온 인장은 기물처럼 빈 슬롯으로 이동하거나 차있는 슬롯과 교환합니다.
        if (sourcePiece == null && sourceIndex != -1)
        {
            if (currentSeal == null)
            {
                inventory.MoveSeal(sourceIndex, targetIndex);
                AnimateDroppedSealIntoSlot(droppedSeal);
            }
            else
            {
                AnimateCurrentSealToSourceSlot(droppedSeal);
                AnimateDroppedSealIntoSlot(droppedSeal);
                inventory.SwapSeals(sourceIndex, targetIndex);
            }

            RefreshPanelAfterAnimation();
            droppedSeal.MarkDropHandledSuccessfully();
            return;
        }

        // 기물에서 떼어온 인장은 빈 슬롯에 추가하거나, 차있는 슬롯과 장착 상태를 교환합니다.
        if (currentSeal == null)
        {
            inventory.InsertSeal(droppedSeal.SealData, targetIndex);
            AnimateDroppedSealIntoSlot(droppedSeal);
            RefreshPanelAfterAnimation();
            droppedSeal.MarkDropHandledSuccessfully();
            return;
        }

        SealData inventorySeal = currentSeal.SealData;
        if (!inventory.ReplaceSeal(targetIndex, droppedSeal.SealData)) return;

        AnimateDroppedSealIntoSlot(droppedSeal);
        sourcePiece?.EquipSeal(inventorySeal);
        RefreshPanelAfterAnimation();
        droppedSeal.MarkDropHandledSuccessfully();
    }

    private void AnimateCurrentSealToSourceSlot(DraggableSeal droppedSeal)
    {
        if (currentSeal == null || droppedSeal == null || droppedSeal.OriginalParent == null) return;

        RectTransform sealRect = currentSeal.GetComponent<RectTransform>();
        if (sealRect == null) return;

        currentSeal.transform.SetParent(droppedSeal.OriginalParent, true);
        sealRect.DOKill();
        sealRect.DOJumpAnchorPos(Vector2.zero, SwapJumpPower, 1, SwapDuration).SetEase(Ease.OutQuad);
    }

    private void AnimateDroppedSealIntoSlot(DraggableSeal droppedSeal)
    {
        if (droppedSeal == null) return;

        RectTransform sealRect = droppedSeal.GetComponent<RectTransform>();
        if (sealRect == null) return;

        droppedSeal.transform.SetParent(transform, true);
        sealRect.DOKill();
        sealRect.DOJumpAnchorPos(Vector2.zero, SwapJumpPower, 1, SwapDuration).SetEase(Ease.OutQuad);
    }

    private void RefreshPanelAfterAnimation()
    {
        DOVirtual.DelayedCall(SwapDuration, () =>
        {
            FindFirstObjectByType<SealInventoryPanel>()?.RefreshSealItems();
        });
    }
}
