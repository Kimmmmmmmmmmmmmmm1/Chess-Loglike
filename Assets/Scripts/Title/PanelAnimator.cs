using UnityEngine;
using DG.Tweening;

/// <summary>
/// UI 패널 애니메이터: MapManager(MapPanel)와 동일한 순차 시퀀스로 애니메이션을 수행합니다.
/// 열기: 위에서 내려오기(Y 이동) → 가로로 넓게 펼쳐지기(sizeDelta.x 확장)
/// 닫기: 가로로 접히기(sizeDelta.x 축소) → 위로 올라가기(Y 이동)
/// ※ stretch 앵커 패널도 올바르게 동작하도록 실제 픽셀 너비를 기준으로 sizeDelta.x를 계산합니다.
/// </summary>
[DisallowMultipleComponent]
public class PanelAnimator : MonoBehaviour
{
    [Header("Animation Settings (MapPanel Style)")]
    [Tooltip("Y 이동 애니메이션 소요 시간 (MapManager 0.4초와 동일)")]
    [SerializeField] private float moveDuration = 0.4f;

    [Tooltip("가로 펼침/접힘 애니메이션 소요 시간 (MapManager 0.4초와 동일)")]
    [SerializeField] private float widthDuration = 0.4f;

    [Tooltip("접혔을 때 실제 화면 너비(px). MapManager의 collapsedPanelWidth에 해당 (예: 72)")]
    [SerializeField] private float collapsedActualWidth = 72f;

    [Tooltip("패널의 실제 높이만큼 위에서 내려올지 여부 (MapManager와 동일)")]
    [SerializeField] private bool useFullHeightOffset = true;

    [Tooltip("useFullHeightOffset=false일 때 적용되는 Y 슬라이드 거리")]
    [SerializeField] private float customSlideOffsetY = 480f;

    [Tooltip("Y 이동 이징 (MapManager Ease.OutQuad 동일)")]
    [SerializeField] private Ease moveEase = Ease.OutQuad;

