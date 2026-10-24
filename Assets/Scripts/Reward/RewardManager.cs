using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public partial class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // UI-only manager. Uses inspector references only.

    [Header("UI")]
    public GameObject rewardPanel;
    public Transform rewardContainer;
    public GameObject rewardButtonPrefab;
    public Button closeButton;
    public TMPro.TextMeshProUGUI rewardSummaryText;

    [Header("Settings")]
    public int maxRewardCount = 5;

    [Header("Data")]
    public ShopPieceData pieceData; // 기물 데이터를 가져오기 위해 사용

    [Header("Animation")]
    public float rewardAnimDuration = 0.5f;
    public float rewardCloseDuration = 0.3f;
    public float rewardCollapsedHeight = 72f;

    private RectTransform rewardPanelRect;
    private float rewardExpandedHeight = 0f;
    private Vector2 rewardPanelFinalAnchoredPos = Vector2.zero;

    private TMPro.TextMeshProUGUI titleText;
    private TMPro.TextMeshProUGUI closeButtonText;
    private ScrollRect rewardScrollRect;
    private GameManager.BattleRewardResult currentBattleReward;
    private bool isSettlementPhase = false;

    [Header("Movement")]
    public float panelMoveOffset = 60f;
    public float panelMoveDuration = 0.25f;
    public Ease panelHeightEase = Ease.OutCubic; // decelerating
    public Ease panelMoveEaseIn = Ease.OutCubic;
    public Ease panelMoveEaseOut = Ease.InCubic;

    private int activeRewardCount = 0;

    [Header("Reroll")]
    public Button rerollButton;
    public TMPro.TextMeshProUGUI rerollCostText;
    public int baseRerollCost = 5;

    [Header("Reward Probabilities (완전 랜덤 가중치)")]
    [Tooltip("기물 등장 확률 가중치")]
    [Range(0f, 100f)] public float pieceWeight = 35f;

    [Tooltip("인장 등장 확률 가중치")]
    [Range(0f, 100f)] public float sealWeight = 35f;

    [Tooltip("유물 등장 확률 가중치")]
    [Range(0f, 100f)] public float artifactWeight = 20f;

    [Tooltip("골드 등장 확률 가중치")]
    [Range(0f, 100f)] public float coinWeight = 10f;

    private int currentRarity = 0;

    /// <summary>
    /// 가중치 비율에 따라 완전 랜덤으로 보상 타입을 추첨합니다.
    /// 인스펙터의 pieceWeight, sealWeight, artifactWeight, coinWeight 설정에 의해 결정됩니다.
    /// </summary>
    public RewardButton.RewardType GetRandomRewardType(bool allowCoin = true)
    {
        bool isElite = GameManager.Instance != null && GameManager.Instance.CurrentBattleType == BattleType.Elite;

        float pWeight = Mathf.Max(0f, pieceWeight);
        float sWeight = Mathf.Max(0f, sealWeight);
        float aWeight = Mathf.Max(0f, artifactWeight);
        float cWeight = allowCoin ? Mathf.Max(0f, coinWeight) : 0f;
        if (ShotgunKingSystem.IsActive) pWeight = 0f;

        // 엘리트 전투 시 유물/인장/고급 기물 위주로 보상 타입 가중치 보정 (골드 선택지 최소화)
        if (isElite)
        {
            aWeight = 35f;
            sWeight = 35f;
            pWeight = 25f;
            cWeight = allowCoin ? 5f : 0f;
        }

        float totalWeight = pWeight + sWeight + aWeight + cWeight;
        if (totalWeight <= 0f)
        {
            return RewardButton.RewardType.Piece;
        }

        float randomVal = Random.Range(0f, totalWeight);
        float current = 0f;

        current += pWeight;
        if (randomVal < current) return RewardButton.RewardType.Piece;

        current += sWeight;
        if (randomVal < current) return RewardButton.RewardType.Seal;

        current += aWeight;
        if (randomVal < current) return RewardButton.RewardType.Artifact;

        return RewardButton.RewardType.Coin;
    }

    /// <summary>
    /// 외부 코드 또는 밸런스 설정에서 보상 확률 가중치를 손쉽게 재설정할 수 있는 편의 메서드입니다.
    /// </summary>
    public void SetRewardWeights(float piece, float seal, float artifact, float coin)
    {
        pieceWeight = piece;
        sealWeight = seal;
        artifactWeight = artifact;
        coinWeight = coin;
    }

    private void Start()
    {
        // Register self as the current UI with RewardService (if present)
        if (RewardService.Instance != null)
        {
            RewardService.Instance.RegisterUI(this);
        }

        SetupHorizontalRewardLayout();

        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnRewardClaimed);
        }

        if (rerollButton == null && rewardPanel != null)
        {
            Button[] buttons = rewardPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b.name.Contains("Reroll") || b.name.Contains("Refresh"))
                {
                    rerollButton = b;
                    break;
                }
            }
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(OnRerollClicked);
            rerollButton.onClick.AddListener(OnRerollClicked);
        }

        UpdateRerollUI();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
        }
    }



    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        if (RewardService.Instance != null)
        {
            RewardService.Instance.UnregisterUI(this);
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(OnRerollClicked);
        }
    }

    private int GetCurrentRerollCost()
    {
        int cost = baseRerollCost;
        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.ApplyArtifactWithLevel("A002", level =>
            {
                int discount = 2 + (level - 1);
                cost = Mathf.Max(1, baseRerollCost - discount);
            });
        }
        return cost;
    }

    private void UpdateRerollUI()
    {
        if (rerollCostText != null)
        {
            rerollCostText.text = $"{GetCurrentRerollCost()}";
        }
    }

    private List<SavedRewardCardData> pendingRewardCards = new List<SavedRewardCardData>();
    public IReadOnlyList<SavedRewardCardData> PendingRewardCards => pendingRewardCards;

    public void SetPendingRewardCards(List<SavedRewardCardData> cards)
    {
        pendingRewardCards.Clear();
        if (cards != null)
        {
            pendingRewardCards.AddRange(cards);
        }
    }

    public void ClearPendingRewardCards()
    {
        pendingRewardCards.Clear();
    }

    public void OnRerollClicked()
    {
        if (GameManager.Instance == null) return;
        int cost = GetCurrentRerollCost();
        if (GameManager.Instance.Coin < cost)
        {
            if (rerollButton != null)
            {
                rerollButton.transform.DOShakePosition(0.3f, 5f, 20, 90, false, true);
            }
            return;
        }

        if (GameManager.Instance.UseCoin(cost))
        {
            ClearPendingRewardCards();
            GenerateRewards(3, currentRarity);
            UpdateRerollUI();
            if (rewardContainer != null)
            {
                rewardContainer.DOShakePosition(0.25f, 6f, 20, 90, false, true);
            }
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Reward)
        {
            int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
            BattleType bType = GameManager.Instance != null ? GameManager.Instance.CurrentBattleType : BattleType.Normal;
            bool bossCleared = GameManager.Instance != null && GameManager.Instance.BossJustCleared;

            int rarity = 0;
            if (bType == BattleType.Boss || bossCleared)
            {
                // 보스 전투: 전설/에픽급 확정 (최소 3)
                rarity = Mathf.Max(3, (cleared / 3) + 1);
            }
            else if (bType == BattleType.Elite)
            {
                // 엘리트 전투: 고급(Rare 이상) 확정 (최소 2)
                rarity = Mathf.Max(2, (cleared / 3) + 1);
            }
            else
            {
                // 일반 전투
                rarity = Mathf.Clamp(cleared / 3, 0, 3);
            }

            ShowRewards(3, rarity);
        }
    }

    public void ShowRewards(int count, int rarity)
    {
        currentRarity = rarity;
        SetupHorizontalRewardLayout();

        if (rewardPanel != null)
        {
            rewardPanel.SetActive(true);
            rewardPanel.transform.SetAsLastSibling();

            RectTransform rect = rewardPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = rewardPanelFinalAnchoredPos;
            }

            CanvasGroup cg = rewardPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;
            }
        }

        // 전투 정산 결과 계산 및 골드 즉시 흡수 연출 (정산 팝업 클릭 대기 생략)
        currentBattleReward = GameManager.Instance != null
            ? GameManager.Instance.CalculateBattleReward(pieceData)
            : default;

        Vector3 startPos = rewardPanel != null ? rewardPanel.transform.position : transform.position;
        PlayCoinFlyAnimation(currentBattleReward.TotalReward, startPos, null);

        // 정산 팝업 없이 곧바로 3지선다 보상 선택창으로 진입
        ShowChoicePhase(count, rarity);
    }



    private void ShowChoicePhase(int count, int rarity)
    {
        isSettlementPhase = false;

        if (rewardPanelRect != null)
        {
            rewardPanelRect.DOKill();
            rewardPanelRect.DOSizeDelta(new Vector2(540f, 250f), 0.25f).SetEase(Ease.OutQuad);
            rewardPanelRect.anchoredPosition = Vector2.zero;
        }

        if (titleText != null)
        {
            BattleType bType = GameManager.Instance != null ? GameManager.Instance.CurrentBattleType : BattleType.Normal;
            bool bossCleared = GameManager.Instance != null && GameManager.Instance.BossJustCleared;

            string header;
            if (bType == BattleType.Boss || bossCleared)
            {
                header = $"💀 보스 격파! (+{currentBattleReward.TotalReward}G)";
            }
            else if (bType == BattleType.Elite)
            {
                header = $"🔥 엘리트 승리! (+{currentBattleReward.TotalReward}G)";
            }
            else
            {
                header = $"⚔️ 전투 승리! (+{currentBattleReward.TotalReward}G)";
            }
            titleText.text = header;

            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            if (titleRect != null)
            {
                titleRect.anchoredPosition = new Vector2(0f, 102f);
            }
        }

        // 정산 세로 계산식 텍스트 숨기기
        if (rewardSummaryText != null)
        {
            rewardSummaryText.gameObject.SetActive(false);
        }

        // 선택 보상 카드 컨테이너 표시
        if (rewardScrollRect != null)
        {
            rewardScrollRect.gameObject.SetActive(true);
            RectTransform scrollRectTransform = rewardScrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
            {
                scrollRectTransform.sizeDelta = new Vector2(490f, 185f);
                scrollRectTransform.anchoredPosition = new Vector2(0f, -4f);
            }
        }

        if (rerollButton != null)
        {
            bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
            rerollButton.gameObject.SetActive(!isTutorial);
            if (!isTutorial)
            {
                RectTransform rerollRect = rerollButton.GetComponent<RectTransform>();
                if (rerollRect != null)
                {
                    rerollRect.anchoredPosition = new Vector2(215f, 102f);
                }
                UpdateRerollUI();
            }
        }

        // 닫기 / 선택 완료 버튼 설정
        if (closeButton != null)
        {
            bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
            closeButton.gameObject.SetActive(!isTutorial);
            closeButton.interactable = !isTutorial;
            if (closeButtonText != null)
            {
                closeButtonText.text = "선택 완료";
            }
            RectTransform btnRect = closeButton.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                btnRect.anchoredPosition = new Vector2(0f, -108f);
                btnRect.sizeDelta = new Vector2(100f, 24f);
            }

            closeButton.onClick.RemoveAllListeners();
            if (!isTutorial)
            {
                closeButton.onClick.AddListener(OnRewardClaimed);
            }
        }

        GenerateRewards(count, rarity);

        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
            if (cleared == 1)
            {
                TutorialManager.Instance.ShowRewardPhase();
            }
            else if (cleared == 2)
            {
                TutorialManager.Instance.ShowRewardStage2Phase();
            }
            else if (cleared == 3)
            {
                TutorialManager.Instance.ShowRewardStage3Phase();
            }
        }
    }



    private void GenerateRewards(int count, int rarity)
    {
        if (rewardContainer == null) return;
        if (rewardButtonPrefab == null)
        {
            rewardButtonPrefab = Resources.Load<GameObject>("UI/RewardBtn");
        }
        if (rewardButtonPrefab == null) return;

        // 기존 버튼 제거
        foreach (Transform child in rewardContainer)
        {
            Destroy(child.gameObject);
        }

        activeRewardCount = 0;
        bool hasSavedCards = pendingRewardCards != null && pendingRewardCards.Count > 0;
        bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;

        if (hasSavedCards)
        {
            for (int i = 0; i < pendingRewardCards.Count; i++)
            {
                var cardData = pendingRewardCards[i];
                GameObject obj = Instantiate(rewardButtonPrefab, rewardContainer);
                if (obj == null) continue;

                RewardButton btn = obj.GetComponent<RewardButton>();
                if (btn != null)
                {
                    btn.InitializeWithSavedData(this, rarity, cardData);
                    if (isTutorial && i != 0)
                    {
                        btn.SetTutorialDisabled(true);
                    }
                    activeRewardCount++;
                }

                AnimateCardEntrance(obj, btn, i, isTutorial);
            }
        }
        else
        {
            bool coinAlreadyGenerated = false;
            pendingRewardCards.Clear();
            int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;

            // 각 카드 슬롯마다 가중치 기반으로 완전 랜덤 추첨 (골드는 최대 1개)
            for (int i = 0; i < count; i++)
            {
                GameObject obj = Instantiate(rewardButtonPrefab, rewardContainer);
                if (obj == null) continue;

                RewardButton btn = obj.GetComponent<RewardButton>();
                if (btn != null)
                {
                    if (isTutorial && i == 0)
                    {
                        InitializeTutorialFixedCard(btn, rarity, cleared, ref coinAlreadyGenerated);
                    }
                    else
                    {
                        RewardButton.RewardType randomType = GetRandomRewardType(!coinAlreadyGenerated);
                        if (randomType == RewardButton.RewardType.Coin)
                        {
                            coinAlreadyGenerated = true;
                        }
                        btn.InitializeWithType(this, rarity, randomType);

                        // 튜토리얼 1, 2, 3스테이지에서는 다른 선택지를 비활성화하여 확정 보상만 선택 가능하도록 제한
                        if (isTutorial && cleared <= 3)
                        {
                            btn.SetTutorialDisabled(true);
                        }
                    }
                    activeRewardCount++;
                    pendingRewardCards.Add(btn.GetCardData());
                }

                AnimateCardEntrance(obj, btn, i, isTutorial);
            }

            // 새로운 선택지 생성 직후 즉시 세이브에 기록
            StageSaveManager.EnsureInstance()?.SaveCurrentRun(pendingReward: true);
        }
    }

    private void InitializeTutorialFixedCard(RewardButton btn, int rarity, int cleared, ref bool coinAlreadyGenerated)
    {
        if (cleared == 1)
        {
            SealData brokenBishopSeal = Resources.Load<SealData>("Seal/Diagnal");
            if (brokenBishopSeal != null)
            {
                btn.InitializeWithFixedSeal(this, rarity, brokenBishopSeal);
            }
            else
            {
                btn.InitializeWithType(this, rarity, RewardButton.RewardType.Seal);
            }
        }
        else if (cleared == 2)
        {
            ArtifactData lightningArtifact = StageSaveManager.FindArtifactData("AC02");
            if (lightningArtifact != null)
            {
                btn.InitializeWithFixedArtifact(this, rarity, lightningArtifact);
            }
            else
            {
                btn.InitializeWithType(this, rarity, RewardButton.RewardType.Artifact);
            }
        }
        else if (cleared == 3)
        {
            ArtifactData protectionPotion = StageSaveManager.FindArtifactData("AC03");
            if (protectionPotion != null)
            {
                btn.InitializeWithFixedArtifact(this, rarity, protectionPotion);
            }
            else
            {
                btn.InitializeWithType(this, rarity, RewardButton.RewardType.Artifact);
            }
        }
        else
        {
            RewardButton.RewardType randomType = GetRandomRewardType(!coinAlreadyGenerated);
            if (randomType == RewardButton.RewardType.Coin) coinAlreadyGenerated = true;
            btn.InitializeWithType(this, rarity, randomType);
        }
    }

    public void DisableAllRewardButtons()
    {
        if (rewardContainer != null)
        {
            foreach (Transform child in rewardContainer)
            {
                Button btn = child.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = false;
                }
            }
        }

        if (closeButton != null)
        {
            closeButton.interactable = false;
        }

        if (rerollButton != null)
        {
            rerollButton.interactable = false;
        }
    }

    public void OnRewardButtonClicked()
    {
        // 3개 중 하나만 선택: 선택 즉시 나머지 버튼 비활성화 및 완료 처리
        DisableAllRewardButtons();

        activeRewardCount = 0;
        OnRewardClaimed();
    }

    public void OnRewardClaimed()
    {
        // 튜토리얼 진행 중에는 보상 카드를 직접 선택하지 않고 나가기/닫기를 누르는 것을 완전 차단
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            if (activeRewardCount > 0)
            {
                return;
            }
        }

        ClearPendingRewardCards();
        if (rewardPanel != null)
        {
            if (rewardContainer != null)
            {
                foreach (Transform child in rewardContainer)
                {
                    Destroy(child.gameObject);
                }
            }
            if (rewardPanelRect != null)
            {
                rewardPanelRect.DOKill();
                if (rewardExpandedHeight > 0f)
                {
                    SetRewardPanelHeight(rewardExpandedHeight);
                }
                rewardPanelRect.anchoredPosition = rewardPanelFinalAnchoredPos;
            }

            rewardPanel.SetActive(false);
            PieceManager pieceManager = PieceManager.Instance;
            if (pieceManager == null)
            {
                pieceManager = FindFirstObjectByType<PieceManager>(FindObjectsInactive.Include);
            }
            if (pieceManager != null && pieceManager.HasPlacementSnapshot)
            {
                pieceManager.RestorePlacementPositions(false);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.BossJustCleared = false;
                // 전투 보상 완료 후 다음 행선지 선택(StageSelect)으로 전환
                GameManager.Instance.AdvanceToStageSelect();
            }
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceToStageSelect();
        }
    }
}
