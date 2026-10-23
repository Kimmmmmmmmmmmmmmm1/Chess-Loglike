using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

/// <summary>
/// 작업장(공방) 매니저:
/// - ArtifactInhance(유물 강화)와 PieceSynthesis(기물 합성) 기능을 제공합니다.
/// - 언제든 이전 메뉴로 돌아오거나 [정비 완료 / 다음으로]를 눌러 다음 스테이지로 진행할 수 있습니다.
/// </summary>
public class WorkShopManager : MonoBehaviour
{
    public static WorkShopManager Instance { get; private set; }

    [Header("UI")]
    public GameObject workShopPanel;        // 작업장 패널
    public Button artifactInhanceButton;    // 유물 강화 버튼
    public Button pieceSynthesisButton;     // 기물 합성 버튼
    public TextMeshProUGUI titleText;       // "작업장" 타이틀 텍스트
    private RectTransform workShopPanelRect;

    [Header("Animation")]
    public float animDuration = 0.5f;
    public Ease openEase = Ease.OutBack;
    public Ease closeEase = Ease.InQuad;
    public float panelOpenX = 0f;             // 패널이 열렸을 때의 X 위치 (가운데)
    public float panelOpenY = 0f;             // 패널이 열렸을 때의 Y 위치 (세로)

    [Header("Reference")]
    public ArtifactInhanceManager artifactInhanceManager;
    public PiecesSynthManager piecesSynthManager;

    private bool isOpen = false;
    private bool hasSelectedOption = false;

