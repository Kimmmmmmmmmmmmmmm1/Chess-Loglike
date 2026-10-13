using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using DG.Tweening;

public class MousePointer : MonoBehaviour
{
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Tweener distanceTween;
    private bool isHoveringPiece;
    private RectTransform[] corners;
    private float currentAnimBaseDist;
    private float currentDist;
    private bool isMouseDown = false;
    private ArtifactSlot currentHoveredArtifact;
    private Vector2Int? currentGridPos;

    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 20f;
    [SerializeField] private float baseDistanceMultiplier = 0.25f;
    [SerializeField] private float hoverDistanceMultiplier = 0.45f;
    [SerializeField] private float pulseStrengthMultiplier = 0.1f;

    private float BaseDistance => PieceManager.Instance?.gridManager?.cellSize.x * baseDistanceMultiplier ?? 12f;
    private float HoverDistance => PieceManager.Instance?.gridManager?.cellSize.x * hoverDistanceMultiplier ?? 21f;
    private float PulseStrength => PieceManager.Instance?.gridManager?.cellSize.x * pulseStrengthMultiplier ?? 4.5f;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        CreateCorners();
        currentDist = BaseDistance;
        currentAnimBaseDist = BaseDistance;
        StartPulseAnimation(BaseDistance);
    }

    private void CreateCorners()
    {
        Image mainImage = GetComponent<Image>();
        Sprite sprite = mainImage != null ? mainImage.sprite : null;
        Color color = mainImage != null ? mainImage.color : Color.white;
        Vector2 size = rectTransform.sizeDelta;

        if (mainImage != null)
        {
            mainImage.enabled = false; // 메인 이미지는 숨김
            mainImage.raycastTarget = false;
        }

        corners = new RectTransform[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject go = new GameObject($"Corner_{i}");
            go.transform.SetParent(transform, false);
            
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            
            corners[i] = rt;
        }

        // 초기 회전 설정 (스프라이트가 모서리 형태일 경우를 대비해 4방향 회전)
        // 0:좌상, 1:우상, 2:좌하, 3:우하
        corners[0].localRotation = Quaternion.Euler(0, 0, 0);
        corners[1].localRotation = Quaternion.Euler(0, 0, -90);
        corners[2].localRotation = Quaternion.Euler(0, 0, 90);
        corners[3].localRotation = Quaternion.Euler(0, 0, 180);
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
        {
            SetCornersActive(false);
            return;
        }

        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null || rectTransform == null || parentCanvas == null)
            return;

        // GridManager의 gridCellsParent를 기준으로 마우스 위치를 로컬 좌표로 변환
        GridManager gridManager = PieceManager.Instance.gridManager;
        RectTransform referenceRect = gridManager.gridCellsParent;
        if (referenceRect == null)
            return;

        Vector2 localPoint;
        Camera cam = null;
        if (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera || parentCanvas.renderMode == RenderMode.WorldSpace)
        {
            cam = parentCanvas.worldCamera;
            if (cam == null) cam = Camera.main;
        }

        Vector2 screenPos = Vector2.zero;
        bool inputFound = false;

        if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
            inputFound = true;
        }
        else if (Pointer.current != null)
        {
            screenPos = Pointer.current.position.ReadValue();
            inputFound = true;
        }

        if (!inputFound)
        {
            try 
            { 
                screenPos = Input.mousePosition; 
                inputFound = true;
            } 
            catch { /* Ignore */ }
        }

        if (!inputFound)
        {
            return;
        }

        // 마우스 누름/뗌 애니메이션
        bool isPressed = false;
        if (Mouse.current != null)
        {
            isPressed = Mouse.current.leftButton.isPressed;
        }
        else
        {
            try
            {
                isPressed = Input.GetMouseButton(0);
            }
            catch { /* Ignore */ }
        }

        bool isDragging = PieceController.IsAnyDragging;
        bool isHolding = isPressed || isDragging;

        if (isHolding && !isMouseDown)
        {
            isMouseDown = true;
            AnimatePressDown();
        }
        else if (!isHolding && isMouseDown)
        {
            isMouseDown = false;
            AnimatePressUp();
        }

        // 인벤토리 슬롯 체크
        if (EventSystem.current != null)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (RaycastResult result in results)
            {
                InventorySlot slot = result.gameObject.GetComponent<InventorySlot>();
                if (slot == null) slot = result.gameObject.GetComponentInParent<InventorySlot>();

                if (slot != null)
                {
                    currentGridPos = null;
                    if (currentHoveredArtifact != null)
                    {
                        currentHoveredArtifact.HideTooltip();
                        currentHoveredArtifact = null;
                    }

                    SetCornersActive(true);
                    if (Vector3.Distance(rectTransform.position, slot.transform.position) < 0.2f)
                    {
                        rectTransform.position = slot.transform.position;
                    }
                    else
                    {
                        rectTransform.position = Vector3.Lerp(rectTransform.position, slot.transform.position, Time.deltaTime * smoothSpeed);
                    }
                    
                    PieceController piece = slot.GetComponentInChildren<PieceController>();
                    UpdatePointerState(piece != null, isDragging, piece);
                    return;
                }

                SealInventorySlot sealSlot = result.gameObject.GetComponent<SealInventorySlot>();
                if (sealSlot == null) sealSlot = result.gameObject.GetComponentInParent<SealInventorySlot>();
                if (sealSlot != null)
                {
                    currentGridPos = null;
                    SetCornersActive(true);
                    if (Vector3.Distance(rectTransform.position, sealSlot.transform.position) < 0.2f)
                    {
                        rectTransform.position = sealSlot.transform.position;
                    }
                    else
                    {
                        rectTransform.position = Vector3.Lerp(rectTransform.position, sealSlot.transform.position, Time.deltaTime * smoothSpeed);
                    }
                    UpdatePointerState(true, isDragging, null, true);
                    return;
                }

                if (!result.gameObject.TryGetComponent<ArtifactSlot>(out var artifactSlot)) artifactSlot = result.gameObject.GetComponentInParent<ArtifactSlot>();

                if (artifactSlot != null)
                {
                    currentGridPos = null;
                    if (currentHoveredArtifact != artifactSlot)
                    {
                        if (currentHoveredArtifact != null) currentHoveredArtifact.HideTooltip();
                        currentHoveredArtifact = artifactSlot;
                        currentHoveredArtifact.ShowTooltip();
                    }

                    SetCornersActive(true);
                    if (Vector3.Distance(rectTransform.position, artifactSlot.transform.position) < 0.2f)
                    {
                        rectTransform.position = artifactSlot.transform.position;
                    }
                    else
                    {
                        rectTransform.position = Vector3.Lerp(rectTransform.position, artifactSlot.transform.position, Time.deltaTime * smoothSpeed);
                    }
                    UpdatePointerState(true, isDragging, null, true);
                    return;
                }
            }
        }

        if (currentHoveredArtifact != null)
        {
            currentHoveredArtifact.HideTooltip();
            currentHoveredArtifact = null;
        }

        if (!gridManager.IsGridCreated)
        {
            currentGridPos = null;
            SetCornersActive(false);
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(referenceRect, screenPos, cam, out localPoint))
        {
            // 보드 원점을 기준으로 그리드 좌표 계산 (타일 경계 지터 방지를 위한 히스테리시스 적용)
            Vector2Int? gridPos = GetGridPositionWithHysteresis(gridManager, localPoint);

            // 그리드 범위 내에 있는지 확인
            if (gridPos.HasValue)
            {
                SetCornersActive(true);
                
                // 그리드 위치를 UI 위치(AnchoredPosition)로 변환 후 월드 좌표로 적용
                Vector2 targetAnchoredPos = gridManager.GridToUiPosition(gridPos.Value);
                Vector3 worldPos = referenceRect.TransformPoint(targetAnchoredPos);
                
                // 부드러운 이동 (목표 위치에 매우 가까우면 스냅하여 미세 지터 방지)
                if (Vector3.Distance(rectTransform.position, worldPos) < 0.2f)
                {
                    rectTransform.position = worldPos;
                }
                else
                {
                    rectTransform.position = Vector3.Lerp(rectTransform.position, worldPos, Time.deltaTime * smoothSpeed);
                }

                PieceController piece = PieceManager.Instance.GetPieceAt(gridPos.Value);
                UpdatePointerState(piece != null, isDragging, piece);
            }
            else
            {
                // 보드 밖이면 숨김
                currentGridPos = null;
                SetCornersActive(false);
            }
        }
    }

    private Vector2Int? GetGridPositionWithHysteresis(GridManager gridManager, Vector2 localPoint)
    {
        Vector2Int? nearest = gridManager.GetNearestGridPosition(localPoint);
        if (!nearest.HasValue)
        {
            currentGridPos = null;
            return null;
        }

        if (!currentGridPos.HasValue)
        {
            currentGridPos = nearest;
            return nearest;
        }

        if (currentGridPos.Value == nearest.Value)
        {
            return currentGridPos;
        }

        if (!gridManager.IsInBounds(currentGridPos.Value))
        {
            currentGridPos = nearest;
            return nearest;
        }

        Vector2 currentCellCenter = gridManager.GridToUiPosition(currentGridPos.Value);
        Vector2 nearestCellCenter = gridManager.GridToUiPosition(nearest.Value);

        float distToCurrent = Vector2.Distance(localPoint, currentCellCenter);
        float distToNearest = Vector2.Distance(localPoint, nearestCellCenter);

        // 새 셀로 전환하려면 마우스가 현재 셀보다 새 셀에 threshold 이상 더 가까워야 함 (히스테리시스)
        float threshold = gridManager.cellSize.x * 0.15f;
        if (distToNearest < distToCurrent - threshold)
        {
            currentGridPos = nearest;
        }

        return currentGridPos;
    }

    private void UpdatePointerState(bool hovering, bool isDragging, PieceController piece, bool isArtifact = false)
    {
        float targetBaseDist = BaseDistance;

        if (isDragging)
        {
            // 드래그(기물 잡기) 중에는 아래 기물 타입과 무관하게 균일한 크기로 고정하여 크기 널뛰기/지터 방지
            targetBaseDist = HoverDistance;
        }
        else if (hovering)
        {
            if (piece != null)
            {
                float offset = 0f;
                switch (piece.Type)
                {
                    case PieceType.King:
                        offset = 4f; // 왕은 4픽셀 더 크게
                        break;
                    case PieceType.Pawn:
                        offset = -4f; // 폰은 4픽셀 더 작게
                        break;
                    default:
                        offset = 0f;
                        break;
                }
                targetBaseDist = HoverDistance + offset;
            }
            else if (isArtifact)
            {
                targetBaseDist = HoverDistance - 4f;
            }
        }

        bool isHolding = isMouseDown || isDragging;
        bool stateChanged = (hovering || isDragging) != isHoveringPiece;
        bool distChanged = Mathf.Abs(targetBaseDist - currentAnimBaseDist) > 0.1f;

        // 상태가 바뀌거나 타겟 크기가 크게 변했을 때 애니메이션 갱신
        if (stateChanged || distChanged)
        {
            isHoveringPiece = (hovering || isDragging);
            currentAnimBaseDist = targetBaseDist;
            
            if (isHolding)
                AnimatePressDown();
            else
                StartPulseAnimation(targetBaseDist);
        }
    }

    private void SetCornersActive(bool active)
    {
        if (corners == null) return;
        foreach (var c in corners) c.gameObject.SetActive(active);
    }

    private void AnimatePressDown()
    {
        distanceTween?.Kill();
        float targetDist = Mathf.Max(0f, currentAnimBaseDist - 4f);
        distanceTween = DOTween.To(() => currentDist, x => { currentDist = x; UpdateCornerPositions(currentDist); }, targetDist, 0.1f).SetEase(Ease.OutQuad);
    }

    private void AnimatePressUp()
    {
        distanceTween?.Kill();
        distanceTween = DOTween.To(() => currentDist, x => { currentDist = x; UpdateCornerPositions(currentDist); }, currentAnimBaseDist, 0.2f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => {
                if (!isMouseDown && !PieceController.IsAnyDragging)
                {
                    StartPulseAnimation(currentAnimBaseDist);
                }
            });
    }

    private void StartPulseAnimation(float centerDist)
    {
        if (isMouseDown || PieceController.IsAnyDragging)
        {
            return;
        }

        distanceTween?.Kill();
        
        currentDist = centerDist;
        distanceTween = DOTween.To(() => currentDist, x => {
            currentDist = x;
            UpdateCornerPositions(currentDist);
        }, centerDist + PulseStrength, 0.6f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void UpdateCornerPositions(float dist)
    {
        if (corners == null || corners.Length < 4) return;

        corners[0].anchoredPosition = new Vector2(-dist, dist);   // 좌상
        corners[1].anchoredPosition = new Vector2(dist, dist);    // 우상
        corners[2].anchoredPosition = new Vector2(-dist, -dist);  // 좌하
        corners[3].anchoredPosition = new Vector2(dist, -dist);   // 우하
    }

    // 그리드 범위 체크는 gridManager.IsInBounds로 대체

    private void OnDestroy()
    {
        distanceTween?.Kill();
    }

    private void OnDisable()
    {
        if (currentHoveredArtifact != null)
        {
            currentHoveredArtifact.HideTooltip();
            currentHoveredArtifact = null;
        }
    }
}
