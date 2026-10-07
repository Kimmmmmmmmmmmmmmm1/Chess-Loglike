using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 인장 인벤토리에서 드래그 가능한 인장 UI 아이템입니다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DraggableSeal : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float ReturnDuration = 0.3f;
    private const float SuccessfulDropDestroyDelay = 0.4f;
    [SerializeField] private Image sealIconImage; // 실제 아이콘을 표시하는 자식 이미지

    public SealData SealData { get; private set; }
    public bool DropHandledSuccessfully { get; private set; }
    public Image SealIconImage => sealIconImage;
    public PieceController SourcePiece => sourcePiece;
    public int OriginalInventoryIndex => originalInventoryIndex;
    public Transform OriginalParent => originalParent;

    private Canvas parentCanvas;
    private Transform originalParent;
    private CanvasGroup canvasGroup;
    private PieceController sourcePiece;
    private bool removedFromPiece;
    private Vector2 initialAnchoredPosition; // 드래그 시작 시의 원래 anchoredPosition
    private int originalInventoryIndex = -1; // 인벤토리에서의 원래 인덱스
    private int assignedInventoryIndex = -1;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        parentCanvas = GetComponentInParent<Canvas>();
        BindSealIconImage();
    }

    public void Initialize(SealData data)
    {
        SealData = data;
        assignedInventoryIndex = -1;
        BindSealIconImage();

        // 루트는 레이캐스트용으로 유지하고, 실제 아이콘은 자식 이미지에만 표시합니다.
        Image rootImage = GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.sprite = null;
            rootImage.color = new Color(rootImage.color.r, rootImage.color.g, rootImage.color.b, 0f);
            rootImage.raycastTarget = true;
        }

        if (sealIconImage != null)
        {
            sealIconImage.enabled = true;
            sealIconImage.sprite = data.icon;
            sealIconImage.color = new Color(sealIconImage.color.r, sealIconImage.color.g, sealIconImage.color.b, 1f);
            sealIconImage.raycastTarget = false;
        }

        // 툴팁 핸들러 추가
        SealTooltipHandler tooltipHandler = gameObject.GetComponent<SealTooltipHandler>();
        if (tooltipHandler == null) tooltipHandler = gameObject.AddComponent<SealTooltipHandler>();
        tooltipHandler.Initialize(data);
    }

    public void SetInventoryIndex(int index)
    {
        assignedInventoryIndex = index;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        DropHandledSuccessfully = false;
        originalInventoryIndex = -1;
        // 전투 준비 단계가 아니면 드래그 불가
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.Prepare)
        {
            eventData.pointerDrag = null;
            return;
        }

        // 만약 이 인장이 기물에 장착된 상태라면, 드래그 시작 시 바로 장착 해제 처리
        sourcePiece = GetComponentInParent<PieceController>();
        removedFromPiece = false;
        if (sourcePiece != null)
        {
            // 드래그 중인 원본 UI가 사라지지 않도록 비주얼 파괴는 OnEndDrag에서 처리합니다.
            removedFromPiece = sourcePiece.UnEquipSeal(SealData, false);
        }

        // 인벤토리에서 온 인장은 기물처럼 성공한 드롭에서만 데이터를 이동/교환합니다.
        if (sourcePiece == null && SealInventory.Instance != null)
        {
            originalInventoryIndex = assignedInventoryIndex;
        }

        initialAnchoredPosition = GetComponent<RectTransform>().anchoredPosition; // 드래그 시작 시의 원래 위치 저장
        originalParent = transform.parent;

        // 드래그 동안 최상위 캔버스에서 렌더링되도록 부모 변경
        transform.SetParent(parentCanvas.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false; // 드롭 이벤트를 뒤에 있는 오브젝트가 받도록 함
    }

    public void OnDrag(PointerEventData eventData)
    {
        // RectTransformUtility를 사용하여 모든 Canvas Render Mode에서 정확한 좌표를 계산합니다.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform.parent as RectTransform,
            eventData.position,
            parentCanvas.worldCamera,
            out Vector2 localPoint
        );
        GetComponent<RectTransform>().anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        GameObject droppedOn = eventData.pointerEnter;
        ISealDropTarget dropTarget = (droppedOn != null) ? droppedOn.GetComponentInParent<ISealDropTarget>() : null;

        // 드롭 대상이 인벤토리의 기물인지 확인
        PieceController targetPiece = (dropTarget as Component)?.GetComponent<PieceController>();
        // 드롭 대상이 인벤토리 기물이거나, 적 기물인 경우 드롭을 허용하지 않음
        bool isInvalidDropTarget = targetPiece != null && (targetPiece.CurrentLocation == PieceLocation.Inventory || targetPiece.IsEnemy);

        // 유효한 드롭 대상에게만 처리를 위임
        if (dropTarget != null && !isInvalidDropTarget)
        {
            // 드롭 대상에게 처리 위임
            dropTarget.HandleSealDrop(this);
            if (DropHandledSuccessfully)
            {
                DOVirtual.DelayedCall(SuccessfulDropDestroyDelay, () =>
                {
                    if (gameObject != null) Destroy(gameObject);
                });
            }
            else
            {
                RestoreToSource(); // 실패 시 복원
            }
        }
        else
        {
            // 드롭에 실패했거나, 인벤토리 기물 위에 드롭한 경우
            RestoreToSource();
        }

        // 드롭이 성공적으로 처리되어 이 오브젝트가 파괴될 예정이 아니라면 레이캐스트를 다시 활성화합니다.
        if (!DropHandledSuccessfully && canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void BindSealIconImage()
    {
        if (sealIconImage != null)
        {
            return;
        }

        if (transform.childCount > 0)
        {
            sealIconImage = GetComponentInChildren<Image>(true);
            if (sealIconImage != null && sealIconImage.gameObject == gameObject)
            {
                sealIconImage = null;
            }
        }

        if (sealIconImage == null)
        {
            GameObject iconObject = new GameObject("SealIcon");
            iconObject.transform.SetParent(transform, false);

            RectTransform iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            sealIconImage = iconObject.AddComponent<Image>();
            sealIconImage.raycastTarget = false;
        }
    }

    public void MarkDropHandledSuccessfully()
    {
        DropHandledSuccessfully = true;
    }

    private void RestoreToSource()
    {
        // 상호작용 다시 활성화
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        // 애니메이션을 위해 최상단으로 렌더링 순서 조정
        transform.SetAsLastSibling();

        // 원래 부모로 복귀
        transform.SetParent(originalParent, true);

        // 제자리로 돌아가는 애니메이션 (PieceController의 ReturnToOriginalPosition 참고)
        var rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.DOAnchorPos(initialAnchoredPosition, ReturnDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    // 애니메이션 완료 후 데이터 복구
                    RestoreData();
                    // 이 DraggableSeal 오브젝트는 파괴하고, SealInventoryPanel이 새로 UI를 그리도록 함
                    if (gameObject != null) Destroy(gameObject);
                });
        }
        else
        {
            // RectTransform이 없는 예외적인 경우, 즉시 복구
            RestoreData();
            if (gameObject != null) Destroy(gameObject);
        }
    }

    private void RestoreData()
    {
        if (removedFromPiece && sourcePiece != null)
        {
            // 기물에서 떼어낸 경우, 다시 장착
            sourcePiece.EquipSeal(SealData);
        }
        else if (SealInventory.Instance != null && originalInventoryIndex == -1)
        {
            SealInventory.Instance.AddSeal(SealData);

            // UI 새로고침
            FindFirstObjectByType<SealInventoryPanel>()?.RefreshSealItems();
        }
        else
        {
            FindFirstObjectByType<SealInventoryPanel>()?.RefreshSealItems();
        }
    }
