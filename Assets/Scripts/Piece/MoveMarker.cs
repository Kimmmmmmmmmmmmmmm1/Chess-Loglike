using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class MoveMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Sprites")]
    [Tooltip("일반 이동 위치에 표시할 스프라이트 (비어있으면 기본 스프라이트 유지)")]
    [SerializeField] private Sprite normalSprite;
    [Tooltip("적 기물을 잡을 수 있는 위치에 표시할 캡처 스프라이트")]
    [SerializeField] private Sprite captureSprite;

    [Header("Sizes")]
    [Tooltip("일반 마커 시각적 이미지 크기")]
    [SerializeField] private Vector2 markerDotSize = new Vector2(12f, 12f);
    [Tooltip("적 기물 캡처 마커 시각적 이미지 크기")]
    [SerializeField] private Vector2 captureDotSize = new Vector2(28f, 28f);

    [Header("References")]
    [SerializeField] private Image markerVisualImage;

    private PieceManager manager;
    private Vector2Int gridPosition;
    private bool isHovered;
    private bool isCapture;
    private Color baseColor;

    public Vector2Int GridPosition => gridPosition;
    public bool IsHovered => isHovered;
    public bool IsCapture => isCapture;

    private void Awake()
    {
        EnsureVisualSetup();
    }

    private void EnsureVisualSetup()
    {
        // 불필요한 Button 컴포넌트가 있다면 비활성화(색상 덮어쓰기 방지)
        if (TryGetComponent<Button>(out var button))
        {
            button.enabled = false;
        }

        TryGetComponent<Image>(out var rootImage);

        // markerVisualImage가 null이거나, 루트 GameObject의 Image로 잘못 연결되어 있는 경우
        if (markerVisualImage == null || (rootImage != null && markerVisualImage == rootImage))
        {
            // 자식 중에 이미 Visual이 있는지 확인
            Transform existingVisual = transform.Find("Visual");
            if (existingVisual != null && existingVisual.TryGetComponent<Image>(out var existingImage))
            {
                markerVisualImage = existingImage;
            }
            else
            {
                if (normalSprite == null && rootImage != null && rootImage.sprite != null)
                {
                    normalSprite = rootImage.sprite;
                }

                // 자식 Visual 오브젝트 생성
                GameObject visualObj = new GameObject("Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                visualObj.transform.SetParent(transform, false);

                RectTransform visualRect = visualObj.GetComponent<RectTransform>();
                visualRect.anchorMin = new Vector2(0.5f, 0.5f);
                visualRect.anchorMax = new Vector2(0.5f, 0.5f);
                visualRect.pivot = new Vector2(0.5f, 0.5f);
                visualRect.anchoredPosition = Vector2.zero;

                markerVisualImage = visualObj.GetComponent<Image>();
                markerVisualImage.raycastTarget = false; // 실제 마우스 감지는 부모(타일 크기)가 처리
            }

            // 루트 Image는 타일 전체 크기 마우스 감지용 투명 영역으로 설정
            if (rootImage != null)
            {
                rootImage.sprite = null;
                rootImage.color = Color.clear;
                rootImage.raycastTarget = true;
            }
        }

        UpdateVisualAppearance();
    }

    public void Initialize(PieceManager pieceManager, Vector2Int position, bool isEnemy, bool? isCaptureTarget = null)
    {
        manager = pieceManager;
        gridPosition = position;
        isCapture = isCaptureTarget ?? CheckIsCapture(pieceManager, position, isEnemy);

        EnsureVisualSetup();
        UpdateVisualAppearance();

        transform.localScale = Vector3.one;

        // 적 마커면 흰색, 내 마커면 검정색
        baseColor = isEnemy ? Color.white : Color.black;
        
        // 초기 상태: 반투명
        UpdateColor(0.5f);
    }

    private bool CheckIsCapture(PieceManager pieceManager, Vector2Int position, bool isEnemy)
    {
        if (pieceManager == null) return false;
        PieceController pieceAt = pieceManager.GetPieceAt(position);
        return pieceAt != null && pieceAt.IsEnemy != isEnemy && !pieceAt.IsHidden;
    }

    private void UpdateVisualAppearance()
    {
        if (markerVisualImage == null) return;

        RectTransform visualRect = markerVisualImage.rectTransform;

        // 적 기물을 잡을 수 있는 칸이면 캡처 스프라이트 + 28x28
        if (isCapture && captureSprite != null)
        {
            markerVisualImage.sprite = captureSprite;
            visualRect.sizeDelta = captureDotSize; // 28 x 28
        }
        else // 일반 이동 칸이면 일반 스프라이트 + 12x12
        {
            if (normalSprite != null)
            {
                markerVisualImage.sprite = normalSprite;
            }
            visualRect.sizeDelta = markerDotSize; // 12 x 12
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        UpdateColor(1f); // 마우스 올리면 불투명
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        UpdateColor(0.5f); // 마우스 떼면 반투명
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (ShotgunKingSystem.TryHandleShotAimClick(gridPosition, eventData.button))
        {
            eventData.Use();
        }
    }

    private void UpdateColor(float alpha)
    {
        if (markerVisualImage != null)
        {
            markerVisualImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }
}
