using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class GridCell : MonoBehaviour
{
    public Vector2Int gridPosition;
    public RectTransform rectTransform;
    private Vector2 originalPosition;
    
    private Image cellImage;
    private Color originalColor;
    
    private Coroutine shakeCoroutine;

    public void Initialize(Vector2Int position, RectTransform rect, Vector2 anchoredPos, Vector2 size)
    {
        gridPosition = position;
        rectTransform = rect;
        rectTransform.anchoredPosition = anchoredPos;
        originalPosition = anchoredPos;
        rectTransform.sizeDelta = size;
        
        cellImage = GetComponent<Image>();
        if (cellImage != null)
        {
            originalColor = cellImage.color;
        }
    }

    public void SetGray(bool isGray, Color grayColor)
    {
        // 셀은 회색 피드백을 표시하지 않음 (이미지 없음)
    }

    /// <summary>
    /// 그리드 셀에 흔들 효과를 적용합니다.
    /// </summary>
    /// <param name="duration">흔들 지속 시간</param>
    /// <param name="strength">흔들 강도</param>
    public void Shake(float duration = 0.5f, float strength = 5f)
    {
        rectTransform.DOKill();
        rectTransform.DOShakeAnchorPos(duration, strength, 10, 90f);
    }

    /// <summary>
    /// 그리드 셀을 계속 흔들 효과를 적용합니다.
    /// </summary>
    /// <param name="duration">반복 흔들 지속 시간</param>
    /// <param name="strength">흔들 강도</param>
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
    /// 주변 GridPoint가 연결된 Line이 없으면 GridPoint도 함께 파괴합니다.
    /// </summary>
    public void DestroyCell()
    {
        // 이 셀에 있던 기물 제거
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.RemovePieceAtPosition(gridPosition);
        }

        // 이 셀을 이루는 4개 GridPoint 확인 및 파괴 체크
        CheckAndDestroyRelatedGridPoints();

        // 사라지는 애니메이션
        rectTransform.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(rectTransform.DOScale(1.2f, 0.1f));
        seq.Append(rectTransform.DOScale(0f, 0.2f));
        seq.OnComplete(() =>
        {
            // 셀 비활성화 (필요시 GameObject 제거)
            gameObject.SetActive(false);
        });
    }

    /// <summary>
    /// GridCell(x, y)를 이루는 4개 GridPoint를 확인하고,
    /// 각 GridPoint의 라인이 모두 파괴되었다면 GridPoint도 파괴합니다.
    /// GridCell(x, y)는 다음 4개 GridPoint와 연결됩니다:
    /// - (x, y)       : 좌측 하단
    /// - (x+1, y)     : 우측 하단
    /// - (x, y+1)     : 좌측 상단
    /// - (x+1, y+1)   : 우측 상단
    /// </summary>
    private void CheckAndDestroyRelatedGridPoints()
    {
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null) return;

        Vector2Int[] relatedPoints = new Vector2Int[]
        {
            gridPosition,
            gridPosition + Vector2Int.right,
            gridPosition + Vector2Int.up,
            gridPosition + Vector2Int.right + Vector2Int.up
        };

        foreach (Vector2Int pointPos in relatedPoints)
        {
            GridPoint gridPoint = gridManager.GetGridPoint(pointPos);
            if (gridPoint != null && !gridPoint.isDestroyed)
            {
                TryDestroyGridPoint(gridPoint);
            }
        }
    }

    /// <summary>
    /// GridPoint의 모든 라인이 비활성화되었다면 GridPoint를 파괴합니다.
    /// </summary>
    private void TryDestroyGridPoint(GridPoint gridPoint)
    {
        bool hasActiveLine = false;

        // 4개의 라인 중 활성화된 것이 있는지 확인
        if (gridPoint.lineTop != null && gridPoint.lineTop.gameObject.activeInHierarchy)
            hasActiveLine = true;
        else if (gridPoint.lineBottom != null && gridPoint.lineBottom.gameObject.activeInHierarchy)
            hasActiveLine = true;
        else if (gridPoint.lineLeft != null && gridPoint.lineLeft.gameObject.activeInHierarchy)
            hasActiveLine = true;
        else if (gridPoint.lineRight != null && gridPoint.lineRight.gameObject.activeInHierarchy)
            hasActiveLine = true;

        // 활성화된 라인이 없으면 GridPoint 파괴
        if (!hasActiveLine)
        {
            gridPoint.DestroyIntersectingLines();
        }
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
