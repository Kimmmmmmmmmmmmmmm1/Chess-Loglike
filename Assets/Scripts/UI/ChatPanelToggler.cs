using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ChatPanelToggler : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform targetRectTransform;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Size")]
    [SerializeField] private Vector2 expandedSize = new Vector2(400f, 480f);
    [SerializeField] private bool onlyChangeHeight = true;

    [Header("Animation")]
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private Ease ease = Ease.OutCubic;

    [Header("State")]
    [SerializeField] private bool startExpanded = true;

    private Vector2 originalSize;
    private bool isExpanded;

    private void Awake()
    {
        if (targetRectTransform == null)
        {
            targetRectTransform = GetComponent<RectTransform>();
        }

        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        originalSize = targetRectTransform.sizeDelta;
    }

    private void Start()
    {
        isExpanded = startExpanded;
        ApplyStateImmediate(isExpanded);
    }

    public void Toggle()
    {
        if (isExpanded)
        {
            Collapse();
        }
        else
        {
            Expand();
        }
    }

    public void Expand()
    {
        SetState(true);
    }

    public void Collapse()
    {
        SetState(false);
    }

    private void SetState(bool expanded)
    {
        isExpanded = expanded;

        if (targetRectTransform == null)
        {
            return;
        }

        targetRectTransform.DOKill();

        Vector2 targetSize = GetTargetSize(expanded);
        targetRectTransform.DOSizeDelta(targetSize, duration).SetEase(ease);

        if (!expanded && scrollRect != null)
        {
            DOVirtual.DelayedCall(duration, () =>
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            });
        }
    }

    private void ApplyStateImmediate(bool expanded)
    {
        if (targetRectTransform == null)
        {
            return;
        }

        targetRectTransform.sizeDelta = GetTargetSize(expanded);
    }

    private Vector2 GetTargetSize(bool expanded)
    {
        if (onlyChangeHeight)
        {
            return new Vector2(originalSize.x, expanded ? expandedSize.y : originalSize.y);
        }

        return expanded ? expandedSize : originalSize;
    }
}
