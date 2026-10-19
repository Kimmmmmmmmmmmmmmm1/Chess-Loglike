using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using DG.Tweening;
using System.Collections.Generic;

using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class RewardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

    public void EnsureCardLayout()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(145f, 180f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }

        // 1. Root Image (카드 배경)
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
            originalBgColor = backgroundImage.color;
        }

        // 2. Outline
        if (cardOutline == null) cardOutline = GetComponent<Outline>();
        if (cardOutline == null) cardOutline = gameObject.AddComponent<Outline>();
        cardOutline.effectDistance = new Vector2(2f, 2f);

        // 3. Header Banner
        Transform hObj = transform.Find("Header");
        if (hObj == null)
        {
            GameObject headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(transform, false);
            hObj = headerGo.transform;
        }
        RectTransform headerRect = hObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, 0f);
        headerRect.sizeDelta = new Vector2(0f, 26f);
        headerRect.localScale = Vector3.one;

        headerBg = hObj.GetComponent<Image>() ?? hObj.gameObject.AddComponent<Image>();

        Transform htObj = hObj.Find("HeaderText");
        if (htObj == null)
        {
            GameObject htGo = new GameObject("HeaderText", typeof(RectTransform));
            htGo.transform.SetParent(hObj, false);
            htObj = htGo.transform;
        }
        RectTransform htRect = htObj.GetComponent<RectTransform>();
        htRect.anchorMin = Vector2.zero;
        htRect.anchorMax = Vector2.one;
        htRect.anchoredPosition = Vector2.zero;
        htRect.sizeDelta = Vector2.zero;
        htRect.localScale = Vector3.one;

        headerText = htObj.GetComponent<TextMeshProUGUI>() ?? htObj.gameObject.AddComponent<TextMeshProUGUI>();
        headerText.fontSize = 10f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.enableAutoSizing = true;
        headerText.fontSizeMin = 7.5f;
        headerText.fontSizeMax = 10.5f;
        headerText.textWrappingMode = TextWrappingModes.NoWrap;
        headerText.overflowMode = TextOverflowModes.Ellipsis;
        if (infoText != null && headerText.font == null) headerText.font = infoText.font;

        // 4. Center Icon
        if (iconImage != null)
        {
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -28f);
            iconRect.sizeDelta = new Vector2(36f, 36f);
            iconRect.localScale = Vector3.one;
            iconImage.preserveAspect = true;
        }

        // 5. Title Text
        Transform tObj = transform.Find("TitleText");
        if (tObj == null)
        {
            if (infoText != null && infoText.gameObject.name != "TitleText")
            {
                tObj = infoText.transform;
                tObj.name = "TitleText";
            }
            else
            {
                GameObject tGo = new GameObject("TitleText", typeof(RectTransform));
                tGo.transform.SetParent(transform, false);
                tObj = tGo.transform;
            }
        }
        RectTransform titleRect = tObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -66f);
        titleRect.sizeDelta = new Vector2(-10f, 26f);
        titleRect.localScale = Vector3.one;

        titleText = tObj.GetComponent<TextMeshProUGUI>() ?? tObj.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 11.5f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 8f;
        titleText.fontSizeMax = 12f;
        titleText.textWrappingMode = TextWrappingModes.Normal;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        if (infoText != null && titleText.font == null) titleText.font = infoText.font;
        infoText = titleText;

        // 6. Description Text
        Transform dObj = transform.Find("DescText");
        if (dObj == null)
        {
            GameObject dGo = new GameObject("DescText", typeof(RectTransform));
            dGo.transform.SetParent(transform, false);
            dObj = dGo.transform;
        }
        RectTransform descRect = dObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0.5f, 1f);
        descRect.anchoredPosition = new Vector2(0f, -94f);
        descRect.sizeDelta = new Vector2(-12f, 48f);
        descRect.localScale = Vector3.one;

        descText = dObj.GetComponent<TextMeshProUGUI>() ?? dObj.gameObject.AddComponent<TextMeshProUGUI>();
        descText.fontSize = 9.5f;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.85f, 0.85f, 0.88f, 1f);
        descText.enableAutoSizing = true;
        descText.fontSizeMin = 6.5f;
        descText.fontSizeMax = 9.5f;
        descText.textWrappingMode = TextWrappingModes.Normal;
        descText.overflowMode = TextOverflowModes.Ellipsis;
        if (infoText != null && descText.font == null) descText.font = infoText.font;

        // 7. Select Button
        Transform sObj = transform.Find("SelectBtn");
        if (sObj == null)
        {
            GameObject sGo = new GameObject("SelectBtn", typeof(RectTransform));
            sGo.transform.SetParent(transform, false);
            sObj = sGo.transform;
        }
        RectTransform selectRect = sObj.GetComponent<RectTransform>();
        selectRect.anchorMin = new Vector2(0f, 0f);
        selectRect.anchorMax = new Vector2(1f, 0f);
        selectRect.pivot = new Vector2(0.5f, 0f);
        selectRect.anchoredPosition = new Vector2(0f, 7f);
        selectRect.sizeDelta = new Vector2(-20f, 24f);
        selectRect.localScale = Vector3.one;

        selectBtnBg = sObj.GetComponent<Image>() ?? sObj.gameObject.AddComponent<Image>();

        Transform stObj = sObj.Find("Text");
        if (stObj == null)
        {
            GameObject stGo = new GameObject("Text", typeof(RectTransform));
            stGo.transform.SetParent(sObj, false);
            stObj = stGo.transform;
        }
        RectTransform stRect = stObj.GetComponent<RectTransform>();
        stRect.anchorMin = Vector2.zero;
        stRect.anchorMax = Vector2.one;
        stRect.anchoredPosition = Vector2.zero;
        stRect.sizeDelta = Vector2.zero;
        stRect.localScale = Vector3.one;

        selectBtnText = stObj.GetComponent<TextMeshProUGUI>() ?? stObj.gameObject.AddComponent<TextMeshProUGUI>();
        selectBtnText.fontSize = 11f;
        selectBtnText.fontStyle = FontStyles.Bold;
        selectBtnText.alignment = TextAlignmentOptions.Center;
        selectBtnText.color = Color.white;
        selectBtnText.enableAutoSizing = true;
        selectBtnText.fontSizeMin = 8f;
        selectBtnText.fontSizeMax = 11f;
        selectBtnText.text = "선택";
        if (infoText != null && selectBtnText.font == null) selectBtnText.font = infoText.font;
    }

    public void Initialize(RewardManager manager, int rarity)
    {
        InitializeWithType(manager, rarity, null);
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

            if (isElite)
            {
                coinAmount = (Random.Range(35, 65) + (rarity * 10)) * 2;
            }
            else
            {
                coinAmount = Random.Range(20, 50) + (rarity * 10);
            }

            titleString = $"+{coinAmount} Gold";
            descString = "즉시 보유 골드에 추가됩니다.";

            if (iconImage != null)
            {
                iconImage.color = new Color(1f, 0.85f, 0.2f, 1f);
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
            }

            CollectionManager.EnsureInstance()?.RecordPieceSeen(pieceType);
        }
        else if (type == RewardType.Seal)
        {
            attachedSeal = GetRandomSeal(null, isElite: isElite);
            if (attachedSeal != null)
            {
                CollectionManager.EnsureInstance()?.RecordSealSeen(attachedSeal);
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
                type = RewardType.Coin;
                coinAmount = isElite ? ((Random.Range(35, 65) + (rarity * 10)) * 2) : (Random.Range(20, 50) + (rarity * 10));
                themeColor = new Color(1f, 0.85f, 0.2f, 1f);
                headerString = "🪙 골드";
                titleString = $"+{coinAmount} Gold";
                descString = "즉시 보유 골드에 추가됩니다.";
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

            if (artifactReward != null)
            {
                CollectionManager.EnsureInstance()?.RecordArtifactSeen(artifactReward);
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
                coinAmount = isElite ? ((Random.Range(40, 80) + (rarity * 10)) * 2) : Random.Range(30, 60);
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

        SealRarity rarity;
        if (isElite)
        {
            // 엘리트 전투 인장: Common 배제! Epic(50%)과 Rare(50%) 확정
            rarity = Random.value < 0.5f ? SealRarity.Epic : SealRarity.Rare;
        }
        else
        {
            // 일반 전투 보상 인장: 전설 제외 (Epic, Rare, Common 중 가중치 추첨)
            float roll = Random.Range(0f, 14.5f);
            if (roll < 2.5f) rarity = SealRarity.Epic;
            else if (roll < 6.5f) rarity = SealRarity.Rare;
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
        if (button != null) button.interactable = false;

        transform.DOKill();
        transform.DOPunchScale(Vector3.one * 0.12f, 0.2f, 10, 1).SetUpdate(true)
            .OnComplete(ExecuteRewardClaim);
    }

    private void ExecuteRewardClaim()
    {
        switch (type)
        {
            case RewardType.Coin:
                AnimateCoinFly(coinAmount);
                break;
            case RewardType.Piece:
                InventorySlot emptySlot = FindEmptyInventorySlot();
                if (emptySlot != null && PieceSpawner.Instance != null)
                {
                    emptySlot.IsReserved = true;
                    PieceSpawner.Instance.SpawnPieceAndFlyToInventory(pieceType, transform.position, emptySlot, attachedSeal, (piece) => {
                        emptySlot.IsReserved = false;
                    });
                }
                else if (PieceInventory.Instance != null)
                {
                    PieceInventory.Instance.AddPiece(pieceType);
                    if (attachedSeal != null && SealInventory.Instance != null)
                    {
                        SealInventory.Instance.AddSeal(attachedSeal);
                    }
                }
                break;
            case RewardType.Seal:
                if (SealInventory.Instance != null && attachedSeal != null)
                {
                    SealInventory.Instance.AddSeal(attachedSeal);
                }
                break;
            case RewardType.Artifact:
                if (ArtifactManager.Instance != null && artifactReward != null)
                {
                    AnimateArtifactFly(artifactReward);
                }
                break;
        }

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }

        if (manager != null)
        {
            manager.OnRewardButtonClicked();
        }

        Destroy(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        isHovered = true;

        transform.DOKill();
        transform.DOScale(Vector3.one * hoverScale, hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true);

        if (cardOutline != null)
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
        transform.DOScale(Vector3.one, hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true);

        if (cardOutline != null)
        {
            cardOutline.effectDistance = new Vector2(2f, 2f);
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

    private void AnimateCoinFly(int amount)
    {
        if (GameManager.Instance.coinText == null)
        {
            GameManager.Instance.AddCoin(amount);
            return;
        }

        // 애니메이션 시작 시 즉시 데이터 처리
        GameManager.Instance.AddCoin(amount);

        GameObject flyObj = new GameObject("FlyingCoin");
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            flyObj.transform.SetParent(rootCanvas.transform, true);
        }
        else
        {
            flyObj.transform.SetParent(transform.root, true);
        }
        
        flyObj.transform.position = transform.position;
        flyObj.transform.localScale = Vector3.one;

        TextMeshProUGUI flyText = flyObj.AddComponent<TextMeshProUGUI>();
        flyText.raycastTarget = false;
        flyText.text = $"+{amount}";
        flyText.fontSize = 40;
        flyText.color = Color.yellow;
        flyText.alignment = TextAlignmentOptions.Center;
        flyText.textWrappingMode = TextWrappingModes.NoWrap;
        if (infoText != null) flyText.font = infoText.font;
        else if (GameManager.Instance != null && GameManager.Instance.coinText != null)
        {
            flyText.font = GameManager.Instance.coinText.font;
        }
        if (flyText.font == null)
        {
            flyText.font = TMP_Settings.defaultFontAsset;
        }
        
        Sequence seq = DOTween.Sequence();
        seq.Append(flyObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(GameManager.Instance.coinText.transform.position, 0.8f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0.5f, 0.8f).SetDelay(0.2f));
        
        // 완료 시 시각화만 업데이트
        seq.OnComplete(() =>
        {
            Destroy(flyObj);
        });
    }

    private void AnimateArtifactFly(ArtifactData artifact)
    {
        if (ArtifactManager.Instance == null || artifact == null) return;

        GameObject flyObj = new GameObject("FlyingArtifact");
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            flyObj.transform.SetParent(rootCanvas.transform, true);
        }
        else
        {
            flyObj.transform.SetParent(transform.root, true);
        }
        
        flyObj.transform.position = transform.position;
        flyObj.transform.localScale = Vector3.one;

        Image flyImage = flyObj.AddComponent<Image>();
        flyImage.sprite = artifact.icon;
        flyImage.raycastTarget = false;
        
        RectTransform rectTransform = flyObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(64, 64);

        // 유물 인벤토리 슬롯 찾기
        Vector3 targetPos = transform.position;
        ArtifactSlot incomingSlot = ArtifactManager.Instance.GetIncomingSlot(artifact);
        if (incomingSlot != null)
        {
            targetPos = incomingSlot.transform.position;
        }

        Sequence seq = DOTween.Sequence();
        seq.Append(flyObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(targetPos, 0.8f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0.3f, 0.8f).SetDelay(0.2f));
        
        // 완료 시 데이터 추가 및 시각화 업데이트
        seq.OnComplete(() =>
        {
            if (ArtifactManager.Instance != null)
            {
                ArtifactManager.Instance.AddArtifact(artifact);
            }
            Destroy(flyObj);
        });
    }
}
