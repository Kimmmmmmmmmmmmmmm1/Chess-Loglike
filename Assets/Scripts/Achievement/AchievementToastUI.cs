using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 업적 달성 토스트 메시지의 개별 뷰 및 애니메이션 컴포넌트입니다.
/// 인스펙터에서 프리팹으로 커스텀 바인딩하거나, 프리팹이 없을 경우 기본 UI를 코드로 자동 생성합니다.
/// </summary>
[DisallowMultipleComponent]
public class AchievementToastUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private RectTransform containerRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image accentBar;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject iconFrameRoot;
    [SerializeField] private TextMeshProUGUI fallbackIconBadge;
    [SerializeField] private TextMeshProUGUI tagText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Animation Settings")]
    [SerializeField] private float slideDistance = 140f;
    [SerializeField] private float enterDuration = 0.35f;
    [SerializeField] private float exitDuration = 0.28f;
    [SerializeField] private Ease enterEase = Ease.OutCubic;
    [SerializeField] private Ease exitEase = Ease.InCubic;

    private Sequence activeSequence;
    private Vector2 defaultAnchoredPosition = new Vector2(-24f, -24f);

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        KillActiveSequence();
    }

    public void ResolveReferences()
    {
        if (containerRect == null) containerRect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// 토스트를 지정된 시간(초) 동안 화면 우상단에 표시한 뒤 사라지게 합니다.
    /// </summary>
    public void PlayToast(AchievementData achievement, float displayDuration, Vector2 targetAnchoredPos, Action onCompleted)
    {
        if (achievement == null)
        {
            onCompleted?.Invoke();
            return;
        }

        ResolveReferences();
        KillActiveSequence();

        defaultAnchoredPosition = targetAnchoredPos;
        SetContent(achievement);

        // 초기 상태: 화면 오른쪽 바깥쪽으로 오프셋, 투명도 0, 약간 축소
        Vector2 startPos = defaultAnchoredPosition + new Vector2(slideDistance, 0f);
        Vector2 endPos = defaultAnchoredPosition + new Vector2(slideDistance * 0.8f, 0f);

        containerRect.anchoredPosition = startPos;
        containerRect.localScale = Vector3.one * 0.92f;
        canvasGroup.alpha = 0f;
        gameObject.SetActive(true);

        activeSequence = DOTween.Sequence();
        activeSequence.SetUpdate(true); // 일시정지(TimeScale = 0) 상태에서도 정상 표시

        // 1. 등장 연출 (슬라이드 인 + 페이드 인 + 스케일 복귀)
        activeSequence.Append(containerRect.DOAnchorPos(defaultAnchoredPosition, enterDuration).SetEase(enterEase));
        activeSequence.Join(canvasGroup.DOFade(1f, enterDuration * 0.8f).SetEase(Ease.OutQuad));
        activeSequence.Join(containerRect.DOScale(1f, enterDuration).SetEase(Ease.OutBack, 1.15f));

        // 2. 유지 시간 (요구사항: 1.5초)
        activeSequence.AppendInterval(Mathf.Max(0.1f, displayDuration));

        // 3. 퇴장 연출 (오른쪽으로 슬라이드 아웃 + 페이드 아웃)
        activeSequence.Append(containerRect.DOAnchorPos(endPos, exitDuration).SetEase(exitEase));
        activeSequence.Join(canvasGroup.DOFade(0f, exitDuration).SetEase(Ease.InQuad));
        activeSequence.Join(containerRect.DOScale(0.92f, exitDuration).SetEase(exitEase));

        activeSequence.OnComplete(() =>
        {
            gameObject.SetActive(false);
            onCompleted?.Invoke();
        });
    }

    private void SetContent(AchievementData achievement)
    {
        if (tagText != null)
        {
            tagText.text = "✦ 업적 달성!";
        }

        if (titleText != null)
        {
            titleText.text = !string.IsNullOrEmpty(achievement.achievementName)
                ? achievement.achievementName
                : achievement.name;
        }

        if (descriptionText != null)
        {
            descriptionText.text = achievement.description ?? string.Empty;
        }

        // 아이콘 적용
        bool hasIcon = achievement.icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = achievement.icon;
            iconImage.enabled = hasIcon;
            iconImage.gameObject.SetActive(hasIcon);
        }

        if (fallbackIconBadge != null)
        {
            fallbackIconBadge.gameObject.SetActive(!hasIcon);
            if (!hasIcon)
            {
                fallbackIconBadge.text = "🏆";
            }
        }
    }

    public void KillActiveSequence()
    {
        if (activeSequence != null && activeSequence.IsActive())
        {
            activeSequence.Kill();
            activeSequence = null;
        }
    }

    /// <summary>
    /// 프리팹이 없을 때 런타임에서 기본 토스트 UI 오브젝트를 완전하게 생성합니다.
    /// </summary>
    public static AchievementToastUI CreateDefault(Transform parent)
    {
        TMP_FontAsset font = ResolveDefaultFont();

        // 1. 루트 컨테이너
        GameObject root = new GameObject("AchievementToast", typeof(RectTransform), typeof(CanvasGroup));
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
        }

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.sizeDelta = new Vector2(380f, 84f);
        rootRect.anchoredPosition = new Vector2(-24f, -24f);

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = 0f;

        // 배경 이미지 (고급스러운 짙은 흑갈색 슬레이트)
        Image bgImg = root.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.10f, 0.09f, 0.95f);
        bgImg.raycastTarget = false;

        // 외곽선 (따뜻한 골드 톤)
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.68f, 0.32f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 그림자
        Shadow shadow = root.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(3f, -3f);

        // 2. 좌측 골드 포인트 바
        GameObject accentObj = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        accentObj.transform.SetParent(root.transform, false);
        RectTransform accentRect = accentObj.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(5f, 0f);
        accentRect.anchoredPosition = Vector2.zero;
        Image accentImg = accentObj.GetComponent<Image>();
        accentImg.color = new Color(0.96f, 0.78f, 0.32f, 1f);
        accentImg.raycastTarget = false;

        // 3. 아이콘 프레임 (좌측)
        GameObject frameObj = new GameObject("IconFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
        frameObj.transform.SetParent(root.transform, false);
        RectTransform frameRect = frameObj.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, 0.5f);
        frameRect.anchorMax = new Vector2(0f, 0.5f);
        frameRect.pivot = new Vector2(0f, 0.5f);
        frameRect.sizeDelta = new Vector2(54f, 54f);
        frameRect.anchoredPosition = new Vector2(16f, 0f);

        Image frameImg = frameObj.GetComponent<Image>();
        frameImg.color = new Color(0.18f, 0.15f, 0.13f, 1f);
        frameImg.raycastTarget = false;

        Outline frameOutline = frameObj.GetComponent<Outline>();
        frameOutline.effectColor = new Color(0.65f, 0.52f, 0.28f, 0.75f);
        frameOutline.effectDistance = new Vector2(1f, -1f);

        // 3-1. 아이콘 이미지
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(frameObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.sizeDelta = new Vector2(-8f, -8f);
        iconRect.anchoredPosition = Vector2.zero;
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // 3-2. 폴백 배지 텍스트 (아이콘 없을 때 트로피 아이콘)
        GameObject fallbackBadgeObj = new GameObject("FallbackBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
        fallbackBadgeObj.transform.SetParent(frameObj.transform, false);
        RectTransform badgeRect = fallbackBadgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = Vector2.zero;
        badgeRect.anchorMax = Vector2.one;
        badgeRect.sizeDelta = Vector2.zero;
        badgeRect.anchoredPosition = Vector2.zero;
        TextMeshProUGUI badgeTMP = fallbackBadgeObj.GetComponent<TextMeshProUGUI>();
        if (font != null) badgeTMP.font = font;
        badgeTMP.text = "🏆";
        badgeTMP.fontSize = 26f;
        badgeTMP.alignment = TextAlignmentOptions.Center;
        badgeTMP.color = new Color(0.96f, 0.78f, 0.32f, 1f);
        badgeTMP.raycastTarget = false;

        // 4. 텍스트 컨테이너
        GameObject textContainer = new GameObject("TextContainer", typeof(RectTransform));
        textContainer.transform.SetParent(root.transform, false);
        RectTransform textContainerRect = textContainer.GetComponent<RectTransform>();
        textContainerRect.anchorMin = new Vector2(0f, 0f);
        textContainerRect.anchorMax = new Vector2(1f, 1f);
        textContainerRect.offsetMin = new Vector2(80f, 6f);
        textContainerRect.offsetMax = new Vector2(-12f, -6f);

        // 4-1. 태그 텍스트 ("✦ 업적 달성!")
        GameObject tagObj = new GameObject("TagText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tagObj.transform.SetParent(textContainer.transform, false);
        RectTransform tagRect = tagObj.GetComponent<RectTransform>();
        tagRect.anchorMin = new Vector2(0f, 1f);
        tagRect.anchorMax = new Vector2(1f, 1f);
        tagRect.pivot = new Vector2(0f, 1f);
        tagRect.anchoredPosition = new Vector2(0f, 0f);
        tagRect.sizeDelta = new Vector2(0f, 18f);

        TextMeshProUGUI tagTMP = tagObj.GetComponent<TextMeshProUGUI>();
        if (font != null) tagTMP.font = font;
        tagTMP.text = "✦ 업적 달성!";
        tagTMP.fontSize = 12f;
        tagTMP.fontStyle = FontStyles.Bold;
        tagTMP.color = new Color(0.96f, 0.78f, 0.32f, 1f);
        tagTMP.raycastTarget = false;

        // 4-2. 제목 텍스트
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(textContainer.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -18f);
        titleRect.sizeDelta = new Vector2(0f, 24f);

        TextMeshProUGUI titleTMP = titleObj.GetComponent<TextMeshProUGUI>();
        if (font != null) titleTMP.font = font;
        titleTMP.text = "업적 이름";
        titleTMP.fontSize = 16f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.98f, 0.94f, 1f);
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.maxVisibleLines = 1;
        titleTMP.raycastTarget = false;

        // 4-3. 설명 텍스트
        GameObject descObj = new GameObject("DescText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descObj.transform.SetParent(textContainer.transform, false);
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0f, 0f);
        descRect.offsetMin = new Vector2(0f, 0f);
        descRect.offsetMax = new Vector2(0f, -42f);

        TextMeshProUGUI descTMP = descObj.GetComponent<TextMeshProUGUI>();
        if (font != null) descTMP.font = font;
        descTMP.text = "업적 설명";
        descTMP.fontSize = 11.5f;
        descTMP.color = new Color(0.82f, 0.78f, 0.72f, 0.9f);
        descTMP.overflowMode = TextOverflowModes.Ellipsis;
        descTMP.maxVisibleLines = 2;
        descTMP.raycastTarget = false;

        // ToastUI 컴포넌트 부착 및 필드 바인딩
        AchievementToastUI toastUI = root.AddComponent<AchievementToastUI>();
        toastUI.containerRect = rootRect;
        toastUI.canvasGroup = cg;
        toastUI.backgroundImage = bgImg;
        toastUI.accentBar = accentImg;
        toastUI.iconImage = iconImg;
        toastUI.iconFrameRoot = frameObj;
        toastUI.fallbackIconBadge = badgeTMP;
        toastUI.tagText = tagTMP;
        toastUI.titleText = titleTMP;
        toastUI.descriptionText = descTMP;

        root.SetActive(false);
        return toastUI;
    }

    private static TMP_FontAsset ResolveDefaultFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (fonts != null && fonts.Length > 0)
        {
            return fonts[0];
        }

        return null;
    }
}

