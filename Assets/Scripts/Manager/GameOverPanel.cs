using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

public class GameOverPanel : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public Transform panelContainer;
    
    [Header("Stats Text")]
    public TextMeshProUGUI clearedStageText;
    public TextMeshProUGUI enemyCapturedText;
    public TextMeshProUGUI playerLostText;

    [Header("Buttons")]
    public Button restartButton;
    public Button quitButton;

    [Header("Animation Settings")]
    public float fadeDuration = 0.5f;
    public float popupDuration = 0.6f;
    [SerializeField] private float slideDuration = 0.4f;
    [SerializeField] private float collapsedHorizontalScale = 0.08f;
    [SerializeField] private Ease slideEase = Ease.OutQuad;
    [SerializeField] private Ease unfoldEase = Ease.OutQuad;

    private RectTransform panelRectTransform;
    private Vector2 openAnchoredPosition;
    private Vector2 openSizeDelta;
    private Vector3 openScale = Vector3.one;
    private bool hasCachedPanelTransform;
    private Sequence panelSequence;

    private void Start()
    {
        CachePanelTransform();

        // 초기화: 패널 숨기기
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        
        if (panelContainer != null)
        {
            panelContainer.localScale = openScale;
            SetPanelWidth(openSizeDelta.x);
        }

        // 버튼 리스너 연결
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
            var restartButtonText = restartButton.GetComponentInChildren<TextMeshProUGUI>();
            if (restartButtonText != null)
            {
                restartButtonText.text = "메인으로";
            }
        }
        
        if (quitButton != null)
        {
            quitButton.gameObject.SetActive(false);
        }

        // 게임 상태 변경 이벤트 구독
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.GameOver)
            {
                ShowPanel();
            }
        }
    }

    private void OnDestroy()
    {
        panelSequence?.Kill();
        panelSequence = null;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.GameOver)
        {
            ShowPanel();
        }
    }

    private void ShowPanel()
    {
        CachePanelTransform();
        UpdateStatsUI();

        panelSequence?.Kill();
        panelSequence = null;
        canvasGroup?.DOKill();
        panelContainer?.DOKill();
        panelRectTransform?.DOKill();

        // 배경 페이드 인
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // TimeScale이 0이어도 동작하도록 설정
        }

        if (panelContainer != null)
        {
            panelContainer.localScale = openScale;
            SetPanelWidth(GetCollapsedPanelWidth());

            panelSequence = DOTween.Sequence().SetUpdate(true);

            if (panelRectTransform != null)
            {
                float startOffsetY = GetPanelStartOffsetY();
                panelRectTransform.anchoredPosition = openAnchoredPosition + Vector2.up * startOffsetY;

                panelSequence.Append(
                    panelRectTransform
                        .DOAnchorPos(openAnchoredPosition, slideDuration)
                        .SetEase(slideEase)
                );
            }

            panelSequence.Append(
                DOTween.To(
                    () => panelRectTransform != null ? panelRectTransform.sizeDelta.x : openSizeDelta.x,
                    SetPanelWidth,
                    openSizeDelta.x,
                    popupDuration
                )
                    .SetEase(unfoldEase)
            );

            panelSequence.OnComplete(() =>
            {
                panelSequence = null;
            });
        }
    }

    private void CachePanelTransform()
    {
        if (panelContainer == null)
        {
            return;
        }

        if (panelRectTransform == null)
        {
            panelRectTransform = panelContainer as RectTransform;
        }

        if (!hasCachedPanelTransform)
        {
            if (panelRectTransform != null)
            {
                openAnchoredPosition = panelRectTransform.anchoredPosition;
                openSizeDelta = panelRectTransform.sizeDelta;
            }

            if (panelContainer.localScale != Vector3.zero)
            {
                openScale = panelContainer.localScale;
            }

            hasCachedPanelTransform = true;
        }
    }

    private float GetPanelStartOffsetY()
    {
        if (panelRectTransform == null)
        {
            return 0f;
        }

        float height = panelRectTransform.rect.height;
        return height > 0f ? height : 240f;
    }

    private float GetCollapsedPanelWidth()
    {
        float openWidth = openSizeDelta.x > 0f ? openSizeDelta.x : 360f;
        return Mathf.Max(1f, openWidth * collapsedHorizontalScale);
    }

    private void SetPanelWidth(float width)
    {
        if (panelRectTransform == null)
        {
            return;
        }

        Vector2 size = panelRectTransform.sizeDelta;
        size.x = width;
        panelRectTransform.sizeDelta = size;
    }

    private void UpdateStatsUI()
    {
        if (GameManager.Instance == null) return;

        if (clearedStageText != null)
            clearedStageText.text = $"클리어한 스테이지: {GameManager.Instance.ClearedStage}";
        
        if (enemyCapturedText != null)
            enemyCapturedText.text = $"잡은 적 기물: {GameManager.Instance.EnemyPiecesCaptured}";
        
        if (playerLostText != null)
            playerLostText.text = $"잃은 내 기물: {GameManager.Instance.PlayerPiecesCaptured}";
    }

    private void OnRestartClicked()
    {
        SceneManager.LoadScene(SceneName.TitleScene.ToString());
    }
}
