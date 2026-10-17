using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 미니멀 솔리드 흑백(Solid Black & White) 하스스톤 스타일 체스 기물 카드 툴팁
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class PieceCardTooltipView : MonoBehaviour
{
    private const int BoardCols = 6;
    private const int BoardRows = 5;

    [Header("UI Root References")]
    [SerializeField] private RectTransform cardRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image shadowImage;
    [SerializeField] private Image cardBorderImage;
    [SerializeField] private Image cardBodyImage;

    [Header("Header Elements")]
    [SerializeField] private Image valueBadgeBg;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI tierText;

    [Header("Portrait Elements")]
    [SerializeField] private Image portraitFrame;
    [SerializeField] private Image portraitImage;

    [Header("6x5 Mini Board Elements")]
    [SerializeField] private RectTransform miniBoardContainer;
    [SerializeField] private TextMeshProUGUI boardLabel;
    private Image[,] boardCells = new Image[BoardCols, BoardRows];

    [Header("Footer Elements")]
    [SerializeField] private TextMeshProUGUI ruleText;
    [SerializeField] private GameObject sealRowObject;
    [SerializeField] private Image sealIconImage;
    [SerializeField] private TextMeshProUGUI sealInfoText;
    [SerializeField] private TextMeshProUGUI flavorText;

    // Procedural Sprites (Solid, Hatched/Dotted Pattern, Origin Ring, Cross, Default Emblem)
    private static Sprite solidWhiteSprite;
    private static Sprite hatchedPatternSprite;
    private static Sprite originMarkerSprite;
    private static Sprite pawnCaptureSprite;
    private static Sprite defaultEmblemSprite;

    private static readonly Dictionary<PieceType, string> PieceKoreanNames = new Dictionary<PieceType, string>
    {
        { PieceType.King,        "킹" },
        { PieceType.Rook,        "룩" },
        { PieceType.Knight,      "나이트" },
        { PieceType.Bishop,      "비숍" },
        { PieceType.Queen,       "퀸" },
        { PieceType.Pawn,        "폰" },
        { PieceType.Amazon,      "아마존" },
        { PieceType.Chancellor,  "챈슬러" },
        { PieceType.Archbishop,  "아치비숍" },
        { PieceType.Centaur,     "켄타우로스" },
        { PieceType.Cannon,      "캐논" },
        { PieceType.Grasshopper, "그래스호퍼" }
    };

    private Tweener popScaleTween;
    private Tweener popFadeTween;
    private RectTransform cachedRectTransform;
    private RectTransform shadowRectTransform;
    private RectTransform footerRectTransform;
    private GameObject divider3Object;
    private bool isHierarchyInitialized;

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
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// 절차적 텍스처 및 스프라이트 생성 (도트 빗금 패턴, 원형 링, 캡처 마크, 기본 엠블럼)
    /// </summary>
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

        if (hatchedPatternSprite == null)
        {
            // 16x16 45도 도트 빗금 스트라이프 패턴 (2px 화이트, 2px 다크)
            Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            Color darkColor = new Color(0.12f, 0.12f, 0.15f, 1f);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    bool stripe = ((x + y) % 4) < 2;
                    tex.SetPixel(x, y, stripe ? Color.white : darkColor);
                }
            }
            tex.Apply();
            hatchedPatternSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        }

        if (originMarkerSprite == null)
        {
            // 기물 본체 위치를 나타내는 32x32 동심원 화이트 링
            Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(15.5f, 15.5f);
            Color bg = new Color(0.08f, 0.08f, 0.1f, 1f);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= 5.5f)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else if (dist >= 8.5f && dist <= 11f)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        tex.SetPixel(x, y, bg);
                    }
                }
            }
            tex.Apply();
            originMarkerSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }

        if (pawnCaptureSprite == null)
        {
            // 폰 대각선 포획용 화이트 바탕 위 십자/× 마크
            Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color crossColor = new Color(0.1f, 0.1f, 0.12f, 1f);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    bool isCross = (Mathf.Abs(x - y) <= 2 || Mathf.Abs(x - (31 - y)) <= 2) && (x >= 7 && x <= 24 && y >= 7 && y <= 24);
                    tex.SetPixel(x, y, isCross ? crossColor : Color.white);
                }
            }
            tex.Apply();
            pawnCaptureSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }

        if (defaultEmblemSprite == null)
        {
            // 일러스트가 없는 일반 툴팁용 미니멀 다이아몬드 스타 엠블럼 (64x64)
            Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = Mathf.Abs(x - center.x);
                    float dy = Mathf.Abs(y - center.y);
                    float manhattan = dx + dy;
                    bool outerRing = manhattan >= 22f && manhattan <= 25f;
                    bool innerStar = (dx * 0.45f + dy <= 10f) || (dy * 0.45f + dx <= 10f);
                    bool centerHole = manhattan <= 4f;
                    if ((outerRing || innerStar) && !centerHole)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            defaultEmblemSprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        }
    }

    /// <summary>
    /// UI 계층 구조가 비어있거나 초기화되지 않았을 경우 코드로 자동 구성
    /// </summary>
    public void EnsureUIHierarchy()
    {
        if (isHierarchyInitialized && cardRoot != null && boardCells[0, 0] != null) return;

        EnsureProceduralSprites();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/NeoDunggeunmoPro-Regular SDF");
        if (font == null)
        {
            font = Resources.Load<TMP_FontAsset>("NeoDunggeunmoPro-Regular SDF");
        }
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
        rootRt.sizeDelta = new Vector2(380, 640);
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
        shadowRectTransform.sizeDelta = new Vector2(392, 652);
        shadowRectTransform.anchoredPosition = new Vector2(10, -12);
        shadowImage = shadowObj.GetComponent<Image>() ?? shadowObj.AddComponent<Image>();
        shadowImage.sprite = solidWhiteSprite;
        shadowImage.color = new Color(0f, 0f, 0f, 0.82f);
        shadowImage.raycastTarget = false;

        // 2. Card Root (Outer Border)
        Transform cardT = transform.Find("CardRoot");
        GameObject cardObj = cardT != null ? cardT.gameObject : new GameObject("CardRoot", typeof(RectTransform));
        cardObj.transform.SetParent(transform, false);
        cardRoot = cardObj.GetComponent<RectTransform>();
        cardRoot.sizeDelta = new Vector2(380, 640);
        cardBorderImage = cardObj.GetComponent<Image>() ?? cardObj.AddComponent<Image>();
        cardBorderImage.sprite = solidWhiteSprite;
        cardBorderImage.color = Color.white; // 2px Solid White Border
        cardBorderImage.raycastTarget = false;

        // 3. Card Body (Ebony / Marble Black Fill)
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
        cardBodyImage.color = new Color(0.065f, 0.065f, 0.075f, 1f); // #111113 매끄러운 흑단목 딥 블랙
        cardBodyImage.raycastTarget = false;

        // 프리팹에 기존 하위 오브젝트가 남아있다면 중복 생성을 막기 위해 정리
        for (int i = bodyRt.childCount - 1; i >= 0; i--)
        {
            Transform child = bodyRt.GetChild(i);
            child.SetParent(null, false);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        // --- 상단 헤더 (Header) ---
        // 높이: 68px, Y: 0 ~ -68
        GameObject headerObj = CreateSubPanel("Header", bodyRt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -34), new Vector2(0, 68));
        
        // 가치(점수/등급) 뱃지 (좌측 상단 모서리, 50x50)
        GameObject badgeObj = CreateSubPanel("ValueBadge", headerObj.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(50, 50));
        valueBadgeBg = badgeObj.AddComponent<Image>();
        valueBadgeBg.sprite = solidWhiteSprite;
        valueBadgeBg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
        valueBadgeBg.raycastTarget = false;
        
        GameObject badgeInner = CreateSubPanel("BadgeInner", badgeObj.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-4, -4));
        Image badgeInnerImg = badgeInner.AddComponent<Image>();
        badgeInnerImg.sprite = solidWhiteSprite;
        badgeInnerImg.color = new Color(0.08f, 0.08f, 0.1f, 1f);
        badgeInnerImg.raycastTarget = false;

        // 폰트 크기: 28 (자동 크기 조절 지원)
        valueText = CreateTMPText("ValueText", badgeInner.GetComponent<RectTransform>(), font, 28f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        valueText.enableAutoSizing = true;
        valueText.fontSizeMin = 14f;
        valueText.fontSizeMax = 30f;
        valueText.text = "1";

        // 이름 & 등급 텍스트 (상단 중앙~우측, 뱃지 오른쪽 x: 74부터 시작)
        GameObject titlePanel = CreateSubPanel("TitlePanel", headerObj.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), new Vector2(38, 0), new Vector2(-76, 0));
        RectTransform titlePanelRt = titlePanel.GetComponent<RectTransform>();
        titlePanelRt.offsetMin = new Vector2(74, 0);
        titlePanelRt.offsetMax = new Vector2(-16, 0);

        // 타이틀 이름 (폰트: 26)
        titleText = CreateTMPText("TitleText", titlePanelRt, font, 26f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 16f;
        titleText.fontSizeMax = 26f;
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0, 0.48f);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

        // 등급/분류 (폰트: 18)
        tierText = CreateTMPText("TierText", titlePanelRt, font, 18f, FontStyles.Normal, new Color(0.75f, 0.75f, 0.82f, 1f), TextAlignmentOptions.Left);
        tierText.enableAutoSizing = true;
        tierText.fontSizeMin = 12f;
        tierText.fontSizeMax = 18f;
        RectTransform tierRt = tierText.rectTransform;
        tierRt.anchorMin = new Vector2(0, 0);
        tierRt.anchorMax = new Vector2(1, 0.48f);
        tierRt.offsetMin = tierRt.offsetMax = Vector2.zero;

        // 구분선 1 (Y: -69)
        CreateDividerLine(bodyRt, -69);

        // --- 중앙 일러스트 (Portrait) ---
        // Y: -71 ~ -199 (중심: -135, 크기: 110x110)
        GameObject portraitPanel = CreateSubPanel("PortraitPanel", bodyRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -135), new Vector2(110, 110));
        portraitFrame = portraitPanel.AddComponent<Image>();
        portraitFrame.sprite = solidWhiteSprite;
        portraitFrame.color = new Color(0.28f, 0.28f, 0.32f, 1f);
        portraitFrame.raycastTarget = false;

        GameObject portraitInner = CreateSubPanel("PortraitInner", portraitPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-2, -2));
        Image portraitInnerImg = portraitInner.AddComponent<Image>();
        portraitInnerImg.sprite = solidWhiteSprite;
        portraitInnerImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        portraitInnerImg.raycastTarget = false;

        GameObject portraitImgObj = CreateSubPanel("PortraitImage", portraitInner.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10, -10));
        portraitImage = portraitImgObj.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;

        // 구분선 2 (Y: -201)
        CreateDividerLine(bodyRt, -201);

        // --- 6x5 미니 보드 그래픽 (Movement Board) ---
        // Y: -203 ~ -409 (중심: -306, 크기: 216x180)
        GameObject boardPanel = CreateSubPanel("BoardPanel", bodyRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -306), new Vector2(216, 180));
        miniBoardContainer = boardPanel.GetComponent<RectTransform>();

        // 폰트 크기: 16
        boardLabel = CreateTMPText("BoardLabel", miniBoardContainer, font, 16f, FontStyles.Normal, new Color(0.6f, 0.6f, 0.68f, 1f), TextAlignmentOptions.Center);
        RectTransform labelRt = boardLabel.rectTransform;
        labelRt.anchorMin = new Vector2(0, 1);
        labelRt.anchorMax = new Vector2(1, 1);
        labelRt.anchoredPosition = new Vector2(0, 14);
        labelRt.sizeDelta = new Vector2(0, 22);
        boardLabel.text = "── 6×5 MOVEMENT BOARD ──";

        // 보드 프레임 박스 (6열 5행)
        GameObject gridBox = CreateSubPanel("GridBox", miniBoardContainer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image gridBoxBorder = gridBox.AddComponent<Image>();
        gridBoxBorder.sprite = solidWhiteSprite;
        gridBoxBorder.color = new Color(0.25f, 0.25f, 0.3f, 1f);
        gridBoxBorder.raycastTarget = false;

        GameObject gridInner = CreateSubPanel("GridInner", gridBox.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-3, -3));
        Image gridInnerBg = gridInner.AddComponent<Image>();
        gridInnerBg.sprite = solidWhiteSprite;
        gridInnerBg.color = new Color(0.08f, 0.08f, 0.1f, 1f);
        gridInnerBg.raycastTarget = false;

        // 30개 타일 생성 (열 6, 행 5, 각 33px)
        float tileW = 33f;
        float tileH = 33f;
        float startX = -((BoardCols - 1) * tileW) * 0.5f;
        float startY = -((BoardRows - 1) * tileH) * 0.5f;

        for (int r = 0; r < BoardRows; r++)
        {
            for (int c = 0; c < BoardCols; c++)
            {
                GameObject tileObj = new GameObject($"Tile_{c}_{r}", typeof(RectTransform));
                tileObj.transform.SetParent(gridInner.transform, false);
                RectTransform tileRt = tileObj.GetComponent<RectTransform>();
                tileRt.sizeDelta = new Vector2(tileW - 1f, tileH - 1f);
                tileRt.anchoredPosition = new Vector2(startX + c * tileW, startY + r * tileH);

                Image tileImg = tileObj.AddComponent<Image>();
                tileImg.sprite = solidWhiteSprite;
                tileImg.raycastTarget = false;
                boardCells[c, r] = tileImg;
            }
        }

        // 구분선 3 (Y: -411)
        divider3Object = CreateDividerLine(bodyRt, -411);

        // --- 하단 푸터 (Rules & Seal & Flavor) ---
        // Y: -413 ~ -636 (높이: 223px, 바닥 정렬)
        GameObject footerObj = CreateSubPanel("FooterPanel", bodyRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 111.5f), new Vector2(0, 223));
        footerRectTransform = footerObj.GetComponent<RectTransform>();

        // 특수 룰 / 메인 설명 텍스트 (상단 55%, 폰트: 20)
        ruleText = CreateTMPText("RuleText", footerRectTransform, font, 20f, FontStyles.Normal, new Color(0.92f, 0.92f, 0.94f, 1f), TextAlignmentOptions.TopLeft);
        ruleText.enableAutoSizing = true;
        ruleText.fontSizeMin = 13f;
        ruleText.fontSizeMax = 20f;
        RectTransform ruleRt = ruleText.rectTransform;
        ruleRt.anchorMin = new Vector2(0, 0.56f);
        ruleRt.anchorMax = new Vector2(1, 1);
        ruleRt.offsetMin = new Vector2(16, 0);
        ruleRt.offsetMax = new Vector2(-16, -6);
        ruleText.text = "특수 룰 설명";

        // 인장 / 부가 정보 슬롯 행 (중간 28%~56%)
        sealRowObject = CreateSubPanel("SealRow", footerRectTransform, new Vector2(0, 0.28f), new Vector2(1, 0.56f), Vector2.zero, Vector2.zero);
        RectTransform sealRowRt = sealRowObject.GetComponent<RectTransform>();
        sealRowRt.offsetMin = new Vector2(16, 0);
        sealRowRt.offsetMax = new Vector2(-16, 0);

        GameObject sealIconObj = CreateSubPanel("SealIcon", sealRowRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(28, 28));
        sealIconImage = sealIconObj.AddComponent<Image>();
        sealIconImage.preserveAspect = true;
        sealIconImage.raycastTarget = false;

        // 인장 / 부가 정보 텍스트 (폰트: 18)
        sealInfoText = CreateTMPText("SealInfo", sealRowRt, font, 18f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);
        sealInfoText.enableAutoSizing = true;
        sealInfoText.fontSizeMin = 12f;
        sealInfoText.fontSizeMax = 18f;
        RectTransform sealInfoRt = sealInfoText.rectTransform;
        sealInfoRt.anchorMin = new Vector2(0, 0);
        sealInfoRt.anchorMax = new Vector2(1, 1);
        sealInfoRt.offsetMin = new Vector2(36, 0);
        sealInfoRt.offsetMax = Vector2.zero;

        // 플레이버 텍스트 (하단 0%~28%, 폰트: 16)
        flavorText = CreateTMPText("FlavorText", footerRectTransform, font, 16f, FontStyles.Italic, new Color(0.58f, 0.58f, 0.65f, 1f), TextAlignmentOptions.BottomLeft);
        flavorText.enableAutoSizing = true;
        flavorText.fontSizeMin = 12f;
        flavorText.fontSizeMax = 16f;
        RectTransform flavorRt = flavorText.rectTransform;
        flavorRt.anchorMin = new Vector2(0, 0);
        flavorRt.anchorMax = new Vector2(1, 0.28f);
        flavorRt.offsetMin = new Vector2(16, 6);
        flavorRt.offsetMax = new Vector2(-16, 0);
        flavorText.text = "플레이버 텍스트";

        isHierarchyInitialized = true;
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
        GameObject div = CreateSubPanel("Divider", parent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, yPos), new Vector2(348, 1));
        Image img = div.AddComponent<Image>();
        img.sprite = solidWhiteSprite;
        img.color = new Color(0.22f, 0.22f, 0.26f, 1f);
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

    private void SetMiniBoardVisible(bool visible)
    {
        float cardH = visible ? 640f : 430f;
        RectTransform.sizeDelta = new Vector2(380f, cardH);
        if (cardRoot != null)
        {
            cardRoot.sizeDelta = new Vector2(380f, cardH);
        }
        if (shadowRectTransform != null)
        {
            shadowRectTransform.sizeDelta = new Vector2(392f, cardH + 12f);
        }
        if (miniBoardContainer != null)
        {
            miniBoardContainer.gameObject.SetActive(visible);
        }
        if (divider3Object != null)
        {
            divider3Object.SetActive(visible);
        }
    }

    private void UpdateFooterRowLayout(bool showSealRow, bool showFlavorRow, bool hasSealIcon)
    {
        if (sealRowObject != null)
        {
            sealRowObject.SetActive(showSealRow);
        }

        if (flavorText != null)
        {
            flavorText.gameObject.SetActive(showFlavorRow);
        }

        if (sealInfoText != null)
        {
            RectTransform sealInfoRt = sealInfoText.rectTransform;
            sealInfoRt.offsetMin = new Vector2(hasSealIcon ? 36f : 0f, 0f);
            sealInfoRt.offsetMax = Vector2.zero;
        }

        if (ruleText != null)
        {
            RectTransform ruleRt = ruleText.rectTransform;
            float minAnchorY = 0.06f;
            if (showSealRow && showFlavorRow)
            {
                minAnchorY = 0.56f;
            }
            else if (showSealRow || showFlavorRow)
            {
                minAnchorY = 0.30f;
            }

            ruleRt.anchorMin = new Vector2(0f, minAnchorY);
            ruleRt.anchorMax = new Vector2(1f, 1f);
            ruleRt.offsetMin = new Vector2(16f, 0f);
            ruleRt.offsetMax = new Vector2(-16f, -6f);
        }

        if (sealRowObject != null && showSealRow)
        {
            RectTransform sealRowRt = sealRowObject.GetComponent<RectTransform>();
            if (showFlavorRow)
            {
                sealRowRt.anchorMin = new Vector2(0f, 0.28f);
                sealRowRt.anchorMax = new Vector2(1f, 0.56f);
            }
            else
            {
                sealRowRt.anchorMin = new Vector2(0f, 0.04f);
                sealRowRt.anchorMax = new Vector2(1f, 0.30f);
            }
            sealRowRt.offsetMin = new Vector2(16f, 0f);
            sealRowRt.offsetMax = new Vector2(-16f, 0f);
        }
    }

    /// <summary>
    /// 기물 카드 데이터 설정 및 미니 보드 렌더링
    /// </summary>
    public void SetPieceData(PieceType pieceType, SealData attachedSeal, bool isEnemy)
    {
        EnsureUIHierarchy();
        SetMiniBoardVisible(true);
        if (boardLabel != null)
        {
            boardLabel.text = "── 6×5 MOVEMENT BOARD ──";
        }

        // 1. 체스 기물 가치 (점수)
        int chessValue = PieceController.GetChessValueForType(pieceType);
        if (valueText != null)
        {
            valueText.text = pieceType == PieceType.King ? "★" : chessValue.ToString();
        }

        // 2. 기물 이름 & 희귀도
        string pieceName = PieceController.GetPieceNameForType(pieceType);
        if (titleText != null)
        {
            titleText.text = pieceName;
        }

        int rarity = PieceController.GetRarityForType(pieceType);
        if (tierText != null)
        {
            string stars = new string('★', Mathf.Clamp(rarity, 1, 5));
            string tierName = rarity switch
            {
                5 => "Legendary",
                4 => "Mythic",
                3 => "Epic",
                2 => "Rare",
                _ => "Common"
            };
            tierText.text = $"<color=#FFFFFF>{stars}</color>  <size=85%>{tierName} (등급 {rarity})</size>";
        }

        // 3. 중앙 포트레이트 일러스트
        if (portraitImage != null)
        {
            Sprite pieceSprite = PieceSpriteData.Instance != null
                ? PieceSpriteData.Instance.GetSprite(pieceType, isEnemy)
                : null;
            if (pieceSprite == null && PieceManager.Instance != null)
            {
                pieceSprite = PieceManager.Instance.GetSpriteFor(pieceType);
            }
            if (pieceSprite == null)
            {
                PieceData pd = Resources.Load<PieceData>($"Piece/Piece_{pieceType}");
                if (pd != null) pieceSprite = pd.icon;
            }
            portraitImage.sprite = pieceSprite != null ? pieceSprite : defaultEmblemSprite;
            portraitImage.color = Color.white;
            portraitImage.enabled = portraitImage.sprite != null;
        }

        // 4. 6x5 미니 보드 그래픽 렌더링
        RenderMiniBoard(pieceType, attachedSeal, isEnemy);

        // 5. 특수 룰 설명
        if (ruleText != null)
        {
            ruleText.text = PieceController.GetSpecialRuleTextForType(pieceType);
        }

        // 6. 장착된 인장 정보
        bool hasSealIcon = false;
        if (sealRowObject != null)
        {
            if (attachedSeal != null)
            {
                hasSealIcon = attachedSeal.icon != null;
                if (sealIconImage != null)
                {
                    sealIconImage.sprite = attachedSeal.icon;
                    sealIconImage.preserveAspect = true;
                    sealIconImage.enabled = hasSealIcon;
                }
                if (sealInfoText != null)
                {
                    sealInfoText.text = $"<color=#FFFFFF>[인장: {attachedSeal.sealName}]</color> <size=90%>{attachedSeal.description}</size>";
                }
            }
            else
            {
                if (sealIconImage != null) sealIconImage.enabled = false;
                if (sealInfoText != null)
                {
                    sealInfoText.text = "<color=#62626C>인장 슬롯: 비어있음</color>";
                }
            }
        }

        // 7. 플레이버 텍스트
        if (flavorText != null)
        {
            PieceData pieceData = Resources.Load<PieceData>($"Piece/Piece_{pieceType}");
            string flavor = pieceData != null && !string.IsNullOrEmpty(pieceData.flavorText)
                ? pieceData.flavorText
                : GetDefaultFlavorText(pieceType);
            flavorText.text = FormatFlavorText(flavor);
        }

        UpdateFooterRowLayout(true, true, hasSealIcon);
    }

    /// <summary>
    /// 유물 카드 데이터 설정
    /// </summary>
    public void SetArtifactData(ArtifactData artifact, int overrideLevel = -1)
    {
        if (artifact == null) return;

        EnsureUIHierarchy();
        SetMiniBoardVisible(false);

        int level = overrideLevel > 0 ? overrideLevel : artifact.Level;

        if (valueText != null)
        {
            if (artifact.IsActive)
            {
                valueText.text = artifact.StackCount > 1 ? $"×{artifact.StackCount}" : "A";
            }
            else
            {
                valueText.text = artifact.MaxLevel > 1 ? $"L{level}" : "★";
            }
        }

        if (titleText != null)
        {
            titleText.text = artifact.artifactName;
        }

        int rarityTier = artifact.rarity switch
        {
            ArtifactRarity.Legendary => 4,
            ArtifactRarity.Epic => 3,
            ArtifactRarity.Rare => 2,
            _ => 1
        };
        if (tierText != null)
        {
            string stars = new string('★', rarityTier);
            string typeLabel = artifact.IsActive
                ? "액티브 유물"
                : (artifact.MaxLevel > 1 ? $"패시브 유물 Lv.{level}" : "패시브 유물");
            tierText.text = $"<color=#FFFFFF>{stars}</color>  <size=85%>{artifact.rarity} ({typeLabel})</size>";
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = artifact.icon != null ? artifact.icon : defaultEmblemSprite;
            portraitImage.color = Color.white;
            portraitImage.enabled = true;
        }

        if (ruleText != null)
        {
            ruleText.text = artifact.GetDescription(level);
        }

        bool hasIcon = artifact.icon != null;
        if (sealIconImage != null)
        {
            sealIconImage.sprite = artifact.icon;
            sealIconImage.preserveAspect = true;
            sealIconImage.enabled = hasIcon;
        }

        if (sealInfoText != null)
        {
            if (artifact.IsActive)
            {
                string stackLabel = artifact.StackCount > 1 ? $" (보유 {artifact.StackCount}개)" : string.Empty;
                sealInfoText.text = $"<color=#FFFFFF>[액티브 사용{stackLabel}]</color> <size=90%>내 턴에 클릭하여 발동 (1회용, 턴 소모)</size>";
            }
            else if (artifact.MaxLevel > 1)
            {
                sealInfoText.text = $"<color=#FFFFFF>[강화 등급]</color> <size=90%>현재 Lv.{level} / 최대 Lv.{artifact.MaxLevel}</size>";
            }
            else
            {
                sealInfoText.text = "<color=#FFFFFF>[패시브 효과]</color> <size=90%>보유하는 동안 상시 발동</size>";
            }
        }

        bool hasFlavor = !string.IsNullOrEmpty(artifact.flavorText);
        if (flavorText != null && hasFlavor)
        {
            flavorText.text = FormatFlavorText(artifact.flavorText);
        }

        UpdateFooterRowLayout(true, hasFlavor, hasIcon);
    }

    /// <summary>
    /// 인장 카드 데이터 설정 (이동 추가/변경 인장일 경우 6x5 보드 표시)
    /// </summary>
    public void SetSealData(SealData seal)
    {
        if (seal == null) return;

        EnsureUIHierarchy();

        List<Vector2Int> sealMoveOffsets = GetStandaloneSealPreviewOffsets(seal, out bool replacesMovement);
        bool hasMovementBoard = sealMoveOffsets.Count > 0;
        SetMiniBoardVisible(hasMovementBoard);

        if (hasMovementBoard)
        {
            if (boardLabel != null)
            {
                boardLabel.text = replacesMovement
                    ? "── 6×5 SEAL MOVE (이동 변경) ──"
                    : "── 6×5 SEAL MOVE (이동 추가) ──";
            }
            RenderSealOnlyMiniBoard(sealMoveOffsets);
        }

        int rarityTier = seal.rarity switch
        {
            SealRarity.Legendary => 4,
            SealRarity.Epic => 3,
            SealRarity.Rare => 2,
            _ => 1
        };

        if (valueText != null)
        {
            valueText.text = "◈";
        }

        if (titleText != null)
        {
            titleText.text = seal.sealName;
        }

        if (tierText != null)
        {
            string stars = new string('★', rarityTier);
            tierText.text = $"<color=#FFFFFF>{stars}</color>  <size=85%>{seal.rarity} (인장)</size>";
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = seal.icon != null ? seal.icon : defaultEmblemSprite;
            portraitImage.color = Color.white;
            portraitImage.enabled = true;
        }

        if (ruleText != null)
        {
            ruleText.text = seal.description;
        }

        bool hasIcon = seal.icon != null;
        if (sealIconImage != null)
        {
            sealIconImage.sprite = seal.icon;
            sealIconImage.preserveAspect = true;
            sealIconImage.enabled = hasIcon;
        }

        if (sealInfoText != null)
        {
            string compatibleList = "모든 기물";
            if (seal.compatiblePieces != null && seal.compatiblePieces.Count > 0)
            {
                List<string> names = new List<string>();
                foreach (PieceType pt in seal.compatiblePieces)
                {
                    names.Add(PieceKoreanNames.TryGetValue(pt, out string kor) ? kor : pt.ToString());
                }
                compatibleList = string.Join(", ", names);
            }
            sealInfoText.text = $"<color=#FFFFFF>[장착 가능]</color> <size=90%>{compatibleList}</size>";
        }

        bool hasFlavor = !string.IsNullOrEmpty(seal.flavorText);
        if (flavorText != null && hasFlavor)
        {
            flavorText.text = FormatFlavorText(seal.flavorText);
        }

        UpdateFooterRowLayout(true, hasFlavor, hasIcon);
    }

    /// <summary>
    /// 일반 툴팁 카드 데이터 설정 (도감 미해금 항목, 업적, 카운터 등)
    /// </summary>
    public void SetGeneralCardData(
        string title,
        string description,
        string flavor = "",
        Sprite icon = null,
        string tierOrSubtitle = "",
        string badgeText = "★",
        string subInfoText = "",
        Sprite subInfoIcon = null,
        Color? portraitColor = null)
    {
        EnsureUIHierarchy();
        SetMiniBoardVisible(false);

        string resolvedTitle = title;
        string resolvedDescription = description;
        if (string.IsNullOrWhiteSpace(resolvedTitle) && !string.IsNullOrWhiteSpace(resolvedDescription))
        {
            int colonIdx = resolvedDescription.IndexOf(':');
            if (colonIdx > 0 && colonIdx < 20)
            {
                resolvedTitle = resolvedDescription.Substring(0, colonIdx).Trim();
            }
            else
            {
                resolvedTitle = "상세 정보";
            }
        }

        if (valueText != null)
        {
            valueText.text = string.IsNullOrEmpty(badgeText) ? "★" : badgeText;
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrEmpty(resolvedTitle) ? "정보" : resolvedTitle;
        }

        if (tierText != null)
        {
            tierText.text = string.IsNullOrEmpty(tierOrSubtitle)
                ? "<color=#FFFFFF>★</color>  <size=85%>INFO</size>"
                : tierOrSubtitle;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = icon != null ? icon : defaultEmblemSprite;
            portraitImage.color = portraitColor ?? Color.white;
            portraitImage.enabled = true;
        }

        if (ruleText != null)
        {
            ruleText.text = resolvedDescription ?? string.Empty;
        }

        bool hasSubInfo = !string.IsNullOrEmpty(subInfoText);
        bool hasSubIcon = hasSubInfo && subInfoIcon != null;
        if (hasSubInfo)
        {
            if (sealIconImage != null)
            {
                sealIconImage.sprite = subInfoIcon;
                sealIconImage.preserveAspect = true;
                sealIconImage.enabled = hasSubIcon;
            }
            if (sealInfoText != null)
            {
                sealInfoText.text = subInfoText;
            }
        }
        else if (sealIconImage != null)
        {
            sealIconImage.enabled = false;
        }

        bool hasFlavor = !string.IsNullOrEmpty(flavor);
        if (flavorText != null && hasFlavor)
        {
            flavorText.text = FormatFlavorText(flavor);
        }

        UpdateFooterRowLayout(hasSubInfo, hasFlavor, hasSubIcon);
    }

    private static string FormatFlavorText(string flavor)
    {
        if (string.IsNullOrWhiteSpace(flavor)) return string.Empty;
        string trimmed = flavor.Trim();
        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
        {
            return trimmed;
        }
        return $"\"{trimmed}\"";
    }

    private static List<Vector2Int> GetStandaloneSealPreviewOffsets(SealData seal, out bool replacesMovement)
    {
        replacesMovement = false;
        if (seal == null || seal.sealPrefab == null)
        {
            return new List<Vector2Int>();
        }

        SealBase previewSeal = seal.sealPrefab.GetComponent<SealBase>();
        if (previewSeal == null)
        {
            return new List<Vector2Int>();
        }

        replacesMovement = previewSeal.ReplacesMovementPreview;
        PieceType samplePiece = (seal.compatiblePieces != null && seal.compatiblePieces.Count > 0)
            ? seal.compatiblePieces[0]
            : PieceType.King;

        List<Vector2Int> offsets = replacesMovement
            ? previewSeal.GetPreviewMovementOffsets(samplePiece, false)
            : previewSeal.GetPreviewAdditionalMovementOffsets(samplePiece, false);

        return offsets ?? new List<Vector2Int>();
    }

    private void RenderSealOnlyMiniBoard(List<Vector2Int> sealOffsets)
    {
        int originCol = 2;
        int originRow = 2;
        HashSet<Vector2Int> sealTiles = new HashSet<Vector2Int>();

        if (sealOffsets != null)
        {
            foreach (Vector2Int off in sealOffsets)
            {
                int c = originCol + off.x;
                int r = originRow + off.y;
                if (c >= 0 && c < BoardCols && r >= 0 && r < BoardRows)
                {
                    sealTiles.Add(new Vector2Int(c, r));
                }
            }
        }

        Color darkChecker = new Color(0.13f, 0.13f, 0.16f, 1f);
        Color lightChecker = new Color(0.21f, 0.21f, 0.25f, 1f);

        for (int r = 0; r < BoardRows; r++)
        {
            for (int c = 0; c < BoardCols; c++)
            {
                Image cell = boardCells[c, r];
                if (cell == null) continue;

                Vector2Int pos = new Vector2Int(c, r);
                if (c == originCol && r == originRow)
                {
                    cell.sprite = originMarkerSprite;
                    cell.color = Color.white;
                }
                else if (sealTiles.Contains(pos))
                {
                    cell.sprite = hatchedPatternSprite;
                    cell.color = Color.white;
                }
                else
                {
                    cell.sprite = solidWhiteSprite;
                    cell.color = ((c + r) % 2 == 0) ? darkChecker : lightChecker;
                }
            }
        }
    }

    /// <summary>
    /// 6x5 미니 보드 그래픽 렌더링
    /// 기본 이동 칸: 하얗게 반전(Solid White)
    /// 인장 추가 칸: 도트 빗금 패턴(Hatched Pattern)
    /// </summary>
    private void RenderMiniBoard(PieceType pieceType, SealData seal, bool isEnemy)
    {
        int originCol = 2; // 중앙 열
        int originRow = pieceType == PieceType.Pawn ? 1 : 2; // 폰은 1행(초기 전진 2칸 및 대각선 포획 강조)

        // 이동 오프셋 수집
        List<Vector2Int> baseOffsets = PieceController.GetBaseMovementOffsetsForType(pieceType, isEnemy);
        bool replacesMovement;
        List<Vector2Int> sealOffsets = PieceController.GetTooltipAdditionalOffsetsForType(pieceType, seal, out replacesMovement);

        HashSet<Vector2Int> baseTiles = new HashSet<Vector2Int>();
        HashSet<Vector2Int> sealTiles = new HashSet<Vector2Int>();
        HashSet<Vector2Int> pawnCaptureTiles = new HashSet<Vector2Int>();

        if (pieceType == PieceType.Pawn)
        {
            // 폰 기본 이동 (전방 1칸, 2칸)
            baseTiles.Add(new Vector2Int(originCol, originRow + 1));
            baseTiles.Add(new Vector2Int(originCol, originRow + 2));
            // 폰 대각선 포획
            pawnCaptureTiles.Add(new Vector2Int(originCol - 1, originRow + 1));
            pawnCaptureTiles.Add(new Vector2Int(originCol + 1, originRow + 1));
        }
        else
        {
            if (!replacesMovement)
            {
                foreach (Vector2Int off in baseOffsets)
                {
                    int c = originCol + off.x;
                    int r = originRow + off.y;
                    if (c >= 0 && c < BoardCols && r >= 0 && r < BoardRows)
                    {
                        baseTiles.Add(new Vector2Int(c, r));
                    }
                }
            }
        }

        if (sealOffsets != null)
        {
            foreach (Vector2Int off in sealOffsets)
            {
                int c = originCol + off.x;
                int r = originRow + off.y;
                if (c >= 0 && c < BoardCols && r >= 0 && r < BoardRows)
                {
                    sealTiles.Add(new Vector2Int(c, r));
                }
            }
        }

        // 체커보드 기본 색상
        Color darkChecker = new Color(0.13f, 0.13f, 0.16f, 1f);
        Color lightChecker = new Color(0.21f, 0.21f, 0.25f, 1f);

        for (int r = 0; r < BoardRows; r++)
        {
            for (int c = 0; c < BoardCols; c++)
            {
                Image cell = boardCells[c, r];
                if (cell == null) continue;

                Vector2Int pos = new Vector2Int(c, r);

                if (c == originCol && r == originRow)
                {
                    // 기물 본체 위치: 원형 링 마커
                    cell.sprite = originMarkerSprite;
                    cell.color = Color.white;
                }
                else if (pawnCaptureTiles.Contains(pos))
                {
                    // 폰 대각선 포획 전용 칸
                    cell.sprite = pawnCaptureSprite;
                    cell.color = Color.white;
                }
                else if (sealTiles.Contains(pos))
                {
                    // 인장으로 변형/추가된 경로: 도트 빗금 패턴 (Hatched Pattern)
                    cell.sprite = hatchedPatternSprite;
                    cell.color = Color.white;
                }
                else if (baseTiles.Contains(pos))
                {
                    // 기본 이동 가능 타일: 하얗게 반전(Solid White)
                    cell.sprite = solidWhiteSprite;
                    cell.color = Color.white;
                }
                else
                {
                    // 일반 빈 타일 (체커보드 흑백 패턴)
                    cell.sprite = solidWhiteSprite;
                    cell.color = ((c + r) % 2 == 0) ? darkChecker : lightChecker;
                }
            }
        }
    }

    /// <summary>
    /// 체스닷컴 스타일 여백 안전 영역 위치 산정 (보드를 가리지 않고 기물 우측/측면 여백에 고정)
    /// </summary>
    /// <summary>
    /// 기물의 끝보다 더 옆(기본 우측, 화면 넘침 시 좌측)에 카드를 띄워 기물과 겹치지 않도록 배치
    /// </summary>
    public void PositionCard(Vector3 sourceWorldOrScreenPos, GameObject source = null)
    {
        // 1. 소스 오브젝트를 렌더링하는 카메라 찾기 (게임 캔버스는 ScreenSpaceCamera)
        Camera sourceCam = null;
        Canvas sourceCanvas = source != null ? source.GetComponentInParent<Canvas>() : null;
        if (sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            sourceCam = sourceCanvas.worldCamera != null ? sourceCanvas.worldCamera : Camera.main;
        }
        else if (sourceCanvas == null)
        {
            sourceCam = Camera.main;
        }

        Canvas tooltipCanvas = GetComponentInParent<Canvas>();
        float scaleFactor = tooltipCanvas != null ? tooltipCanvas.scaleFactor : 1f;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        // 2. 소스 기물/UI의 정확한 스크린 바운즈(좌우/상하 끝 지점) 측정
        RectTransform sourceRt = source != null ? source.GetComponent<RectTransform>() : null;
        if (sourceRt != null)
        {
            Vector3[] corners = new Vector3[4];
            sourceRt.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(sourceCam, corners[i]);
                if (sp.x < minX) minX = sp.x;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.y > maxY) maxY = sp.y;
            }
        }

        // 유효하지 않거나 Transform이 없을 경우 fallback
        if (float.IsInfinity(minX) || float.IsInfinity(maxX) || maxX <= minX)
        {
            Vector2 center = sourceWorldOrScreenPos;
            if (source == null && sourceWorldOrScreenPos.sqrMagnitude < 0.01f)
            {
                if (UnityEngine.InputSystem.Mouse.current != null)
                {
                    center = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                }
                else
                {
                    center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                }
            }
            else if (sourceCam != null && (Mathf.Abs(sourceWorldOrScreenPos.z) > 0.01f || (sourceWorldOrScreenPos.x < 100f && sourceWorldOrScreenPos.y < 100f)))
            {
                center = sourceCam.WorldToScreenPoint(sourceWorldOrScreenPos);
            }
            float r = 26f * scaleFactor;
            minX = center.x - r;
            maxX = center.x + r;
            minY = center.y - r;
            maxY = center.y + r;
        }

        float screenW = Screen.width;
        float screenH = Screen.height;

        float rawW = cardRoot != null && cardRoot.sizeDelta.x > 0f ? cardRoot.sizeDelta.x : 380f;
        float rawH = cardRoot != null && cardRoot.sizeDelta.y > 0f ? cardRoot.sizeDelta.y : 640f;
        float cardW = rawW * scaleFactor;
        float cardH = rawH * scaleFactor;

        // 대상 끝에서부터 띄울 간격
        float gap = 24f * scaleFactor;
        float margin = 16f * scaleFactor;

        // 3. 대상의 오른쪽 끝(maxX)보다 gap만큼 더 옆에 카드의 왼쪽 끝이 오도록 배치
        float targetX = maxX + gap + (cardW * 0.5f);

        // 만약 오른쪽으로 배치했을 때 카드가 화면 밖으로 벗어난다면, 대상의 왼쪽 끝(minX)보다 gap만큼 더 왼쪽으로 배치
        if (targetX + (cardW * 0.5f) > screenW - margin)
        {
            float leftPlacementX = minX - gap - (cardW * 0.5f);
            if (leftPlacementX - (cardW * 0.5f) >= margin)
            {
                targetX = leftPlacementX;
            }
            else
            {
                // 화면 폭이 극도로 좁은 경우에만 화면 테두리 내로 클램핑
                targetX = Mathf.Clamp(targetX, margin + (cardW * 0.5f), screenW - margin - (cardW * 0.5f));
            }
        }

        // 4. Y축 정렬: 대상 중심 높이에 맞추되 화면 상하로 잘리지 않게 클램핑
        float pieceCenterY = (minY + maxY) * 0.5f;
        float targetY = Mathf.Clamp(pieceCenterY, margin + (cardH * 0.5f), screenH - margin - (cardH * 0.5f));

        transform.position = new Vector3(targetX, targetY, 0f);
    }

    /// <summary>
    /// 탄력 있는 팝업 애니메이션 (Scale Up + Fade In)
    /// </summary>
    public void Show()
    {
        EnsureUIHierarchy();

        popScaleTween?.Kill();
        popFadeTween?.Kill();

        gameObject.SetActive(true);
        transform.localScale = Vector3.one * 0.88f;
        canvasGroup.alpha = 0f;

        // 탄력 있는 쫀득한 스케일 업 (Ease.OutBack)
        popScaleTween = transform.DOScale(Vector3.one, 0.22f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);

        popFadeTween = canvasGroup.DOFade(1f, 0.16f)
            .SetUpdate(true);
    }

    /// <summary>
    /// 즉시 닫기 (호버 해제 시 잔류 버그 방지)
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

    private string GetDefaultFlavorText(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => "가장 작지만 끝에 도달하면 무엇이든 될 수 있는 병사.",
            PieceType.Knight => "장애물을 가볍게 뛰어넘는 기동력의 기사.",
            PieceType.Bishop => "대각선의 길을 지배하는 성직자.",
            PieceType.Rook => "견고한 성벽처럼 전장을 가로지르는 탑.",
            PieceType.Queen => "전장에서 가장 강력하고 치명적인 통치자.",
            PieceType.King => "왕관의 무게를 견디며 전장을 지휘하는 왕.",
            PieceType.Amazon => "퀸의 위력과 나이트의 도약을 모두 품은 전설의 여전사.",
            PieceType.Chancellor => "철통같은 직선 요새와 도약 기습을 겸비한 재상.",
            PieceType.Archbishop => "대각선 참회와 변칙적인 도약을 구사하는 대주교.",
            PieceType.Centaur => "전설의 반인반마, 전방위 호위와 도약 기동의 조화.",
            PieceType.Cannon => "도약대를 딛고 적의 심장을 타격하는 강력한 포병.",
            PieceType.Grasshopper => "기물을 디딤돌 삼아 단숨에 도약하는 메뚜기.",
            _ => "전장을 누비는 체스 기물."
        };
    }
}
