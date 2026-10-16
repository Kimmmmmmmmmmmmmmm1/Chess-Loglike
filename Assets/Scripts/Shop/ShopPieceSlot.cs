using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using DG.Tweening;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class ShopPieceSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Shop Settings")]
    public PieceType pieceType;
    public int cost = 10;

    [Header("References")]
    public Button buyButton;
    public TextMeshProUGUI costText;
    public Image pieceIconImage;
    public Image sealIconImage;

    private TextMeshProUGUI asciiPieceLabel;
    private bool isSoldOut = false;
    private SealData attachedSeal;
    private bool isStandaloneSeal = false;
    private SealData standaloneSeal;
    private bool isArtifact = false;
    private ArtifactData artifactData;

    public SealData AttachedSeal => attachedSeal;
    public bool IsStandaloneSeal => isStandaloneSeal;
    public SealData StandaloneSeal => standaloneSeal;
    public bool IsArtifact => isArtifact;
    public ArtifactData ArtifactData => artifactData;

    private void Start()
    {
        if (buyButton == null) buyButton = GetComponent<Button>();
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnBuyClick);
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (isSoldOut)
        {
            if (costText != null)
            {
                costText.text = "";
                Image parentImg = costText.GetComponentInParent<Image>();
                if (parentImg != null) parentImg.enabled = false;
            }
            if (buyButton != null) buyButton.interactable = false;
            if (pieceIconImage != null)
            {
                pieceIconImage.sprite = null;
                Color c = pieceIconImage.color;
                c.a = 0.5f;
                pieceIconImage.color = c;
            }
            if (sealIconImage != null)
            {
                sealIconImage.enabled = false;
            }
            Image rootImg = GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.enabled = false;
            }
            return;
        }

        Image activeRootImg = GetComponent<Image>();
        if (activeRootImg != null)
        {
            activeRootImg.enabled = true;
        }

        if (costText != null)
        {
            costText.text = $"{cost}";
            Image parentImg = costText.GetComponentInParent<Image>();
            if (parentImg != null) parentImg.enabled = true;
        }

        if (isArtifact)
        {
            if (pieceIconImage != null)
            {
                pieceIconImage.enabled = true;
                pieceIconImage.sprite = artifactData != null ? artifactData.icon : null;
                pieceIconImage.preserveAspect = true;
                Color c = pieceIconImage.color;
                c.a = 1f;
                pieceIconImage.color = c;
            }

            if (sealIconImage != null)
            {
                sealIconImage.enabled = false;
            }

            if (buyButton != null) buyButton.interactable = true;
            return;
        }

        if (isStandaloneSeal)
        {
            if (pieceIconImage != null)
            {
                pieceIconImage.enabled = true;
                pieceIconImage.sprite = standaloneSeal != null ? standaloneSeal.icon : null;
                pieceIconImage.preserveAspect = true;
                Color c = pieceIconImage.color;
                c.a = 1f;
                pieceIconImage.color = c;
            }

            if (sealIconImage != null)
            {
                sealIconImage.enabled = false;
            }

            if (buyButton != null) buyButton.interactable = true;
            return;
        }

        if (pieceIconImage != null && PieceManager.Instance != null)
        {
            pieceIconImage.enabled = true;
            pieceIconImage.sprite = PieceManager.Instance.GetSpriteFor(pieceType);
            pieceIconImage.preserveAspect = true;
            Color c = pieceIconImage.color;
            c.a = 1f;
            pieceIconImage.color = c;
        }

        if (sealIconImage != null)
        {
            if (attachedSeal != null && attachedSeal.icon != null)
            {
                sealIconImage.sprite = attachedSeal.icon;
                sealIconImage.preserveAspect = true;
                sealIconImage.enabled = true;

                var handler = sealIconImage.GetComponent<SealTooltipHandler>();
                if (handler == null) handler = sealIconImage.gameObject.AddComponent<SealTooltipHandler>();
                handler.Initialize(attachedSeal);
                sealIconImage.raycastTarget = true;
            }
            else
            {
                sealIconImage.enabled = false;
            }
        }
        if (buyButton != null) buyButton.interactable = true;
    }

    private void OnBuyClick()
    {
        if (isSoldOut) return;

        if (GameManager.Instance == null) return;

        if (GameManager.Instance.Coin < cost)
        {
            if (buyButton != null)
            {
                ShakeUI();
            }
            return;
        }

        if (isArtifact)
        {
            if (artifactData == null || ArtifactManager.Instance == null)
            {
                if (buyButton != null)
                {
                    ShakeUI();
                }
                return;
            }

            if (GameManager.Instance.UseCoin(cost))
            {
                GameManager.Instance.RecordPurchase();
                BuyArtifact();
                SetSoldOut();
                if (TooltipManager.Instance != null)
                {
                    TooltipManager.Instance.HideTooltip(gameObject);
                }
            }
            return;
        }

        if (isStandaloneSeal)
        {
            if (standaloneSeal == null || SealInventory.Instance == null || !CanAcceptSeal())
            {
                if (buyButton != null)
                {
                    ShakeUI();
                }
                return;
            }

            if (GameManager.Instance.UseCoin(cost))
            {
                GameManager.Instance.RecordPurchase();
                BuyStandaloneSeal();
                SetSoldOut();
                if (TooltipManager.Instance != null)
                {
                    TooltipManager.Instance.HideTooltip(gameObject);
                }
            }
            return;
        }

        InventorySlot emptySlot = FindEmptyInventorySlot();
        if (emptySlot == null)
        {
            if (buyButton != null)
            {
                ShakeUI();
            }
            return;
        }

        if (GameManager.Instance.UseCoin(cost))
        {
            GameManager.Instance.RecordPurchase();
            CreatePieceInSlot(emptySlot);
            SetSoldOut();
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip(gameObject);
            }
        }
    }

    private void BuyArtifact()
    {
        if (artifactData == null || ArtifactManager.Instance == null) return;

        ArtifactData purchasedArtifact = artifactData;

        if (pieceIconImage == null || purchasedArtifact.icon == null)
        {
            ArtifactManager.Instance.AddArtifact(purchasedArtifact);
            return;
        }

        GameObject flyingIcon = new GameObject("FlyingArtifact");
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            flyingIcon.transform.SetParent(canvas.transform, true);
        }
        else
        {
            flyingIcon.transform.SetParent(transform.root, true);
        }

        flyingIcon.transform.position = pieceIconImage.transform.position;
        flyingIcon.transform.localScale = pieceIconImage.transform.localScale;

        Image img = flyingIcon.AddComponent<Image>();
        img.sprite = purchasedArtifact.icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = false;

        if (flyingIcon.transform is RectTransform flyingRect && pieceIconImage.rectTransform != null)
        {
            flyingRect.sizeDelta = pieceIconImage.rectTransform.sizeDelta;
        }

        Vector3 targetPos = transform.position;
        ArtifactSlot incomingSlot = ArtifactManager.Instance.GetIncomingSlot(purchasedArtifact);
        if (incomingSlot != null)
        {
            targetPos = incomingSlot.transform.position;
        }

        Sequence seq = DOTween.Sequence();
        seq.Append(flyingIcon.transform.DOMove(targetPos, 0.7f).SetEase(Ease.InOutQuad));
        seq.Join(flyingIcon.transform.DOScale(0.3f, 0.7f));
        seq.OnComplete(() =>
        {
            if (ArtifactManager.Instance != null)
            {
                ArtifactManager.Instance.AddArtifact(purchasedArtifact);
            }
            if (flyingIcon != null)
            {
                Destroy(flyingIcon);
            }
        });
    }

    private bool CanAcceptSeal()
    {
        if (SealInventory.Instance == null) return false;

        var sealSlots = FindObjectsByType<SealInventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (sealSlots != null && sealSlots.Length > 0)
        {
            int currentCount = SealInventory.Instance.OwnedSeals.Count(s => s != null);
            return currentCount < sealSlots.Length;
        }

        return true;
    }

    private void BuyStandaloneSeal()
    {
        if (standaloneSeal == null || SealInventory.Instance == null) return;

        SealData purchasedSeal = standaloneSeal;
        SealInventory.Instance.AddSeal(purchasedSeal);
        int targetIndex = SealInventory.Instance.GetSealIndex(purchasedSeal);

        var sealSlots = FindObjectsByType<SealInventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(s => s.transform.GetSiblingIndex())
            .ToArray();

        Vector3 targetPos = transform.position;
        if (targetIndex >= 0 && targetIndex < sealSlots.Length)
        {
            targetPos = sealSlots[targetIndex].transform.position;
        }
        else if (sealSlots.Length > 0)
        {
            targetPos = sealSlots[0].transform.position;
        }

        if (pieceIconImage == null || purchasedSeal.icon == null)
        {
            FindFirstObjectByType<SealInventoryPanel>(FindObjectsInactive.Include)?.RefreshSealItems();
            return;
        }

        GameObject flyingIcon = new GameObject("FlyingSeal");
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            flyingIcon.transform.SetParent(canvas.transform, true);
        }
        else
        {
            flyingIcon.transform.SetParent(transform.root, true);
        }

        flyingIcon.transform.position = pieceIconImage.transform.position;
        flyingIcon.transform.localScale = pieceIconImage.transform.localScale;

        Image img = flyingIcon.AddComponent<Image>();
        img.sprite = purchasedSeal.icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = false;

        if (flyingIcon.transform is RectTransform flyingRect && pieceIconImage.rectTransform != null)
        {
            flyingRect.sizeDelta = pieceIconImage.rectTransform.sizeDelta;
        }

        Sequence seq = DOTween.Sequence();
        seq.Append(flyingIcon.transform.DOMove(targetPos, 0.55f).SetEase(Ease.InOutQuad));
        seq.Join(flyingIcon.transform.DOScale(0.6f, 0.55f));
        seq.OnComplete(() =>
        {
            FindFirstObjectByType<SealInventoryPanel>(FindObjectsInactive.Include)?.RefreshSealItems();
            if (flyingIcon != null)
            {
                Destroy(flyingIcon);
            }
        });
    }

    private void ShakeUI()
    {
        buyButton.transform.DOKill(true);

        LayoutElement layoutElement = buyButton.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = buyButton.gameObject.AddComponent<LayoutElement>();

        GameObject placeholder = new GameObject("LayoutPlaceholder");
        placeholder.transform.SetParent(buyButton.transform.parent, false);
        placeholder.transform.SetSiblingIndex(buyButton.transform.GetSiblingIndex());

        RectTransform placeholderRect = placeholder.AddComponent<RectTransform>();
        RectTransform buttonRect = buyButton.GetComponent<RectTransform>();
        placeholderRect.sizeDelta = buttonRect.sizeDelta;

        LayoutElement placeholderLE = placeholder.AddComponent<LayoutElement>();
        placeholderLE.preferredWidth = layoutElement.preferredWidth;
        placeholderLE.preferredHeight = layoutElement.preferredHeight;
        placeholderLE.flexibleWidth = layoutElement.flexibleWidth;
        placeholderLE.flexibleHeight = layoutElement.flexibleHeight;
        placeholderLE.minWidth = layoutElement.minWidth;
        placeholderLE.minHeight = layoutElement.minHeight;

        layoutElement.ignoreLayout = true;

        buyButton.transform.DOShakePosition(0.5f, new Vector3(10f, 0, 0), 20, 90, false, true)
            .OnComplete(() =>
            {
                layoutElement.ignoreLayout = false;
                Destroy(placeholder);
            });
    }

    private InventorySlot FindEmptyInventorySlot()
    {
        var slots = FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);
        var sortedSlots = slots.OrderBy(s => s.transform.GetSiblingIndex()).ToArray();

        return sortedSlots.FirstOrDefault(slot => slot.GetComponentInChildren<PieceController>() == null && !slot.IsReserved);
    }

    private void CreatePieceInSlot(InventorySlot slot)
    {
        if (PieceSpawner.Instance != null)
        {
            slot.IsReserved = true;
            PieceSpawner.Instance.SpawnPieceAndFlyToInventory(pieceType, transform.position, slot, attachedSeal, (piece) => {
                slot.IsReserved = false;
            });
        }
    }

    public void Setup(PieceType type, int itemCost, SealData seal = null)
    {
        isArtifact = false;
        artifactData = null;
        isStandaloneSeal = false;
        standaloneSeal = null;
        pieceType = type;
        cost = itemCost;
        attachedSeal = seal;
        isSoldOut = false;
        CollectionManager.EnsureInstance()?.RecordPieceSeen(pieceType);
        if (seal != null)
        {
            CollectionManager.EnsureInstance()?.RecordSealSeen(seal);
        }
        UpdateUI();
    }

    public void SetupSeal(SealData seal, int itemCost)
    {
        isArtifact = false;
        artifactData = null;
        isStandaloneSeal = true;
        standaloneSeal = seal;
        attachedSeal = null;
        cost = itemCost;
        isSoldOut = false;
        if (seal != null)
        {
            CollectionManager.EnsureInstance()?.RecordSealSeen(seal);
        }
        UpdateUI();
    }

    public void SetupArtifact(ArtifactData artifact, int itemCost)
    {
        isArtifact = true;
        artifactData = artifact;
        isStandaloneSeal = false;
        standaloneSeal = null;
        attachedSeal = null;
        cost = itemCost;
        isSoldOut = false;
        if (artifact != null)
        {
            CollectionManager.EnsureInstance()?.RecordArtifactSeen(artifact);
        }
        UpdateUI();
    }

    public void SetSoldOut()
    {
        isSoldOut = true;
        UpdateUI();
    }

    public void PlaySealEffect(SealRarity rarity)
    {
        Image targetIcon = isStandaloneSeal ? pieceIconImage : sealIconImage;
        if (targetIcon == null) return;

        // 초기화
        targetIcon.transform.DOKill();
        targetIcon.transform.localScale = Vector3.one;
        targetIcon.color = Color.white;

        switch (rarity)
        {
            case SealRarity.Common:
                targetIcon.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 10, 1);
                break;
            case SealRarity.Rare:
                targetIcon.transform.DOPunchScale(Vector3.one * 0.4f, 0.6f, 10, 1);
                SpawnParticles(targetIcon, 6, new Color(0f, 0.5f, 1f), 60f, 0.6f);
                break;
            case SealRarity.Epic:
                targetIcon.transform.DOPunchScale(Vector3.one * 0.5f, 0.8f, 10, 1);
                targetIcon.transform.DOShakeRotation(0.8f, 30f, 10, 90);
                SpawnParticles(targetIcon, 12, new Color(0.8f, 0f, 1f), 90f, 0.8f);
                break;
            case SealRarity.Legendary:
                Sequence seq = DOTween.Sequence();
                seq.Append(targetIcon.transform.DOPunchScale(Vector3.one * 0.7f, 1.0f, 10, 1));
                seq.Join(targetIcon.transform.DOShakeRotation(1.0f, 45f, 10, 90));
                seq.Join(targetIcon.DOColor(new Color(1f, 0.8f, 0f), 0.2f).SetLoops(6, LoopType.Yoyo));
                seq.OnComplete(() => targetIcon.color = Color.white);
                SpawnParticles(targetIcon, 20, new Color(1f, 0.9f, 0.2f), 130f, 1.2f);
                break;
        }
    }

    private void SpawnParticles(Image sourceIcon, int count, Color color, float distance, float duration)
    {
        if (sourceIcon == null) return;

        for (int i = 0; i < count; i++)
        {
            GameObject p = new GameObject("SealParticle");
            p.transform.SetParent(transform, true); // 슬롯을 부모로 설정
            p.transform.position = sourceIcon.transform.position;
            
            Image img = p.AddComponent<Image>();
            if (sourceIcon.sprite != null) img.sprite = sourceIcon.sprite;
            img.preserveAspect = true;
            img.color = color;
            img.raycastTarget = false;

            float scale = UnityEngine.Random.Range(0.3f, 0.6f);
            p.transform.localScale = Vector3.one * scale;

            float angle = UnityEngine.Random.Range(0f, 360f);
            Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0);
            
            p.transform.DOMove(p.transform.position + dir * distance, duration).SetEase(Ease.OutQuad);
            p.transform.DOScale(0f, duration).SetEase(Ease.InQuad);
            p.transform.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(-180f, 180f)), duration);
            img.DOFade(0f, duration).OnComplete(() => Destroy(p));
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null && !isSoldOut)
        {
            if (isArtifact && artifactData != null)
            {
                TooltipManager.Instance.ShowTooltip(
                    artifactData.GetTooltipTitle(),
                    artifactData.GetTooltipDescription(),
                    transform.position,
                    artifactData.flavorText,
                    TooltipManager.TooltipPriorityDefault,
                    gameObject);
            }
            else if (isStandaloneSeal && standaloneSeal != null)
            {
                TooltipManager.Instance.ShowTooltip(
                    standaloneSeal.sealName,
                    SealTooltipHandler.BuildDescription(standaloneSeal),
                    transform.position,
                    standaloneSeal.flavorText,
                    TooltipManager.TooltipPriorityDefault,
                    gameObject);
            }
            else
            {
                TooltipManager.Instance.ShowPieceTooltip(pieceType, attachedSeal, transform.position, gameObject);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }
    }
}
