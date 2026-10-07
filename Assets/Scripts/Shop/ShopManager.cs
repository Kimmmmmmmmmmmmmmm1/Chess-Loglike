using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("UI")]
    public GameObject shopPanel; // 인스펙터에서 상점 UI 패널을 연결하세요
    public Button confirmButton;
    public Button rerollButton;
    public TextMeshProUGUI rerollCostText;
    public Button pieceLimitUpgradeButton;
    public TextMeshProUGUI pieceLimitUpgradeCostText;
    public RectTransform inventoryContainer; // 인벤토리 컨테이너 참조
    private RectTransform shopPanelRect;
    private int inventoryOriginalSiblingIndex = -1; // 인벤토리 원래 순서 저장

    [Header("Shop Generation")]
    public ShopPieceData shopPieceData;
    public List<SealData> allSeals; // 등장 가능한 모든 인장 목록
    public float sealDropChance = 15f; // 인장 등장 확률 (기본 15%)
    public float artifactSealChanceBonus = 10f; // 유물(A003) 보유 시 추가 확률
    public GameObject shopSlotPrefab;
    public Transform shopItemsContainer;
    public int itemsToDisplay = 2;
    public int rerollCost = 5;
    public int pieceLimitUpgradeCost = 200;
    public int pieceLimitUpgradeAmount = 1;
    public int pieceLimitUpgradeCostIncrease = 100;

    [Header("Artifact Shop")]
    public ShopArtifactSlot shopArtifactSlot; // 씬에 배치된 유물 슬롯

    [Header("Merchant")]
    [SerializeField] private GameObject merchantPrefab;

    [Header("Animation")]
    public float animDuration = 0.5f;
    public Ease openEase = Ease.OutBack;
    public Ease closeEase = Ease.InQuad;
    public float shopCollapsedWidth = 72f;
    public float shopExpandedWidth = 512f;

    private Tween closeDelayTween;
    private string lastRestoredInventoryScene = null;
    private GameObject merchantObject;
    private Button merchantButton;
    private static readonly Vector2 merchantAnchoredPosition = new Vector2(-80f, 0f);
    private const float merchantEnterStartX = 120f;
    private const float merchantEnterDuration = 0.45f;
    private const Ease merchantEnterEase = Ease.OutBack;
    private const float merchantExitTargetX = 120f;
    private const float merchantExitDuration = 0.35f;
    private const Ease merchantExitEase = Ease.InBack;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Instance.gameObject.scene != gameObject.scene)
            {
                Destroy(Instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        Instance = this;

        if (shopPanel != null)
        {
            shopPanelRect = shopPanel.GetComponent<RectTransform>();
        }
    }

    private void SetShopPanelWidth(float width)
    {
        if (shopPanelRect == null)
        {
            return;
        }

        Vector2 size = shopPanelRect.sizeDelta;
        size.x = width;
        shopPanelRect.sizeDelta = size;
    }

    private Tween CreateShopPanelWidthTween(float targetWidth)
    {
        return DOTween.To(
            () => shopPanelRect.sizeDelta.x,
            value => SetShopPanelWidth(value),
            targetWidth,
            animDuration
        );
    }

    private void Start()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmButtonClick);
        }
        if (rerollButton != null)
        {
            rerollButton.onClick.AddListener(OnRerollButtonClick);
            UpdateRerollCostUI();
        }
        if (pieceLimitUpgradeButton != null)
        {
            pieceLimitUpgradeButton.onClick.AddListener(OnPieceLimitUpgradeClick);
            RecalculateUpgradeCost();
            UpdatePieceLimitUpgradeUI();
        }

        // 자동으로 폴더에서 모든 인장 데이터 로드
        // 인스펙터에 일부만 들어있어도 폴더 기준으로 항상 새로고침
        LoadAllSealsFromFolder();

        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.OnArtifactAdded += OnArtifactAdded;
            ArtifactManager.Instance.OnArtifactEnhanced += OnArtifactEnhanced;
        }

        //TryRestoreInventoryForScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.OnArtifactAdded -= OnArtifactAdded;
            ArtifactManager.Instance.OnArtifactEnhanced -= OnArtifactEnhanced;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        // Scene-owned ShopManager should release the singleton before the next scene instantiates its own copy.
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (shopPanel == null)
        {
            shopPanel = GameObject.Find("ShopPanel");
            if (shopPanel != null)
            {
                shopPanelRect = shopPanel.GetComponent<RectTransform>();
                shopPanel.SetActive(false);
            }
        }

        if (confirmButton == null)
        {
            GameObject btnObj = GameObject.Find("ConfirmButton");
            if (btnObj != null)
            {
                confirmButton = btnObj.GetComponent<Button>();
                confirmButton.onClick.RemoveListener(OnConfirmButtonClick);
                confirmButton.onClick.AddListener(OnConfirmButtonClick);
            }
        }

        if (rerollButton == null)
        {
            GameObject btnObj = GameObject.Find("RerollButton");
            if (btnObj != null)
            {
                rerollButton = btnObj.GetComponent<Button>();
                rerollButton.onClick.RemoveListener(OnRerollButtonClick);
                rerollButton.onClick.AddListener(OnRerollButtonClick);
            }
        }

        if (rerollCostText == null)
        {
            GameObject textObj = GameObject.Find("RerollCostText");
            if (textObj != null)
            {
                rerollCostText = textObj.GetComponent<TextMeshProUGUI>();
                UpdateRerollCostUI();
            }
        }

        if (pieceLimitUpgradeButton == null)
        {
            GameObject btnObj = GameObject.Find("PieceLimitUpgradeButton");
            if (btnObj != null)
            {
                pieceLimitUpgradeButton = btnObj.GetComponent<Button>();
                pieceLimitUpgradeButton.onClick.RemoveListener(OnPieceLimitUpgradeClick);
                pieceLimitUpgradeButton.onClick.AddListener(OnPieceLimitUpgradeClick);
            }
        }

        if (pieceLimitUpgradeCostText == null)
        {
            GameObject textObj = GameObject.Find("PieceLimitUpgradeCostText");
            if (textObj != null) pieceLimitUpgradeCostText = textObj.GetComponent<TextMeshProUGUI>();
            UpdatePieceLimitUpgradeUI();
        }

        if (shopItemsContainer == null)
        {
            GameObject containerObj = GameObject.Find("ShopItemsContainer");
            if (containerObj != null) shopItemsContainer = containerObj.transform;
        }

        if (shopArtifactSlot == null)
        {
            shopArtifactSlot = FindFirstObjectByType<ShopArtifactSlot>();
        }

        //TryRestoreInventoryForScene(scene);
    }

    private void TryRestoreInventoryForScene(Scene scene)
    {
        if (scene.name != SceneName.GameScene.ToString())
        {
            return;
        }

        if (lastRestoredInventoryScene == scene.name)
        {
            return;
        }
        RestoreInventory();
        lastRestoredInventoryScene = scene.name;
    }

    public void ResetInventoryRestoreMarker()
    {
        lastRestoredInventoryScene = null;
    }

    private void OnArtifactAdded(ArtifactData artifact)
    {
        // A001 유물을 획득했고 상점 패널이 켜져있다면 3번째 슬롯 확장 연출 실행
        if (artifact.id == "A001" && shopPanel != null && shopPanel.activeSelf)
        {
            UnlockThirdSlot();
        }

        if (artifact.id == "A002")
        {
            if (shopPanel != null && shopPanel.activeSelf)
            {
                AnimateRerollCostChange();
            }
            else
            {
                UpdateRerollCostUI();
            }
        }

        if (artifact.id == "A003")
        {
            float levelBonus = artifactSealChanceBonus * artifact.Level;
        }
    }

    private void OnArtifactEnhanced(ArtifactData artifact)
    {
        if (artifact == null)
        {
            return;
        }

        if (artifact.id == "A002")
        {
            if (shopPanel != null && shopPanel.activeSelf)
            {
                AnimateRerollCostChange();
            }
            UpdateRerollCostUI();
        }

        if (artifact.id == "A003")
        {
            float levelBonus = artifactSealChanceBonus * artifact.Level;
        }
    }

    private void UnlockThirdSlot()
    {
        if (shopItemsContainer == null) return;

        // ShopPieceSlot 컴포넌트를 가진 자식들만 찾아서 리스트업 (다른 UI 요소가 섞여있을 경우 대비)
        List<ShopPieceSlot> slots = new List<ShopPieceSlot>();
        for (int i = 0; i < shopItemsContainer.childCount; i++)
        {
            var slot = shopItemsContainer.GetChild(i).GetComponent<ShopPieceSlot>();
            if (slot != null) slots.Add(slot);
        }

        if (slots.Count < 3) return;

        // 3번째 슬롯 (리스트 인덱스 2) 타겟팅
        Transform oldSlotTrans = slots[2].transform;
        GameObject oldSlotObj = oldSlotTrans.gameObject;
        int originalSiblingIndex = oldSlotTrans.GetSiblingIndex(); // 원래 계층 순서 저장
        
        var pieceInfo = shopPieceData.GetRandomPiece();
        if (pieceInfo != null)
        {
            SealData seal = GetRandomSeal(pieceInfo.pieceType);
            int finalPrice = CalculatePriceWithSeal(pieceInfo.Price, seal);

            // 연출: 기존 슬롯이 사라지고 새로운 슬롯이 생성됨
            oldSlotTrans.DOKill();
            Sequence seq = DOTween.Sequence();
            
            // 1. 기존 슬롯 축소
            seq.Append(oldSlotTrans.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
            
            // 2. 교체 작업
            seq.AppendCallback(() => 
            {
                // 부모에서 분리하여 레이아웃 및 인덱스 영향 제거
                oldSlotTrans.SetParent(null);
                Destroy(oldSlotObj);

                // 새 슬롯 생성
                GameObject newSlotObj = Instantiate(shopSlotPrefab, shopItemsContainer);
                newSlotObj.transform.SetSiblingIndex(originalSiblingIndex); // 원래 위치 유지
                newSlotObj.transform.localScale = Vector3.zero;

                ShopPieceSlot newSlot = newSlotObj.GetComponent<ShopPieceSlot>();
                if (newSlot != null)
                {
                    newSlot.Setup(pieceInfo.pieceType, finalPrice, seal);
                    
                    // 3. 새 슬롯 확대 등장
                    newSlotObj.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack)
                        .OnComplete(() => 
                        {
                            if (newSlot.AttachedSeal != null)
                            {
                                newSlot.PlaySealEffect(newSlot.AttachedSeal.rarity);
                            }
                        });
                }
            });
        }
    }

    private void AnimateRerollCostChange()
    {
        if (rerollCostText == null) return;

        int newCost = GetCurrentRerollCost();
        Color originalColor = rerollCostText.color;
        Vector3 originalScale = rerollCostText.transform.localScale;

        rerollCostText.transform.DOKill();
        rerollCostText.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Append(rerollCostText.transform.DOScale(originalScale * 1.5f, 0.3f).SetEase(Ease.OutBack));
        seq.Join(rerollCostText.DOColor(Color.green, 0.3f));
        seq.AppendCallback(() => rerollCostText.text = $"{newCost}");
        seq.AppendInterval(0.2f);
        seq.Append(rerollCostText.transform.DOScale(originalScale, 0.3f).SetEase(Ease.InBack));
        seq.Join(rerollCostText.DOColor(originalColor, 0.3f));
    }

    public void EnterShopNode()
    {
        if (shopPanel != null && shopPanel.activeSelf)
        {
            Close();
        }

        EnsureMerchantObject();

        if (merchantObject == null)
        {
            Open();
            return;
        }

        merchantObject.SetActive(true);
        merchantObject.transform.SetAsLastSibling();
        PlayMerchantEnterAnimation();
    }

    public void OpenFromMerchant()
    {
        Open(false);
    }

    public void HideMerchant(bool animated = false)
    {
        if (merchantObject == null) return;

        merchantObject.transform.DOKill();
        RectTransform rect = merchantObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.DOKill();
        }

        if (!animated || !merchantObject.activeSelf)
        {
            merchantObject.SetActive(false);
            return;
        }

        PlayMerchantExitAnimation();
    }

    public void Open(bool hideMerchant = true)
    {
        if (hideMerchant)
        {
            HideMerchant();
        }

        // 닫기 지연 트윈이 실행 중이라면 취소 (보상 -> 상점 전환 시 닫히는 문제 방지)
        if (closeDelayTween != null && closeDelayTween.IsActive())
        {
            closeDelayTween.Kill();
        }

        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            GenerateShopItems();
            GenerateShopArtifact(); // 상점이 열릴 때 유물 생성 (리롤 시에는 호출되지 않음)

            // 패널 애니메이션: 가로 폭을 넓히는 방식으로 전개
            if (shopPanelRect != null)
            {
                shopPanelRect.DOKill();
                SetShopPanelWidth(shopCollapsedWidth);
                CreateShopPanelWidthTween(shopExpandedWidth).SetEase(openEase);
            }
        }

        // 장기판 이동 연출 제거: 패널이 그리드 위로 겹치도록 변경

        // 인벤토리를 BoardContainer 위로 이동 (렌더링 순서 조정)
        if (inventoryContainer != null)
        {
            inventoryOriginalSiblingIndex = inventoryContainer.GetSiblingIndex();
            inventoryContainer.SetAsLastSibling();
        }
    }

    public void Close(Action onComplete = null)
    {
        HideMerchant(true);

        // 이미 닫혀있다면 즉시 종료 (불필요한 지연/대사 방지)
        if (shopPanel != null && !shopPanel.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        closeDelayTween = DOVirtual.DelayedCall(0f, () =>
        {
            if (shopPanelRect != null)
            {
                // 패널 폭을 줄여서 숨김
                shopPanelRect.DOKill();
                CreateShopPanelWidthTween(shopCollapsedWidth)
                    .SetEase(closeEase)
                    .OnComplete(() =>
                    {
                        if (shopPanel != null) shopPanel.SetActive(false);
                        onComplete?.Invoke();
                    });
            }
            else if (shopPanel != null)
            {
                shopPanel.SetActive(false);
                onComplete?.Invoke();
            }
            else
            {
                onComplete?.Invoke();
            }

            // 장기판 원위치 복귀 연출 제거: 패널이 겹치므로 별도 복귀 동작 불필요

            // 인벤토리 렌더링 순서 복원
            if (inventoryContainer != null && inventoryOriginalSiblingIndex >= 0)
            {
                inventoryContainer.SetSiblingIndex(inventoryOriginalSiblingIndex);
            }
        }).SetLink(gameObject);
    }

    private void EnsureMerchantObject()
    {
        if (merchantObject == null)
        {
            Transform canvasTransform = GetMerchantCanvasTransform();
            if (merchantPrefab == null || canvasTransform == null)
            {
                return;
            }

            merchantObject = Instantiate(merchantPrefab, canvasTransform);
        }

        if (merchantObject == null)
        {
            return;
        }

        if (merchantObject.transform.parent == null)
        {
            merchantObject.transform.SetParent(GetMerchantCanvasTransform(), false);
        }

        SetupMerchantRect(merchantObject.GetComponent<RectTransform>());

        WireMerchantClick();
    }

    private Transform GetMerchantCanvasTransform()
    {
        if (shopPanel != null)
        {
            Canvas panelCanvas = shopPanel.GetComponentInParent<Canvas>(true);
            if (panelCanvas != null)
            {
                return panelCanvas.transform;
            }
        }

        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        return canvas != null ? canvas.transform : transform;
    }

    private void SetupMerchantRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = merchantAnchoredPosition;
    }

    private void PlayMerchantEnterAnimation()
    {
        merchantObject.transform.DOKill();
        merchantObject.transform.localScale = Vector3.one;

        RectTransform rect = merchantObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.DOKill();
            Vector2 targetPosition = merchantAnchoredPosition;

            rect.anchoredPosition = new Vector2(merchantEnterStartX, targetPosition.y);
            rect.DOAnchorPos(targetPosition, merchantEnterDuration).SetEase(merchantEnterEase);
            return;
        }

        Vector3 targetLocalPosition = merchantObject.transform.localPosition;
        merchantObject.transform.localPosition = new Vector3(merchantEnterStartX, targetLocalPosition.y, targetLocalPosition.z);
        merchantObject.transform.DOLocalMove(targetLocalPosition, merchantEnterDuration).SetEase(merchantEnterEase);
    }

    private void PlayMerchantExitAnimation()
    {
        RectTransform rect = merchantObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 exitPosition = new Vector2(merchantExitTargetX, rect.anchoredPosition.y);
            rect.DOAnchorPos(exitPosition, merchantExitDuration)
                .SetEase(merchantExitEase)
                .OnComplete(() =>
                {
                    if (merchantObject != null)
                    {
                        merchantObject.SetActive(false);
                    }
                });
            return;
        }

        Vector3 exitLocalPosition = new Vector3(merchantExitTargetX, merchantObject.transform.localPosition.y, merchantObject.transform.localPosition.z);
        merchantObject.transform.DOLocalMove(exitLocalPosition, merchantExitDuration)
            .SetEase(merchantExitEase)
            .OnComplete(() =>
            {
                if (merchantObject != null)
                {
                    merchantObject.SetActive(false);
                }
            });
    }

    private void WireMerchantClick()
    {
        merchantButton = merchantObject.GetComponent<Button>();
        if (merchantButton == null)
        {
            merchantButton = merchantObject.GetComponentInChildren<Button>(true);
        }

        if (merchantButton != null)
        {
            merchantButton.onClick.RemoveListener(OpenFromMerchant);
            merchantButton.onClick.AddListener(OpenFromMerchant);
            return;
        }

        ShopMerchantClickTarget clickTarget = merchantObject.GetComponent<ShopMerchantClickTarget>();
        if (clickTarget == null)
        {
            clickTarget = merchantObject.AddComponent<ShopMerchantClickTarget>();
        }

        clickTarget.Initialize(this);
    }

    private void OnConfirmButtonClick()
    {
        if (confirmButton != null) confirmButton.interactable = false; // 중복 클릭 방지

        // 상점이 닫히고 장기판이 돌아온 후 Battle 상태로 전환
        Close(() => 
        {
            if (confirmButton != null) confirmButton.interactable = true;
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeFlowState(GameFlowState.Map);
            }
        });
    }

    private void OnRerollButtonClick()
    {
        if (GameManager.Instance == null) return;

        int currentRerollCost = GetCurrentRerollCost();

        if (GameManager.Instance.Coin < currentRerollCost)
        {
            if (rerollButton != null)
            {
                ShakeButton(rerollButton);
            }
            return;
        }

        if (GameManager.Instance.UseCoin(currentRerollCost))
        {
            GenerateShopItems();
            if (shopItemsContainer != null) shopItemsContainer.DOShakePosition(0.3f, 5f, 20, 90, false, true);
        }
    }

    private void OnPieceLimitUpgradeClick()
    {
        if (GameManager.Instance == null || PieceManager.Instance == null) return;

        if (PieceManager.Instance.MaxPlayerPiecesOnBoard >= PieceManager.AbsoluteMaxPlayerPieces)
        {
            return;
        }

        if (GameManager.Instance.Coin < pieceLimitUpgradeCost)
        {
            if (pieceLimitUpgradeButton != null)
            {
                ShakeButton(pieceLimitUpgradeButton);
            }
            return;
        }

        if (GameManager.Instance.UseCoin(pieceLimitUpgradeCost))
        {
            PieceManager.Instance.IncreaseMaxPlayerPieces(pieceLimitUpgradeAmount);
            RecalculateUpgradeCost();
            UpdatePieceLimitUpgradeUI();
            
            if (pieceLimitUpgradeButton != null)
            {
                pieceLimitUpgradeButton.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1);
            }
        }
    }

    private void RecalculateUpgradeCost()
    {
        if (PieceManager.Instance == null) return;

        int currentMax = PieceManager.Instance.MaxPlayerPiecesOnBoard;
        int baseMax = 3; 
        int upgradesDone = currentMax - baseMax;

        int calcCost = 200;
        int calcIncrease = 100;

        for (int i = 0; i < upgradesDone; i++)
        {
            calcCost += calcIncrease;
            if ((i + 1) % 5 == 0)
            {
                calcIncrease *= 2;
            }
        }

        pieceLimitUpgradeCost = calcCost;
        pieceLimitUpgradeCostIncrease = calcIncrease;
    }

    private void UpdatePieceLimitUpgradeUI()
    {
        if (pieceLimitUpgradeCostText != null)
        {
            if (PieceManager.Instance != null && PieceManager.Instance.MaxPlayerPiecesOnBoard >= PieceManager.AbsoluteMaxPlayerPieces)
            {
                pieceLimitUpgradeCostText.text = "MAX";
                if (pieceLimitUpgradeButton != null) pieceLimitUpgradeButton.interactable = false;
            }
            else
            {
                pieceLimitUpgradeCostText.text = $"{pieceLimitUpgradeCost}";
            }
        }
    }

    private void ShakeButton(Button targetButton)
    {
        if (targetButton == null) return;

        // 이전 트윈을 완료하여 상태를 복구 (OnComplete 실행됨)
        targetButton.transform.DOKill(true);

        // LayoutElement 컴포넌트 확인 및 추가
        LayoutElement layoutElement = targetButton.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = targetButton.gameObject.AddComponent<LayoutElement>();

        // 플레이스홀더 생성 (레이아웃 공간 확보용)
        GameObject placeholder = new GameObject("LayoutPlaceholder");
        placeholder.transform.SetParent(targetButton.transform.parent, false);
        placeholder.transform.SetSiblingIndex(targetButton.transform.GetSiblingIndex());
        
        RectTransform placeholderRect = placeholder.AddComponent<RectTransform>();
        RectTransform buttonRect = targetButton.GetComponent<RectTransform>();
        placeholderRect.sizeDelta = buttonRect.sizeDelta;
        
        // LayoutElement 속성 복사
        LayoutElement placeholderLE = placeholder.AddComponent<LayoutElement>();
        placeholderLE.preferredWidth = layoutElement.preferredWidth;
        placeholderLE.preferredHeight = layoutElement.preferredHeight;
        placeholderLE.flexibleWidth = layoutElement.flexibleWidth;
        placeholderLE.flexibleHeight = layoutElement.flexibleHeight;
        placeholderLE.minWidth = layoutElement.minWidth;
        placeholderLE.minHeight = layoutElement.minHeight;

        // 흔들리는 동안 레이아웃 그룹의 영향을 받지 않도록 설정
        layoutElement.ignoreLayout = true;

        // 좌우(X축) 흔들림 적용
        targetButton.transform.DOShakePosition(0.5f, new Vector3(10f, 0, 0), 20, 90, false, true)
            .OnComplete(() => 
            {
                layoutElement.ignoreLayout = false;
                Destroy(placeholder);
            });
    }

    private int GetCurrentRerollCost()
    {
        int currentCost = rerollCost;

        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.ApplyArtifactWithLevel("A002", level =>
            {
                int discount = 2 + (level - 1);
                currentCost = Mathf.Max(1, rerollCost - discount);
            });
        }

        return currentCost;
    }

    private void UpdateRerollCostUI()
    {
        if (rerollCostText != null)
        {
            rerollCostText.text = $"{GetCurrentRerollCost()}";
        }
    }

    private void GenerateShopItems()
    {
        if (shopPieceData == null || shopSlotPrefab == null || shopItemsContainer == null) return;

        // 기존 아이템 제거
        for (int i = shopItemsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = shopItemsContainer.GetChild(i);
            if (child.GetComponent<ShopPieceSlot>() != null)
            {
                Destroy(child.gameObject);
            }
        }

        int currentItemsToDisplay = itemsToDisplay;
        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.ApplyArtifactWithLevel("A001", level =>
            {
                currentItemsToDisplay = Mathf.Clamp(itemsToDisplay + level, 0, 3);
            });
        }

        // 새 아이템 생성
        for (int i = 0; i < 3; i++)
        {
            GameObject slotObj = Instantiate(shopSlotPrefab, shopItemsContainer);
            ShopPieceSlot slot = slotObj.GetComponent<ShopPieceSlot>();

            if (slot != null)
            {
                if (i < currentItemsToDisplay)
                {
                    var pieceInfo = shopPieceData.GetRandomPiece();
                    if (pieceInfo != null)
                    {
                        SealData seal = GetRandomSeal(pieceInfo.pieceType);
                        int finalPrice = CalculatePriceWithSeal(pieceInfo.Price, seal);
                        slot.Setup(pieceInfo.pieceType, finalPrice, seal);
                    }
                }
                else
                {
                    slot.SetSoldOut();
                }
            }
            
            // 등장 애니메이션: 스케일 0에서 1로 커지며 순차적으로 등장
            slotObj.transform.localScale = Vector3.zero;
            slotObj.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetDelay(i * 0.1f)
                .OnComplete(() => {
                    if (slot != null && slot.AttachedSeal != null)
                    {
                        slot.PlaySealEffect(slot.AttachedSeal.rarity);
                    }
                });
        }
    }

    private void GenerateShopArtifact()
    {
        if (shopArtifactSlot == null || ArtifactManager.Instance == null) return;

        // 상점에서 판매 가능한 유물 중 랜덤으로 하나 가져오기
        ArtifactData artifact = ArtifactManager.Instance.GetRandomArtifactForShop();
        if (artifact != null)
        {
            shopArtifactSlot.gameObject.SetActive(true);
            shopArtifactSlot.Setup(artifact);
        }
        else
        {
            shopArtifactSlot.gameObject.SetActive(false);
        }
    }

    private int CalculatePriceWithSeal(int basePrice, SealData seal)
    {
        if (seal == null) return basePrice;

        float multiplier = 1.0f;
        switch (seal.rarity)
        {
            case SealRarity.Common:
                multiplier = 1.5f;
                break;
            case SealRarity.Rare:
                multiplier = 1.7f;
                break;
            case SealRarity.Epic:
                multiplier = 2.0f;
                break;
            case SealRarity.Legendary:
                multiplier = 2.5f;
                break;
        }

        return Mathf.RoundToInt(basePrice * multiplier);
    }

    private SealData GetRandomSeal(PieceType pieceType)
    {
        if (allSeals == null || allSeals.Count == 0)
        {
            return null;
        }

        float roll = UnityEngine.Random.Range(0f, 100f);
        
        // 스테이지 진행도에 따른 확률 보정 (0 ~ 9 스테이지까지 선형 증가)
        float progress = Mathf.Clamp01(GameManager.Instance.ClearedStage+1 / 10f);

        float artifactBonus = 0f;
        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.ApplyArtifactWithLevel("A003", level =>
            {
                artifactBonus = artifactSealChanceBonus * level;
            });
        }
        
        // 현재 적용될 총 확률 (유물 등으로 sealDropChance가 바뀌어도 비율 유지)
        float currentChance = (sealDropChance + artifactBonus) * progress;
        
        // 누적 확률 계산 (비율: 0.5 : 2.5 : 4 : 8) -> 총합 15 기준 비율
        if (roll < currentChance * (0.5f / 15f)) return GetRandomSealByRarity(SealRarity.Legendary, pieceType);
        if (roll < currentChance * (3.0f / 15f)) return GetRandomSealByRarity(SealRarity.Epic, pieceType);
        if (roll < currentChance * (7.0f / 15f)) return GetRandomSealByRarity(SealRarity.Rare, pieceType);
        if (roll < currentChance) return GetRandomSealByRarity(SealRarity.Common, pieceType);

        return null;
    }

    private SealData GetRandomSealByRarity(SealRarity rarity, PieceType pieceType)
    {
        var candidates = allSeals.Where(s => 
            s.rarity == rarity && 
            s.isSoldInShop && // 상점 판매 가능 인장만
            (s.compatiblePieces == null || s.compatiblePieces.Count == 0 || s.compatiblePieces.Contains(pieceType))).ToList();
        
        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    public void RestoreInventory()
    {
        if (PieceInventory.Instance == null || PieceSpawner.Instance == null || PieceSpawner.Instance.piecePrefab == null) return;

        // 저장된 인벤토리 데이터를 복사 (생성 과정에서 리스트가 수정되는 것을 방지하기 위해 복사본 사용)
        var savedPieceInfos = new List<PieceInventory.PieceInfo>(PieceInventory.Instance.OwnedPieces);

        if (savedPieceInfos.Count == 0)
        {
            return;
        }

        // 비활성 오브젝트까지 포함해 슬롯을 찾습니다.
        var slots = FindObjectsByType<InventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sortedSlots = slots.OrderBy(s => s.transform.GetSiblingIndex()).ToList();

        if (sortedSlots.Count == 0)
        {
            return;
        }
        
        // 리스트를 비우고 생성하면서 다시 채워넣음 (중복 추가 방지)
        PieceInventory.Instance.ClearPieces();

        foreach (var pieceInfo in savedPieceInfos)
        {
            // 비어있는 첫 번째 슬롯 찾기
            var targetSlot = sortedSlots.FirstOrDefault(slot => slot.GetComponentInChildren<PieceController>() == null);
            
            if (targetSlot != null)
            {
                GameObject pieceObj = Instantiate(PieceSpawner.Instance.piecePrefab, targetSlot.transform);
                PieceController piece = pieceObj.GetComponent<PieceController>();
                
                if (piece != null)
                {
                    piece.Initialize(pieceInfo.pieceType, false);

                    // 인벤토리로 이동 (이 과정에서 PieceInventory.AddPiece가 호출됨)
                    piece.MoveToInventory(targetSlot.transform);
                }
            }
        }

    }

    /// <summary>
    /// Resources/Seal 폴더에서 모든 SealData 자동 로드
    /// </summary>
    private void LoadAllSealsFromFolder()
    {
        allSeals = new List<SealData>();
        
        #if UNITY_EDITOR
        // 에디터 환경: AssetDatabase 사용
        string folderPath = "Assets/Data/Seal";
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SealData", new[] { folderPath });
        
        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            
            SealData seal = UnityEditor.AssetDatabase.LoadAssetAtPath<SealData>(assetPath);
            if (seal != null)
            {
                allSeals.Add(seal);
            }
        }
        #else
        // 런타임 환경: Resources 폴더 사용
        SealData[] loadedSeals = Resources.LoadAll<SealData>("Seal");
        allSeals.AddRange(loadedSeals);
        #endif

    }
}

public class ShopMerchantClickTarget : MonoBehaviour, IPointerClickHandler
{
    private ShopManager shopManager;

    public void Initialize(ShopManager manager)
    {
        shopManager = manager;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OpenShop();
    }

    private void OnMouseDown()
    {
        OpenShop();
    }

    private void OpenShop()
    {
        if (shopManager != null)
        {
            shopManager.OpenFromMerchant();
        }
    }
}
