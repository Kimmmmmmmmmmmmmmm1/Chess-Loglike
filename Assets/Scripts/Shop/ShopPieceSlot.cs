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
    public SealData AttachedSeal => attachedSeal;

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

        if (costText != null) costText.text = $"{cost}";

        if (pieceIconImage != null && PieceManager.Instance != null)
        {
            pieceIconImage.sprite = PieceManager.Instance.GetSpriteFor(pieceType);
            Color c = pieceIconImage.color;
            c.a = 1f;
            pieceIconImage.color = c;
        }

        if (sealIconImage != null)
        {
            if (attachedSeal != null && attachedSeal.icon != null)
            {
                sealIconImage.sprite = attachedSeal.icon;
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
        }
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

    public void SetSoldOut()
    {
        isSoldOut = true;
        UpdateUI();
    }

    public void PlaySealEffect(SealRarity rarity)
    {
        if (sealIconImage == null) return;

        sealIconImage.transform.DOKill();
        sealIconImage.transform.localScale = Vector3.one;
        sealIconImage.color = Color.white;

        if (EffectManager.Instance != null)
        {
            string cmd = attachedSeal != null && !string.IsNullOrEmpty(attachedSeal.injectionCommand)
                ? $"[{attachedSeal.injectionCommand.ToUpperInvariant()}]"
                : "[CODE INJECTION DETECTED]";
            EffectManager.Instance.PlayCliInjectionEffect(sealIconImage.transform.position, cmd);
        }

        switch (rarity)
        {
            case SealRarity.Common:
                sealIconImage.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 10, 1);
                break;
            case SealRarity.Rare:
                sealIconImage.transform.DOPunchScale(Vector3.one * 0.4f, 0.6f, 10, 1);
                SpawnGlitchBits(4, new Color(0f, 1f, 0f, 1f), 45f, 0.5f);
                break;
            case SealRarity.Epic:
                sealIconImage.transform.DOPunchScale(Vector3.one * 0.5f, 0.8f, 10, 1);
                sealIconImage.transform.DOShakeRotation(0.8f, 30f, 10, 90);
                SpawnGlitchBits(6, new Color(0f, 1f, 0.6f, 1f), 65f, 0.6f);
                break;
            case SealRarity.Legendary:
                Sequence seq = DOTween.Sequence();
                seq.Append(sealIconImage.transform.DOPunchScale(Vector3.one * 0.7f, 1.0f, 10, 1));
                seq.Join(sealIconImage.transform.DOShakeRotation(1.0f, 45f, 10, 90));
                seq.Join(sealIconImage.DOColor(new Color(0f, 1f, 0f, 1f), 0.2f).SetLoops(6, LoopType.Yoyo));
                seq.OnComplete(() => sealIconImage.color = Color.white);
                SpawnGlitchBits(8, new Color(0f, 1f, 0f, 1f), 85f, 0.8f);
                break;
        }
    }

    private void SpawnGlitchBits(int count, Color color, float distance, float duration)
    {
        string[] tokens = { "0x1", "INJ", "PATCH", ">>" };
        for (int i = 0; i < count; i++)
        {
            GameObject p = new GameObject("InjectionGlitchBit", typeof(RectTransform));
            p.transform.SetParent(transform, true);
            p.transform.position = sealIconImage.transform.position;
            RectTransform rt = p.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(36f, 14f);

            TextMeshProUGUI tmp = p.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }
            tmp.text = tokens[i % tokens.Length];
            tmp.fontSize = 8.5f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;

            float angle = UnityEngine.Random.Range(0f, 360f);
            Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0);

            p.transform.DOMove(p.transform.position + dir * distance, duration).SetEase(Ease.OutQuad);
            tmp.DOFade(0f, duration).OnComplete(() => Destroy(p));
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null && !isSoldOut)
        {
            string movementPattern = PieceController.GenerateMovementPatternForType(pieceType, attachedSeal);
            TooltipManager.Instance.ShowTooltip(
                string.Empty,
                movementPattern,
                transform.position,
                string.Empty,
                TooltipManager.TooltipPriorityPieceMove,
                gameObject);
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
