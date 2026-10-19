using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class RewardManager : MonoBehaviour
{
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

    public void SetupHorizontalRewardLayout()
    {
        if (rewardPanel != null)
        {
            rewardPanelRect = rewardPanel.GetComponent<RectTransform>();
            if (rewardPanelRect != null)
            {
                rewardPanelRect.pivot = new Vector2(0.5f, 0.5f);
                rewardPanelFinalAnchoredPos = Vector2.zero;
            }

            Transform titleTextObj = rewardPanel.transform.Find("Image/GameObject/Text (TMP)");
            if (titleTextObj != null)
            {
                titleText = titleTextObj.GetComponent<TMPro.TextMeshProUGUI>();
            }
        }

        if (closeButton != null)
        {
            closeButtonText = closeButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        }

        if (rewardContainer != null)
        {
            // 세로 레이아웃 그룹을 제거하고 가로 레이아웃 그룹으로 설정
            var vertical = rewardContainer.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
            {
                DestroyImmediate(vertical);
            }

            var horizontal = rewardContainer.GetComponent<HorizontalLayoutGroup>();
            if (horizontal == null)
            {
                horizontal = rewardContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            horizontal.spacing = 15f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = false;
            horizontal.childControlHeight = false;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;

            RectTransform containerRect = rewardContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                containerRect.anchorMin = new Vector2(0f, 0f);
                containerRect.anchorMax = new Vector2(1f, 1f);
                containerRect.pivot = new Vector2(0.5f, 0.5f);
                containerRect.anchoredPosition = Vector2.zero;
                containerRect.sizeDelta = Vector2.zero;
            }

            // 부모 ScrollRect 및 Viewport 크기 및 스크롤 제한 설정
            rewardScrollRect = rewardContainer.GetComponentInParent<ScrollRect>();
            if (rewardScrollRect != null)
            {
                rewardScrollRect.horizontal = false;
                rewardScrollRect.vertical = false;
                RectTransform scrollRectTransform = rewardScrollRect.GetComponent<RectTransform>();
                if (scrollRectTransform != null)
                {
                    scrollRectTransform.sizeDelta = new Vector2(490f, 185f);
                    scrollRectTransform.anchoredPosition = new Vector2(0f, -4f);
                }

                if (rewardScrollRect.verticalScrollbar != null)
                {
                    rewardScrollRect.verticalScrollbar.gameObject.SetActive(false);
                }
            }
        }

        // Summary Text 자동 바인딩 / 생성
        if (rewardSummaryText == null && rewardPanel != null)
        {
            Transform found = rewardPanel.transform.Find("RewardSummaryText");
            if (found == null)
            {
                found = rewardPanel.transform.Find("Image/GameObject/RewardSummaryText");
            }
            if (found != null)
            {
                rewardSummaryText = found.GetComponent<TMPro.TextMeshProUGUI>();
            }

            if (rewardSummaryText == null)
            {
                Transform titleTextObj = rewardPanel.transform.Find("Image/GameObject/Text (TMP)");
                Transform parentTarget = titleTextObj != null ? titleTextObj.parent : rewardPanel.transform;

                GameObject summaryObj = new GameObject("RewardSummaryText");
                summaryObj.transform.SetParent(parentTarget, false);
                rewardSummaryText = summaryObj.AddComponent<TMPro.TextMeshProUGUI>();

                if (titleTextObj != null)
                {
                    TMPro.TextMeshProUGUI titleTmp = titleTextObj.GetComponent<TMPro.TextMeshProUGUI>();
                    if (titleTmp != null)
                    {
                        rewardSummaryText.font = titleTmp.font;
                    }
                }

                rewardSummaryText.alignment = TMPro.TextAlignmentOptions.Center;
                rewardSummaryText.fontSize = 11f;
                rewardSummaryText.textWrappingMode = TMPro.TextWrappingModes.Normal;

                RectTransform summaryRect = rewardSummaryText.GetComponent<RectTransform>();
                if (summaryRect != null)
                {
                    summaryRect.anchorMin = new Vector2(0.5f, 0.5f);
                    summaryRect.anchorMax = new Vector2(0.5f, 0.5f);
                    summaryRect.pivot = new Vector2(0.5f, 0.5f);
                    summaryRect.anchoredPosition = new Vector2(0f, 15f);
                    summaryRect.sizeDelta = new Vector2(280f, 110f);
                }
            }
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

    private void ShowSettlementPhase(GameManager.BattleRewardResult result)
    {
        isSettlementPhase = true;

        if (rewardPanelRect != null)
        {
            rewardPanelRect.DOKill();
            rewardPanelRect.sizeDelta = new Vector2(320f, 240f);
            rewardPanelRect.anchoredPosition = Vector2.zero;
        }

        if (titleText != null)
        {
            titleText.text = "전투 정산";
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            if (titleRect != null)
            {
                titleRect.anchoredPosition = new Vector2(0f, 92f);
            }
        }

        if (rewardSummaryText != null)
        {
            rewardSummaryText.gameObject.SetActive(true);
            UpdateRewardSummaryUI(result);
            RectTransform summaryRect = rewardSummaryText.GetComponent<RectTransform>();
            if (summaryRect != null)
            {
                summaryRect.anchoredPosition = new Vector2(0f, 12f);
                summaryRect.sizeDelta = new Vector2(280f, 115f);
            }
        }

        // 선택 보상 카드 및 리롤 버튼 숨기기
        if (rewardScrollRect != null)
        {
            rewardScrollRect.gameObject.SetActive(false);
        }

        if (rerollButton != null)
        {
            rerollButton.gameObject.SetActive(false);
        }

        // "보상 받기" 버튼 활성화
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(true);
            closeButton.interactable = true;
            if (closeButtonText != null)
            {
                closeButtonText.text = "보상 받기";
            }
            RectTransform btnRect = closeButton.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                btnRect.anchoredPosition = new Vector2(0f, -85f);
            }

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClaimSettlementRewardClicked);
        }
    }

    private void OnClaimSettlementRewardClicked()
    {
        if (closeButton != null)
        {
            closeButton.interactable = false;
        }

        Vector3 startPos = closeButton != null
            ? closeButton.transform.position
            : (rewardPanel != null ? rewardPanel.transform.position : transform.position);

        // 골드 날아가는 연출 실행 후 선택 보상 창으로 전환
        PlayCoinFlyAnimation(currentBattleReward.TotalReward, startPos, () =>
        {
            ShowChoicePhase(3, currentRarity);
        });
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
            rerollButton.gameObject.SetActive(true);
            RectTransform rerollRect = rerollButton.GetComponent<RectTransform>();
            if (rerollRect != null)
            {
                rerollRect.anchoredPosition = new Vector2(215f, 102f);
            }
            UpdateRerollUI();
        }

        // 닫기 / 선택 완료 버튼 설정
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(true);
            closeButton.interactable = true;
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
            closeButton.onClick.AddListener(OnRewardClaimed);
        }

        GenerateRewards(count, rarity);
    }

    private void PlayCoinFlyAnimation(int amount, Vector3 startPos, System.Action onComplete)
    {
        if (amount <= 0 || GameManager.Instance == null || GameManager.Instance.coinText == null)
        {
            if (amount > 0 && GameManager.Instance != null)
            {
                GameManager.Instance.AddCoin(amount);
            }
            onComplete?.Invoke();
            return;
        }

        GameObject flyObj = new GameObject("FlyingCoin");
        Canvas rootCanvas = rewardPanel != null ? rewardPanel.GetComponentInParent<Canvas>() : null;
        if (rootCanvas != null)
        {
            flyObj.transform.SetParent(rootCanvas.transform, true);
        }
        else
        {
            flyObj.transform.SetParent(rewardPanel != null ? rewardPanel.transform.root : transform.root, true);
        }

        flyObj.transform.position = startPos;
        flyObj.transform.localScale = Vector3.one;

        TMPro.TextMeshProUGUI flyText = flyObj.AddComponent<TMPro.TextMeshProUGUI>();
        flyText.raycastTarget = false;
        flyText.text = $"+{amount}";
        flyText.fontSize = 34;
        flyText.color = Color.yellow;
        flyText.alignment = TMPro.TextAlignmentOptions.Center;
        flyText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        if (GameManager.Instance.coinText != null)
        {
            flyText.font = GameManager.Instance.coinText.font;
        }
        if (flyText.font == null)
        {
            flyText.font = TMPro.TMP_Settings.defaultFontAsset;
        }

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(flyObj.transform.DOScale(1.25f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(GameManager.Instance.coinText.transform.position, 0.75f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0.5f, 0.75f).SetDelay(0.15f));

        seq.OnComplete(() =>
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoin(amount);
            }
            Destroy(flyObj);
            onComplete?.Invoke();
        });
    }

    private void UpdateRewardSummaryUI(GameManager.BattleRewardResult result)
    {
        if (rewardSummaryText == null) return;

        string summary = 
            $"기본 보상 +{result.BaseReward}\n" +
            $"적 기물 포획 +{result.EnemyCaptureReward}\n" +
            $"내 기물 포획당함 +{result.PlayerLossReward}\n" +
            $"__________________\n" +
            $"<color=#FFD700><b>총 보상 +{result.TotalReward}G</b></color>";

        rewardSummaryText.text = summary;
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
        bool coinAlreadyGenerated = false;

        // 각 카드 슬롯마다 가중치 기반으로 완전 랜덤 추첨 (골드는 최대 1개)
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(rewardButtonPrefab, rewardContainer);
            if (obj == null)
            {
                continue;
            }

            RewardButton btn = obj.GetComponent<RewardButton>();
            if (btn != null)
            {
                RewardButton.RewardType randomType = GetRandomRewardType(!coinAlreadyGenerated);
                if (randomType == RewardButton.RewardType.Coin)
                {
                    coinAlreadyGenerated = true;
                }
                btn.InitializeWithType(this, rarity, randomType);
                activeRewardCount++;
            }

            // 카드 등장 애니메이션: 순차적으로 팝업
            obj.transform.localScale = Vector3.zero;
            obj.transform.DOScale(Vector3.one, 0.35f)
                .SetEase(Ease.OutBack)
                .SetDelay(i * 0.08f + 0.1f)
                .SetUpdate(true);
        }
    }

    public void OnRewardButtonClicked()
    {
        // 3개 중 하나만 선택: 선택 즉시 나머지 버튼 비활성화 및 완료 처리
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

        activeRewardCount = 0;
        OnRewardClaimed();
    }

    public void OnRewardClaimed()
    {
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

    private void SetRewardPanelHeight(float height)
    {
        if (rewardPanelRect == null)
        {
            return;
        }

        Vector2 size = rewardPanelRect.sizeDelta;
        size.y = height;
        rewardPanelRect.sizeDelta = size;
    }

    private Tween CreateRewardPanelHeightTween(float targetHeight)
    {
        if (rewardPanelRect == null)
        {
            return DOVirtual.DelayedCall(0f, () => { });
        }

        return DOTween.To(
            () => rewardPanelRect.sizeDelta.y,
            value => SetRewardPanelHeight(value),
            targetHeight,
            rewardAnimDuration
        );
    }
}
