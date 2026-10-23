using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 대화창 UI 뷰 컴포넌트입니다.
/// 디자인 요소 없는 깔끔한 반투명 검정 사각형 박스와 흰색 텍스트, 역삼각형(▼) 안내 마커로 구성됩니다.
/// 프리팹으로 에디터에 배치하거나 런타임에 Resources.Load / CreateDefault로 동적 생성할 수 있습니다.
/// </summary>
[DisallowMultipleComponent]
public class TutorialDialogUI : MonoBehaviour
{
    [Header("Hierarchy & Panels")]
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private Image overlayCatcher;
    [SerializeField] private RectTransform dialogPanelRect;
    [SerializeField] private Image dialogBgImage;

    [Header("Text & Indicators")]
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI nextIndicator;

    [Header("Choice Buttons")]
    [SerializeField] private GameObject choicesContainer;
    [SerializeField] private Button proceedButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI proceedButtonText;
    [SerializeField] private TextMeshProUGUI skipButtonText;

    private Tween indicatorTween;
    private Tween proceedHighlightTween;
    private float defaultIndicatorY = 18f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        KillIndicatorTween();
        KillProceedHighlightTween();
    }

    private void KillProceedHighlightTween()
    {
        if (proceedHighlightTween != null)
        {
            proceedHighlightTween.Kill();
            proceedHighlightTween = null;
        }

        if (proceedButton != null)
        {
            proceedButton.transform.localScale = Vector3.one;
            Outline outline = proceedButton.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }
    }

    public void ResolveReferences()
    {
        Canvas c = GetComponent<Canvas>();
        if (c == null) c = gameObject.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 2000;

        GraphicRaycaster gr = GetComponent<GraphicRaycaster>();
        if (gr == null) gr = gameObject.AddComponent<GraphicRaycaster>();

        if (overlayCanvasGroup == null) overlayCanvasGroup = GetComponent<CanvasGroup>();
        if (overlayCatcher == null) overlayCatcher = GetComponent<Image>();

        if (dialogPanelRect == null)
        {
            Transform t = transform.Find("TutorialDialogPanel");
            if (t != null) dialogPanelRect = t.GetComponent<RectTransform>();
        }

        if (dialogPanelRect != null)
        {
            if (dialogBgImage == null) dialogBgImage = dialogPanelRect.GetComponent<Image>();

            if (bodyText == null)
            {
                Transform bt = dialogPanelRect.Find("BodyText");
                if (bt != null) bodyText = bt.GetComponent<TextMeshProUGUI>();
            }

            if (nextIndicator == null)
            {
                Transform ni = dialogPanelRect.Find("NextIndicator");
                if (ni != null) nextIndicator = ni.GetComponent<TextMeshProUGUI>();
            }

            if (choicesContainer == null)
            {
                Transform cc = dialogPanelRect.Find("ChoicesContainer");
                if (cc != null) choicesContainer = cc.gameObject;
            }
        }

        if (choicesContainer != null)
        {
            if (proceedButton == null)
            {
                Transform pb = choicesContainer.transform.Find("ProceedButton");
                if (pb != null)
                {
                    proceedButton = pb.GetComponent<Button>();
                    proceedButtonText = pb.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }

            if (skipButton == null)
            {
                Transform sb = choicesContainer.transform.Find("SkipButton");
                if (sb != null)
                {
                    skipButton = sb.GetComponent<Button>();
                    skipButtonText = sb.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }
        }

        if (nextIndicator != null)
        {
            defaultIndicatorY = Mathf.Max(nextIndicator.rectTransform.anchoredPosition.y, 18f);
            Vector2 pos = nextIndicator.rectTransform.anchoredPosition;
            pos.y = defaultIndicatorY;
            nextIndicator.rectTransform.anchoredPosition = pos;
        }
    }

    /// <summary>
    /// 첫 경험 질문 단계(스킵 여부)를 화면에 표시합니다.
    /// </summary>
    public void ShowQuestion(string questionText, Action onProceed, Action onSkip, string proceedText = null, string skipText = null)
    {
        ResolveReferences();

        if (bodyText != null)
        {
            RectTransform rt = bodyText.rectTransform;
            rt.offsetMin = new Vector2(16f, 32f);
            rt.offsetMax = new Vector2(-16f, -10f);
            bodyText.text = questionText;
            bodyText.color = Color.white;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
        }

        if (proceedButtonText != null && !string.IsNullOrEmpty(proceedText))
        {
            proceedButtonText.text = proceedText;
        }

        if (skipButtonText != null && !string.IsNullOrEmpty(skipText))
        {
            skipButtonText.text = skipText;
        }

        if (nextIndicator != null)
        {
            nextIndicator.gameObject.SetActive(false);
            KillIndicatorTween();
        }

        if (choicesContainer != null)
        {
            choicesContainer.SetActive(true);
        }

        if (overlayCatcher != null)
        {
            overlayCatcher.raycastTarget = true;
        }

        if (dialogBgImage != null)
        {
            dialogBgImage.raycastTarget = true;
        }

        if (proceedButton != null)
        {
            proceedButton.onClick.RemoveAllListeners();
            proceedButton.onClick.AddListener(() => onProceed?.Invoke());

            // 외곽선 글로우 효과 없이 펄스 효과만 적용
            Outline outline = proceedButton.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;

            proceedHighlightTween = proceedButton.transform.DOScale(Vector3.one * 1.05f, 0.45f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => onSkip?.Invoke());
        }
    }

    /// <summary>
    /// 일반 튜토리얼 텍스트 단계를 표시합니다.
    /// isActionStep이 true인 경우(플레이어 행동 대기), 다음(▼) 마커를 숨기고 화면 상호작용을 허용합니다.
    /// </summary>
    public void ShowStep(string messageText, bool isActionStep = false)
    {
        ResolveReferences();
        KillProceedHighlightTween();

        if (choicesContainer != null)
        {
            choicesContainer.SetActive(false);
        }

        if (bodyText != null)
        {
            RectTransform rt = bodyText.rectTransform;
            rt.offsetMin = new Vector2(16f, 10f);
            rt.offsetMax = new Vector2(-28f, -10f);
            bodyText.text = messageText;
            bodyText.color = Color.white;
            bodyText.alignment = TextAlignmentOptions.MidlineLeft;

            // 텍스트 전환 시 미세한 펀치 효과
            bodyText.transform.DOKill();
            bodyText.transform.localScale = Vector3.one;
            bodyText.transform.DOPunchScale(Vector3.one * 0.03f, 0.12f, 4, 1).SetUpdate(true);
        }

        if (isActionStep)
        {
            // 행동을 취해야 하는 스텝: 역삼각형(▼) 마커를 숨기고 화면 클릭 및 드래그 허용
            if (nextIndicator != null)
            {
                nextIndicator.gameObject.SetActive(false);
                KillIndicatorTween();
            }
            if (overlayCatcher != null)
            {
                overlayCatcher.raycastTarget = false;
            }
            if (dialogBgImage != null)
            {
                dialogBgImage.raycastTarget = false;
            }
        }
        else
        {
            // 단순 설명 스텝: 역삼각형(▼) 마커를 표시하고 화면 전체 클릭으로 대화 진행 허용
            if (nextIndicator != null)
            {
                nextIndicator.gameObject.SetActive(true);
                nextIndicator.text = "▼";
                nextIndicator.color = Color.white;
                StartIndicatorAnimation();
            }
            if (overlayCatcher != null)
            {
                overlayCatcher.raycastTarget = true;
            }
            if (dialogBgImage != null)
            {
                dialogBgImage.raycastTarget = true;
            }
        }
    }

    public void FadeIn(Action onComplete)
    {
        ResolveReferences();

        if (dialogPanelRect != null)
        {
            dialogPanelRect.DOKill();
            dialogPanelRect.localScale = new Vector3(0.97f, 0.97f, 1f);
            dialogPanelRect.DOScale(Vector3.one, 0.2f).SetUpdate(true);
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.DOKill();
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true).OnComplete(() => onComplete?.Invoke());
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    public void FadeOut(Action onComplete)
    {
        ResolveReferences();
        KillIndicatorTween();

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.DOKill();
            overlayCanvasGroup.DOFade(0f, 0.18f).SetUpdate(true).OnComplete(() => onComplete?.Invoke());
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    private void StartIndicatorAnimation()
    {
        if (nextIndicator == null) return;

        KillIndicatorTween();
        RectTransform rt = nextIndicator.rectTransform;
        Vector2 pos = rt.anchoredPosition;
        pos.y = defaultIndicatorY;
        rt.anchoredPosition = pos;

        // 역삼각형(▼) 아래위로 부드럽게 깜빡이며 움직이는 애니메이션
        indicatorTween = rt.DOAnchorPosY(defaultIndicatorY - 3f, 0.45f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    private void KillIndicatorTween()
    {
        if (indicatorTween != null && indicatorTween.IsActive())
        {
            indicatorTween.Kill();
            indicatorTween = null;
        }
    }

    /// <summary>
    /// 프리팹 에셋이 없거나 동적으로 UI를 구성해야 할 때 호출되는 기본 팩토리 메서드입니다.
    /// 디자인 없이 반투명 검정색 박스(Color(0,0,0,0.8))와 흰색 텍스트, 역삼각형(▼)만으로 구성합니다.
    /// </summary>
    public static TutorialDialogUI CreateDefault(Transform parent)
    {
        TMP_FontAsset font = ResolveFont();

        // 1. 전체 화면 오버레이 컨테이너
        GameObject overlay = new GameObject("TutorialDialogUI", typeof(RectTransform), typeof(CanvasGroup));
        if (parent != null)
        {
            overlay.transform.SetParent(parent, false);
        }

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = Vector3.one;

        Image overlayBg = overlay.AddComponent<Image>();
        overlayBg.color = new Color(0f, 0f, 0f, 0.001f);
        overlayBg.raycastTarget = true;

        CanvasGroup cg = overlay.GetComponent<CanvasGroup>();
        cg.alpha = 1f;

        // 2. 하단 다이얼로그 박스 (디자인 없는 순수 반투명 검정색 직사각형)
        GameObject panelObj = new GameObject("TutorialDialogPanel", typeof(RectTransform));
        panelObj.transform.SetParent(overlay.transform, false);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 10f);
        panelRect.sizeDelta = new Vector2(580f, 68f);
        panelRect.localScale = Vector3.one;

        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.sprite = null; // 스프라이트/테두리 없는 단색 직사각형
        panelImg.color = new Color(0f, 0f, 0f, 0.82f); // 디자인 없는 반투명 검정색 박스
        panelImg.raycastTarget = true;

        // 3. 본문 텍스트 (소제목 없이 흰색 텍스트만)
        GameObject textObj = new GameObject("BodyText", typeof(RectTransform));
        textObj.transform.SetParent(panelObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 10f);
        textRect.offsetMax = new Vector2(-28f, -10f);

        TextMeshProUGUI bodyTmp = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) bodyTmp.font = font;
        bodyTmp.text = "";
        bodyTmp.fontSize = 11.5f;
        bodyTmp.color = Color.white;
        bodyTmp.lineSpacing = 1.25f;
        bodyTmp.textWrappingMode = TextWrappingModes.Normal;
        bodyTmp.overflowMode = TextOverflowModes.Overflow;
        bodyTmp.alignment = TextAlignmentOptions.MidlineLeft;
        bodyTmp.raycastTarget = false;

        // 4. 다음 단계 안내 역삼각형 마커 (하단 ~키 등 텍스트 제거하고 오직 역삼각형 ▼ 만 남김)
        GameObject indicatorObj = new GameObject("NextIndicator", typeof(RectTransform));
        indicatorObj.transform.SetParent(panelObj.transform, false);

        RectTransform indicatorRect = indicatorObj.GetComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(1f, 0f);
        indicatorRect.anchorMax = new Vector2(1f, 0f);
        indicatorRect.pivot = new Vector2(1f, 0f);
        indicatorRect.anchoredPosition = new Vector2(-12f, 18f);
        indicatorRect.sizeDelta = new Vector2(16f, 16f);

        TextMeshProUGUI indicatorTmp = indicatorObj.AddComponent<TextMeshProUGUI>();
        if (font != null) indicatorTmp.font = font;
        indicatorTmp.text = "▼";
        indicatorTmp.fontSize = 10f;
        indicatorTmp.color = Color.white;
        indicatorTmp.alignment = TextAlignmentOptions.BottomRight;
        indicatorTmp.raycastTarget = false;

        // 5. 선택지 컨테이너 (경험 질문용 버튼 2개)
        GameObject choicesObj = new GameObject("ChoicesContainer", typeof(RectTransform));
        choicesObj.transform.SetParent(panelObj.transform, false);

        RectTransform choicesRect = choicesObj.GetComponent<RectTransform>();
        choicesRect.anchorMin = new Vector2(0.5f, 0f);
        choicesRect.anchorMax = new Vector2(0.5f, 0f);
        choicesRect.pivot = new Vector2(0.5f, 0f);
        choicesRect.anchoredPosition = new Vector2(0f, 7f);
        choicesRect.sizeDelta = new Vector2(460f, 22f);

        HorizontalLayoutGroup hlg = choicesObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        Button proceedBtn = CreateSimpleButton(choicesObj.transform, "ProceedButton", "튜토리얼 진행 (Enter)", font, new Color(0.18f, 0.18f, 0.22f, 0.95f));
        Button skipBtn = CreateSimpleButton(choicesObj.transform, "SkipButton", "건너뛰기 (Esc)", font, new Color(0.12f, 0.12f, 0.14f, 0.95f));

        choicesObj.SetActive(false);

        // 6. TutorialDialogUI 컴포넌트 바인딩
        TutorialDialogUI ui = overlay.AddComponent<TutorialDialogUI>();
        ui.overlayCanvasGroup = cg;
        ui.overlayCatcher = overlayBg;
        ui.dialogPanelRect = panelRect;
        ui.dialogBgImage = panelImg;
        ui.bodyText = bodyTmp;
        ui.nextIndicator = indicatorTmp;
        ui.choicesContainer = choicesObj;
        ui.proceedButton = proceedBtn;
        ui.skipButton = skipBtn;
        ui.proceedButtonText = proceedBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        ui.skipButtonText = skipBtn.GetComponentInChildren<TextMeshProUGUI>(true);

        return ui;
    }

    private static Button CreateSimpleButton(Transform parent, string name, string label, TMP_FontAsset font, Color bgColor)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180f, 20f);
        rt.localScale = Vector3.one;

        Image img = btnObj.AddComponent<Image>();
        img.sprite = null;
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = cb;

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 9.5f;
        tmp.fontStyle = FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }

    private static TMP_FontAsset ResolveFont()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium SDF");
        if (font == null && GameManager.Instance != null && GameManager.Instance.coinText != null)
        {
            font = GameManager.Instance.coinText.font;
        }
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
        }
        return font;
    }
}

