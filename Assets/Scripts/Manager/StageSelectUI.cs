using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// 스테이지 행선지(전투, 상점, 공방, 보물 등) 및 범용 선택지 카드를 제시하는 UI 매니저입니다.
/// StageChoiceOption 인스턴스를 받는 API(ShowChoices)를 통해 프로젝트 어디서든 커스텀 선택지 UI로 재사용할 수 있습니다.
/// </summary>
public class StageSelectUI : MonoBehaviour
{
    public static StageSelectUI Instance { get; private set; }

    [Header("Prefabs (Optional - loaded from Resources if null)")]
    [SerializeField] private GameObject stageSelectPanelPrefab;
    [SerializeField] private GameObject stageChoiceCardPrefab;

    private GameObject rootPanel;
    private RectTransform panelRect;
    private TextMeshProUGUI headerTitleText;
    private TextMeshProUGUI headerSubtitleText;
    private Transform cardContainer;

    private readonly List<GameObject> activeCards = new List<GameObject>();
    private bool isSelecting = false;
    private bool lastStageHadShopOrWorkshop = false;

    private readonly List<StageNodeType> currentPendingNodeTypes = new List<StageNodeType>();
    public IReadOnlyList<StageNodeType> CurrentPendingNodeTypes => currentPendingNodeTypes;
    public int CurrentNextStage { get; private set; }

    public void SetPendingStageNodes(List<StageNodeType> nodes)
    {
        currentPendingNodeTypes.Clear();
        if (nodes != null)
        {
            currentPendingNodeTypes.AddRange(nodes);
        }
    }

