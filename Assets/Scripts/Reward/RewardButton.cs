using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using DG.Tweening;
using System.Collections.Generic;

using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public partial class RewardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    public enum RewardType { Coin, Piece, Artifact, Seal }

    [Header("UI")]
    public TextMeshProUGUI infoText;
    public Image iconImage;

    [Header("Card UI (Stage Choice Card Style)")]
    public Image headerBg;
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public Outline cardOutline;
    public Image selectBtnBg;
    public TextMeshProUGUI selectBtnText;

    [Header("Hover Settings")]
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float hoverDuration = 0.18f;

    private RewardType type;
    private int coinAmount;
    private PieceType pieceType;
    private RewardManager manager;
    private ArtifactData artifactReward;
    private Button button;
    private SealData attachedSeal;
    private Image sealIconImage;
    private Image backgroundImage;
    private Color originalBgColor = Color.white;
    private bool isHovered = false;

    public SealData AttachedSeal => attachedSeal;

    private void Awake()
    {
        EnsureCardLayout();
    }

    public void Initialize(RewardManager manager, int rarity)
    {
        InitializeWithType(manager, rarity, null);
    }

    public SavedRewardCardData GetCardData()
    {
        return new SavedRewardCardData
        {
            rewardType = (int)type,
            coinAmount = coinAmount,
            pieceType = pieceType,
            artifactId = artifactReward != null ? artifactReward.id : null,
            attachedSealId = attachedSeal != null ? CollectionManager.GetSealId(attachedSeal) : null
        };
    }

    public void InitializeWithSavedData(RewardManager manager, int rarity, SavedRewardCardData data)
    {
        this.manager = manager;
        button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                originalBgColor = backgroundImage.color;
            }
        }

        type = (RewardType)data.rewardType;
        coinAmount = data.coinAmount;
        pieceType = data.pieceType;
        if (!string.IsNullOrEmpty(data.artifactId))
        {
            artifactReward = ArtifactManager.Instance != null ? ArtifactManager.Instance.GetArtifactDataById(data.artifactId) : null;
            if (artifactReward == null) artifactReward = StageSaveManager.FindArtifactData(data.artifactId);
        }
        if (!string.IsNullOrEmpty(data.attachedSealId))
        {
            attachedSeal = StageSaveManager.FindSealData(data.attachedSealId);
        }

        SetupSavedRewardContent(rarity);
    }

    public void InitializeWithFixedSeal(RewardManager manager, int rarity, SealData seal)
    {
        this.manager = manager;
        button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                originalBgColor = backgroundImage.color;
            }
        }

        type = RewardType.Seal;
        attachedSeal = seal;
        SetupSavedRewardContent(rarity);
    }

    public void InitializeWithFixedArtifact(RewardManager manager, int rarity, ArtifactData artifact)
    {
        this.manager = manager;
        button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                originalBgColor = backgroundImage.color;
            }
        }

        type = RewardType.Artifact;
        artifactReward = artifact;
        SetupSavedRewardContent(rarity);
    }

    private bool isTutorialHighlighted = false;

    /// <summary>
    /// 튜토리얼 중 지정된 보상 카드를 강조 표시합니다 (펄스 애니메이션 + 황금빛 외곽선 + 최상단 소팅오더).
    /// </summary>
    public void SetTutorialHighlighted(bool highlighted)
    {
        isTutorialHighlighted = highlighted;
        Canvas cardCanvas = GetComponent<Canvas>();
        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();

        if (highlighted)
        {
            if (cardCanvas == null) cardCanvas = gameObject.AddComponent<Canvas>();
            cardCanvas.overrideSorting = true;
            Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
            cardCanvas.sortingOrder = parentCanvas != null ? Mathf.Max(parentCanvas.sortingOrder + 50, 1000) : 1000;

            if (raycaster == null) raycaster = gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;

            if (cardOutline != null)
            {
                cardOutline.enabled = false;
            }

            if (!isHovered)
            {
                StartTutorialPulse();
            }
        }
        else
        {
            if (cardCanvas != null)
            {
                cardCanvas.overrideSorting = false;
            }
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }

            transform.DOKill();
            transform.localScale = Vector3.one;
            if (cardOutline != null)
            {
                cardOutline.effectColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                cardOutline.effectDistance = new Vector2(1f, 1f);
            }
        }
    }

    private void StartTutorialPulse()
    {
        transform.DOKill();
        transform.localScale = Vector3.one * 0.98f;
        transform.DOScale(Vector3.one * 1.02f, 0.45f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    /// <summary>
    /// 튜토리얼 중 선택 불가능한 보상 카드를 비활성화 및 반투명 처리합니다.
    /// </summary>
    public void SetTutorialDisabled(bool disabled)
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = !disabled;
        }

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = disabled ? 0.35f : 1f;
        cg.interactable = !disabled;
        cg.blocksRaycasts = !disabled;
    }

    public void InitializeWithType(RewardManager manager, int rarity, RewardType? forcedType)
    {
        this.manager = manager;
        button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                originalBgColor = backgroundImage.color;
            }
        }

        if (forcedType.HasValue)
        {
            type = forcedType.Value;
        }
        else if (manager != null)
        {
            type = manager.GetRandomRewardType();
        }
        else
        {
            // 확률 설정: 돈(90), 기물(7), 유물(3)
            float rand = Random.Range(0f, 100f);

            // 희귀도에 따른 확률 보정 (희귀도가 높을수록 기물/유물 확률 증가)
            float baseCoinChance = Mathf.Max(50f, 90f - (rarity * 10f));
            float basePieceChance = 7f + (rarity * 5f);
            float baseSealChance = 5f + (rarity * 2f); // 인장 보상 확률 추가
            float baseArtifactChance = 100f - baseCoinChance - basePieceChance - baseSealChance;
            
            float adjustedArtifactChance = baseArtifactChance;
            
            // 조정된 유물 확률에 따라 코인과 기물 확률 재분배
            float remainingChance = 100f - adjustedArtifactChance;
            float totalBaseChance = baseCoinChance + basePieceChance + baseSealChance;
            float coinChance = (baseCoinChance / totalBaseChance) * remainingChance;
            float pieceChance = coinChance + ((basePieceChance / totalBaseChance) * remainingChance);
            float sealChance = pieceChance + ((baseSealChance / totalBaseChance) * remainingChance);

            if (rand < coinChance)
            {
                type = RewardType.Coin;
            }
            else if (rand < pieceChance)
            {
                type = RewardType.Piece;
            }
            else if (rand < sealChance)
            {
                type = RewardType.Seal;
            }
            else
            {
                type = RewardType.Artifact;
            }
        }

        if (ShotgunKingSystem.IsActive && type == RewardType.Piece)
        {
            type = RewardType.Coin;
        }

        SetupRewardContent(rarity);
    }

    private void SetupRewardContent(int rarity)
    {
        bool isElite = GameManager.Instance != null && GameManager.Instance.CurrentBattleType == BattleType.Elite;

        if (type == RewardType.Coin)
        {
            if (isElite)
            {
                coinAmount = (Random.Range(35, 65) + (rarity * 10)) * 2;
            }
            else
            {
                coinAmount = Random.Range(20, 50) + (rarity * 10);
            }
        }
        else if (type == RewardType.Piece)
        {
            attachedSeal = null; // 보상 기물은 인장 미부착 상태로 확정
            if (isElite)
            {
                // 엘리트 전투 기물: 폰 제외! 고티어 기물 확정
                var elitePieces = new[] { PieceType.Queen, PieceType.Rook, PieceType.Chancellor, PieceType.Archbishop, PieceType.Centaur };
                pieceType = elitePieces[Random.Range(0, elitePieces.Length)];
            }
            else
            {
                ShopPieceData pieceData = null;
                if (RewardService.Instance != null)
                {
                    pieceData = RewardService.Instance.pieceData;
                }
                if (pieceData == null)
                {
                    pieceData = manager != null ? manager.pieceData : null;
                }

                if (pieceData != null)
                {
                    var pieceInfo = pieceData.GetRandomPiece(excludeLegendary: true);
                    pieceType = (pieceInfo != null && pieceInfo.pieceType != PieceType.Amazon) ? pieceInfo.pieceType : PieceType.Pawn;
                }
                else
                {
                    pieceType = PieceType.Pawn;
                }
            }
        }
        else if (type == RewardType.Seal)
        {
            attachedSeal = GetRandomSeal(null, isElite: isElite);
            if (attachedSeal == null)
            {
                type = RewardType.Coin;
                coinAmount = isElite ? ((Random.Range(35, 65) + (rarity * 10)) * 2) : (Random.Range(20, 50) + (rarity * 10));
            }
        }
        else if (type == RewardType.Artifact)
        {
            if (ArtifactManager.Instance != null)
            {
                artifactReward = ArtifactManager.Instance.GetRandomArtifact(
                    excludeLegendary: true,
                    minRarity: isElite ? ArtifactRarity.Rare : ArtifactRarity.Common
                );
            }

            if (artifactReward == null)
            {
                type = RewardType.Coin;
                coinAmount = isElite ? ((Random.Range(40, 80) + (rarity * 10)) * 2) : Random.Range(30, 60);
            }
        }

        SetupSavedRewardContent(rarity);
    }

    private void SetupSavedRewardContent(int rarity)
    {
        EnsureCardLayout();

        bool isElite = GameManager.Instance != null && GameManager.Instance.CurrentBattleType == BattleType.Elite;
        Color themeColor = Color.white;
        string headerString = "보상";
        string titleString = "";
        string descString = "";

        if (type == RewardType.Coin)
        {
            themeColor = new Color(1f, 0.85f, 0.2f, 1f); // 골드
            headerString = "🪙 골드";
            titleString = $"+{coinAmount} Gold";
            descString = "즉시 보유 골드에 추가됩니다.";

            if (iconImage != null)
            {
                iconImage.color = new Color(1f, 0.85f, 0.2f, 1f);
            }
        }
        else if (type == RewardType.Piece)
        {
            if (pieceType == PieceType.Amazon)
            {
                themeColor = new Color(1f, 0.85f, 0.2f, 1f);
                headerString = "⭐ 전설 기물";
            }
            else if (pieceType == PieceType.Queen || pieceType == PieceType.Rook || pieceType == PieceType.Chancellor || pieceType == PieceType.Archbishop || pieceType == PieceType.Centaur)
            {
                themeColor = new Color(0f, 0.95f, 1f, 1f);
                headerString = "👑 고급 기물";
            }
            else
            {
                themeColor = new Color(0.55f, 0.82f, 1f, 1f);
                headerString = "♟️ 기물 영입";
            }

            titleString = PieceController.GetPieceNameForType(pieceType);

            PieceData pieceDataObj = PieceData.GetData(pieceType);
            string pieceDesc = pieceDataObj != null && !string.IsNullOrEmpty(pieceDataObj.description)
                ? pieceDataObj.description
                : PieceController.GetSpecialRuleTextForType(pieceType);

            descString = !string.IsNullOrEmpty(pieceDesc) ? pieceDesc : "대기석으로 즉시 영입합니다.";
            
            if (iconImage != null && PieceManager.Instance != null)
            {
                Sprite s = PieceManager.Instance.GetSpriteFor(pieceType);
                iconImage.sprite = s;
                iconImage.preserveAspect = true;
                iconImage.color = s != null ? Color.white : new Color(0f, 0.12f, 0f, 0.9f);
            }

            if (attachedSeal != null)
            {
                CreateSealIcon(attachedSeal);
                TutorialManager.Instance?.CheckBuffDiscovered(attachedSeal);
            }

            CollectionManager.EnsureInstance()?.RecordPieceSeen(pieceType);
        }
        else if (type == RewardType.Seal)
        {
            if (attachedSeal != null)
            {
                CollectionManager.EnsureInstance()?.RecordSealSeen(attachedSeal);
                TutorialManager.Instance?.CheckBuffDiscovered(attachedSeal);
                themeColor = GetRarityColor(attachedSeal.rarity);
                headerString = $"📜 인장 [{GetRarityKorean(attachedSeal.rarity)}]";
                titleString = attachedSeal.sealName;
                descString = !string.IsNullOrEmpty(attachedSeal.description) ? attachedSeal.description : "기물에 장착 가능한 인장";

                if (iconImage != null)
                {
                    iconImage.sprite = attachedSeal.icon;
                    iconImage.preserveAspect = true;
                    iconImage.color = Color.white;
                }
                CreateSealIcon(attachedSeal);
            }
            else
            {
                themeColor = new Color(1f, 0.85f, 0.2f, 1f);
                headerString = "🪙 골드";
                titleString = $"+{coinAmount} Gold";
                descString = "즉시 보유 골드에 추가됩니다.";
            }
        }
        else if (type == RewardType.Artifact)
        {
            if (artifactReward != null)
            {
                CollectionManager.EnsureInstance()?.RecordArtifactSeen(artifactReward);
                TutorialManager.Instance?.CheckBuffDiscovered(artifactReward);
                themeColor = GetRarityColor((SealRarity)(int)artifactReward.rarity);
                headerString = $"🔮 유물 [{GetRarityKorean((SealRarity)(int)artifactReward.rarity)}]";
                titleString = artifactReward.artifactName;
                descString = !string.IsNullOrEmpty(artifactReward.description) ? artifactReward.description : "패시브 효과 유물";

                if (iconImage != null)
                {
                    iconImage.sprite = artifactReward.icon;
                    iconImage.preserveAspect = true;
                    iconImage.color = Color.white;
                }
            }
            else
            {
                type = RewardType.Coin;
                if (coinAmount <= 0)
                {
                    coinAmount = isElite ? ((Random.Range(40, 80) + (rarity * 10)) * 2) : Random.Range(30, 60);
                }
                themeColor = new Color(1f, 0.85f, 0.2f, 1f);
                headerString = "🪙 골드";
                titleString = $"+{coinAmount} Gold";
                descString = "즉시 보유 골드에 추가됩니다.";
            }
        }

        // 카드 시각 요소에 테마 색상 및 텍스트 반영
        if (headerText != null) headerText.text = headerString;
        if (titleText != null)
        {
            titleText.text = titleString;
            titleText.color = themeColor;
        }
        if (descText != null) descText.text = descString;
        if (infoText != null) infoText.text = titleString;

        if (headerBg != null)
        {
            Color hColor = themeColor;
            hColor.a = 0.35f;
            headerBg.color = hColor;
        }

        if (cardOutline != null)
        {
            cardOutline.effectColor = themeColor;
            cardOutline.effectDistance = new Vector2(2f, 2f);
        }

        if (selectBtnBg != null)
        {
            Color bColor = themeColor * 0.85f;
            bColor.a = 1f;
            selectBtnBg.color = bColor;
        }
    }

    private Color GetRarityColor(SealRarity rarity)
    {
        switch (rarity)
        {
            case SealRarity.Legendary: return new Color(1f, 0.85f, 0.2f, 1f); // Gold
            case SealRarity.Epic: return new Color(0.85f, 0.45f, 1f, 1f); // Purple
            case SealRarity.Rare: return new Color(0f, 0.85f, 1f, 1f); // Cyan
            default: return new Color(0.75f, 0.8f, 0.9f, 1f); // Common
        }
    }

    private string GetRarityKorean(SealRarity rarity)
    {
        switch (rarity)
        {
            case SealRarity.Legendary: return "전설";
            case SealRarity.Epic: return "영웅";
            case SealRarity.Rare: return "희귀";
            default: return "일반";
        }
    }

    private SealData GetRandomSeal(PieceType? pieceType, bool isElite = false)
    {
        if (ShotgunKingSystem.IsActive) return ShotgunKingSystem.GetRandomWeaponPart();
        if (ShopManager.Instance == null || ShopManager.Instance.allSeals == null) return null;

        float rareBonus = ArtifactEffectHandlers.GetRareChanceBonus();
        float rareMult = 1f + (rareBonus / 100f);

        SealRarity rarity;
        if (isElite)
        {
            // 엘리트 전투 인장: Common 배제! Epic과 Rare 확정 (기본 50:50, 희귀 확률 보너스 반영)
            float epicChance = Mathf.Clamp(0.5f * rareMult, 0.5f, 0.95f);
            rarity = Random.value < epicChance ? SealRarity.Epic : SealRarity.Rare;
        }
        else
        {
            // 일반 전투 보상 인장: 전설 제외 (Epic, Rare, Common 중 가중치 추첨, 희귀 확률 보너스 적용)
            float epicWeight = 2.5f * rareMult;
            float rareWeight = 4.0f * rareMult;
            float commonWeight = 8.0f;
            float total = epicWeight + rareWeight + commonWeight;
            float roll = Random.Range(0f, total);
            if (roll < epicWeight) rarity = SealRarity.Epic;
            else if (roll < epicWeight + rareWeight) rarity = SealRarity.Rare;
            else rarity = SealRarity.Common;
        }

        var candidates = ShopManager.Instance.allSeals.Where(s => s.rarity == rarity && s.rarity != SealRarity.Legendary && s.isSoldInShop &&
            (pieceType == null || s.compatiblePieces == null || s.compatiblePieces.Count == 0 || s.compatiblePieces.Contains(pieceType.Value))).ToList();

        if (candidates.Count == 0 && isElite)
        {
            // 다른 고등급(Rare/Epic)으로 폴백
            SealRarity fallbackRarity = (rarity == SealRarity.Epic) ? SealRarity.Rare : SealRarity.Epic;
            candidates = ShopManager.Instance.allSeals.Where(s => s.rarity == fallbackRarity && s.rarity != SealRarity.Legendary && s.isSoldInShop &&
                (pieceType == null || s.compatiblePieces == null || s.compatiblePieces.Count == 0 || s.compatiblePieces.Contains(pieceType.Value))).ToList();
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private void CreateSealIcon(SealData seal)
    {
        if (iconImage == null || seal.icon == null) return;
        
        GameObject sealObj = new GameObject("SealIcon");
        sealObj.transform.SetParent(iconImage.transform, false);
        
        sealIconImage = sealObj.AddComponent<Image>();
        sealIconImage.sprite = seal.icon;
        sealIconImage.preserveAspect = true;
        sealIconImage.raycastTarget = true;

        // 툴팁 핸들러 추가
        SealTooltipHandler tooltipHandler = sealObj.AddComponent<SealTooltipHandler>();
        tooltipHandler.Initialize(seal);
        
        RectTransform rt = sealObj.GetComponent<RectTransform>();
        // 우측 하단에 작게 표시
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-5, 5);
        rt.sizeDelta = new Vector2(12, 12);
    }

    public void PlaySealEffect(SealRarity rarity)
    {
        // 희귀/고등급 아이템 등장 이펙트 비활성화
    }

    private void OnClicked()
    {
        if (GameManager.Instance == null) return;
        if (manager != null)
        {
            manager.DisableAllRewardButtons();
        }
        if (button != null) button.interactable = false;

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }

        transform.DOKill();
        transform.DOPunchScale(Vector3.one * 0.12f, 0.2f, 10, 1).SetUpdate(true)
            .OnComplete(ExecuteRewardClaim);
    }

    private void ExecuteRewardClaim()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }

        // 선택되지 않은 다른 보상 카드들은 축소
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling != null && sibling != transform)
                {
                    sibling.DOKill();
                    sibling.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
                }
            }
        }

        switch (type)
        {
            case RewardType.Coin:
                AnimateCoinFly(coinAmount, FinishRewardClaim);
                break;
            case RewardType.Piece:
                InventorySlot emptySlot = FindEmptyInventorySlot();
                if (emptySlot != null && PieceSpawner.Instance != null)
                {
                    emptySlot.IsReserved = true;
                    PieceSpawner.Instance.SpawnPieceAndFlyToInventory(pieceType, transform.position, emptySlot, attachedSeal, (piece) => {
                        emptySlot.IsReserved = false;
                        FinishRewardClaim();
                    });
                }
                else if (PieceInventory.Instance != null)
                {
                    PieceInventory.Instance.AddPiece(pieceType);
                    if (attachedSeal != null && SealInventory.Instance != null)
                    {
                        SealInventory.Instance.AddSeal(attachedSeal);
                    }
                    FinishRewardClaim();
                }
                else
                {
                    FinishRewardClaim();
                }
                break;
            case RewardType.Seal:
                AnimateSealFly(attachedSeal, FinishRewardClaim);
                break;
            case RewardType.Artifact:
                if (ArtifactManager.Instance != null && artifactReward != null)
                {
                    AnimateArtifactFly(artifactReward, FinishRewardClaim);
                }
                else
                {
                    FinishRewardClaim();
                }
                break;
            default:
                FinishRewardClaim();
                break;
        }
    }

    private void FinishRewardClaim()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }

        // 보상 데이터 반영 완료 직후 저장
        StageSaveManager.EnsureInstance()?.SaveCurrentRun(pendingReward: false);

        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            TutorialManager.Instance.OnRewardSelected();
        }

        if (manager != null)
        {
            manager.OnRewardButtonClicked();
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceToStageSelect();
        }

        Destroy(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        isHovered = true;

        transform.DOKill();
        transform.DOScale(Vector3.one * hoverScale, hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true);

        if (cardOutline != null && !isTutorialHighlighted)
        {
            cardOutline.effectDistance = new Vector2(3f, 3f);
        }

        if (backgroundImage != null)
        {
            backgroundImage.DOKill();
            Color highlightColor = new Color(
                Mathf.Min(1f, originalBgColor.r + 0.08f),
                Mathf.Min(1f, originalBgColor.g + 0.08f),
                Mathf.Min(1f, originalBgColor.b + 0.08f),
                originalBgColor.a
            );
            backgroundImage.DOColor(highlightColor, hoverDuration).SetUpdate(true);
        }

        SoundManager.Instance?.PlaySFX(SFXType.Hover);

        if (TooltipManager.Instance != null)
        {
            if (type == RewardType.Piece)
            {
                TooltipManager.Instance.ShowPieceTooltip(pieceType, attachedSeal, transform.position, gameObject);
            }
            else if (type == RewardType.Artifact && artifactReward != null)
            {
                TooltipManager.Instance.ShowArtifactTooltip(
                    artifactReward,
                    transform.position,
                    gameObject,
                    TooltipManager.TooltipPriorityDefault);
            }
            else if (type == RewardType.Seal && attachedSeal != null)
            {
                TooltipManager.Instance.ShowSealTooltip(
                    attachedSeal,
                    transform.position,
                    gameObject,
                    TooltipManager.TooltipPriorityDefault);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovered) return;
        isHovered = false;

        transform.DOKill();
        if (isTutorialHighlighted)
        {
            StartTutorialPulse();
        }
        else
        {
            transform.DOScale(Vector3.one, hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        if (cardOutline != null)
        {
            if (isTutorialHighlighted)
            {
                cardOutline.enabled = false;
            }
            else
            {
                cardOutline.effectDistance = new Vector2(2f, 2f);
            }
        }

        if (backgroundImage != null)
        {
            backgroundImage.DOKill();
            backgroundImage.DOColor(originalBgColor, hoverDuration).SetUpdate(true);
        }

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }
    }

    private void OnDisable()
    {
        if (isHovered)
        {
            isHovered = false;
            transform.localScale = Vector3.one;
            if (backgroundImage != null) backgroundImage.color = originalBgColor;
            TooltipManager.Instance?.HideTooltip(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }
    }

    private InventorySlot FindEmptyInventorySlot()
    {
        var slots = FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);
        var sortedSlots = slots.OrderBy(s => s.transform.GetSiblingIndex()).ToArray();

        return sortedSlots.FirstOrDefault(slot => slot.GetComponentInChildren<PieceController>() == null && !slot.IsReserved);
    }
}
