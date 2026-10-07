using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

// IPointerEnterHandler, IPointerExitHandler 인터페이스를 상속받습니다.
public class UIMouseDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Vector2 originalAnchorPos;
    public RectTransform uiRectTransform;

    private void Awake()
    {
        originalAnchorPos = uiRectTransform.anchoredPosition;
    }
    public void MoveUIPosition(Vector2 targetAnchorPos, float duration)
    {
        // 현재 위치에서 targetAnchorPos로 duration 시간 동안 이동
        uiRectTransform.DOAnchorPos(targetAnchorPos, duration)
            .SetEase(Ease.OutBack) // 목적지에서 살짝 튕기는 애니메이션 (레트로 게임 UI에 잘 어울림)
            .OnComplete(() => 
            {
                Debug.Log("UI 이동 완료!");
            });
    }
    // 마우스가 UI 영역 안으로 들어올 때 실행됩니다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        MoveUIPosition(originalAnchorPos + new Vector2(64, 0), 0.5f); // 예시: UI를 화면 중앙으로 이동
    }

    // 마우스가 UI 영역 밖으로 나갈 때 실행됩니다.
    public void OnPointerExit(PointerEventData eventData)
    {
        MoveUIPosition(originalAnchorPos, 0.5f); // 예시: UI를 원래 위치로 이동
    }
}