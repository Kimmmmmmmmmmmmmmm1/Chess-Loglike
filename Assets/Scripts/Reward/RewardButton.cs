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
        backgroundImage = GetComponent<Image>();
        if (backgroundImage != null)
        {
            originalBgColor = backgroundImage.color;
        }
    }

    public void Initialize(RewardManager manager, int rarity, bool isTreasure = false)
    {
        InitializeWithType(manager, rarity, null, isTreasure);
    }

    public void InitializeWithType(RewardManager manager, int rarity, RewardType? forcedType, bool isTreasure = false)
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
            type = manager.GetRandomRewardType(isTreasure);
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
            
            // 유물 확률 조정
            float artifactMultiplier = isTreasure ? 3f : 0.5f;
            float adjustedArtifactChance = baseArtifactChance * artifactMultiplier;
            
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

        SetupRewardContent(rarity);
    }

    private void SetupRewardContent(int rarity)
    {
        if (type == RewardType.Coin)
        {
            coinAmount = Random.Range(20, 50) + (rarity * 10); // 희귀도에 따라 코인 증가
            if (infoText != null) infoText.text = $"{coinAmount} Coin";
        }
        else if (type == RewardType.Piece)
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
                var pieceInfo = pieceData.GetRandomPiece();
                pieceType = pieceInfo != null ? pieceInfo.pieceType : PieceType.Pawn;
                if (infoText != null) infoText.text = PieceController.GetPieceNameForType(pieceType);
                
                if (iconImage != null && PieceManager.Instance != null)
                {
                    Sprite s = PieceManager.Instance.GetSpriteFor(pieceType);
                    iconImage.sprite = s;
                    iconImage.color = s != null ? Color.white : new Color(0f, 0.12f, 0f, 0.9f);
                }
            }
            else
            {
                pieceType = PieceType.Pawn;
                if (infoText != null) infoText.text = PieceController.GetPieceNameForType(PieceType.Pawn);
            }

            CollectionManager.EnsureInstance()?.RecordPieceSeen(pieceType);
        }
        else if (type == RewardType.Seal)
        {
            attachedSeal = GetRandomSeal(null); // 특정 기물 타입에 제약 없이 인장 획득
            if (attachedSeal != null)
            {
                CollectionManager.EnsureInstance()?.RecordSealSeen(attachedSeal);
                if (infoText != null) infoText.text = attachedSeal.sealName;
                if (iconImage != null) iconImage.sprite = attachedSeal.icon;
                CreateSealIcon(attachedSeal);
            }
            else // 획득할 인장이 없으면 코인으로 대체
            {
                type = RewardType.Coin;
                coinAmount = Random.Range(20, 50) + (rarity * 10);
                if (infoText != null) infoText.text = $"{coinAmount} Coin";
            }
        }
        else if (type == RewardType.Artifact)
        {
            // 유물 보상 시도
            if (ArtifactManager.Instance != null)
            {
                artifactReward = ArtifactManager.Instance.GetRandomArtifact();
            }

            if (artifactReward != null)
            {
                CollectionManager.EnsureInstance()?.RecordArtifactSeen(artifactReward);
                if (infoText != null) infoText.text = artifactReward.artifactName;
                if (iconImage != null)
                {
                    iconImage.sprite = artifactReward.icon;
                    iconImage.color = Color.white;
                }
            }
            else
            {
                // 유물이 없거나(다 모음) 매니저가 없으면 코인으로 대체
                type = RewardType.Coin;
                coinAmount = Random.Range(30, 60);
                if (infoText != null) infoText.text = $"{coinAmount} Coin";
            }
        }
    }

    private SealData GetRandomSeal(PieceType? pieceType)
    {
        if (ShopManager.Instance == null || ShopManager.Instance.allSeals == null) return null;

        // 상점의 희귀도 분포 비율을 따름 (0.5 : 2.5 : 4 : 8) -> 총합 15
        float roll = Random.Range(0f, 15f);
        
        SealRarity rarity;
        if (roll < 0.5f) rarity = SealRarity.Legendary;
        else if (roll < 3.0f) rarity = SealRarity.Epic;
        else if (roll < 7.0f) rarity = SealRarity.Rare;
        else rarity = SealRarity.Common;

        var candidates = ShopManager.Instance.allSeals.Where(s => s.rarity == rarity && s.isSoldInShop &&
            (pieceType == null || s.compatiblePieces == null || s.compatiblePieces.Count == 0 || s.compatiblePieces.Contains(pieceType.Value))).ToList();

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
        if (sealIconImage == null) return;

        // 초기화
        sealIconImage.transform.DOKill();
        sealIconImage.transform.localScale = Vector3.one;
        sealIconImage.color = Color.white;

        switch (rarity)
        {
            case SealRarity.Common:
                sealIconImage.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 10, 1);
                break;
            case SealRarity.Rare:
                sealIconImage.transform.DOPunchScale(Vector3.one * 0.4f, 0.6f, 10, 1);
                SpawnParticles(6, new Color(0f, 0.5f, 1f), 60f, 0.6f);
                break;
            case SealRarity.Epic:
                sealIconImage.transform.DOPunchScale(Vector3.one * 0.5f, 0.8f, 10, 1);
                sealIconImage.transform.DOShakeRotation(0.8f, 30f, 10, 90);
                SpawnParticles(12, new Color(0.8f, 0f, 1f), 90f, 0.8f);
                break;
            case SealRarity.Legendary:
                Sequence seq = DOTween.Sequence();
                seq.Append(sealIconImage.transform.DOPunchScale(Vector3.one * 0.7f, 1.0f, 10, 1));
                seq.Join(sealIconImage.transform.DOShakeRotation(1.0f, 45f, 10, 90));
                seq.Join(sealIconImage.DOColor(new Color(1f, 0.8f, 0f), 0.2f).SetLoops(6, LoopType.Yoyo));
                seq.OnComplete(() => sealIconImage.color = Color.white);
                SpawnParticles(20, new Color(1f, 0.9f, 0.2f), 130f, 1.2f);
                break;
        }
    }

    private void SpawnParticles(int count, Color color, float distance, float duration)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject p = new GameObject("SealParticle");
            p.transform.SetParent(transform, true);
            p.transform.position = sealIconImage.transform.position;
            
            Image img = p.AddComponent<Image>();
            if (sealIconImage.sprite != null) img.sprite = sealIconImage.sprite;
            img.color = color;
            img.raycastTarget = false;

            float scale = Random.Range(0.3f, 0.6f);
            p.transform.localScale = Vector3.one * scale;

            float angle = Random.Range(0f, 360f);
            Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0);
            
            p.transform.DOMove(p.transform.position + dir * distance, duration).SetEase(Ease.OutQuad);
            p.transform.DOScale(0f, duration).SetEase(Ease.InQuad);
            p.transform.DORotate(new Vector3(0, 0, Random.Range(-180f, 180f)), duration);
            img.DOFade(0f, duration).OnComplete(() => Destroy(p));
        }
    }

    private void OnClicked()
    {
        if (GameManager.Instance == null) return;

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
                    // 인장 지급 로직이 변경되었으므로, 여기서는 인장 없이 기물만 생성합니다.
                    PieceSpawner.Instance.SpawnPieceAndFlyToInventory(pieceType, transform.position, emptySlot, null, (piece) => {
                        emptySlot.IsReserved = false;
                    });
                }
                else if (PieceInventory.Instance != null)
                {
                    PieceInventory.Instance.AddPiece(pieceType);
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

        if (backgroundImage != null)
        {
            backgroundImage.DOKill();
            Color highlightColor = new Color(
                Mathf.Min(1f, originalBgColor.r + 0.15f),
                Mathf.Min(1f, originalBgColor.g + 0.15f),
                Mathf.Min(1f, originalBgColor.b + 0.15f),
                originalBgColor.a
            );
            backgroundImage.DOColor(highlightColor, hoverDuration).SetUpdate(true);
        }

        SoundManager.Instance?.PlaySFX(SFXType.Hover);

        if (TooltipManager.Instance != null)
        {
            if (type == RewardType.Piece)
            {
                string movementPattern = PieceController.GenerateMovementPatternForType(pieceType, attachedSeal);
                TooltipManager.Instance.ShowTooltip(
                    $"{pieceType}",
                    movementPattern,
                    transform.position,
                    attachedSeal != null ? $"인장: {attachedSeal.sealName}" : "",
                    TooltipManager.TooltipPriorityPieceMove,
                    gameObject);
            }
            else if (type == RewardType.Artifact && artifactReward != null)
            {
                TooltipManager.Instance.ShowTooltip(
                    artifactReward.artifactName,
                    artifactReward.description,
                    transform.position,
                    $"유물 (Lv.{artifactReward.Level})",
                    TooltipManager.TooltipPriorityDefault,
                    gameObject);
            }
            else if (type == RewardType.Seal && attachedSeal != null)
            {
                TooltipManager.Instance.ShowTooltip(
                    attachedSeal.sealName,
                    attachedSeal.description,
                    transform.position,
                    $"인장 ({attachedSeal.rarity})",
                    TooltipManager.TooltipPriorityDefault,
                    gameObject);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovered) return;
        isHovered = false;

        transform.DOKill();
        transform.DOScale(Vector3.one, hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true);

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
        int targetIndex = ArtifactManager.Instance.OwnedArtifacts.Count;
        if (ArtifactManager.Instance.artifactSlots != null && targetIndex < ArtifactManager.Instance.artifactSlots.Count)
        {
            if (ArtifactManager.Instance.artifactSlots[targetIndex] != null)
            {
                targetPos = ArtifactManager.Instance.artifactSlots[targetIndex].transform.position;
            }
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
