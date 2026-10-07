using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// 작업장 매니저: ArtifactInhance(유물 강화)와 PieceSynthesis(기물 합성) 중 하나를 선택할 수 있는 패널을 관리합니다.
/// 둘 중 하나를 선택하면 해당 매니저를 활성화하고, 완료 후 다음 노드로 진행합니다.
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
    public float panelOpenX = 160f;           // 패널이 열렸을 때의 X 위치
    public float panelOpenY = 0f;             // 패널이 열렸을 때의 Y 위치 (세로)

    [Header("Reference")]
    public ArtifactInhanceManager artifactInhanceManager;
    public PiecesSynthManager piecesSynthManager;

    private bool isOpen = false;
    private bool hasSelectedOption = false; // 이미 옵션을 선택했는지 여부

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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

        // 매니저 자동 찾기
        if (artifactInhanceManager == null)
        {
            artifactInhanceManager = FindFirstObjectByType<ArtifactInhanceManager>();
        }

        if (piecesSynthManager == null)
        {
            piecesSynthManager = FindFirstObjectByType<PiecesSynthManager>();
        }
    }

    /// <summary>
    /// 작업장 패널을 엽니다.
    /// </summary>
    public void Open()
    {
        if (isOpen || workShopPanel == null) return;

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

        // 버튼 활성화
        if (artifactInhanceButton != null)
        {
            artifactInhanceButton.interactable = true;
        }
        if (pieceSynthesisButton != null)
        {
            pieceSynthesisButton.interactable = true;
        }

        // 패널 위치 초기화 (화면 위쪽 밖)
        if (workShopPanelRect != null)
        {
            workShopPanelRect.anchoredPosition = new Vector2(workShopPanelRect.anchoredPosition.x, workShopPanelRect.rect.height);
            // 패널 애니메이션 (위쪽에서 아래로)
            workShopPanelRect.DOAnchorPosY(panelOpenY, animDuration).SetEase(openEase);
        }

        // 장기판 이동 연출 제거: 패널이 그리드 위로 겹치도록 변경

    }

    /// <summary>
    /// 작업장 패널을 닫습니다.
    /// </summary>
    public void Close()
    {
        if (!isOpen || workShopPanel == null) return;

        isOpen = false;

        // 패널 애니메이션 (화면 위쪽 밖으로)
        if (workShopPanelRect != null)
        {
            workShopPanelRect.DOAnchorPosY(workShopPanelRect.rect.height, animDuration)
                .SetEase(closeEase)
                .OnComplete(() =>
                {
                    if (workShopPanel != null) workShopPanel.SetActive(false);
                });
        }
        else if (workShopPanel != null)
        {
            workShopPanel.SetActive(false);
        }

        // 장기판 원위치 복귀 연출 제거: 패널이 겹치므로 별도 복귀 동작 불필요

    }

    /// <summary>
    /// 유물 강화 버튼 클릭
    /// </summary>
    private void OnArtifactInhanceClick()
    {
        if (hasSelectedOption) return;

        hasSelectedOption = true;
        // 버튼 비활성화
        if (artifactInhanceButton != null)
        {
            artifactInhanceButton.interactable = false;
        }
        if (pieceSynthesisButton != null)
        {
            pieceSynthesisButton.interactable = false;
        }

        // 유물 강화 매니저 열기
        if (artifactInhanceManager != null)
        {
            artifactInhanceManager.OpenInhancePanel();
        }
        else
        {
            // 매니저가 없으면 바로 종료
            OnWorkShopComplete();
        }
    }

    /// <summary>
    /// 기물 합성 버튼 클릭
    /// </summary>
    private void OnPieceSynthesisClick()
    {
        if (hasSelectedOption) return;

        hasSelectedOption = true;
        // 버튼 비활성화
        if (artifactInhanceButton != null)
        {
            artifactInhanceButton.interactable = false;
        }
        if (pieceSynthesisButton != null)
        {
            pieceSynthesisButton.interactable = false;
        }

        // 기물 합성 매니저 열기
        if (piecesSynthManager != null)
        {
            piecesSynthManager.OpenSynthesisPanel();
        }
        else
        {
            // 매니저가 없으면 바로 종료
            OnWorkShopComplete();
        }
    }

    /// <summary>
    /// 작업장 작업 완료 (유물 강화 또는 기물 합성 완료 시 호출)
    /// </summary>
    public void OnWorkShopComplete()
    {
        // 맵으로 돌아가기
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeFlowState(GameFlowState.Map);
        }
    }
}
