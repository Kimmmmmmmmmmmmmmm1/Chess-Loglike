using UnityEngine;
using System.Collections;
using DG.Tweening;

/// <summary>
/// GridLine의 교차점을 나타냅니다.
/// 기물이 실제로 놓일 수 있는 위치입니다.
/// </summary>
public class GridPoint : MonoBehaviour
{
    public Vector2Int gridPosition;
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    
    // 이 점을 이루는 4개의 라인 (상하좌우)
    public GridLine lineTop { get; set; }
    public GridLine lineBottom { get; set; }
    public GridLine lineLeft { get; set; }
    public GridLine lineRight { get; set; }
    
    // 파괴 상태
    public bool isDestroyed { get; private set; } = false;
    
    private Coroutine shakeCoroutine;

    public void Initialize(Vector2Int position, RectTransform rect, Vector2 anchoredPos)
    {
        gridPosition = position;
        rectTransform = rect;
        originalPosition = anchoredPos;
        rectTransform.anchoredPosition = anchoredPos;

        // GridPoint 시각 요소 제거
        var pointImage = gameObject.GetComponent<UnityEngine.UI.Image>();
        if (pointImage != null)
        {
            if (Application.isPlaying) Destroy(pointImage);
            else DestroyImmediate(pointImage);
        }
    }

    /// <summary>
    /// GridPoint를 계속 흔들 효과를 적용합니다.
    /// </summary>
    public void StartContinuousShake(float duration = 0.5f, float strength = 5f)
    {
        StopContinuousShake();
        
        shakeCoroutine = StartCoroutine(ContinuousShakeCoroutine(duration, strength));
        
        // 연결된 GridLine들도 함께 흔들기 (활성화된 라인만)
        if (lineTop != null && lineTop.gameObject.activeInHierarchy)
            lineTop.StartContinuousShake(duration, strength);
        if (lineBottom != null && lineBottom.gameObject.activeInHierarchy)
            lineBottom.StartContinuousShake(duration, strength);
        if (lineLeft != null && lineLeft.gameObject.activeInHierarchy)
            lineLeft.StartContinuousShake(duration, strength);
        if (lineRight != null && lineRight.gameObject.activeInHierarchy)
            lineRight.StartContinuousShake(duration, strength);
    }

    /// <summary>
    /// GridPoint의 계속 흔들기를 중지합니다.
    /// </summary>
    public void StopContinuousShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        if (rectTransform != null)
            rectTransform.DOKill();

        // 연결된 GridLine들의 흔들기도 중지 (활성화된 라인만)
        if (lineTop != null && lineTop.gameObject.activeInHierarchy)
            lineTop.StopContinuousShake();
        if (lineBottom != null && lineBottom.gameObject.activeInHierarchy)
            lineBottom.StopContinuousShake();
        if (lineLeft != null && lineLeft.gameObject.activeInHierarchy)
            lineLeft.StopContinuousShake();
        if (lineRight != null && lineRight.gameObject.activeInHierarchy)
            lineRight.StopContinuousShake();
    }

    private IEnumerator ContinuousShakeCoroutine(float duration, float strength)
    {
        while (gameObject.activeSelf)
        {
            if (rectTransform == null) break;
            rectTransform.DOKill();
            rectTransform.DOShakeAnchorPos(duration, strength, 10, 90f);
            yield return new WaitForSeconds(duration);
        }
    }

    /// <summary>
    /// 이 GridPoint를 이루는 교차 GridLine들을 파괴합니다.
    /// </summary>
    public void DestroyIntersectingLines()
    {
        StopContinuousShake();
        
        // 파괴 상태로 설정
        isDestroyed = true;
        
        // 연결된 라인들 파괴
        if (lineTop != null)
            lineTop.DestroyLine();
        if (lineBottom != null)
            lineBottom.DestroyLine();
        if (lineLeft != null)
            lineLeft.DestroyLine();
        if (lineRight != null)
            lineRight.DestroyLine();
    }

    private void OnDestroy()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        rectTransform?.DOKill();
    }
}
