using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using System;

/// <summary>
/// 보스 클리어/특별 이벤트 연출 효과
/// 왼쪽에서 텍스트, 오른쪽에서 패널이 날라와 겹친 후 팡하고 커지고 사라지는 연출
/// </summary>
public class PresentationEffect : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI textElement;
    [SerializeField] private TextMeshProUGUI subtextElement;
    [SerializeField] private Image panelElement;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float flyInDuration = 0.6f;           // 날라오는 시간
    [SerializeField] private Ease flyInEase = Ease.OutCubic;       // 날라오기 이징
    [SerializeField] private float popDuration = 0.3f;             // 팡 커지는 시간
    [SerializeField] private Ease popEase = Ease.OutBack;          // 팡 이징
    [SerializeField] private float popScale = 1.2f;                // 최대 스케일
    [SerializeField] private float fadeOutDuration = 0.5f;         // 사라지는 시간
    [SerializeField] private Ease fadeOutEase = Ease.InQuad;       // 사라지기 이징
    [SerializeField] private float displayDuration = 1.5f;         // 팡 이후 표시 시간

    private RectTransform rectTransform;
    private RectTransform textRectTransform;
    private RectTransform subtextRectTransform;
    private RectTransform panelRectTransform;
    private Vector2 textTargetAnchoredPosition;
    private Vector2 subtextTargetAnchoredPosition;
    private Vector2 panelTargetAnchoredPosition;
    private Sequence playingSequence;
    private float initialCanvasGroupAlpha = 1f;
    private float initialPanelAlpha = 1f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (textElement != null)
        {
            textRectTransform = textElement.GetComponent<RectTransform>();
            textTargetAnchoredPosition = textRectTransform.anchoredPosition;
        }
        if (subtextElement != null)
        {
            subtextRectTransform = subtextElement.GetComponent<RectTransform>();
            subtextTargetAnchoredPosition = subtextRectTransform.anchoredPosition;
        }
        
        if (panelElement != null)
        {
            panelRectTransform = panelElement.GetComponent<RectTransform>();
            panelTargetAnchoredPosition = panelRectTransform.anchoredPosition;
            initialPanelAlpha = panelElement.color.a;
        }

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            initialCanvasGroupAlpha = canvasGroup.alpha;
        }
    }

    /// <summary>
    /// 연출을 시작합니다.
    /// </summary>
    /// <param name="text">표시할 텍스트 (보스 이름 등)</param>
    /// <param name="panelSprite">표시할 패널 스프라이트</param>
    /// <param name="onComplete">연출 완료 후 콜백</param>
    /// <param name="subtext">표시할 부제 텍스트</param>
    public void PlayPresentation(string text, Sprite panelSprite, Action onComplete = null, string subtext = "")
    {
        // 이전 연출 중지
        if (playingSequence != null && playingSequence.IsPlaying())
        {
            playingSequence.Kill();
        }

        // 요소들 초기화
        if (textElement != null)
        {
            textElement.text = text;
            Color textColor = textElement.color;
            textColor.a = 1f;
            textElement.color = textColor;
        }

        if (subtextElement != null)
        {
            subtextElement.text = subtext;
            Color subTextColor = subtextElement.color;
            subTextColor.a = 1f;
            subtextElement.color = subTextColor;
        }

        if (panelElement != null)
        {
            if (panelSprite != null)
            {
                panelElement.sprite = panelSprite;
            }

            Color panelColor = panelElement.color;
            panelColor.a = initialPanelAlpha;
            panelElement.color = panelColor;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = initialCanvasGroupAlpha;
        }

        // 루트 스케일 초기화
        transform.localScale = Vector3.one;

        // 시작 위치 설정: 텍스트는 왼쪽, 패널은 오른쪽
        if (textRectTransform != null)
        {
            // 왼쪽에서 시작
            textRectTransform.anchoredPosition = textTargetAnchoredPosition + Vector2.left * 200f;
            textRectTransform.localScale = Vector3.one;
        }

        if (subtextRectTransform != null)
        {
            // 왼쪽에서 시작
            subtextRectTransform.anchoredPosition = subtextTargetAnchoredPosition + Vector2.left * 200f;
            subtextRectTransform.localScale = Vector3.one;
        }

        if (panelRectTransform != null)
        {
            // 오른쪽에서 시작
            panelRectTransform.anchoredPosition = panelTargetAnchoredPosition + Vector2.right * 200f;
            panelRectTransform.localScale = Vector3.one;
        }

        // 시퀀스 구성
        playingSequence = DOTween.Sequence();

        // Phase 1: 왼쪽에서 텍스트 날라오기, 오른쪽에서 패널 날라오기 (동시)
        if (textRectTransform != null)
        {
            playingSequence.Join(
                textRectTransform.DOAnchorPosX(textTargetAnchoredPosition.x, flyInDuration)
                    .SetEase(flyInEase)
            );
        }

        if (panelRectTransform != null)
        {
            playingSequence.Join(
                panelRectTransform.DOAnchorPosX(panelTargetAnchoredPosition.x, flyInDuration)
                    .SetEase(flyInEase)
            );
        }

        if (subtextRectTransform != null)
        {
            playingSequence.Join(
                subtextRectTransform.DOAnchorPosX(subtextTargetAnchoredPosition.x, flyInDuration)
                    .SetEase(flyInEase)
            );
        }

        // Phase 2: 팡하고 커지기 (flyIn 완료 후)
        playingSequence.Append(
            transform.DOScale(popScale, popDuration)
                .SetEase(popEase)
        );

        // Phase 3: 표시 유지 (displayDuration 동안)
        playingSequence.AppendInterval(displayDuration);

        // Phase 4: 스르륵 사라지기
        playingSequence.Append(
            DOTween.To(
                () => canvasGroup.alpha,
                x => canvasGroup.alpha = x,
                0f,
                fadeOutDuration
            ).SetEase(fadeOutEase)
        );

        // Phase 5: 스케일도 동시에 축소
        playingSequence.Join(
            transform.DOScale(0f, fadeOutDuration)
                .SetEase(fadeOutEase)
        );

        // 연출 완료
        playingSequence.OnComplete(() =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    private void OnDestroy()
    {
        if (playingSequence != null && playingSequence.IsPlaying())
        {
            playingSequence.Kill();
        }
    }
}
