using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CollectionEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image rarityImage;
    [SerializeField] private Image typeBackgroundImage;
    [SerializeField] private CanvasGroup lockedCanvasGroup;

    [Header("Display")]
    [SerializeField] private Color unlockedIconColor = Color.white;
    [SerializeField] private Color lockedIconColor = new Color(0.18f, 0.15f, 0.12f, 0.65f);
    [SerializeField, Range(0.1f, 1f)] private float lockedAlpha = 0.78f;

    [Header("Rarity Colors")]
    [SerializeField] private Color commonRarityColor = new Color(0.45f, 0.48f, 0.52f, 1f);
    [SerializeField] private Color rareRarityColor = new Color(0.20f, 0.52f, 0.88f, 1f);
    [SerializeField] private Color epicRarityColor = new Color(0.62f, 0.26f, 0.86f, 1f);
    [SerializeField] private Color legendaryRarityColor = new Color(0.92f, 0.55f, 0.12f, 1f);
    [SerializeField] private Color lockedRarityColor = new Color(0.35f, 0.32f, 0.28f, 0.70f);

    [Header("Type Background Colors")]
    [SerializeField] private Color artifactTypeColor = new Color(0.96f, 0.98f, 1.00f, 1f);
    [SerializeField] private Color sealTypeColor = new Color(1.00f, 0.96f, 0.90f, 1f);
    [SerializeField] private Color pieceTypeColor = new Color(0.94f, 0.98f, 0.94f, 1f);
    [SerializeField] private Color lockedTypeColor = new Color(0.78f, 0.74f, 0.68f, 0.94f);

    private string tooltipTitle;
    private string tooltipDescription;
    private string tooltipFlavorText;
    private CollectionDiscoveryState currentState;
    private TextMeshProUGUI iconFallbackLabel;
    private TextMeshProUGUI typeTagLabel;

    public bool IsUnlocked => currentState == CollectionDiscoveryState.Acquired;
    public CollectionDiscoveryState CurrentState => currentState;

    private void Awake()
    {
        NormalizeCardLayout();
    }

    public void InitializeArtifact(ArtifactData artifact, CollectionDiscoveryState state)
    {
        if (artifact == null)
        {
            return;
        }

        NormalizeCardLayout();
        currentState = state;

        bool hasSeen = state >= CollectionDiscoveryState.Seen;
        bool isAcquired = state == CollectionDiscoveryState.Acquired;
        Color rarityColor = GetArtifactRarityColor(artifact.rarity);

        SetIcon(artifact.icon, hasSeen, "유물", rarityColor);
        SetRarityColor(rarityColor, hasSeen);
        SetTypeBackgroundColor(artifactTypeColor, hasSeen);
        UpdateTypeTag("유물", hasSeen, new Color(0.18f, 0.36f, 0.62f, 0.92f));

        string title = artifact.GetTooltipTitle();
        string description = artifact.GetTooltipDescription();
        ApplyStateText(title, description, state);

        if (isAcquired)
        {
            tooltipTitle = title;
            tooltipDescription = description;
            tooltipFlavorText = artifact.flavorText;
        }
        else if (hasSeen)
        {
            tooltipTitle = title;
            tooltipDescription = "획득하여 효과 설명을 해금하세요.";
            tooltipFlavorText = string.Empty;
        }
        else
        {
            tooltipTitle = "???";
            tooltipDescription = "아직 발견하지 못한 유물입니다.";
            tooltipFlavorText = string.Empty;
        }
    }

    public void InitializeArtifact(ArtifactData artifact, bool unlocked)
    {
        InitializeArtifact(artifact, unlocked ? CollectionDiscoveryState.Acquired : CollectionDiscoveryState.Unseen);
    }

    public void InitializeSeal(SealData seal, CollectionDiscoveryState state)
    {
        if (seal == null)
        {
            return;
        }

        NormalizeCardLayout();
        currentState = state;

        bool hasSeen = state >= CollectionDiscoveryState.Seen;
        bool isAcquired = state == CollectionDiscoveryState.Acquired;
        Color rarityColor = GetSealRarityColor(seal.rarity);

        SetIcon(seal.icon, hasSeen, "인장", rarityColor);
        SetRarityColor(rarityColor, hasSeen);
        SetTypeBackgroundColor(sealTypeColor, hasSeen);
        UpdateTypeTag("인장", hasSeen, new Color(0.56f, 0.32f, 0.16f, 0.92f));
        ApplyStateText(seal.sealName, seal.description, state);

        if (isAcquired)
        {
            tooltipTitle = seal.sealName;
            tooltipDescription = seal.description;
            tooltipFlavorText = seal.flavorText;
        }
        else if (hasSeen)
        {
            tooltipTitle = seal.sealName;
            tooltipDescription = "획득하여 효과 설명을 해금하세요.";
            tooltipFlavorText = string.Empty;
        }
        else
        {
            tooltipTitle = "???";
            tooltipDescription = "아직 발견하지 못한 인장입니다.";
            tooltipFlavorText = string.Empty;
        }
    }

    public void InitializeSeal(SealData seal, bool unlocked)
    {
        InitializeSeal(seal, unlocked ? CollectionDiscoveryState.Acquired : CollectionDiscoveryState.Unseen);
    }

    public void InitializePiece(PieceData piece, CollectionDiscoveryState state)
    {
        if (piece == null)
        {
            return;
        }

        NormalizeCardLayout();
        currentState = state;

        bool hasSeen = state >= CollectionDiscoveryState.Seen;
        bool isAcquired = state == CollectionDiscoveryState.Acquired;
        Color rarityColor = GetPieceRarityColor(piece.pieceType);

        SetIcon(piece.icon, hasSeen, "기물", rarityColor);
        SetRarityColor(rarityColor, hasSeen);
        SetTypeBackgroundColor(pieceTypeColor, hasSeen);
        UpdateTypeTag("기물", hasSeen, new Color(0.20f, 0.50f, 0.32f, 0.92f));
        ApplyStateText(piece.pieceName, piece.description, state);

        if (isAcquired)
        {
            tooltipTitle = piece.pieceName;
            tooltipDescription = $"{piece.description}\n\n{PieceController.GenerateMovementPatternForType(piece.pieceType)}";
            tooltipFlavorText = piece.flavorText;
        }
        else if (hasSeen)
        {
            tooltipTitle = piece.pieceName;
            tooltipDescription = "획득하여 효과 설명을 해금하세요.";
            tooltipFlavorText = string.Empty;
        }
        else
        {
            tooltipTitle = "???";
            tooltipDescription = "아직 발견하지 못한 기물입니다.";
            tooltipFlavorText = string.Empty;
        }
    }

    public void InitializePiece(PieceData piece, bool unlocked)
    {
        InitializePiece(piece, unlocked ? CollectionDiscoveryState.Acquired : CollectionDiscoveryState.Unseen);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance == null)
        {
            return;
        }

        TooltipManager.Instance.ShowTooltip(tooltipTitle, tooltipDescription, transform.position, tooltipFlavorText);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Instance?.HideTooltip();
    }

    private void NormalizeCardLayout()
    {
        if (transform is RectTransform rootRect)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;
        }

        if (iconImage != null && iconImage.rectTransform != null)
        {
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, 68f);
            iconImage.rectTransform.sizeDelta = new Vector2(84f, 84f);
        }

        if (rarityImage != null && rarityImage.rectTransform != null)
        {
            rarityImage.rectTransform.anchoredPosition = new Vector2(-94f, 6f);
            rarityImage.rectTransform.sizeDelta = new Vector2(8f, 24f);
        }

        if (nameText != null && nameText.rectTransform != null)
        {
            nameText.rectTransform.anchoredPosition = new Vector2(4f, 6f);
            nameText.rectTransform.sizeDelta = new Vector2(184f, 30f);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 14f;
            nameText.fontSizeMax = 22f;
        }

        if (descriptionText != null && descriptionText.rectTransform != null)
        {
            descriptionText.rectTransform.anchoredPosition = new Vector2(0f, -66f);
            descriptionText.rectTransform.sizeDelta = new Vector2(198f, 102f);
            descriptionText.enableAutoSizing = true;
            descriptionText.fontSizeMin = 13f;
            descriptionText.fontSizeMax = 18f;
            descriptionText.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void SetIcon(Sprite sprite, bool unlocked, string typeLabel, Color rarityColor)
    {
        if (iconImage == null)
        {
            return;
        }

        bool hasCustomSprite = sprite != null && unlocked;
        iconImage.sprite = hasCustomSprite ? sprite : null;
        iconImage.enabled = true;

        if (hasCustomSprite)
        {
            iconImage.color = unlockedIconColor;
        }
        else if (unlocked)
        {
            iconImage.color = new Color(
                Mathf.Lerp(0.22f, rarityColor.r, 0.35f),
                Mathf.Lerp(0.18f, rarityColor.g, 0.35f),
                Mathf.Lerp(0.14f, rarityColor.b, 0.35f),
                0.85f);
        }
        else
        {
            iconImage.color = new Color(0.20f, 0.17f, 0.14f, 0.45f);
        }

        EnsureIconFallbackLabel();
        if (iconFallbackLabel != null)
        {
            if (hasCustomSprite)
            {
                iconFallbackLabel.text = string.Empty;
            }
            else if (unlocked)
            {
                iconFallbackLabel.text = typeLabel ?? string.Empty;
                iconFallbackLabel.fontSize = 22f;
                iconFallbackLabel.color = new Color(1f, 0.96f, 0.88f, 0.95f);
            }
            else
            {
                iconFallbackLabel.text = "?";
                iconFallbackLabel.fontSize = 36f;
                iconFallbackLabel.color = new Color(0.92f, 0.88f, 0.80f, 0.75f);
            }
        }
    }

    private void EnsureIconFallbackLabel()
    {
        if (iconFallbackLabel != null || iconImage == null)
        {
            return;
        }

        Transform existing = iconImage.transform.Find("FallbackLabel");
        if (existing != null)
        {
            iconFallbackLabel = existing.GetComponent<TextMeshProUGUI>();
            return;
        }

        GameObject labelObj = new GameObject("FallbackLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(iconImage.transform, false);

        RectTransform rect = labelObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        iconFallbackLabel = labelObj.GetComponent<TextMeshProUGUI>();
        if (nameText != null && nameText.font != null)
        {
            iconFallbackLabel.font = nameText.font;
        }
        iconFallbackLabel.alignment = TextAlignmentOptions.Center;
        iconFallbackLabel.raycastTarget = false;
    }

    private void UpdateTypeTag(string tagText, bool unlocked, Color badgeColor)
    {
        if (typeTagLabel == null)
        {
            Transform existing = transform.Find("TypeTag");
            if (existing != null)
            {
                typeTagLabel = existing.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                GameObject tagObj = new GameObject("TypeTag", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                tagObj.transform.SetParent(transform, false);

                RectTransform rect = tagObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-16f, -14f);
                rect.sizeDelta = new Vector2(56f, 22f);

                typeTagLabel = tagObj.GetComponent<TextMeshProUGUI>();
                if (nameText != null && nameText.font != null)
                {
                    typeTagLabel.font = nameText.font;
                }
                typeTagLabel.fontSize = 14f;
                typeTagLabel.alignment = TextAlignmentOptions.TopRight;
                typeTagLabel.raycastTarget = false;
            }
        }

        if (typeTagLabel != null)
        {
            typeTagLabel.text = tagText;
            typeTagLabel.color = unlocked ? badgeColor : new Color(0.32f, 0.28f, 0.24f, 0.75f);
        }
    }

    private void SetRarityColor(Color rarityColor, bool unlocked)
    {
        if (rarityImage != null)
        {
            rarityImage.color = unlocked ? rarityColor : lockedRarityColor;
        }
    }

    private void SetTypeBackgroundColor(Color typeColor, bool unlocked)
    {
        if (typeBackgroundImage != null)
        {
            // Keep light parchment tint even if old dark inspector colors were serialized
            Color resolvedUnlockedColor = (typeColor.r + typeColor.g + typeColor.b < 1.8f)
                ? new Color(0.98f, 0.96f, 0.92f, 1f)
                : typeColor;
            Color resolvedLockedColor = (lockedTypeColor.r + lockedTypeColor.g + lockedTypeColor.b < 1.2f)
                ? new Color(0.78f, 0.74f, 0.68f, 0.94f)
                : lockedTypeColor;

            typeBackgroundImage.color = unlocked ? resolvedUnlockedColor : resolvedLockedColor;
        }
    }

    private void ApplyStateText(string title, string description, CollectionDiscoveryState state)
    {
        bool hasSeen = state >= CollectionDiscoveryState.Seen;
        bool isAcquired = state == CollectionDiscoveryState.Acquired;

        SetText(nameText, hasSeen ? title : "???");

        if (isAcquired)
        {
            SetText(descriptionText, description);
            if (descriptionText != null)
            {
                descriptionText.alignment = TextAlignmentOptions.TopLeft;
                descriptionText.enableAutoSizing = true;
                descriptionText.fontSizeMin = 13f;
                descriptionText.fontSizeMax = 18f;
                descriptionText.color = new Color(0.18f, 0.14f, 0.10f, 1f);
            }
        }
        else
        {
            SetText(descriptionText, "?");
            if (descriptionText != null)
            {
                descriptionText.alignment = TextAlignmentOptions.Center;
                descriptionText.enableAutoSizing = false;
                descriptionText.fontSize = 28f;
                descriptionText.color = new Color(0.32f, 0.28f, 0.24f, 0.80f);
            }
        }

        if (nameText != null)
        {
            nameText.color = hasSeen
                ? new Color(0.12f, 0.09f, 0.05f, 1f)
                : new Color(0.28f, 0.24f, 0.20f, 0.85f);
        }

        if (lockedCanvasGroup != null)
        {
            lockedCanvasGroup.alpha = isAcquired ? 1f : (hasSeen ? 0.95f : lockedAlpha);
        }
    }

    private void ApplyLockableText(string title, string description, string lockedDescription)
    {
        ApplyStateText(title, description, currentState);
    }

    private void SetText(TextMeshProUGUI targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value ?? string.Empty;
        }
    }

    private Color GetArtifactRarityColor(ArtifactRarity rarity)
    {
        switch (rarity)
        {
            case ArtifactRarity.Rare:
                return rareRarityColor;
            case ArtifactRarity.Epic:
                return epicRarityColor;
            case ArtifactRarity.Legendary:
                return legendaryRarityColor;
            default:
                return commonRarityColor;
        }
    }

    private Color GetSealRarityColor(SealRarity rarity)
    {
        switch (rarity)
        {
            case SealRarity.Rare:
                return rareRarityColor;
            case SealRarity.Epic:
                return epicRarityColor;
            case SealRarity.Legendary:
                return legendaryRarityColor;
            default:
                return commonRarityColor;
        }
    }

    private Color GetPieceRarityColor(PieceType type)
    {
        return type switch
        {
            PieceType.King => legendaryRarityColor,
            PieceType.Queen => epicRarityColor,
            PieceType.Rook => rareRarityColor,
            PieceType.Bishop => rareRarityColor,
            PieceType.Knight => rareRarityColor,
            _ => commonRarityColor
        };
    }
}
