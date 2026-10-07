using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

public class MapNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public MapNodeData NodeData { get; private set; }
    
    [SerializeField] private Image iconImage;
    [SerializeField] private Image indexImage;
    [SerializeField] private TMP_Text indexText;
    [SerializeField] private Button button;
    private bool isReachable;
    public bool IsSelectable => button != null && button.interactable;
    private float baseScale = 1.0f;

    // You would typically assign these via a Manager or ScriptableObject database
    public void Initialize(MapNodeData data, Sprite icon, System.Action<MapNodeData> onClick)
    {
        this.NodeData = data;
        
        if (iconImage != null) iconImage.sprite = icon;
        if (indexText != null) indexText.text = (data.index + 1).ToString();
        
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(data));
        
        // Set name for hierarchy clarity
        gameObject.name = $"Node_{data.floor}_{data.index}_{data.type}";
    }

    public void SetInteractable(bool interactable)
    {
        button.interactable = interactable;
    }

    public void SetDisplayIndex(int activeIndex)
    {
        bool isActiveIndex = activeIndex >= 0;

        if (indexImage != null)
        {
            indexImage.gameObject.SetActive(isActiveIndex);
        }

        if (indexText != null)
        {
            if (!isActiveIndex)
            {
                indexText.text = "";
            }
            else
            {
                indexText.text = (activeIndex + 1).ToString();
            }
        }
    }

    public void UpdateVisualState(bool isCurrent, bool isReachable, bool isVisited)
    {
        this.isReachable = isReachable;
        transform.DOKill();

        baseScale = 1.0f;
        if (MapManager.Instance != null && MapManager.Instance.MapConfig != null)
        {
            baseScale = MapManager.Instance.MapConfig.nodeSize;
        }

        transform.localScale = Vector3.one * baseScale;

        if (iconImage != null)
        {
            Color targetColor = Color.white;
            float targetAlpha = 1.0f;

            if (isCurrent)
            {
                targetAlpha = 1.0f;
                targetColor = Color.green;
                transform.localScale = Vector3.one * baseScale * 1.2f;
            }
            else if (isVisited)
            {
                targetAlpha = 1.0f; // 지나온 길은 잘 보이게 (회색)
                targetColor = Color.gray; // 지나온 노드는 회색조로
            }
            else if (isReachable)
            {
                targetAlpha = 1.0f;
                targetColor = Color.white;

                // 펄스 애니메이션 복구 (기본 상태에서 계속 움직임)
                transform.DOScale(baseScale * 1.15f, 0.6f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
            else
            {
                targetAlpha = 0.3f; // 나머지는 더 흐릿하게 반투명
                targetColor = Color.white;
            }

            targetColor.a = targetAlpha;
            iconImage.color = targetColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isReachable)
        {
            transform.DOKill();
            transform.DOScale(baseScale * 1.3f, 0.2f).SetEase(Ease.OutQuad); // 호버 시 1.3배로 더 크게 확대
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isReachable)
        {
            transform.DOKill();
            // 원래 크기로 줄어든 뒤 다시 펄스 애니메이션 시작
            transform.DOScale(baseScale, 0.2f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                transform.DOScale(baseScale * 1.15f, 0.6f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            });
        }
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}
