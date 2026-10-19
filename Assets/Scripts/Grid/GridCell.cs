using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class GridCell : MonoBehaviour, IPointerClickHandler
{
    public Vector2Int gridPosition;
    public RectTransform rectTransform;
    public bool isDestroyed = false;
    private Vector2 originalPosition;
    
    private Image cellImage;
    private Color originalColor;
    
    public Image CellImage => cellImage;
    public Color OriginalColor => originalColor;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isDestroyed && ShotgunKingSystem.TryHandleShotAimClick(gridPosition, eventData.button))
        {
            eventData.Use();
        }
    }
    
    private Coroutine shakeCoroutine;

    public void Initialize(Vector2Int position, RectTransform rect, Vector2 anchoredPos, Vector2 size, Color tileColor)
    {
        gridPosition = position;
        rectTransform = rect;
        rectTransform.anchoredPosition = anchoredPos;
        originalPosition = anchoredPos;
        rectTransform.sizeDelta = size;
        isDestroyed = false;
        
        cellImage = GetComponent<Image>();
        if (cellImage != null)
        {
            cellImage.color = tileColor;
            originalColor = tileColor;
        }
    }

    private bool isLastMoveHighlighted = false;

    public void SetGray(bool isGray, Color grayColor)
    {
        if (cellImage != null)
        {
            cellImage.DOKill();
            cellImage.color = isGray ? grayColor : originalColor;
        }
    }

    public void SetLastMoveHighlight(bool highlight, Color highlightColor)
    {
        isLastMoveHighlighted = highlight;
        if (cellImage != null)
        {
            cellImage.DOKill();
            if (highlight)
            {
                cellImage.DOColor(highlightColor, 0.2f).SetEase(Ease.OutQuad);
            }
            else
            {
                cellImage.DOColor(originalColor, 0.2f).SetEase(Ease.OutQuad);
            }
        }
    }

    public void ResetHighlight()
    {
        if (cellImage != null)
        {
            cellImage.DOKill();
            isLastMoveHighlighted = false;
            cellImage.color = originalColor;
        }
    }

    public void SetTileColor(Color color)
    {
        if (cellImage != null)
        {
            cellImage.DOKill();
            cellImage.color = color;
            originalColor = color;
        }
    }

    /// <summary>
    /// 그리드 셀에 흔들 효과를 적용합니다.
    /// </summary>
    public void Shake(float duration = 0.5f, float strength = 5f)
    {
        rectTransform.DOKill();
        rectTransform.DOShakeAnchorPos(duration, strength, 10, 90f);
    }

    /// <summary>
    /// 그리드 셀을 계속 흔들 효과를 적용합니다.
    /// </summary>
    public void StartContinuousShake(float duration = 0.5f, float strength = 5f)
    {
        StopContinuousShake();
        shakeCoroutine = StartCoroutine(ContinuousShakeCoroutine(duration, strength));
    }

    /// <summary>
    /// 그리드 셀의 계속 흔들기를 중지합니다.
    /// </summary>
    public void StopContinuousShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        rectTransform.DOKill();
    }

    private System.Collections.IEnumerator ContinuousShakeCoroutine(float duration, float strength)
    {
        while (gameObject.activeSelf)
        {
            rectTransform.DOKill();
            rectTransform.DOShakeAnchorPos(duration, strength, 10, 90f);
            yield return new WaitForSeconds(duration);
        }
    }

    /// <summary>
    /// 그리드 셀을 파괴합니다. (위에 올라와 있던 기물도 함께 제거)
    /// </summary>
    public void DestroyCell()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // 이 셀에 있던 기물 제거 (붕괴 연출 적용)
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.RemovePieceAtPositionByCollapse(gridPosition);
        }

        // 사라지는 애니메이션
        rectTransform.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(rectTransform.DOScale(1.2f, 0.1f));
        seq.Append(rectTransform.DOScale(0f, 0.2f));
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
        cellImage?.DOKill();
        rectTransform?.DOKill();
    }
}
