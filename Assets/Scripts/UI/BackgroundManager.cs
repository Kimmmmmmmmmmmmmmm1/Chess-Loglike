using UnityEngine;
using UnityEngine.UI;

public class BackgroundManager : MonoBehaviour
{
    [Header("Background Layers")]
    [SerializeField] private Image staticBackground;        // 안움직이는 배경
    [SerializeField] private Image parallaxLayer1;          // 느린 레이어 (깊이감)
    [SerializeField] private Image parallaxLayer2;          // 중간 레이어
    [SerializeField] private Image parallaxLayer3;          // 빠른 레이어 (전경)

    [Header("Parallax Settings")]
    [SerializeField] private float layer1Speed = 50f;       // 레이어 1 속도 (픽셀/초)
    [SerializeField] private float layer2Speed = 100f;      // 레이어 2 속도
    [SerializeField] private float layer3Speed = 150f;      // 레이어 3 속도
    
    [SerializeField] private float layerHeight = 640f;     // 각 레이어의 너비 (반복 단위)

    private RectTransform rectLayer1;
    private RectTransform rectLayer2;
    private RectTransform rectLayer3;
    
    private float currentOffset1 = 0f;
    private float currentOffset2 = 0f;
    private float currentOffset3 = 0f;

    private void Start()
    {
        if (parallaxLayer1 != null) 
        {
            rectLayer1 = parallaxLayer1.GetComponent<RectTransform>();
            CreateDuplicateLayer(parallaxLayer1);
        }
        if (parallaxLayer2 != null) 
        {
            rectLayer2 = parallaxLayer2.GetComponent<RectTransform>();
            CreateDuplicateLayer(parallaxLayer2);
        }
        if (parallaxLayer3 != null) 
        {
            rectLayer3 = parallaxLayer3.GetComponent<RectTransform>();
            CreateDuplicateLayer(parallaxLayer3);
        }
    }

    private void Update()
    {
        // 프레임마다 오프셋 누적
        currentOffset1 += layer1Speed * Time.deltaTime;
        currentOffset2 += layer2Speed * Time.deltaTime;
        currentOffset3 += layer3Speed * Time.deltaTime;

        // 원본 레이어만 업데이트하면 자식 복제본도 함께 따라옴
        UpdateLayerPosition(rectLayer1, currentOffset1);
        UpdateLayerPosition(rectLayer2, currentOffset2);
        UpdateLayerPosition(rectLayer3, currentOffset3);
    }

    private void UpdateLayerPosition(RectTransform layer, float offset)
    {
        if (layer == null) return;

        // 무한 반복을 위해 오프셋을 레이어 너비로 래핑
        float wrappedOffset = Mathf.Repeat(offset, layerHeight);

        // 원본 위치 업데이트
        Vector2 newPos = layer.anchoredPosition;
        newPos.x = -wrappedOffset;
        layer.anchoredPosition = newPos;
    }

    /// <summary>
    /// 레이어의 복제 자식 생성 (무한 반복용)
    /// 복제본을 원본 x좌표 + 너비 위치에 배치
    /// </summary>
    private RectTransform CreateDuplicateLayer(Image originalLayer)
    {
        if (originalLayer == null) return null;

        // 복제 생성 (원본의 자식으로)
        Image duplicate = Instantiate(originalLayer, originalLayer.transform);
        RectTransform duplicateRect = duplicate.GetComponent<RectTransform>();
        
        // 레이아웃 기준을 원본과 맞춤
        RectTransform originalRect = originalLayer.rectTransform;
        duplicateRect.anchorMin = originalRect.anchorMin;
        duplicateRect.anchorMax = originalRect.anchorMax;
        duplicateRect.pivot = originalRect.pivot;
        duplicateRect.localScale = originalRect.localScale;

        // 크기 동기화
        duplicateRect.sizeDelta = originalLayer.rectTransform.sizeDelta;
        
        // 위치 설정: 원본의 현재 위치 + 실제 너비, Y는 원본과 동일하게 유지
        float duplicateOffsetX = originalRect.rect.width > 0f ? originalRect.rect.width : layerHeight;
        Vector2 newPos = originalRect.anchoredPosition;
        newPos.x += duplicateOffsetX;
        duplicateRect.anchoredPosition = newPos;
        
        return duplicateRect;
    }

    /// <summary>
    /// 레이어 너비 재설정 (이미지 크기 기반으로 자동 계산)
    /// </summary>
    public void AutoCalculateLayerHeight()
    {
        if (parallaxLayer1 != null)
        {
            layerHeight = parallaxLayer1.rectTransform.rect.width;
        }
    }

    /// <summary>
    /// 각 레이어의 속도 조정
    /// </summary>
    public void SetLayerSpeeds(float speed1, float speed2, float speed3)
    {
        layer1Speed = speed1;
        layer2Speed = speed2;
        layer3Speed = speed3;
    }
}
