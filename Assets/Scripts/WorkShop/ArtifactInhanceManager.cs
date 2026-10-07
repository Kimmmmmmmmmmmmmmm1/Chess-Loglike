using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 유물 강화 매니저: 두 가지 모드 지원
/// 1. 자동 강화 모드: targetArtifactId 지정 시 자동 강화 1회
/// 2. 슬롯 강화 모드: 유물 선택 슬롯에 클릭으로 유물 선택 후 강화 버튼으로 강화
/// </summary>
public class ArtifactInhanceManager : MonoBehaviour
{
    public static ArtifactInhanceManager Instance { get; private set; }

    [Header("UI")]
    public GameObject inhancePanel;         // 유물 강화 패널
    public Transform enhanceSlot;           // 강화할 유물 슬롯 (Image 포함)
    public Image selectedArtifactImage;     // 선택된 유물 아이콘
    public TextMeshProUGUI selectedArtifactName; // 선택된 유물 이름
    public TextMeshProUGUI selectedArtifactLevel; // 선택된 유물 레벨
    public TextMeshProUGUI enhanceCostText; // 강화 비용 텍스트
    public Button enhanceButton;            // 강화 버튼
    public Button completeButton;           // 취소 버튼
    public TextMeshProUGUI titleText;       // "유물 강화" 타이틀
    public TextMeshProUGUI descriptionText; // 설명 텍스트

    [Header("Enhance Settings")]
    [SerializeField] private string targetArtifactId = ""; // 비워두면 슬롯 모드, 있으면 자동 강화 모드
    [SerializeField] private int enhanceCostPerLevel = 50; // 레벨당 강화 비용

    private ArtifactData selectedArtifact;  // 슬롯 모드에서 선택된 유물

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
        if (inhancePanel != null)
        {
            inhancePanel.SetActive(false);
        }

        if (enhanceButton != null)
        {
            enhanceButton.onClick.AddListener(OnEnhanceClick);
        }

        if (completeButton != null)
        {
            completeButton.onClick.AddListener(OnCancelClick);
        }

        SetCancelButtonLabel();

        UpdateSlotUI();
    }

    /// <summary>
    /// 유물 강화 패널을 엽니다.
    /// </summary>
    public void OpenInhancePanel()
    {
        if (inhancePanel != null)
        {
            inhancePanel.SetActive(true);
        }

        selectedArtifact = null;
        UpdateSlotUI();

        if (descriptionText != null)
        {
            if (string.IsNullOrEmpty(targetArtifactId))
            {
                descriptionText.text = "유물 인벤토리에서 유물을 클릭해서\n" +
                    "강화할 유물을 선택하세요.\n" +
                    "골드를 소모하고 선택된 유물을 강화합니다.";
            }
            else
            {
                descriptionText.text = "선택된 유물을 자동 강화합니다.\n" +
                    "강화 버튼을 누르세요.";
            }
        }

    }

    /// <summary>
    /// 유물 강화 패널을 닫습니다.
    /// </summary>
    public void CloseInhancePanel()
    {
        if (inhancePanel != null)
        {
            inhancePanel.SetActive(false);
        }

        selectedArtifact = null;
    }

    /// <summary>
    /// 슬롯 모드: 유물을 선택합니다 (인벤토리 유물 클릭 시 호출)
    /// </summary>
    public void SetSelectedArtifact(ArtifactData artifact)
    {
        if (artifact == null)
        {
            return;
        }

        // 슬롯 모드가 아니면 동작 안 함
        if (!string.IsNullOrEmpty(targetArtifactId))
        {
            return;
        }

        selectedArtifact = artifact;
        UpdateSlotUI();
    }

    /// <summary>
    /// 슬롯 UI 업데이트
    /// </summary>
    private void UpdateSlotUI()
    {
        if (selectedArtifact == null)
        {
            if (selectedArtifactImage != null)
            {
                selectedArtifactImage.sprite = null;
                selectedArtifactImage.color = new Color(1, 1, 1, 0.3f);
            }

            if (selectedArtifactName != null)
            {
                selectedArtifactName.text = "선택 안 됨";
            }

            if (selectedArtifactLevel != null)
            {
                selectedArtifactLevel.text = "";
            }

            if (enhanceCostText != null)
            {
                enhanceCostText.text = "-";
            }

            if (enhanceButton != null)
            {
                enhanceButton.interactable = false;
            }
        }
        else
        {
            if (selectedArtifactImage != null)
            {
                selectedArtifactImage.sprite = selectedArtifact.icon;
                selectedArtifactImage.color = Color.white;
            }

            if (selectedArtifactName != null)
            {
                selectedArtifactName.text = selectedArtifact.artifactName;
            }

            if (selectedArtifactLevel != null)
            {
                selectedArtifactLevel.text = $"Lv.{selectedArtifact.Level}/{selectedArtifact.MaxLevel}";
            }

            int cost = selectedArtifact.CanEnhance ? enhanceCostPerLevel * selectedArtifact.Level : 0;
            if (enhanceCostText != null)
            {
                enhanceCostText.text = selectedArtifact.CanEnhance ? $"{cost} ₩" : "최대 레벨";
            }

            // 강화 가능 여부 체크: 최대 레벨이 아니고 골드가 충분해야 함
            if (enhanceButton != null)
            {
                bool canEnhance = selectedArtifact.CanEnhance && 
                    (GameManager.Instance != null && GameManager.Instance.Coin >= cost);
                enhanceButton.interactable = canEnhance;
            }
        }
    }

    /// <summary>
    /// 강화 버튼 클릭
    /// </summary>
    private void OnEnhanceClick()
    {
        if (string.IsNullOrEmpty(targetArtifactId))
        {
            // 슬롯 모드
            if (selectedArtifact == null)
            {
                return;
            }

            PerformEnhance(selectedArtifact.id);
        }
        else
        {
            // 자동 강화 모드
            PerformEnhance(targetArtifactId);
        }
    }

    private void PerformEnhance(string artifactId)
    {
        if (ArtifactManager.Instance == null)
        {
            return;
        }

        ArtifactData artifact = ArtifactManager.Instance.GetOwnedArtifact(artifactId);
        if (artifact == null)
        {
            return;
        }

        // 강화 비용 계산 및 검증
        int cost = enhanceCostPerLevel * artifact.Level;
        if (!artifact.CanEnhance)
        {
            return;
        }

        if (GameManager.Instance == null || GameManager.Instance.Coin < cost)
        {
            return;
        }

        // 골드 소모 및 강화
        if (GameManager.Instance.UseCoin(cost))
        {
            if (ArtifactManager.Instance.TryEnhanceArtifact(artifactId, out ArtifactData enhancedArtifact, out int newLevel))
            {
                ArtifactManager.Instance.UpdateEnhanceUI();
            }
        }

        // UI 업데이트
        UpdateSlotUI();
    }

    private void SetCancelButtonLabel()
    {
        if (completeButton == null)
        {
            return;
        }

        TextMeshProUGUI buttonLabel = completeButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonLabel != null)
        {
            buttonLabel.text = "취소";
            return;
        }

        Text legacyText = completeButton.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            legacyText.text = "취소";
        }
    }

    private void OnCancelClick()
    {
        CloseInhancePanel();

        if (WorkShopManager.Instance != null)
        {
            WorkShopManager.Instance.OnWorkShopComplete();
        }
    }
}
