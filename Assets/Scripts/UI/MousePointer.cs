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

        if (isPressed && !isMouseDown)
        {
            isMouseDown = true;
            AnimatePressDown();
        }
        else if (!isPressed && isMouseDown)
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
                    if (currentHoveredArtifact != null)
                    {
                        currentHoveredArtifact.HideTooltip();
                        currentHoveredArtifact = null;
                    }

                    SetCornersActive(true);
                    rectTransform.position = Vector3.Lerp(rectTransform.position, slot.transform.position, Time.deltaTime * smoothSpeed);
                    
                    PieceController piece = slot.GetComponentInChildren<PieceController>();
                    UpdatePointerState(piece != null, PieceController.IsAnyDragging, piece);
                    return;
                }

                SealInventorySlot sealSlot = result.gameObject.GetComponent<SealInventorySlot>();
                if (sealSlot == null) sealSlot = result.gameObject.GetComponentInParent<SealInventorySlot>();
                if (sealSlot != null)
                {
                    SetCornersActive(true);
                    rectTransform.position = Vector3.Lerp(rectTransform.position, sealSlot.transform.position, Time.deltaTime * smoothSpeed);
                    UpdatePointerState(true, PieceController.IsAnyDragging, null, true);
                    return;
                }

                if (!result.gameObject.TryGetComponent<ArtifactSlot>(out var artifactSlot)) artifactSlot = result.gameObject.GetComponentInParent<ArtifactSlot>();

                if (artifactSlot != null)
                {
                    if (currentHoveredArtifact != artifactSlot)
                    {
                        if (currentHoveredArtifact != null) currentHoveredArtifact.HideTooltip();
                        currentHoveredArtifact = artifactSlot;
                        currentHoveredArtifact.ShowTooltip();
                    }

                    SetCornersActive(true);
                    rectTransform.position = Vector3.Lerp(rectTransform.position, artifactSlot.transform.position, Time.deltaTime * smoothSpeed);
                    UpdatePointerState(true, PieceController.IsAnyDragging, null, true);
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
            SetCornersActive(false);
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(referenceRect, screenPos, cam, out localPoint))
        {
            // 보드 원점을 기준으로 그리드 좌표 계산
            Vector2Int? gridPos = gridManager.GetNearestGridPosition(localPoint);

            // 그리드 범위 내에 있는지 확인
            if (gridPos.HasValue)
            {
                SetCornersActive(true);
                
                // 그리드 위치를 UI 위치(AnchoredPosition)로 변환 후 월드 좌표로 적용
                Vector2 targetAnchoredPos = gridManager.GridToUiPosition(gridPos.Value);
                Vector3 worldPos = referenceRect.TransformPoint(targetAnchoredPos);
                
                // 부드러운 이동
                rectTransform.position = Vector3.Lerp(rectTransform.position, worldPos, Time.deltaTime * smoothSpeed);

                PieceController piece = PieceManager.Instance.GetPieceAt(gridPos.Value);
                UpdatePointerState(piece != null, PieceController.IsAnyDragging, piece);
            }
            else
            {
                // 보드 밖이면 숨김
                SetCornersActive(false);
            }
        }
    }

    private void UpdatePointerState(bool hovering, bool isDragging, PieceController piece, bool isArtifact = false)
    {
        float targetBaseDist = BaseDistance;

        if (hovering)
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
        else if (isDragging)
        {
            targetBaseDist = HoverDistance;
        }
        
        // 상태가 바뀌거나 타겟 크기가 크게 변했을 때 애니메이션 갱신
        if ((hovering || isDragging) != isHoveringPiece || Mathf.Abs(targetBaseDist - currentAnimBaseDist) > 0.1f)
        {
            isHoveringPiece = (hovering || isDragging);
            currentAnimBaseDist = targetBaseDist;
            
            if (isMouseDown)
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
        // 기존 비율(0.4배) 대신 현재 거리에서 4픽셀만큼만 안쪽으로 모이도록 수정
        float targetDist = Mathf.Max(0f, currentAnimBaseDist - 4f);
        distanceTween = DOTween.To(() => currentDist, x => { currentDist = x; UpdateCornerPositions(currentDist); }, targetDist, 0.1f).SetEase(Ease.OutQuad);
    }

    private void AnimatePressUp()
    {
        distanceTween?.Kill();
        distanceTween = DOTween.To(() => currentDist, x => { currentDist = x; UpdateCornerPositions(currentDist); }, currentAnimBaseDist, 0.2f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => StartPulseAnimation(currentAnimBaseDist));
    }

    private void StartPulseAnimation(float centerDist)
    {
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