/// <summary>
    /// 인장을 무조건 인벤토리로 되돌려 보내는 메서드입니다.
    /// 기물에서 인장을 장착 해제하거나 허공에 드롭했을 때 호출하기 좋습니다.
    /// </summary>
    public void ReturnToSource()
    {
        // OnEndDrag 등 다른 이벤트에서 Drop 로직이 중복 처리되지 않도록 마킹
        MarkDropHandledSuccessfully();

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        // 도착지가 될 인벤토리 패널 찾기
        SealInventoryPanel inventoryPanel = FindFirstObjectByType<SealInventoryPanel>();
        
        if (inventoryPanel != null)
        {
            // 애니메이션 중 다른 UI에 가려지지 않도록 최상단 캔버스로 이동
            transform.SetParent(parentCanvas.transform, true);
            transform.SetAsLastSibling();

            // 인벤토리 패널의 실제 위치(월드 좌표)로 부드럽게 날아가는 닷트윈 애니메이션
            transform.DOMove(inventoryPanel.transform.position, ReturnDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(CompleteReturnToSource);
        }
        else
        {
            // 인벤토리 패널을 찾지 못한 경우 즉시 데이터만 복구
            //CompleteReturnToSource();
        }
    }

    /// <summary>
    /// 인벤토리 복귀 애니메이션이 끝난 후 데이터를 갱신합니다.
    /// </summary>
    private void CompleteReturnToSource()
    {
        // 1. 인벤토리 싱글톤 데이터에 인장 다시 추가
        if (SealInventory.Instance != null && SealData != null)
        {
            SealInventory.Instance.AddSeal(SealData);
        }

        // 2. 인벤토리 UI 갱신 (새로 들어온 인장을 화면에 그려줌)
        FindFirstObjectByType<SealInventoryPanel>()?.RefreshSealItems();

        // 3. 복귀 연출이 끝난 현재 드래그 UI 오브젝트 파괴
        if (gameObject != null) Destroy(gameObject);
    }
}
