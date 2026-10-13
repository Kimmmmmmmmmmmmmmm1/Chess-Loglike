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

    [Header("Treasure Bonus")]
    [Tooltip("보물상자 시 유물 가중치 배율")]
    public float treasureArtifactMultiplier = 2.5f;

    private int currentRarity = 0;
    private bool currentIsTreasure = false;

    /// <summary>
    /// 가중치 비율에 따라 완전 랜덤으로 보상 타입을 추첨합니다.
    /// 인스펙터의 pieceWeight, sealWeight, artifactWeight, coinWeight 설정에 의해 결정됩니다.
    /// </summary>
    public RewardButton.RewardType GetRandomRewardType(bool isTreasure = false, bool allowCoin = true)
    {
        float pWeight = Mathf.Max(0f, pieceWeight);
        float sWeight = Mathf.Max(0f, sealWeight);
        float aWeight = Mathf.Max(0f, artifactWeight) * (isTreasure ? treasureArtifactMultiplier : 1f);
        float cWeight = allowCoin ? Mathf.Max(0f, coinWeight) : 0f;

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
                rewardPanelRect.sizeDelta = new Vector2(520f, 260f);
                rewardPanelFinalAnchoredPos = Vector2.zero;
                rewardExpandedHeight = 260f;
            }
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

            horizontal.spacing = 10f;
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
            ScrollRect scrollRect = rewardContainer.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = false;
                RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
                if (scrollRectTransform != null)
                {
                    scrollRectTransform.sizeDelta = new Vector2(500f, 64f);
                    scrollRectTransform.anchoredPosition = new Vector2(0f, -35f);
                }

                if (scrollRect.verticalScrollbar != null)
                {
                    scrollRect.verticalScrollbar.gameObject.SetActive(false);
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
                    summaryRect.anchoredPosition = new Vector2(0f, 45f);
                    summaryRect.sizeDelta = new Vector2(480f, 80f);
                }
            }
        }

        if (rewardSummaryText != null)
        {
            RectTransform summaryRect = rewardSummaryText.GetComponent<RectTransform>();
            if (summaryRect != null)
            {
                summaryRect.anchoredPosition = new Vector2(0f, 45f);
                summaryRect.sizeDelta = new Vector2(480f, 80f);
            }
        }

        if (rewardPanel != null)
        {
            Transform titleTextObj = rewardPanel.transform.Find("Image/GameObject/Text (TMP)");
            if (titleTextObj != null)
            {
                RectTransform titleRect = titleTextObj.GetComponent<RectTransform>();
                if (titleRect != null)
                {
                    titleRect.anchoredPosition = new Vector2(0f, 102f);
                }
            }

            if (closeButton != null)
            {
                RectTransform btnRect = closeButton.GetComponent<RectTransform>();
                if (btnRect != null)
                {
                    btnRect.anchoredPosition = new Vector2(0f, -95f);
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
            GenerateRewards(3, currentRarity, currentIsTreasure);
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
            int difficulty = 1;
            if (GameManager.Instance != null)
            {
                difficulty = GameManager.Instance.ClearedStage + 1;
            }

            int rarity = difficulty / 2;
            ShowRewards(3, rarity, false);
        }
    }

    public void ShowRewards(int count, int rarity, bool isTreasure = false)
    {
        currentRarity = rarity;
        currentIsTreasure = isTreasure;
        UpdateRerollUI();
        SetupHorizontalRewardLayout();

        // 게임(전투) 종료시 확정 보상 정산 및 지급
        if (!isTreasure && GameManager.Instance != null)
        {
            var rewardResult = GameManager.Instance.CalculateBattleReward(pieceData);
            GameManager.Instance.AddCoin(rewardResult.TotalReward);
            UpdateRewardSummaryUI(rewardResult);
        }
        else if (rewardSummaryText != null)
        {
            if (isTreasure)
            {
                rewardSummaryText.text = "<color=#FFD700><b>보물상자 보상을 선택하세요!</b></color>";
            }
            else
            {
                rewardSummaryText.text = string.Empty;
            }
        }

        if (rewardPanel != null)
        {
            rewardPanel.SetActive(true);

            // UI가 다른 요소 뒤에 가려지지 않도록 맨 앞으로 가져오기
            rewardPanel.transform.SetAsLastSibling();

            // 위치 초기화
            RectTransform rect = rewardPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = rewardPanelFinalAnchoredPos;
            }

            // 투명도 초기화 (CanvasGroup이 있다면)
            CanvasGroup cg = rewardPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;
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
        }

        GenerateRewards(3, rarity, isTreasure);
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

    private void GenerateRewards(int count, int rarity, bool isTreasure)
    {
        if (rewardContainer == null) return;
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
                RewardButton.RewardType randomType = GetRandomRewardType(isTreasure, !coinAlreadyGenerated);
                if (randomType == RewardButton.RewardType.Coin)
                {
                    coinAlreadyGenerated = true;
                }
                btn.InitializeWithType(this, rarity, randomType, isTreasure);
                activeRewardCount++;
            }

            // 버튼 등장 애니메이션: 순차적으로 팝업
            obj.transform.localScale = Vector3.zero;
            obj.transform.DOScale(Vector3.one, 0.4f)
                .SetEase(Ease.OutBack)
                .SetDelay(i * 0.1f + 0.2f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (btn != null && btn.AttachedSeal != null)
                    {
                        btn.PlaySealEffect(btn.AttachedSeal.rarity);
                    }
                });
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
                // 전투 -> 보상 -> 상점 -> 다음 전투 순서로 전환
                GameManager.Instance.ChangeFlowState(GameFlowState.Shop);
            }
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeFlowState(GameFlowState.Shop);
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
