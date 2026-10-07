using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class GridLine : MonoBehaviour
{
    public Vector2Int gridPosition;
    public bool isVertical = true; // true면 세로, false면 가로
    
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    
    private Image lineImage;
    private Color originalColor;
    
    private Coroutine shakeCoroutine;

    public void Initialize(Vector2Int position, Vector2 anchoredPos)
    {
        gridPosition = position;
        rectTransform = GetComponent<RectTransform>();
        originalPosition = anchoredPos;
        
        lineImage = GetComponent<Image>();
        if (lineImage != null)
        {
            originalColor = lineImage.color;
        }
    }

    public void SetGray(bool isGray)
    {
        if (lineImage == null) return;
        
        lineImage.DOKill();
        lineImage.DOColor(isGray ? Color.gray : originalColor, 0.3f);
    }

    /// <summary>
    /// 그리드 라인에 흔들 효과를 적용합니다.
    /// </summary>
    /// <param name="duration">흔들 지속 시간</param>
    /// <param name="strength">흔들 강도</param>
    public void Shake(float duration = 0.5f, float strength = 5f)
    {
        if (rectTransform == null) return;
        rectTransform.DOKill();
        rectTransform.DOShakeAnchorPos(duration, strength, 10, 90f);
    }

    /// <summary>
    /// 그리드 라인을 계속 흔들 효과를 적용합니다.
    /// </summary>
    /// <param name="duration">반복 흔들 지속 시간</param>
    /// <param name="strength">흔들 강도</param>
    public void StartContinuousShake(float duration = 0.5f, float strength = 5f)
    {
        // 비활성화된 라인은 흔들 수 없음
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        StopContinuousShake();
        shakeCoroutine = StartCoroutine(ContinuousShakeCoroutine(duration, strength));
    }

    /// <summary>
    /// 그리드 라인의 계속 흔들기를 중지합니다.
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
    /// GridLine을 무너지는 효과와 함께 제거합니다.
    /// </summary>
    public void DestroyLine()
    {
        if (!gameObject.activeSelf) return;
        
        StopContinuousShake();
        
        // 무너지는 애니메이션
        Sequence seq = DOTween.Sequence();
        seq.Append(rectTransform.DOScale(0.8f, 0.1f));
        seq.Append(rectTransform.DOScale(0f, 0.2f));
        seq.Join(lineImage.DOFade(0f, 0.2f));
        seq.OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    private void OnDestroy()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        lineImage?.DOKill();
        rectTransform?.DOKill();
    }
}