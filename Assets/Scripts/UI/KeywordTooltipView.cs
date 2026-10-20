// Copyright (c) 2024 ChessLoglike. All rights reserved.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 키워드 상태 정보 데이터 구조체
/// </summary>
[System.Serializable]
public struct KeywordInfo
{
    public string Name;
    public string CountText;
    public string Description;
    public Color ThemeColor;

    public KeywordInfo(string name, string countText, string description, Color color)
    {
        Name = name;
        CountText = countText;
        Description = description;
        ThemeColor = color;
    }
}

/// <summary>
/// 미니멀 화이트 테마(Solid White Card) 키워드 상태 카드 툴팁
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class KeywordTooltipView : MonoBehaviour
{
    private const float CardWidth = 330f;

    [Header("UI Root References")]
    [SerializeField] private RectTransform cardRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image shadowImage;
    [SerializeField] private Image cardBorderImage;
    [SerializeField] private Image cardBodyImage;

    [Header("Header Elements")]
    [SerializeField] private Image badgeBg;
    [SerializeField] private TextMeshProUGUI badgeText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Content Container")]
    [SerializeField] private RectTransform entriesContainer;
    [SerializeField] private TextMeshProUGUI footerTipText;

    private static Sprite solidWhiteSprite;
    private Tweener popScaleTween;
    private Tweener popFadeTween;
    private RectTransform cachedRectTransform;
    private RectTransform shadowRectTransform;
    private bool isHierarchyInitialized;

    private readonly List<GameObject> activeItemObjects = new List<GameObject>();

    public RectTransform RectTransform
    {
        get
        {
            if (cachedRectTransform == null)
            {
                cachedRectTransform = GetComponent<RectTransform>();
                if (cachedRectTransform == null)
                {
                    cachedRectTransform = gameObject.AddComponent<RectTransform>();
                }
            }
            return cachedRectTransform;
        }
    }

    private void Awake()
    {
        EnsureProceduralSprites();
        EnsureUIHierarchy();
        ApplyWhiteTheme();
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void ApplyWhiteTheme()
    {
        if (shadowImage != null) shadowImage.color = new Color(0f, 0f, 0f, 0.25f);
        if (cardBorderImage != null) cardBorderImage.color = new Color(0.22f, 0.24f, 0.28f, 1f);
        if (cardBodyImage != null) cardBodyImage.color = Color.white;
        if (badgeBg != null) badgeBg.color = new Color(0.22f, 0.24f, 0.28f, 1f);
        if (titleText != null) titleText.color = new Color(0.10f, 0.11f, 0.14f, 1f);
        if (subtitleText != null) subtitleText.color = new Color(0.42f, 0.45f, 0.52f, 1f);
        if (footerTipText != null) footerTipText.color = new Color(0.46f, 0.49f, 0.56f, 1f);
    }

    private static void EnsureProceduralSprites()
    {
        if (solidWhiteSprite == null)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] colors = { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(colors);
            tex.Apply();
            solidWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }
    }

    /// <summary>
    /// UI 계층 구조를 코드로 자동 생성/복구 (PieceCardTooltipView와 동일한 디자인 시스템)
    /// </summary>
    public void EnsureUIHierarchy()
    {
        if (isHierarchyInitialized && cardRoot != null && entriesContainer != null) return;

        EnsureProceduralSprites();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/NeoDunggeunmoPro-Regular SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("NeoDunggeunmoPro-Regular SDF");
        if (font == null)
        {
            var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < allFonts.Length; i++)
            {
                if (allFonts[i] != null && allFonts[i].name.Contains("NeoDunggeunmo"))
                {
                    font = allFonts[i];
                    break;
                }
            }
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;

        RectTransform rootRt = RectTransform;
        rootRt.sizeDelta = new Vector2(CardWidth, 320f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // 1. Drop Shadow
        Transform shadowT = transform.Find("Shadow");
        GameObject shadowObj = shadowT != null ? shadowT.gameObject : new GameObject("Shadow", typeof(RectTransform));
        shadowObj.transform.SetParent(transform, false);
        shadowRectTransform = shadowObj.GetComponent<RectTransform>();
        shadowRectTransform.sizeDelta = new Vector2(CardWidth + 12f, 332f);
        shadowRectTransform.anchoredPosition = new Vector2(8f, -10f);
        shadowImage = shadowObj.GetComponent<Image>() ?? shadowObj.AddComponent<Image>();
        shadowImage.sprite = solidWhiteSprite;
        shadowImage.color = new Color(0f, 0f, 0f, 0.25f);
        shadowImage.raycastTarget = false;

        // 2. Card Root (2px Solid Dark Border)
        Transform cardT = transform.Find("CardRoot");
        GameObject cardObj = cardT != null ? cardT.gameObject : new GameObject("CardRoot", typeof(RectTransform));
        cardObj.transform.SetParent(transform, false);
        cardRoot = cardObj.GetComponent<RectTransform>();
        cardRoot.sizeDelta = new Vector2(CardWidth, 320f);
        cardBorderImage = cardObj.GetComponent<Image>() ?? cardObj.AddComponent<Image>();
        cardBorderImage.sprite = solidWhiteSprite;
        cardBorderImage.color = new Color(0.22f, 0.24f, 0.28f, 1f);
        cardBorderImage.raycastTarget = false;

        // 3. Card Body (White Fill)
        Transform bodyT = cardRoot.Find("Body");
        GameObject bodyObj = bodyT != null ? bodyT.gameObject : new GameObject("Body", typeof(RectTransform));
        bodyObj.transform.SetParent(cardRoot, false);
        RectTransform bodyRt = bodyObj.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(2f, 2f);
        bodyRt.offsetMax = new Vector2(-2f, -2f);
        cardBodyImage = bodyObj.GetComponent<Image>() ?? bodyObj.AddComponent<Image>();
        cardBodyImage.sprite = solidWhiteSprite;
        cardBodyImage.color = Color.white; // #FFFFFF 화이트 카드
        cardBodyImage.raycastTarget = false;

        // Clean up old children in Body if any
        for (int i = bodyRt.childCount - 1; i >= 0; i--)
        {
            Transform child = bodyRt.GetChild(i);
            child.SetParent(null, false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        // --- 상단 헤더 (Header) ---
        // 높이: 58px
        GameObject headerObj = CreateSubPanel("Header", bodyRt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -29f), new Vector2(0, 58f));
        
        // 가치/타입 뱃지 (좌측 상단 모서리, 40x40)
        GameObject badgeObj = CreateSubPanel("Badge", headerObj.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30f, 0), new Vector2(40f, 40f));
        badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.sprite = solidWhiteSprite;
        badgeBg.color = new Color(0.22f, 0.24f, 0.28f, 1f);
        badgeBg.raycastTarget = false;

        GameObject badgeInner = CreateSubPanel("BadgeInner", badgeObj.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-4, -4));
        Image badgeInnerImg = badgeInner.AddComponent<Image>();
        badgeInnerImg.sprite = solidWhiteSprite;
        badgeInnerImg.color = new Color(0.14f, 0.16f, 0.20f, 1f);
        badgeInnerImg.raycastTarget = false;

        badgeText = CreateTMPText("BadgeText", badgeInner.GetComponent<RectTransform>(), font, 20f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        badgeText.text = "◆";

        // 타이틀 패널 (뱃지 오른쪽)
        GameObject titlePanel = CreateSubPanel("TitlePanel", headerObj.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        RectTransform titlePanelRt = titlePanel.GetComponent<RectTransform>();
        titlePanelRt.offsetMin = new Vector2(58f, 0);
        titlePanelRt.offsetMax = new Vector2(-12f, 0);

        titleText = CreateTMPText("TitleText", titlePanelRt, font, 20f, FontStyles.Bold, new Color(0.10f, 0.11f, 0.14f, 1f), TextAlignmentOptions.Left);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 14f;
        titleText.fontSizeMax = 20f;
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0, 0.44f);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;
        titleText.text = "상태 효과 (Keywords)";

        subtitleText = CreateTMPText("SubtitleText", titlePanelRt, font, 14f, FontStyles.Normal, new Color(0.42f, 0.45f, 0.52f, 1f), TextAlignmentOptions.Left);
        RectTransform subRt = subtitleText.rectTransform;
        subRt.anchorMin = new Vector2(0, 0);
        subRt.anchorMax = new Vector2(1, 0.44f);
        subRt.offsetMin = subRt.offsetMax = Vector2.zero;
        subtitleText.text = "ACTIVE STATUS & BUFFS";

        // 구분선 (Y: -59)
        CreateDividerLine(bodyRt, -59f);

        // --- 컨텐츠 영역 (Vertical Layout) ---
        GameObject contentObj = CreateSubPanel("ContentContainer", bodyRt, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        entriesContainer = contentObj.GetComponent<RectTransform>();
        entriesContainer.offsetMin = new Vector2(14f, 40f);
        entriesContainer.offsetMax = new Vector2(-14f, -64f);

        VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 구분선 2 (하단 안내선)
        GameObject div2 = CreateSubPanel("DividerBottom", bodyRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36f), new Vector2(CardWidth - 28f, 1f));
        Image div2Img = div2.AddComponent<Image>();
        div2Img.sprite = solidWhiteSprite;
        div2Img.color = new Color(0.85f, 0.87f, 0.91f, 1f);
        div2Img.raycastTarget = false;

        // --- 푸터 팁 텍스트 ---
        GameObject footerObj = CreateSubPanel("FooterTip", bodyRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 18f), new Vector2(0, 28f));
        footerTipText = CreateTMPText("TipText", footerObj.GetComponent<RectTransform>(), font, 13f, FontStyles.Normal, new Color(0.46f, 0.49f, 0.56f, 1f), TextAlignmentOptions.Center);
        footerTipText.text = "※ 피격 또는 턴 진행 시 효과가 소모됩니다.";
        RectTransform tipRt = footerTipText.rectTransform;
        tipRt.anchorMin = Vector2.zero;
        tipRt.anchorMax = Vector2.one;
        tipRt.offsetMin = new Vector2(12f, 0);
        tipRt.offsetMax = new Vector2(-12f, 0);

        isHierarchyInitialized = true;
        ApplyWhiteTheme();
    }

    private GameObject CreateSubPanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return go;
    }

    private GameObject CreateDividerLine(RectTransform parent, float yPos)
    {
        GameObject div = CreateSubPanel("Divider", parent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, yPos), new Vector2(CardWidth - 28f, 1f));
        Image img = div.AddComponent<Image>();
        img.sprite = solidWhiteSprite;
        img.color = new Color(0.85f, 0.87f, 0.91f, 1f);
        img.raycastTarget = false;
        return div;
    }

    private TextMeshProUGUI CreateTMPText(string name, RectTransform parent, TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    /// <summary>
    /// 문자열 리스트로 키워드 설정 (하위 호환)
    /// </summary>
    public void SetKeywords(List<string> keywords)
    {
        if (keywords == null || keywords.Count == 0)
        {
            Hide();
            return;
        }

        List<KeywordInfo> infos = new List<KeywordInfo>();
        foreach (var kw in keywords)
        {
            switch (kw)
            {
                case "무적":
                    infos.Add(new KeywordInfo("무적 (Invincible)", "상시 지속", "모든 공격과 파괴 효과로부터 피해를 받지 않습니다.", new Color(0.1f, 0.65f, 0.25f)));
                    break;
                case "보호":
                    infos.Add(new KeywordInfo("보호 (Shield)", "×1", "다음 1회의 적 공격을 완전히 무효화합니다.", new Color(0.85f, 0.55f, 0.05f)));
                    break;
                case "회피":
                    infos.Add(new KeywordInfo("회피 (Evasion)", "×1", "피격 시 피해 없이 인접한 빈 칸으로 도약합니다.", new Color(0.1f, 0.5f, 0.9f)));
                    break;
                case "역습":
                case "역공":
                    infos.Add(new KeywordInfo("역습 (Counter)", "×1", "피격 시 반격하여 공격한 적 기물을 즉시 처치합니다.", new Color(0.85f, 0.2f, 0.2f)));
                    break;
                case "실명":
                    infos.Add(new KeywordInfo("실명 (Blindness)", "1턴 지속", "지속 시간 동안 적 기물을 포획할 수 없습니다.", new Color(0.35f, 0.35f, 0.42f)));
                    break;
                case "혼란":
                    infos.Add(new KeywordInfo("혼란 (Confusion)", "1턴 지속", "지속 시간 동안 폰의 행마법으로만 이동할 수 있습니다.", new Color(0.85f, 0.25f, 1f)));
                    break;
                default:
                    infos.Add(new KeywordInfo(kw, "활성", "기물에 적용된 특수 상태 효과입니다.", new Color(0.3f, 0.33f, 0.4f)));
                    break;
            }
        }

        SetKeywords(infos);
    }

    /// <summary>
    /// 상세 키워드 정보 리스트 설정 및 카드 리사이징
    /// </summary>
    public void SetKeywords(List<KeywordInfo> keywords)
    {
        if (keywords == null || keywords.Count == 0)
        {
            Hide();
            return;
        }

        EnsureUIHierarchy();

        // 기존 아이템 오브젝트 제거
        foreach (var item in activeItemObjects)
        {
            if (item != null)
            {
                if (Application.isPlaying) Destroy(item);
                else DestroyImmediate(item);
            }
        }
        activeItemObjects.Clear();

        TMP_FontAsset font = titleText != null ? titleText.font : null;

        float totalItemHeight = 0f;

        // 키워드별 카드 슬롯 생성
        for (int i = 0; i < keywords.Count; i++)
        {
            KeywordInfo info = keywords[i];

            GameObject entryObj = new GameObject($"Keyword_{info.Name}", typeof(RectTransform));
            entryObj.transform.SetParent(entriesContainer, false);
            activeItemObjects.Add(entryObj);

            RectTransform entryRt = entryObj.GetComponent<RectTransform>();
            entryRt.sizeDelta = new Vector2(CardWidth - 28f, 68f);

            // 항목 배경 (부드러운 라이트 그레이 박스)
            Image bg = entryObj.AddComponent<Image>();
            bg.sprite = solidWhiteSprite;
            bg.color = new Color(0.95f, 0.96f, 0.98f, 1f);
            bg.raycastTarget = false;

            // 좌측 색상 인디케이터 바
            GameObject barObj = CreateSubPanel("ColorBar", entryRt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(2f, 0), new Vector2(4f, 0));
            Image barImg = barObj.AddComponent<Image>();
            barImg.sprite = solidWhiteSprite;
            barImg.color = info.ThemeColor;
            barImg.raycastTarget = false;

            // 헤더 텍스트: ● 키워드 이름 + 스택
            string hexColor = ColorUtility.ToHtmlStringRGB(info.ThemeColor);
            string headerString = $"<color=#{hexColor}>● {info.Name}</color>  <size=85%><color=#6B7280>[{info.CountText}]</color></size>";
            TextMeshProUGUI headerTxt = CreateTMPText("Header", entryRt, font, 16f, FontStyles.Bold, new Color(0.10f, 0.11f, 0.14f, 1f), TextAlignmentOptions.TopLeft);
            RectTransform hRt = headerTxt.rectTransform;
            hRt.anchorMin = new Vector2(0, 0.5f);
            hRt.anchorMax = new Vector2(1, 1);
            hRt.offsetMin = new Vector2(12f, 0);
            hRt.offsetMax = new Vector2(-8f, -6f);
            headerTxt.text = headerString;

            // 설명 텍스트
            TextMeshProUGUI descTxt = CreateTMPText("Desc", entryRt, font, 14f, FontStyles.Normal, new Color(0.20f, 0.22f, 0.26f, 1f), TextAlignmentOptions.TopLeft);
            RectTransform dRt = descTxt.rectTransform;
            dRt.anchorMin = new Vector2(0, 0);
            dRt.anchorMax = new Vector2(1, 0.55f);
            dRt.offsetMin = new Vector2(12f, 4f);
            dRt.offsetMax = new Vector2(-8f, 0);
            descTxt.text = info.Description;

            totalItemHeight += 78f;
        }

        // 전체 카드 높이 동적 계산 (헤더 64 + 푸터 44 + 컨텐츠 높이)
        float cardHeight = Mathf.Clamp(110f + totalItemHeight, 220f, 520f);
        RectTransform.sizeDelta = new Vector2(CardWidth, cardHeight);
        if (cardRoot != null) cardRoot.sizeDelta = new Vector2(CardWidth, cardHeight);
        if (shadowRectTransform != null) shadowRectTransform.sizeDelta = new Vector2(CardWidth + 12f, cardHeight + 12f);

        Show();
    }

    /// <summary>
    /// 팝업 탄력 애니메이션 (Scale Up + Fade In)
    /// </summary>
    public void Show()
    {
        EnsureUIHierarchy();

        popScaleTween?.Kill();
        popFadeTween?.Kill();

        gameObject.SetActive(true);
        transform.localScale = Vector3.one * 0.88f;
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        popScaleTween = transform.DOScale(Vector3.one, 0.22f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);

        if (canvasGroup != null)
        {
            popFadeTween = canvasGroup.DOFade(1f, 0.16f)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// 즉시 숨기기 (호버 이탈 시 잔류 버그 방지)
    /// </summary>
    public void Hide()
    {
        popScaleTween?.Kill();
        popFadeTween?.Kill();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
}
