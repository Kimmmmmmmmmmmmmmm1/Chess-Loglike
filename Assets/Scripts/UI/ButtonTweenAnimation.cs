using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonTweenAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale Settings")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float pressScale = 0.9f;
    [SerializeField] private float normalScale = 1f;

    [Header("Animation Settings")]
    [SerializeField] private float hoverDuration = 0.2f;
    [SerializeField] private float pressDuration = 0.1f;
    [SerializeField] private Ease hoverEase = Ease.OutQuad;
    [SerializeField] private Ease pressEase = Ease.InOutQuad;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Vector2 originalPosition;
    private Tweener currentTween;
    private Tweener positionTween;
    private bool isPressed;
    private bool isHovered;

    public Vector2 OriginalPosition => originalPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalScale = rectTransform.localScale;
            originalPosition = rectTransform.anchoredPosition;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHoverState(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHoverState(false);
    }

    public void SetHoverState(bool state)
    {
        if (isHovered == state) return;

        isHovered = state;
        if (!isPressed)
        {
            float targetScale = state ? hoverScale : normalScale;
            AnimateScale(originalScale * targetScale, hoverDuration, hoverEase);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        AnimateScale(originalScale * pressScale, pressDuration, pressEase);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        if (isHovered)
        {
            AnimateScale(originalScale * hoverScale, pressDuration, pressEase);
        }
        else
        {
            AnimateScale(originalScale * normalScale, pressDuration, pressEase);
        }
    }

    public void MoveTo(Vector2 targetPosition, float duration, Ease ease)
    {
        if (rectTransform == null) return;
        
        positionTween?.Kill();
        positionTween = rectTransform.DOAnchorPos(targetPosition, duration).SetEase(ease);
    }

    public void ResetPosition(float duration, Ease ease)
    {
        if (rectTransform == null) return;

        positionTween?.Kill();
        positionTween = rectTransform.DOAnchorPos(originalPosition, duration).SetEase(ease);
    }

    private void AnimateScale(Vector3 targetScale, float duration, Ease ease)
    {
        if (rectTransform == null)
        {
            return;
        }

        currentTween?.Kill();
        currentTween = rectTransform.DOScale(targetScale, duration).SetEase(ease);
    }

    private void OnDestroy()
    {
        currentTween?.Kill();
        positionTween?.Kill();
    }
}
