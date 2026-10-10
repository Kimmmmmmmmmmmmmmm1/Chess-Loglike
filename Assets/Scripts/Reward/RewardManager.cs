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
    public RewardButton.RewardType GetRandomRewardType(bool isTreasure = false)
    {
        float pWeight = Mathf.Max(0f, pieceWeight);
        float sWeight = Mathf.Max(0f, sealWeight);
        float aWeight = Mathf.Max(0f, artifactWeight) * (isTreasure ? treasureArtifactMultiplier : 1f);
        float cWeight = Mathf.Max(0f, coinWeight);

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

        if (rewardPanel != null)
        {
            rewardPanelRect = rewardPanel.GetComponent<RectTransform>();
            if (rewardPanelRect != null)
            {
                rewardExpandedHeight = rewardPanelRect.sizeDelta.y;
            }
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

        if (rewardPanel != null)
        {
            rewardPanel.SetActive(true);

            // UI가 다른 요소 뒤에 가려지지 않도록 맨 앞으로 가져오기
            rewardPanel.transform.SetAsLastSibling();

            // 위치 초기화 (혹시 화면 밖으로 나갔을 경우 대비)
            RectTransform rect = rewardPanel.GetComponent<RectTransform>();
            rewardPanelFinalAnchoredPos = new Vector2(0f, 120f);
            if (rect != null) rect.anchoredPosition = rewardPanelFinalAnchoredPos;

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

        // 각 카드 슬롯마다 가중치 기반으로 완전 랜덤 추첨
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
                RewardButton.RewardType randomType = GetRandomRewardType(isTreasure);
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
