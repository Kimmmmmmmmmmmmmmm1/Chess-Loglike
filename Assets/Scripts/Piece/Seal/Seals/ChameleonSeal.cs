using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 카멜레온의 인장: 이 인장을 단 기물이 적 기물을 잡으면 잡은 기물의 종류로 변신합니다.
/// 인장은 유지되므로 다른 기물을 잡을 때마다 계속 변신합니다.
/// 프로모션과 유사한 에너지 파동 연출을 원형(Aura Ring & Circle)으로 재생합니다.
/// </summary>
public class ChameleonSeal : SealBase
{
    [Header("Chameleon Effect Settings")]
    [SerializeField] private Color auraColor = new Color(0.2f, 0.95f, 0.6f, 0.85f); // 에메랄드/카멜레온 그린
    [SerializeField] private float effectDuration = 0.5f;
    [SerializeField] private float maxCircleRadius = 140f;

    private static Sprite runtimeCircleSprite;

    public override void OnAfterCapture(PieceType capturedType)
    {
        if (owner == null) return;
        if (owner.Type == capturedType) return; // 동일한 기물이면 불필요

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(ChameleonTransformRoutine(capturedType));
        }
        else
        {
            owner.ChangeType(capturedType);
        }
    }

    private IEnumerator ChameleonTransformRoutine(PieceType targetType)
    {
        // 1단계: 변신 전 응축 효과 및 사운드
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SFXType.OpenPanel);
        }

        // 기물이 에너지를 모으듯 살짝 떨림
        owner.transform.DOShakePosition(effectDuration * 0.5f, strength: new Vector3(4f, 4f, 0f), vibrato: 15, randomness: 0, snapping: false, fadeOut: true);

        // 원형 오라 이펙트 생성
        GameObject effectGo = CreateCircleEffectObject(out Image effectImage, out RectTransform effectRect);

        if (effectRect != null && effectImage != null)
        {
            effectRect.sizeDelta = Vector2.zero;
            effectImage.color = auraColor;

            // 원형으로 팽창하며 퍼지는 연출 (OutQuad)
            effectRect.DOSizeDelta(new Vector2(maxCircleRadius * 2f, maxCircleRadius * 2f), effectDuration * 0.6f)
                .SetEase(Ease.OutBack);
        }

        yield return new WaitForSeconds(effectDuration * 0.5f);

        // 2단계: 기물 종류 변신 및 타격감
        // 프로모션 후 다른 기물을 잡았다면 승급의 인장/승급 상태를 해제하여 라운드 종료 시 폰으로 돌아가지 않음
        owner.RemovePromotionSeal();
        owner.ChangeType(targetType);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SFXType.Confirm);
        }
        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.PlayCameraShake(0.18f, 0.35f);
        }

        // 등장 펀치 스케일 (프로모션 스타일)
        owner.transform.DOPunchScale(new Vector3(0.35f, 0.35f, 0f), 0.35f, 7, 0.5f);

        // 3단계: 원형 이펙트 페이드 아웃
        if (effectImage != null)
        {
            effectImage.DOFade(0f, effectDuration * 0.5f).OnComplete(() =>
            {
                if (effectGo != null) Destroy(effectGo);
            });
        }
        else if (effectGo != null)
        {
            Destroy(effectGo);
        }
    }

    private GameObject CreateCircleEffectObject(out Image effectImage, out RectTransform effectRect)
    {
        effectImage = null;
        effectRect = null;

        Canvas parentCanvas = owner.GetComponentInParent<Canvas>();
        RectTransform canvasRect = parentCanvas != null ? parentCanvas.GetComponent<RectTransform>() : null;
        if (canvasRect == null)
        {
            Canvas c = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (c != null) canvasRect = c.GetComponent<RectTransform>();
        }

        if (canvasRect == null) return null;

        GameObject go = new GameObject("ChameleonCircleEffect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvasRect, false);

        effectImage = go.GetComponent<Image>();
        effectImage.sprite = GetOrCreateCircleSprite();
        effectImage.raycastTarget = false;

        effectRect = effectImage.rectTransform;
        effectRect.pivot = new Vector2(0.5f, 0.5f);

        // 기물의 중심 위치를 캔버스 로컬 좌표로 변환
        Camera cam = canvasRect.GetComponentInParent<Canvas>()?.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, owner.transform.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out Vector2 localPoint);
        effectRect.anchoredPosition = localPoint;

        return go;
    }

    /// <summary>
    /// 매끄러운 원형 및 링 형태의 그라데이션 스프라이트를 런타임에 동적으로 생성합니다.
    /// 외부 리소스 의존 없이 항상 깔끔한 원형 오라를 렌더링합니다.
    /// </summary>
    private static Sprite GetOrCreateCircleSprite()
    {
        if (runtimeCircleSprite != null) return runtimeCircleSprite;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float maxR = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float norm = dist / maxR;

                if (norm > 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    // 중심부는 은은하고(0.4), 외곽 링 부근(0.65~0.85)에서 밝고 진하게 빛나며 바깥쪽으로 부드럽게 감쇠되는 오라 형태
                    float alpha = 0f;
                    if (norm < 0.7f)
                    {
                        alpha = Mathf.Lerp(0.35f, 0.95f, norm / 0.7f);
                    }
                    else
                    {
                        alpha = Mathf.Lerp(0.95f, 0f, (norm - 0.7f) / 0.3f);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        tex.Apply();

        runtimeCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return runtimeCircleSprite;
    }
}
