using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class PagedAnimatedPanelView : MonoBehaviour
{
    [Header("Pagination")]
    [SerializeField] protected Transform contentRoot;
    [SerializeField] protected ScrollRect scrollRect;
    [SerializeField] protected int entriesPerPage = 8;
    [SerializeField] protected Button previousPageButton;
    [SerializeField] protected Button nextPageButton;
    [SerializeField] protected TextMeshProUGUI pageText;

    [Header("Entry Animation")]
    [SerializeField] protected float entryAnimationDuration = 0.06f;
    [SerializeField] protected float entryStaggerDelay = 0.02f;
    [SerializeField] protected float staggerSpacingMultiplier = 0.55f;
    [SerializeField] protected int animationEntriesPerRow = 4;
    [SerializeField] protected float secondaryRowDelayOffset = 0.008f;
    [SerializeField] protected Vector3 entryStartOffset = new Vector3(0f, -30f, 0f);
    [SerializeField] protected Vector3 entryStartTilt = new Vector3(12f, 0f, -8f);

    protected int currentPageIndex;

    private readonly List<Coroutine> entryCoroutines = new List<Coroutine>();

    public abstract void GoToPreviousPage();
    public abstract void GoToNextPage();

    protected void BindPaginationButtons()
    {
        if (previousPageButton != null)
        {
            previousPageButton.onClick.AddListener(GoToPreviousPage);
        }

        if (nextPageButton != null)
        {
            nextPageButton.onClick.AddListener(GoToNextPage);
        }
    }

    protected void UnbindPaginationButtons()
    {
        if (previousPageButton != null)
        {
            previousPageButton.onClick.RemoveListener(GoToPreviousPage);
        }

        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveListener(GoToNextPage);
        }
    }

    protected void StopEntryAnimations()
    {
        foreach (Coroutine coroutine in entryCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }

        entryCoroutines.Clear();
    }

    protected void ClearContent()
    {
        StopEntryAnimations();

        if (contentRoot == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform childTransform = contentRoot.GetChild(i);
            GameObject childObj = childTransform.gameObject;
            childObj.SetActive(false); // 레이아웃에서 즉시 제외되도록 비활성화
            childTransform.SetParent(null, false);
            Destroy(childObj);         // 안전한 삭제
        }
    }

    protected int GetEntriesPerPage()
    {
        return Mathf.Max(1, entriesPerPage);
    }

    protected int GetTotalPageCount(int itemCount)
    {
        if (itemCount <= 0)
        {
            return 1;
        }

        int pageSize = GetEntriesPerPage();
        return Mathf.CeilToInt(itemCount / (float)pageSize);
    }

    protected int GetLastPageIndex(int itemCount)
    {
        return Mathf.Max(0, GetTotalPageCount(itemCount) - 1);
    }

    protected bool TrySetPageIndex(int desiredPageIndex, int itemCount)
    {
        int clampedPageIndex = Mathf.Clamp(desiredPageIndex, 0, GetLastPageIndex(itemCount));
        if (clampedPageIndex == currentPageIndex)
        {
            return false;
        }

        currentPageIndex = clampedPageIndex;
        return true;
    }

    protected void ResetPageIndex()
    {
        currentPageIndex = 0;
    }

    protected void ResetScrollToTop()
    {
        ScrollRect targetScrollRect = ResolveScrollRect();
        if (targetScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        targetScrollRect.StopMovement();
        targetScrollRect.verticalNormalizedPosition = 1f;
    }

    protected void UpdatePaginationControls(int itemCount)
    {
        int totalPages = GetTotalPageCount(itemCount);
        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, totalPages - 1);

        if (previousPageButton != null)
        {
            previousPageButton.interactable = currentPageIndex > 0;
        }

        if (nextPageButton != null)
        {
            nextPageButton.interactable = currentPageIndex < totalPages - 1;
        }

        if (pageText != null)
        {
            pageText.text = $"{currentPageIndex + 1} / {totalPages}";
        }
    }

    protected void AnimateCurrentEntries()
    {
        if (contentRoot == null)
        {
            return;
        }

        StopEntryAnimations();
        PrepareCurrentEntriesForAnimation();

        float baseOpenDelay = 0f;
        PanelAnimator panelAnimator = GetComponent<PanelAnimator>();
        if (panelAnimator != null && panelAnimator.IsOpening)
        {
            baseOpenDelay = panelAnimator.TotalOpenDuration * 0.72f;
        }

        int childCount = contentRoot.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = GetAnimationTarget(contentRoot.GetChild(i));
            float delay = baseOpenDelay + GetEntryDelay(i);
            Coroutine coroutine = StartCoroutine(AnimateEntryWithDelay(child, delay, entryAnimationDuration));
            entryCoroutines.Add(coroutine);
        }
    }

    private float GetEntryDelay(int index)
    {
        float compressedStagger = Mathf.Max(0f, entryStaggerDelay) * Mathf.Max(0f, staggerSpacingMultiplier);
        int entriesPerRowForDelay = Mathf.Max(1, animationEntriesPerRow);

        if (entriesPerRowForDelay <= 1)
        {
            return index * compressedStagger;
        }

        int row = index / entriesPerRowForDelay;
        int column = index % entriesPerRowForDelay;
        return (column * compressedStagger) + (row * Mathf.Max(0f, secondaryRowDelayOffset));
    }

    private void PrepareCurrentEntriesForAnimation()
    {
        int childCount = contentRoot.childCount;
        for (int i = 0; i < childCount; i++)
        {
            PrepareEntryForAnimation(GetAnimationTarget(contentRoot.GetChild(i)));
        }
    }

    protected virtual void PrepareEntryForAnimation(Transform child)
    {
        if (child == null)
        {
            return;
        }

        RectTransform rectTransform = child as RectTransform;
        CanvasGroup canvasGroup = child.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = child.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        child.localScale = Vector3.one;
        child.localRotation = Quaternion.Euler(entryStartTilt);

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = (Vector2)entryStartOffset;
        }
        else
        {
            child.localPosition = entryStartOffset;
        }
    }

    protected Transform CreateEntryContainer(string containerName = null)
    {
        if (contentRoot == null)
        {
            return null;
        }

        GameObject containerObject = new GameObject(string.IsNullOrEmpty(containerName) ? "EntryContainer" : containerName, typeof(RectTransform));
        RectTransform containerRect = containerObject.GetComponent<RectTransform>();
        containerRect.SetParent(contentRoot, false);
        containerRect.localScale = Vector3.one;
        containerRect.localRotation = Quaternion.identity;
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero;
        return containerRect;
    }

    protected Transform GetAnimationTarget(Transform container)
    {
        if (container == null)
        {
            return null;
        }

        if (container.childCount > 0)
        {
            return container.GetChild(0);
        }

        return container;
    }

    private ScrollRect ResolveScrollRect()
    {
        if (scrollRect != null)
        {
            return scrollRect;
        }

        if (contentRoot != null)
        {
            scrollRect = contentRoot.GetComponentInParent<ScrollRect>();
        }

        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        return scrollRect;
    }

    private IEnumerator AnimateEntryWithDelay(Transform child, float delay, float duration)
    {
        if (delay > 0f)
        {
            float waited = 0f;
            while (waited < delay)
            {
                if (child == null)
                {
                    yield break;
                }

                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        yield return AnimateEntry(child, duration);
    }

    private IEnumerator AnimateEntry(Transform child, float duration)
    {
        if (child == null)
        {
            yield break;
        }

        RectTransform rectTransform = child as RectTransform;
        CanvasGroup canvasGroup = child.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = child.gameObject.AddComponent<CanvasGroup>();
        }

        Vector3 fromPosition = rectTransform != null ? (Vector3)rectTransform.anchoredPosition : child.localPosition;
        Vector3 toPosition = Vector3.zero;
        Quaternion fromRotation = child.localRotation;
        Quaternion toRotation = Quaternion.identity;

        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsedTime = 0f;
        while (elapsedTime < safeDuration)
        {
            if (child == null)
            {
                yield break;
            }

            elapsedTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / safeDuration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector3.Lerp(fromPosition, toPosition, easedProgress);
            }
            else
            {
                child.localPosition = Vector3.Lerp(fromPosition, toPosition, easedProgress);
            }

            child.localRotation = Quaternion.Slerp(fromRotation, toRotation, easedProgress);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, easedProgress);
            yield return null;
        }

        if (child == null)
        {
            yield break;
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = toPosition;
        }
        else
        {
            child.localPosition = toPosition;
        }

        child.localRotation = toRotation;
        canvasGroup.alpha = 1f;
    }
}