    // 동적 생성/관리되는 UI 요소들
    private Button finishButton;
    private TextMeshProUGUI subtitleText;
    private TextMeshProUGUI tipText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        panelOpenX = 0f;
        panelOpenY = 0f;
    }

    private void Start()
    {
        if (workShopPanel != null)
        {
            workShopPanelRect = workShopPanel.GetComponent<RectTransform>();
            workShopPanel.SetActive(false);
        }

        if (artifactInhanceButton != null)
        {
            artifactInhanceButton.onClick.AddListener(OnArtifactInhanceClick);
        }

        if (pieceSynthesisButton != null)
        {
            pieceSynthesisButton.onClick.AddListener(OnPieceSynthesisClick);
        }

        if (artifactInhanceManager == null)
        {
            artifactInhanceManager = FindFirstObjectByType<ArtifactInhanceManager>();
        }

        if (piecesSynthManager == null)
        {
            piecesSynthManager = FindFirstObjectByType<PiecesSynthManager>();
        }

        SetupEnhancedUI();
    }

    private void SetupEnhancedUI()
    {
        if (workShopPanel == null) return;

        if (workShopPanelRect == null) workShopPanelRect = workShopPanel.GetComponent<RectTransform>();
        if (workShopPanelRect != null)
        {
            workShopPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            workShopPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            workShopPanelRect.pivot = new Vector2(0.5f, 0.5f);
            workShopPanelRect.anchoredPosition = new Vector2(panelOpenX, panelOpenY);
            workShopPanelRect.sizeDelta = new Vector2(340f, 240f);
        }

        TMP_FontAsset font = null;
        if (titleText != null) font = titleText.font;
        if (font == null && GameManager.Instance != null && GameManager.Instance.coinText != null)
            font = GameManager.Instance.coinText.font;
        if (font == null) font = TMP_Settings.defaultFontAsset;

        // 1. 타이틀 다듬기
        if (titleText != null)
        {
            titleText.text = "⚒️ 대장장이 공방";
            titleText.fontSize = 18f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(1f, 0.85f, 0.4f, 1f);
            titleText.alignment = TextAlignmentOptions.Center;

            RectTransform titleRt = titleText.GetComponent<RectTransform>();
            if (titleRt != null)
            {
                titleRt.anchoredPosition = new Vector2(0f, -22f);
                titleRt.sizeDelta = new Vector2(400f, 26f);
            }
        }

        // 2. 서브타이틀 텍스트 추가
        if (subtitleText == null)
        {
            GameObject subObj = new GameObject("WorkshopSubtitle");
            subObj.transform.SetParent(workShopPanel.transform, false);
            RectTransform subRt = subObj.AddComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -46f);
            subRt.sizeDelta = new Vector2(400f, 18f);

            subtitleText = subObj.AddComponent<TextMeshProUGUI>();
            subtitleText.font = font;
            subtitleText.text = "기물을 합성하거나 유물을 강화하여 덱을 정비하세요";
            subtitleText.fontSize = 10.5f;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.85f, 0.85f, 0.88f, 0.9f);
        }

        // 3. 유물 강화 버튼 스타일링
        if (artifactInhanceButton != null)
        {
            RectTransform btnRt = artifactInhanceButton.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.sizeDelta = new Vector2(140f, 140f);
                btnRt.anchoredPosition = new Vector2(-78f, 12f);
            }

            Image btnImg = artifactInhanceButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);
            }

            Outline outline = artifactInhanceButton.GetComponent<Outline>();
            if (outline == null) outline = artifactInhanceButton.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.65f, 1f, 0.8f);
            outline.effectDistance = new Vector2(2f, 2f);

            TextMeshProUGUI label = artifactInhanceButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.font = font;
                label.text = "<size=24>✨</size>\n<b>유물 강화</b>\n\n<size=10><color=#90C4FF>골드를 소모하여\n유물 성능 레벨업</color></size>";
                label.alignment = TextAlignmentOptions.Center;
                label.lineSpacing = 6f;
            }

            AddButtonHoverEffect(artifactInhanceButton, outline);
        }

        // 4. 기물 합성 버튼 스타일링
        if (pieceSynthesisButton != null)
        {
            RectTransform btnRt = pieceSynthesisButton.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.sizeDelta = new Vector2(140f, 140f);
                btnRt.anchoredPosition = new Vector2(78f, 12f);
            }

            Image btnImg = pieceSynthesisButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = new Color(0.22f, 0.16f, 0.10f, 0.95f);
            }

            Outline outline = pieceSynthesisButton.GetComponent<Outline>();
            if (outline == null) outline = pieceSynthesisButton.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.65f, 0.2f, 0.8f);
            outline.effectDistance = new Vector2(2f, 2f);

            TextMeshProUGUI label = pieceSynthesisButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.font = font;
                label.text = "<size=24>⚒️</size>\n<b>기물 합성</b>\n\n<size=10><color=#FFD285>기물 2개를 합쳐\n상위 기물로 진화</color></size>";
                label.alignment = TextAlignmentOptions.Center;
                label.lineSpacing = 6f;
            }

            AddButtonHoverEffect(pieceSynthesisButton, outline);
        }

        // 5. 하단 정비 완료 / 다음으로 버튼 추가
        if (finishButton == null)
        {
            GameObject finishObj = new GameObject("WorkshopFinishButton");
            finishObj.transform.SetParent(workShopPanel.transform, false);

            RectTransform finishRt = finishObj.AddComponent<RectTransform>();
            finishRt.anchorMin = new Vector2(0.5f, 0f);
            finishRt.anchorMax = new Vector2(0.5f, 0f);
            finishRt.pivot = new Vector2(0.5f, 0f);
            finishRt.anchoredPosition = new Vector2(0f, 16f);
            finishRt.sizeDelta = new Vector2(160f, 32f);

            Image finishBg = finishObj.AddComponent<Image>();
            finishBg.color = new Color(0.18f, 0.62f, 0.42f, 0.95f);

            Outline finishOutline = finishObj.AddComponent<Outline>();
            finishOutline.effectColor = new Color(0.4f, 1f, 0.7f, 0.9f);
            finishOutline.effectDistance = new Vector2(1.5f, 1.5f);

            finishButton = finishObj.AddComponent<Button>();
            finishButton.onClick.AddListener(OnWorkShopComplete);

            GameObject finishTextObj = new GameObject("Text");
            finishTextObj.transform.SetParent(finishObj.transform, false);
            RectTransform textRt = finishTextObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.anchoredPosition = Vector2.zero;
            textRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI finishText = finishTextObj.AddComponent<TextMeshProUGUI>();
            finishText.font = font;
            finishText.text = "정비 완료 / 다음으로 →";
            finishText.fontSize = 11.5f;
            finishText.fontStyle = FontStyles.Bold;
            finishText.alignment = TextAlignmentOptions.Center;
            finishText.color = Color.white;

            AddButtonHoverEffect(finishButton, finishOutline);
        }

        // 6. 하단 팁 텍스트
        if (tipText == null)
        {
            GameObject tipObj = new GameObject("WorkshopTipText");
            tipObj.transform.SetParent(workShopPanel.transform, false);
            RectTransform tipRt = tipObj.AddComponent<RectTransform>();
            tipRt.anchorMin = new Vector2(0.5f, 0f);
            tipRt.anchorMax = new Vector2(0.5f, 0f);
            tipRt.pivot = new Vector2(0.5f, 0f);
            tipRt.anchoredPosition = new Vector2(0f, 54f);
            tipRt.sizeDelta = new Vector2(400f, 16f);

            tipText = tipObj.AddComponent<TextMeshProUGUI>();
            tipText.font = font;
            tipText.text = "💡 하단 대기석에서 기물 간 인장 장착/교체도 가능합니다.";
            tipText.fontSize = 9.5f;
            tipText.alignment = TextAlignmentOptions.Center;
            tipText.color = new Color(0.7f, 0.7f, 0.75f, 0.8f);
        }
    }

    private void AddButtonHoverEffect(Button btn, Outline outline)
    {
        if (btn == null) return;
        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) =>
        {
            if (btn.interactable)
            {
                btn.transform.DOKill();
                btn.transform.DOScale(1.04f, 0.15f).SetUpdate(true);
                if (outline != null) outline.effectDistance = new Vector2(3f, 3f);
            }
        });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) =>
        {
            btn.transform.DOKill();
            btn.transform.DOScale(1.0f, 0.15f).SetUpdate(true);
            if (outline != null) outline.effectDistance = new Vector2(2f, 2f);
        });
        trigger.triggers.Add(exit);
    }

    public void Open()
    {
        if (isOpen || workShopPanel == null) return;

        SetupEnhancedUI();

        // 작업장 오픈 시 하위 패널은 항상 닫힌 상태로 초기화
        if (artifactInhanceManager != null)
        {
            artifactInhanceManager.CloseInhancePanel();
        }
        if (piecesSynthManager != null)
        {
            piecesSynthManager.CloseSynthesisPanel();
        }

        isOpen = true;
        hasSelectedOption = false;
        workShopPanel.SetActive(true);

        SetMainMenuElementsVisible(true);

        if (artifactInhanceButton != null) artifactInhanceButton.interactable = true;
        if (pieceSynthesisButton != null) pieceSynthesisButton.interactable = true;
        if (finishButton != null) finishButton.interactable = true;

        if (workShopPanelRect != null)
        {
            workShopPanelRect.DOKill();
            workShopPanelRect.anchoredPosition = new Vector2(panelOpenX, panelOpenY);
        }
    }

    public void Close()
    {
        if (!isOpen || workShopPanel == null) return;

        isOpen = false;

        if (artifactInhanceManager != null)
        {
            artifactInhanceManager.CloseInhancePanel();
        }
        if (piecesSynthManager != null)
        {
            piecesSynthManager.CloseSynthesisPanel();
        }

        if (workShopPanelRect != null)
        {
            workShopPanelRect.DOKill();
        }
        if (workShopPanel != null)
        {
            workShopPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 하위 패널에서 [← 뒤로]를 눌렀을 때 메인 공방 메뉴로 복귀합니다.
    /// </summary>
    public void ReturnToMain()
    {
        hasSelectedOption = false;

        if (artifactInhanceButton != null) artifactInhanceButton.interactable = true;
        if (pieceSynthesisButton != null) pieceSynthesisButton.interactable = true;
        if (finishButton != null) finishButton.interactable = true;

        SetMainMenuElementsVisible(true);
    }

    private void SetMainMenuElementsVisible(bool visible)
    {
        if (artifactInhanceButton != null) artifactInhanceButton.gameObject.SetActive(visible);
        if (pieceSynthesisButton != null) pieceSynthesisButton.gameObject.SetActive(visible);
        if (subtitleText != null) subtitleText.gameObject.SetActive(visible);
        if (finishButton != null) finishButton.gameObject.SetActive(visible);
        if (tipText != null) tipText.gameObject.SetActive(visible);
    }

    private void OnArtifactInhanceClick()
    {
        if (hasSelectedOption) return;
        hasSelectedOption = true;

        SetMainMenuElementsVisible(false);

        if (artifactInhanceManager != null)
        {
            artifactInhanceManager.OpenInhancePanel();
        }
        else
        {
            ReturnToMain();
        }
    }

    private void OnPieceSynthesisClick()
    {
        if (hasSelectedOption) return;
        hasSelectedOption = true;

        SetMainMenuElementsVisible(false);

        if (piecesSynthManager != null)
        {
            piecesSynthManager.OpenSynthesisPanel();
        }
        else
        {
            ReturnToMain();
        }
    }

    /// <summary>
    /// 작업장 작업 완료 및 다음 스테이지로 진행
    /// </summary>
    public void OnWorkShopComplete()
    {
        Close();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnNonBattleStageCompleted();
        }
    }
}
