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
                SetRewardPanelHeight(rewardCollapsedHeight);
            }
            rewardPanel.SetActive(false);
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnRewardClaimed);
        }

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

            // 보상 개수: 스테이지/3 (최소 1개, 최대 maxRewardCount개)
            int count = Mathf.Clamp(difficulty / 3, 1, maxRewardCount);
            // 보상 등급(희귀도): 스테이지/2 (스테이지가 높을수록 좋은 보상 확률 증가)
            int rarity = difficulty / 2;
            ShowRewards(count, rarity, false);
        }
    }

    public void ShowRewards(int count, int rarity, bool isTreasure = false)
    {
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

            // 패널 등장 애니메이션: 세로 높이 축소 -> 원래 높이로 확장
            if (rewardPanelRect != null)
            {
                rewardPanelRect.DOKill();
                SetRewardPanelHeight(rewardCollapsedHeight);
                // start from slightly above, move down into final position, then expand height
                rewardPanelRect.anchoredPosition = rewardPanelFinalAnchoredPos + new Vector2(0f, panelMoveOffset);
                rewardPanelRect.DOAnchorPos(rewardPanelFinalAnchoredPos, panelMoveDuration).SetEase(panelMoveEaseIn).SetUpdate(true).OnComplete(() =>
                {
                    CreateRewardPanelHeightTween(rewardExpandedHeight).SetEase(panelHeightEase).SetUpdate(true);
                });
            }
        }

        GenerateRewards(count, rarity, isTreasure);
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

        // 지정된 개수의 보상 선택지 생성
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
                btn.Initialize(this, rarity, isTreasure);
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
        activeRewardCount--;
        if (activeRewardCount <= 0)
        {
            OnRewardClaimed();
        }
    }

    public void OnRewardClaimed()
    {
        if (rewardPanel != null)
        {
            // 패널 퇴장 애니메이션: 가로 폭 원래 폭 -> 축소 폭
            if (rewardPanelRect != null)
            {
                rewardPanelRect.DOKill();
                // collapse height then move up and hide
                CreateRewardPanelHeightTween(rewardCollapsedHeight).SetEase(panelHeightEase).SetUpdate(true).OnComplete(() =>
                {
                    rewardPanelRect.DOAnchorPos(rewardPanelFinalAnchoredPos + new Vector2(0f, panelMoveOffset), panelMoveDuration).SetEase(panelMoveEaseOut).SetUpdate(true).OnComplete(() =>
                    {
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
                            GameManager.Instance.ChangeFlowState(GameFlowState.Map);
                            GameManager.Instance.BossJustCleared = false;
                        }
                    });
                });
            }
            else
            {
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
                    GameManager.Instance.ChangeFlowState(GameFlowState.Map);
                    GameManager.Instance.BossJustCleared = false;
                }
            }
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeFlowState(GameFlowState.Map);
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