    public void ResetSelectionState()
    {
        lastStageHadShopOrWorkshop = false;
        currentPendingNodeTypes.Clear();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneName.TitleScene.ToString())
        {
            Close(true);
            ResetSelectionState();
        }
    }

    #region Generalized Choice Card API

    /// <summary>
    /// 임의의 선택지(StageChoiceOption) 목록을 받아 카드 선택 모달을 엽니다.
    /// 이벤트, 딜레마, 보상, 축복 등 원하는 곳에서 자유롭게 호출할 수 있습니다.
    /// </summary>
    public void ShowChoices(IList<StageChoiceOption> options, string headerTitle = "CHOICE", string headerSubtitle = "선택지를 고르세요")
    {
        if (options == null || options.Count == 0) return;

        if (SceneManager.GetActiveScene().name == SceneName.TitleScene.ToString())
        {
            Close(true);
            return;
        }

        EnsureUI();

        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
            rootPanel.transform.SetAsLastSibling();

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsDialogShowing)
            {
                TutorialManager.Instance.BringDialogToFront();
            }

            CanvasGroup cg = rootPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.DOKill();
                cg.alpha = 0f;
                cg.DOFade(1f, 0.25f).SetUpdate(true);
            }
        }

        if (headerTitleText != null && !string.IsNullOrEmpty(headerTitle))
        {
            headerTitleText.text = headerTitle;
        }

        if (headerSubtitleText != null)
        {
            headerSubtitleText.text = headerSubtitle ?? "";
        }

        isSelecting = false;
        RenderChoiceCards(options);
    }

    /// <summary>
    /// 가변 인자(params)로 선택지 목록을 전달하여 엽니다.
    /// </summary>
    public void ShowChoices(params StageChoiceOption[] options)
    {
        ShowChoices((IList<StageChoiceOption>)options);
    }

    /// <summary>
    /// 2개의 선택지 인스턴스를 전달하여 엽니다.
    /// </summary>
    public void ShowChoices(StageChoiceOption option1, StageChoiceOption option2, string headerTitle = null, string headerSubtitle = null)
    {
        ShowChoices(new[] { option1, option2 }, headerTitle ?? "CHOICE", headerSubtitle ?? "다음 선택지를 고르세요");
    }

    /// <summary>
    /// ScriptableObject 에셋(StageChoiceData) 목록을 전달하여 엽니다.
    /// </summary>
    public void ShowChoices(IEnumerable<StageChoiceData> dataList, Action<StageChoiceData> onChoiceSelected = null, string headerTitle = null, string headerSubtitle = null)
    {
        if (dataList == null) return;
        var options = new List<StageChoiceOption>();
        foreach (var data in dataList)
        {
            if (data == null) continue;
            var currentData = data;
            options.Add(data.ToOption(() => onChoiceSelected?.Invoke(currentData)));
        }
        ShowChoices(options, headerTitle, headerSubtitle);
    }

    #endregion

    #region Stage Progression Flow

    /// <summary>
    /// 다음 스테이지 번호를 기반으로 기본 행선지 선택지를 생성하여 모달을 엽니다.
    /// </summary>
    public void Open()
    {
        int nextStage = (GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0) + 1;
        Open(nextStage);
    }

    /// <summary>
    /// 지정된 스테이지 번호에 맞는 행선지 카드들을 생성하여 엽니다.
    /// </summary>
    public void Open(int nextStage)
    {
        CurrentNextStage = nextStage;

        if (SceneManager.GetActiveScene().name == SceneName.TitleScene.ToString())
        {
            Close(true);
            return;
        }

        int act = ((nextStage - 1) / 10) + 1;
        int subStage = ((nextStage - 1) % 10) + 1;

        List<StageChoiceOption> options;
        if (nextStage == 2 || (nextStage == 3 && TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive))
        {
            currentPendingNodeTypes.Clear();
            options = GenerateStageOptions(nextStage);
            foreach (var opt in options)
            {
                if (opt.nodeType.HasValue)
                {
                    currentPendingNodeTypes.Add(opt.nodeType.Value);
                }
            }
        }
        else if (currentPendingNodeTypes != null && currentPendingNodeTypes.Count > 0)
        {
            options = new List<StageChoiceOption>();
            foreach (var nodeType in currentPendingNodeTypes)
            {
                options.Add(StageChoiceOption.FromNodeType(nodeType));
            }
        }
        else
        {
            options = GenerateStageOptions(nextStage);
            currentPendingNodeTypes.Clear();
            foreach (var opt in options)
            {
                if (opt.nodeType.HasValue)
                {
                    currentPendingNodeTypes.Add(opt.nodeType.Value);
                }
            }
        }

        ShowChoices(options, $"NEXT STAGE : {act}-{subStage}", "다음 행선지를 선택하세요");

        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive && (nextStage == 2 || nextStage == 3))
        {
            TutorialManager.Instance.ShowStageSelectPhase(nextStage);
        }
    }

    public void Close(bool immediate = false)
    {
        if (rootPanel != null && (rootPanel.activeSelf || immediate))
        {
            CanvasGroup cg = rootPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.DOKill();
                if (immediate)
                {
                    cg.alpha = 0f;
                    rootPanel.SetActive(false);
                    ClearCards();
                    return;
                }

                cg.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
                {
                    if (rootPanel != null) rootPanel.SetActive(false);
                    ClearCards();
                });
            }
            else
            {
                rootPanel.SetActive(false);
                ClearCards();
            }
        }
        else
        {
            ClearCards();
        }
    }

    private void ClearCards()
    {
        for (int i = 0; i < activeCards.Count; i++)
        {
            if (activeCards[i] != null)
            {
                activeCards[i].transform.DOKill();
                Destroy(activeCards[i]);
            }
        }
        activeCards.Clear();
    }

    /// <summary>
    /// 스테이지 규칙(10스테이지 보스, 9스테이지 정비, 2연속 상점/공방 방지 등)에 따라 StageChoiceOption 목록을 생성합니다.
    /// </summary>
    public List<StageChoiceOption> GenerateStageOptions(int nextStage)
    {
        var options = new List<StageChoiceOption>();

        // 튜토리얼 2, 3스테이지 선택지는 둘 다 일반 전투 제시
        if (nextStage == 2 || (nextStage == 3 && TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive))
        {
            options.Add(StageChoiceOption.FromNodeType(StageNodeType.NormalBattle));
            options.Add(StageChoiceOption.FromNodeType(StageNodeType.NormalBattle));
            return options;
        }

        // 10스테이지 배수: 보스 전투 단독 제시
        if (nextStage % 10 == 0)
        {
            lastStageHadShopOrWorkshop = false;
            options.Add(StageChoiceOption.FromNodeType(StageNodeType.BossBattle));
            return options;
        }

        // 9스테이지: 보스 직전 정비 구간 [상점 vs 공방 (또는 엘리트 전투)]
        if (nextStage % 10 == 9)
        {
            lastStageHadShopOrWorkshop = true;
            StageNodeType card1 = StageNodeType.Shop;
            StageNodeType card2 = UnityEngine.Random.value < 0.8f ? StageNodeType.WorkShop : StageNodeType.EliteBattle;

            options.Add(StageChoiceOption.FromNodeType(card1));
            options.Add(StageChoiceOption.FromNodeType(card2));
            return options;
        }

        // 일반 구간 (2~8스테이지):
        // 카드 1: 기본 전투 (80% 일반 전투, 20% 엘리트 전투)
        StageNodeType leftNode = UnityEngine.Random.value < 0.80f ? StageNodeType.NormalBattle : StageNodeType.EliteBattle;

        // 카드 2 후보군 구성:
        // 다른 것은 동일 타입 중복이 안 뜨지만, "전투만 2개 뜨는 선택지(일반 전투 vs 엘리트 전투)"는 확실히 지원!
        var candidates = new List<(StageNodeType type, float weight)>();

        if (leftNode == StageNodeType.NormalBattle)
        {
            // 카드 1이 일반 전투이면 반대쪽 전투인 엘리트 전투 추가 (전투 2개 구도)
            candidates.Add((StageNodeType.EliteBattle, 40f));
        }
        else
        {
            // 카드 1이 엘리트 전투이면 반대쪽 전투인 일반 전투 추가 (전투 2개 구도)
            candidates.Add((StageNodeType.NormalBattle, 40f));
        }

        // 상점/공방 2연속 등장 방지: 직전 스테이지에 상점이나 공방이 떴었다면 이번엔 추가하지 않음
        if (!lastStageHadShopOrWorkshop)
        {
            candidates.Add((StageNodeType.Shop, 25f));
            candidates.Add((StageNodeType.WorkShop, 20f));
        }

        // 보물 노드
        candidates.Add((StageNodeType.Treasure, 15f));

        // 안전 폴백 (혹시 후보가 비어있을 경우)
        if (candidates.Count == 0)
        {
            candidates.Add((leftNode == StageNodeType.NormalBattle ? StageNodeType.EliteBattle : StageNodeType.NormalBattle, 70f));
            candidates.Add((StageNodeType.Treasure, 30f));
        }

        // 가중치 룰렛
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++) totalWeight += candidates[i].weight;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float current = 0f;
        StageNodeType rightNode = candidates[0].type;
        for (int i = 0; i < candidates.Count; i++)
        {
            current += candidates[i].weight;
            if (roll <= current)
            {
                rightNode = candidates[i].type;
                break;
            }
        }

        // 이번 스테이지에 상점 또는 공방이 제시되었는지 기록 (다음 스테이지 2연속 방지용)
        lastStageHadShopOrWorkshop = (leftNode == StageNodeType.Shop || leftNode == StageNodeType.WorkShop ||
                                      rightNode == StageNodeType.Shop || rightNode == StageNodeType.WorkShop);

        options.Add(StageChoiceOption.FromNodeType(leftNode));
        options.Add(StageChoiceOption.FromNodeType(rightNode));
        return options;
    }

    #endregion

    #region Card Rendering & UI Generation

    private void RenderChoiceCards(IList<StageChoiceOption> options)
    {
        ClearCards();

        int count = options.Count;
        if (count == 0) return;

        // 선택지 개수에 맞춘 동적 너비 및 X좌표 배치 계산
        float cardWidth = 190f;
        float[] xPositions = new float[count];

        if (count == 1)
        {
            cardWidth = 230f;
            xPositions[0] = 0f;
        }
        else if (count == 2)
        {
            cardWidth = 195f;
            xPositions[0] = -115f;
            xPositions[1] = 115f;
        }
        else if (count == 3)
        {
            cardWidth = 165f;
            xPositions[0] = -175f;
            xPositions[1] = 0f;
            xPositions[2] = 175f;
        }
        else
        {
            float totalSpan = 520f;
            float step = totalSpan / (count - 1);
            cardWidth = Mathf.Clamp((totalSpan / count) - 10f, 100f, 150f);
            for (int i = 0; i < count; i++)
            {
                xPositions[i] = -totalSpan * 0.5f + (i * step);
            }
        }

        for (int i = 0; i < count; i++)
        {
            CreateCard(options[i], xPositions[i], cardWidth, count >= 3);
        }
    }

    private void CreateCard(StageChoiceOption option, float posX, float cardWidth, bool isCompact)
    {
        if (cardContainer == null || option == null) return;

        if (stageChoiceCardPrefab == null)
        {
            stageChoiceCardPrefab = Resources.Load<GameObject>("UI/StageChoiceCard");
        }

        float cardHeight = 210f;

        if (stageChoiceCardPrefab != null)
        {
            GameObject prefabCardObj = Instantiate(stageChoiceCardPrefab, cardContainer, false);
            prefabCardObj.name = $"Card_{option.id ?? "Option"}";
            activeCards.Add(prefabCardObj);

            RectTransform prefabCardRect = prefabCardObj.GetComponent<RectTransform>();
            if (prefabCardRect != null)
            {
                prefabCardRect.anchorMin = new Vector2(0.5f, 0.5f);
                prefabCardRect.anchorMax = new Vector2(0.5f, 0.5f);
                prefabCardRect.pivot = new Vector2(0.5f, 0.5f);
                prefabCardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
                prefabCardRect.anchoredPosition = new Vector2(posX, -15f);
                prefabCardRect.localScale = Vector3.one;
            }

            StageChoiceCard cardComp = prefabCardObj.GetComponent<StageChoiceCard>();
            if (cardComp == null)
            {
                cardComp = prefabCardObj.AddComponent<StageChoiceCard>();
            }

            cardComp.Setup(option, cardWidth, cardHeight, isCompact, () => OnCardSelected(option, prefabCardObj));

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive &&
                TutorialManager.Instance.CurrentPhase == TutorialPhase.StageSelect && TutorialManager.Instance.IsCurrentStepAction())
            {
                if (option.nodeType == StageNodeType.NormalBattle)
                {
                    cardComp.SetTutorialHighlighted(true);
                }
            }
            return;
        }

        TMP_FontAsset font = null;
        if (GameManager.Instance != null && GameManager.Instance.coinText != null)
        {
            font = GameManager.Instance.coinText.font;
        }
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
        }

        GameObject cardObj = new GameObject($"Card_{option.id ?? "Option"}", typeof(RectTransform));
        cardObj.transform.SetParent(cardContainer, false);
        activeCards.Add(cardObj);

        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
        cardRect.anchoredPosition = new Vector2(posX, -15f);
        cardRect.localScale = Vector3.one;

        // 카드 배경
        Image bgImage = cardObj.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);

        // 아웃라인
        Outline outline = cardObj.AddComponent<Outline>();
        Color themeColor = option.themeColor;
        outline.effectColor = themeColor;
        outline.effectDistance = new Vector2(2f, 2f);

        // 버튼 컴포넌트
        Button btn = cardObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = cb;

        // 상단 헤더 배너
        GameObject headerObj = new GameObject("Header", typeof(RectTransform));
        headerObj.transform.SetParent(cardObj.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, 0f);
        headerRect.sizeDelta = new Vector2(0f, 38f);
        headerRect.localScale = Vector3.one;

        Image headerBg = headerObj.AddComponent<Image>();
        Color headerColor = themeColor;
        headerColor.a = 0.35f;
        headerBg.color = headerColor;

        // 타이틀 텍스트
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform));
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;
        titleRect.localScale = Vector3.one;

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.font = font;
        titleText.text = option.title ?? "";
        titleText.fontSize = isCompact ? 13.5f : 15f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = themeColor;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 8.5f;
        titleText.fontSizeMax = isCompact ? 13.5f : 15f;
        titleText.textWrappingMode = TextWrappingModes.Normal;
        titleText.overflowMode = TextOverflowModes.Ellipsis;

        bool hasReward = !string.IsNullOrEmpty(option.rewardText);

        // 설명 텍스트
        GameObject descObj = new GameObject("DescText", typeof(RectTransform));
        descObj.transform.SetParent(cardObj.transform, false);
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0.5f);
        descRect.anchorMax = new Vector2(1f, 0.5f);
        descRect.pivot = new Vector2(0.5f, 0.5f);
        descRect.anchoredPosition = new Vector2(0f, hasReward ? 15f : 0f);
        descRect.sizeDelta = new Vector2(-20f, hasReward ? 75f : 110f);
        descRect.localScale = Vector3.one;

        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.font = font;
        descText.text = option.description ?? "";
        descText.fontSize = isCompact ? 10.5f : 11.5f;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.85f, 0.85f, 0.88f, 1f);
        descText.enableAutoSizing = true;
        descText.fontSizeMin = 7.5f;
        descText.fontSizeMax = isCompact ? 10.5f : 11.5f;
        descText.textWrappingMode = TextWrappingModes.Normal;
        descText.overflowMode = TextOverflowModes.Ellipsis;

        // 하단 보상 배너 (보상 텍스트가 있을 때만 생성)
        if (hasReward)
        {
            GameObject rewardBannerObj = new GameObject("RewardBanner", typeof(RectTransform));
            rewardBannerObj.transform.SetParent(cardObj.transform, false);
            RectTransform rewardBannerRect = rewardBannerObj.GetComponent<RectTransform>();
            rewardBannerRect.anchorMin = new Vector2(0f, 0f);
            rewardBannerRect.anchorMax = new Vector2(1f, 0f);
            rewardBannerRect.pivot = new Vector2(0.5f, 0f);
            rewardBannerRect.anchoredPosition = new Vector2(0f, 40f);
            rewardBannerRect.sizeDelta = new Vector2(-12f, 32f);
            rewardBannerRect.localScale = Vector3.one;

            Image rewardBg = rewardBannerObj.AddComponent<Image>();
            rewardBg.color = new Color(0f, 0f, 0f, 0.5f);

            GameObject rewardTextObj = new GameObject("RewardText", typeof(RectTransform));
            rewardTextObj.transform.SetParent(rewardBannerObj.transform, false);
            RectTransform rewardTextRect = rewardTextObj.GetComponent<RectTransform>();
            rewardTextRect.anchorMin = Vector2.zero;
            rewardTextRect.anchorMax = Vector2.one;
            rewardTextRect.anchoredPosition = Vector2.zero;
            rewardTextRect.sizeDelta = Vector2.zero;
            rewardTextRect.localScale = Vector3.one;

            TextMeshProUGUI rewardText = rewardTextObj.AddComponent<TextMeshProUGUI>();
            rewardText.font = font;
            rewardText.text = option.rewardText;
            rewardText.fontSize = isCompact ? 8.5f : 9.5f;
            rewardText.alignment = TextAlignmentOptions.Center;
            rewardText.enableAutoSizing = true;
            rewardText.fontSizeMin = 7f;
            rewardText.fontSizeMax = isCompact ? 8.5f : 9.5f;
            rewardText.textWrappingMode = TextWrappingModes.Normal;
            rewardText.overflowMode = TextOverflowModes.Ellipsis;
        }

        // 하단 "선택" 버튼 라벨
        GameObject selectBtnObj = new GameObject("SelectBtn", typeof(RectTransform));
        selectBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform selectBtnRect = selectBtnObj.GetComponent<RectTransform>();
        selectBtnRect.anchorMin = new Vector2(0f, 0f);
        selectBtnRect.anchorMax = new Vector2(1f, 0f);
        selectBtnRect.pivot = new Vector2(0.5f, 0f);
        selectBtnRect.anchoredPosition = new Vector2(0f, 8f);
        selectBtnRect.sizeDelta = new Vector2(-24f, 26f);
        selectBtnRect.localScale = Vector3.one;

        Image selectBtnBg = selectBtnObj.AddComponent<Image>();
        Color btnBgColor = themeColor * 0.8f;
        btnBgColor.a = 1f;
        selectBtnBg.color = btnBgColor;

        GameObject selectBtnTextObj = new GameObject("Text", typeof(RectTransform));
        selectBtnTextObj.transform.SetParent(selectBtnObj.transform, false);
        RectTransform selectBtnTextRect = selectBtnTextObj.GetComponent<RectTransform>();
        selectBtnTextRect.anchorMin = Vector2.zero;
        selectBtnTextRect.anchorMax = Vector2.one;
        selectBtnTextRect.anchoredPosition = Vector2.zero;
        selectBtnTextRect.sizeDelta = Vector2.zero;
        selectBtnTextRect.localScale = Vector3.one;

        TextMeshProUGUI selectBtnText = selectBtnTextObj.AddComponent<TextMeshProUGUI>();
        selectBtnText.font = font;
        selectBtnText.text = string.IsNullOrEmpty(option.buttonText) ? "선택" : option.buttonText;
        selectBtnText.fontSize = 12f;
        selectBtnText.fontStyle = FontStyles.Bold;
        selectBtnText.alignment = TextAlignmentOptions.Center;
        selectBtnText.color = Color.white;

        // 클릭 이벤트
        btn.onClick.AddListener(() => OnCardSelected(option, cardObj));

        // 등장 애니메이션 및 호버 보호
        bool isAppearing = true;

        // 호버 애니메이션 트리거
        EventTrigger trigger = cardObj.AddComponent<EventTrigger>();
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((data) =>
        {
            if (!isSelecting && !isAppearing)
            {
                cardObj.transform.DOKill();
                cardObj.transform.DOScale(1.05f, 0.15f).SetUpdate(true);
                outline.effectDistance = new Vector2(3f, 3f);
            }
        });
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) =>
        {
            if (!isSelecting && !isAppearing)
            {
                cardObj.transform.DOKill();
                cardObj.transform.DOScale(1.0f, 0.15f).SetUpdate(true);
                outline.effectDistance = new Vector2(2f, 2f);
            }
        });
        trigger.triggers.Add(exitEntry);

        // 등장 애니메이션 (0 -> 1 스케일)
        cardObj.transform.localScale = Vector3.zero;
        cardObj.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetUpdate(true)
            .OnComplete(() =>
            {
                isAppearing = false;
                if (cardObj != null)
                {
                    cardObj.transform.localScale = Vector3.one;
                }
            });
    }

    private void OnCardSelected(StageChoiceOption option, GameObject cardObj)
    {
        if (isSelecting) return;

        // 튜토리얼 진행 중일 때 카드 클릭 제어
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            // 1. 스테이지 선택 페이즈 대화창이 아직 안내 진행 중이고, 행동 스텝이 아닌 경우에만 클릭 차단
            if (TutorialManager.Instance.IsDialogShowing && !TutorialManager.Instance.IsCurrentStepAction())
            {
                if (cardObj != null)
                {
                    cardObj.transform.DOKill();
                    cardObj.transform.DOShakePosition(0.4f, new Vector3(5f, 1f, 0f), 20, 90f, false, true).SetUpdate(true);
                }
                return;
            }

            // 2. 튜토리얼 스테이지 선택 단계 중에는 일반 전투 카드만 선택 허용
            if (option == null || option.nodeType != StageNodeType.NormalBattle)
            {
                if (cardObj != null)
                {
                    cardObj.transform.DOKill();
                    cardObj.transform.DOShakePosition(0.4f, new Vector3(5f, 1f, 0f), 20, 90f, false, true).SetUpdate(true);
                }
                return;
            }

            TutorialManager.Instance.OnStageChoiceSelected();
        }

        isSelecting = true;

        if (cardObj != null)
        {
            cardObj.transform.DOKill();
            cardObj.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 10, 1).SetUpdate(true)
                .OnComplete(() =>
                {
                    ExecuteOption(option);
                });
        }
        else
        {
            ExecuteOption(option);
        }
    }

    private void ExecuteOption(StageChoiceOption option)
    {
        currentPendingNodeTypes.Clear();
        Close();

        if (option?.onSelected != null)
        {
            option.onSelected.Invoke();
        }
        else if (option?.nodeType.HasValue == true)
        {
            StageNodeHelper.ExecuteDefaultAction(option.nodeType.Value);
        }
    }

    #endregion

    private Canvas FindTargetCanvas()
    {
        // 1. 코인 텍스트가 속한 인게임 UI 캔버스 우선 탐색 (640x320 캔버스)
        if (GameManager.Instance != null && GameManager.Instance.coinText != null)
        {
            Canvas c = GameManager.Instance.coinText.GetComponentInParent<Canvas>();
            if (c != null && IsValidGameplayCanvas(c))
                return c;
        }

        // 2. 씬 내 "Canvas" 이름의 기본 게임 캔버스 탐색
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj != null)
        {
            Canvas c = canvasObj.GetComponent<Canvas>();
            if (c != null && IsValidGameplayCanvas(c))
                return c;
        }

        // 3. 씬의 모든 캔버스 중 ModalCanvas, TooltipCanvas가 아니고 기준 해상도가 인게임(x < 1000)인 캔버스 탐색
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c == null || !IsValidGameplayCanvas(c)) continue;
            CanvasScaler scaler = c.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.referenceResolution.x < 1000f)
                return c;
        }

        // 4. ModalCanvas, TooltipCanvas를 제외한 다른 유효한 캔버스
        foreach (Canvas c in canvases)
        {
            if (c != null && IsValidGameplayCanvas(c))
                return c;
        }

        return FindFirstObjectByType<Canvas>();
    }

    private static bool IsValidGameplayCanvas(Canvas canvas)
    {
        if (canvas == null) return false;
        string name = canvas.gameObject.name;
        if (name == "ModalCanvas" || name == "TooltipCanvas") return false;
        return true;
    }

    private void EnsureUI()
    {
        Canvas targetCanvas = FindTargetCanvas();
        if (targetCanvas == null) return;

        if (rootPanel != null)
        {
            // Canvas가 바뀌었거나 잘못된 Canvas에 붙어있는 경우 올바른 Canvas로 재부착 및 초기화
            if (rootPanel.transform.parent != targetCanvas.transform)
            {
                rootPanel.transform.SetParent(targetCanvas.transform, false);
                if (panelRect != null)
                {
                    panelRect.anchorMin = Vector2.zero;
                    panelRect.anchorMax = Vector2.one;
                    panelRect.anchoredPosition = Vector2.zero;
                    panelRect.sizeDelta = Vector2.zero;
                    panelRect.localScale = Vector3.one;
                }
            }
            return;
        }

        if (stageSelectPanelPrefab == null)
        {
            stageSelectPanelPrefab = Resources.Load<GameObject>("UI/StageSelectPanel");
        }

        if (stageSelectPanelPrefab != null)
        {
            rootPanel = Instantiate(stageSelectPanelPrefab, targetCanvas.transform, false);
            rootPanel.name = "StageSelectPanel";

            panelRect = rootPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = Vector2.zero;
                panelRect.localScale = Vector3.one;
            }

            StageSelectPanel panelComp = rootPanel.GetComponent<StageSelectPanel>();
            if (panelComp != null)
            {
                headerTitleText = panelComp.headerTitleText;
                headerSubtitleText = panelComp.headerSubtitleText;
                cardContainer = panelComp.cardContainer;
            }
            else
            {
                Transform t = rootPanel.transform.Find("HeaderTitle");
                if (t != null) headerTitleText = t.GetComponent<TextMeshProUGUI>();
                Transform st = rootPanel.transform.Find("HeaderSubtitle");
                if (st != null) headerSubtitleText = st.GetComponent<TextMeshProUGUI>();
                Transform cc = rootPanel.transform.Find("CardContainer");
                if (cc != null) cardContainer = cc;
            }

            rootPanel.SetActive(false);
            return;
        }

        TMP_FontAsset font = null;
        if (GameManager.Instance != null && GameManager.Instance.coinText != null)
        {
            font = GameManager.Instance.coinText.font;
        }
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
        }

        rootPanel = new GameObject("StageSelectPanel", typeof(RectTransform));
        rootPanel.transform.SetParent(targetCanvas.transform, false);

        panelRect = rootPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Image blocker = rootPanel.AddComponent<Image>();
        blocker.color = Color.clear;

        CanvasGroup cg = rootPanel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        // 상단 타이틀 영역
        GameObject titleHeaderObj = new GameObject("HeaderTitle", typeof(RectTransform));
        titleHeaderObj.transform.SetParent(rootPanel.transform, false);
        RectTransform headerTitleRect = titleHeaderObj.GetComponent<RectTransform>();
        headerTitleRect.anchorMin = new Vector2(0.5f, 1f);
        headerTitleRect.anchorMax = new Vector2(0.5f, 1f);
        headerTitleRect.pivot = new Vector2(0.5f, 1f);
        headerTitleRect.anchoredPosition = new Vector2(0f, -22f);
        headerTitleRect.sizeDelta = new Vector2(400f, 30f);
        headerTitleRect.localScale = Vector3.one;

        headerTitleText = titleHeaderObj.AddComponent<TextMeshProUGUI>();
        headerTitleText.font = font;
        headerTitleText.text = "NEXT STAGE";
        headerTitleText.fontSize = 18f;
        headerTitleText.fontStyle = FontStyles.Bold;
        headerTitleText.alignment = TextAlignmentOptions.Center;
        headerTitleText.color = new Color(1f, 0.85f, 0.3f, 1f);

        // 상단 서브타이틀
        GameObject subtitleObj = new GameObject("HeaderSubtitle", typeof(RectTransform));
        subtitleObj.transform.SetParent(rootPanel.transform, false);
        RectTransform subtitleRect = subtitleObj.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 1f);
        subtitleRect.anchorMax = new Vector2(0.5f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -50f);
        subtitleRect.sizeDelta = new Vector2(400f, 20f);
        subtitleRect.localScale = Vector3.one;

        headerSubtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
        headerSubtitleText.font = font;
        headerSubtitleText.text = "다음 행선지를 선택하세요";
        headerSubtitleText.fontSize = 12f;
        headerSubtitleText.alignment = TextAlignmentOptions.Center;
        headerSubtitleText.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);

        // 카드 컨테이너
        GameObject containerObj = new GameObject("CardContainer", typeof(RectTransform));
        containerObj.transform.SetParent(rootPanel.transform, false);
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0f, -10f);
        containerRect.sizeDelta = new Vector2(560f, 240f);
        containerRect.localScale = Vector3.one;
        cardContainer = containerObj.transform;

        rootPanel.SetActive(false);
    }
}
