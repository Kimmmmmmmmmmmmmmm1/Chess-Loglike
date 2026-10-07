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
    [SerializeField] private Color lockedIconColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
    [SerializeField, Range(0.1f, 1f)] private float lockedAlpha = 0.55f;

    [Header("Rarity Colors")]
    [SerializeField] private Color commonRarityColor = new Color(0.75f, 0.78f, 0.8f, 1f);
    [SerializeField] private Color rareRarityColor = new Color(0.25f, 0.62f, 1f, 1f);
    [SerializeField] private Color epicRarityColor = new Color(0.74f, 0.34f, 1f, 1f);
    [SerializeField] private Color legendaryRarityColor = new Color(1f, 0.64f, 0.16f, 1f);
    [SerializeField] private Color lockedRarityColor = new Color(0.22f, 0.22f, 0.22f, 1f);

    [Header("Type Background Colors")]
    [SerializeField] private Color artifactTypeColor = new Color(0.21f, 0.42f, 0.72f, 1f);
    [SerializeField] private Color sealTypeColor = new Color(0.58f, 0.34f, 0.2f, 1f);
    [SerializeField] private Color lockedTypeColor = new Color(0.16f, 0.16f, 0.16f, 1f);

    private string tooltipTitle;
    private string tooltipDescription;
    private string tooltipFlavorText;
    private bool isUnlocked;

    public void InitializeArtifact(ArtifactData artifact, bool unlocked)
    {
        if (artifact == null)
        {
            return;
        }

        isUnlocked = unlocked;
        SetIcon(artifact.icon, unlocked);
        SetRarityColor(GetArtifactRarityColor(artifact.rarity), unlocked);
        SetTypeBackgroundColor(artifactTypeColor, unlocked);

        string title = artifact.GetTooltipTitle();
        string description = artifact.GetTooltipDescription();
        ApplyLockableText(title, description);

        tooltipTitle = unlocked ? title : "???";
        tooltipDescription = unlocked ? description : "아직 발견하지 못한 유물입니다.";
        tooltipFlavorText = unlocked ? artifact.flavorText : string.Empty;
    }

    public void InitializeSeal(SealData seal, bool unlocked)
    {
        if (seal == null)
        {
            return;
        }

        isUnlocked = unlocked;
        SetIcon(seal.icon, unlocked);
        SetRarityColor(GetSealRarityColor(seal.rarity), unlocked);
        SetTypeBackgroundColor(sealTypeColor, unlocked);
        ApplyLockableText(seal.sealName, seal.description);

        tooltipTitle = unlocked ? seal.sealName : "???";
        tooltipDescription = unlocked ? seal.description : "아직 발견하지 못한 인장입니다.";
        tooltipFlavorText = unlocked ? seal.flavorText : string.Empty;
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

    private void SetIcon(Sprite sprite, bool unlocked)
    {
        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
        iconImage.color = unlocked ? unlockedIconColor : lockedIconColor;
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
            typeBackgroundImage.color = unlocked ? typeColor : lockedTypeColor;
        }
    }

    private void ApplyLockableText(string title, string description)
    {
        SetText(nameText, isUnlocked ? title : "???");
        SetText(descriptionText, isUnlocked ? description : "???");

        if (lockedCanvasGroup != null)
        {
            lockedCanvasGroup.alpha = isUnlocked ? 1f : lockedAlpha;
        }
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
}
