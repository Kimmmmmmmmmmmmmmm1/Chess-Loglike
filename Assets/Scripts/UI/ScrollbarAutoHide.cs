using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(ScrollRect))]
public class ScrollbarAutoHide : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("설정")]
    public float fadeDuration = 0.3f;   // 사라지는 데 걸리는 시간
    public float hideDelay = 1.0f;      // 스크롤 멈추고 사라지기 전 대기 시간

    [Header("연결 (자동으로 찾지만 수동 연결 추천)")]
    public CanvasGroup verticalScrollbarGroup;
    public CanvasGroup horizontalScrollbarGroup;

    private ScrollRect scrollRect;
    private Coroutine currentFadeRoutine;
    private bool isDragging = false;
    private Vector2 prevContentPos;

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();

        // 최초 위치 기억해 두고 움직임을 감지한다
        if (scrollRect.content != null)
            prevContentPos = scrollRect.content.anchoredPosition;

        // 스크롤바에 CanvasGroup이 없으면 자동으로 추가해줌
        if (scrollRect.verticalScrollbar && verticalScrollbarGroup == null)
            verticalScrollbarGroup = GetOrAddComponent<CanvasGroup>(scrollRect.verticalScrollbar.gameObject);

        if (scrollRect.horizontalScrollbar && horizontalScrollbarGroup == null)
            horizontalScrollbarGroup = GetOrAddComponent<CanvasGroup>(scrollRect.horizontalScrollbar.gameObject);

        // 시작할 때는 숨기기
        SetAlpha(0f);
    }

    void Update()
    {
        // ScrollRect.velocity가 0으로 고정되는 경우를 대비해 실제 이동량도 함께 본다
        Vector2 currPos = scrollRect.content != null ? scrollRect.content.anchoredPosition : prevContentPos;
        float moved = (currPos - prevContentPos).sqrMagnitude;
        prevContentPos = currPos;

        // 스크롤 속도나 위치 변화 또는 드래그 중이면 보이게 유지
        if (scrollRect.velocity.sqrMagnitude > 0.0004f || moved > 0.0004f || isDragging)
        {
            ShowScrollbar();
        }
        else
        {
            // 멈췄으면 사라지게 (코루틴이 이미 돌고 있다면 냅둠)
            if (currentFadeRoutine == null && GetAlpha() > 0f)
            {
                currentFadeRoutine = StartCoroutine(FadeOut());
            }
        }
    }

    // 드래그 시작할 때 (손 댔을 때)
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        ShowScrollbar();
    }

    // 드래그 끝났을 때 (손 뗐을 때)
    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    void ShowScrollbar()
    {
        if (currentFadeRoutine != null) StopCoroutine(currentFadeRoutine);
        currentFadeRoutine = null;
        SetAlpha(1f);
    }

    IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(hideDelay); // 대기 시간

        float startAlpha = GetAlpha();
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, time / fadeDuration);
            SetAlpha(alpha);
            yield return null;
        }

        SetAlpha(0f);
        currentFadeRoutine = null;
    }

    // 헬퍼 함수들
    T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    void SetAlpha(float alpha)
    {
        if (verticalScrollbarGroup) verticalScrollbarGroup.alpha = alpha;
        if (horizontalScrollbarGroup) horizontalScrollbarGroup.alpha = alpha;
    }

    float GetAlpha()
    {
        if (verticalScrollbarGroup) return verticalScrollbarGroup.alpha;
        if (horizontalScrollbarGroup) return horizontalScrollbarGroup.alpha;
        return 0f;
    }
}