    [Tooltip("가로 펼침/접힘 이징 (MapManager Ease.OutQuad 동일)")]
    [SerializeField] private Ease widthEase = Ease.OutQuad;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 openAnchoredPosition;
    private float expandedSizeDeltaX;   // 완전히 열렸을 때의 sizeDelta.x (캐시)
    private float collapsedSizeDeltaX;  // 접혔을 때의 sizeDelta.x (실제 픽셀 기반으로 계산)
    private bool hasCachedTransform = false;
    private Sequence animationSequence;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        KillActiveSequence();
    }

    private void OnDisable()
    {
        KillActiveSequence();
    }

    private void EnsureInitialized()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (!hasCachedTransform && rectTransform != null)
        {
            openAnchoredPosition = rectTransform.anchoredPosition;
            expandedSizeDeltaX = rectTransform.sizeDelta.x;

            // stretch 앵커를 포함해 어떤 앵커 방식이든 올바르게 동작하도록
            // "실제 픽셀 너비 = 부모너비 + sizeDelta.x" 관계를 이용해 collapsedSizeDeltaX를 역산한다.
            // collapsedActualWidth = parentWidth + collapsedSizeDeltaX
            // ∴ collapsedSizeDeltaX = collapsedActualWidth - parentWidth
            float parentWidth = 0f;
            if (rectTransform.parent is RectTransform parentRect)
            {
                parentWidth = parentRect.rect.width;
            }

            if (parentWidth > 0f)
            {
                collapsedSizeDeltaX = collapsedActualWidth - parentWidth;
            }
            else
            {
                // 부모 너비를 알 수 없는 경우 fallback
                collapsedSizeDeltaX = expandedSizeDeltaX - (rectTransform.rect.width * 0.95f);
            }

            hasCachedTransform = true;
        }
    }

    private float GetSlideOffsetY()
    {
        if (!useFullHeightOffset)
            return customSlideOffsetY;

        if (rectTransform != null && rectTransform.rect.height > 0f)
            return rectTransform.rect.height;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect != null && canvasRect.rect.height > 0f)
                return canvasRect.rect.height;
        }

        return 480f;
    }

    private void SetWidth(float sizeDeltaX)
    {
        if (rectTransform == null) return;
        Vector2 size = rectTransform.sizeDelta;
        size.x = sizeDeltaX;
        rectTransform.sizeDelta = size;
    }

    private Tween CreateWidthTween(float targetSizeDeltaX)
    {
        return DOTween.To(
            () => rectTransform.sizeDelta.x,
            SetWidth,
            targetSizeDeltaX,
            widthDuration
        ).SetEase(widthEase);
    }

    private void KillActiveSequence()
    {
        if (animationSequence != null)
        {
            animationSequence.Kill();
            animationSequence = null;
        }
        rectTransform?.DOKill();
        canvasGroup?.DOKill();
    }

    public void Show(bool instant = false)
    {
        EnsureInitialized();
        KillActiveSequence();

        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        float offsetY = GetSlideOffsetY();

        if (instant)
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = openAnchoredPosition;
                SetWidth(expandedSizeDeltaX);
            }
            return;
        }

        // 초기 상태: 위쪽 밖 + 가로로 접힌 상태 (collapsedActualWidth의 실제 픽셀 너비)
        rectTransform.anchoredPosition = new Vector2(openAnchoredPosition.x, openAnchoredPosition.y + offsetY);
        SetWidth(collapsedSizeDeltaX);
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // MapManager Open() 순서:
        //   1. Append → Y 이동 내려오기  (+ 동시에 페이드인)
        //   2. Append → 가로 폭 펼쳐지기 (collapsedSizeDeltaX → expandedSizeDeltaX)
        animationSequence = DOTween.Sequence().SetUpdate(true);

        Tween moveDown = rectTransform.DOAnchorPosY(openAnchoredPosition.y, moveDuration).SetEase(moveEase);
        Tween fadeIn = canvasGroup != null ? canvasGroup.DOFade(1f, moveDuration).SetEase(moveEase) : null;

        animationSequence.Append(moveDown);
        if (fadeIn != null) animationSequence.Join(fadeIn);
        animationSequence.Append(CreateWidthTween(expandedSizeDeltaX));

        animationSequence.OnComplete(() =>
        {
            rectTransform.anchoredPosition = openAnchoredPosition;
            SetWidth(expandedSizeDeltaX);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            animationSequence = null;
        });
    }

    public void Hide(bool instant = false)
    {
        EnsureInitialized();
        KillActiveSequence();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        float offsetY = GetSlideOffsetY();

        if (instant || !gameObject.activeInHierarchy)
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = new Vector2(openAnchoredPosition.x, openAnchoredPosition.y + offsetY);
                SetWidth(collapsedSizeDeltaX);
            }
            gameObject.SetActive(false);
            return;
        }

        // MapManager Close() 순서:
        //   1. Append → 가로 폭 접히기 (expandedSizeDeltaX → collapsedSizeDeltaX)
        //   2. Append → Y 이동 위로     (+ 동시에 페이드아웃)
        animationSequence = DOTween.Sequence().SetUpdate(true);

        animationSequence.Append(CreateWidthTween(collapsedSizeDeltaX));

        Tween moveUp = rectTransform.DOAnchorPosY(openAnchoredPosition.y + offsetY, moveDuration).SetEase(moveEase);
        Tween fadeOut = canvasGroup != null ? canvasGroup.DOFade(0f, moveDuration).SetEase(moveEase) : null;

        animationSequence.Append(moveUp);
        if (fadeOut != null) animationSequence.Join(fadeOut);

        animationSequence.OnComplete(() =>
        {
            gameObject.SetActive(false);
            animationSequence = null;
        });
    }
}
