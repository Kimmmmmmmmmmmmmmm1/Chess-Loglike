using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// UI 패널 표시/숨김 제어기: 위에서 내려오거나 가로로 접히는 애니메이션 없이 즉시 열고 닫습니다.
/// </summary>
[DisallowMultipleComponent]
public class PanelAnimator : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 openAnchoredPosition;
    private float expandedSizeDeltaX;
    private bool hasCachedTransform = false;

    public bool IsAnimating => false;
    public bool IsOpening => false;
    public float MoveDuration => 0f;
    public float TotalOpenDuration => 0f;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        KillActiveTweens();
    }

    private void OnDisable()
    {
        KillActiveTweens();
    }

    public void RecacheTransform()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (hasCachedTransform && rectTransform != null)
        {
            rectTransform.anchoredPosition = openAnchoredPosition;
            SetWidth(expandedSizeDeltaX);
        }

        hasCachedTransform = false;
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (!hasCachedTransform && rectTransform != null)
        {
            openAnchoredPosition = rectTransform.anchoredPosition;
            expandedSizeDeltaX = rectTransform.sizeDelta.x;
            hasCachedTransform = true;
        }

        DisableStrayBackMask();
    }

    private void DisableStrayBackMask()
    {
        if (rectTransform == null)
        {
            return;
        }

        Transform backTransform = rectTransform.Find("Back");
        if (backTransform != null)
        {
            Mask strayBackMask = backTransform.GetComponent<Mask>();
            if (strayBackMask != null && strayBackMask.enabled)
            {
                strayBackMask.enabled = false;
            }
        }
    }

    private void SetWidth(float sizeDeltaX)
    {
        if (rectTransform == null) return;
        Vector2 size = rectTransform.sizeDelta;
        size.x = sizeDeltaX;
        rectTransform.sizeDelta = size;
    }

    private void KillActiveTweens()
    {
        rectTransform?.DOKill();
        canvasGroup?.DOKill();
    }

    public void Show(bool instant = false)
    {
        EnsureInitialized();
        KillActiveTweens();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = openAnchoredPosition;
            SetWidth(expandedSizeDeltaX);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            canvasGroup.alpha = 1f;
        }

        if (!instant)
        {
            SoundManager.Instance?.PlaySFX(SFXType.OpenPanel);
        }
    }

    public void Hide(bool instant = false)
    {
        EnsureInitialized();
        KillActiveTweens();

        bool wasActive = gameObject.activeInHierarchy;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            canvasGroup.alpha = 1f;
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = openAnchoredPosition;
            SetWidth(expandedSizeDeltaX);
        }

        if (!instant && wasActive)
        {
            SoundManager.Instance?.PlaySFX(SFXType.ClosePanel);
        }

        gameObject.SetActive(false);
    }
}
