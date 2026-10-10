using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementEntryView : MonoBehaviour
{
    [SerializeField] private Image achievementIcon;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI clearTimeText;
    [SerializeField] private Image progressFillImage;
    // Difficulty will be shown as a colored icon whose color represents difficulty level
    [SerializeField] private UnityEngine.UI.Image difficultyIcon;
    [SerializeField] private Toggle unlockedToggle;

    private AchievementData currentAchievement;
    private int currentCount;
    private bool isUnlocked;
    private AchievementProgressTooltipTrigger progressTooltipTrigger;
    private TextMeshProUGUI progressValueLabel;
    private TextMeshProUGUI unlockedStatusLabel;

    private void Awake()
    {
        ResolveFallbackReferences();
        PrepareDisplayOnlyToggle();
    }

    private void OnEnable()
    {
        ResolveFallbackReferences();
        PrepareDisplayOnlyToggle();
    }

    private void ResolveFallbackReferences()
    {
        if (achievementIcon == null)
        {
            Transform iconTransform = transform.Find("Image");
            if (iconTransform != null)
            {
                achievementIcon = iconTransform.GetComponent<Image>();
            }
        }
    }

    public void Initialize(AchievementData achievement, int currentCount, bool isUnlocked, string displayProgressText = null, string clearTimeTextValue = null)
    {
        if (achievement == null)
        {
            return;
        }

        ResolveFallbackReferences();

        currentAchievement = achievement;
        this.currentCount = currentCount;
        this.isUnlocked = isUnlocked;

        bool hasIcon = achievement.icon != null;
        if (achievementIcon != null)
        {
            achievementIcon.sprite = achievement.icon;
            achievementIcon.enabled = hasIcon;
            achievementIcon.gameObject.SetActive(hasIcon);
        }

        ApplyContentHorizontalLayout(hasIcon);

        if (titleText != null)
        {
            titleText.text = achievement.achievementName;
            titleText.color = isUnlocked ? new Color(0.12f, 0.09f, 0.05f, 1f) : new Color(0.22f, 0.18f, 0.14f, 0.92f);
        }

        if (descriptionText != null)
        {
            descriptionText.text = achievement.description;
            descriptionText.color = isUnlocked ? new Color(0.18f, 0.14f, 0.10f, 1f) : new Color(0.28f, 0.24f, 0.19f, 0.88f);
        }

        string progressTextValue = !string.IsNullOrEmpty(displayProgressText)
            ? displayProgressText
            : achievement.GetProgressText(currentCount);

        if (clearTimeText != null)
        {
            clearTimeText.text = isUnlocked ? (clearTimeTextValue ?? string.Empty) : string.Empty;
            clearTimeText.color = new Color(0.26f, 0.20f, 0.13f, 0.85f);
        }

        if (progressFillImage != null)
        {
            int finalTargetCount = achievement.GetFinalTargetCount();
            float progressRatio = isUnlocked
                ? 1f
                : (finalTargetCount <= 0
                    ? 0f
                    : Mathf.Clamp01((float)Mathf.Clamp(currentCount, 0, finalTargetCount) / finalTargetCount));

            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFillImage.fillClockwise = true;
            progressFillImage.fillAmount = progressRatio;
            progressFillImage.color = isUnlocked
                ? new Color(0.28f, 0.68f, 0.34f, 0.95f)
                : new Color(0.78f, 0.56f, 0.20f, 0.95f);
            progressFillImage.raycastTarget = true;

            if (progressFillImage.transform.parent != null)
            {
                Image gaugeBg = progressFillImage.transform.parent.GetComponent<Image>();
                if (gaugeBg != null)
                {
                    gaugeBg.color = new Color(0.18f, 0.14f, 0.10f, 0.82f);
                }
            }

            EnsureProgressValueLabel();
            if (progressValueLabel != null)
            {
                progressValueLabel.text = progressTextValue;
            }

            if (progressTooltipTrigger == null)
            {
                progressTooltipTrigger = progressFillImage.GetComponent<AchievementProgressTooltipTrigger>();
                if (progressTooltipTrigger == null)
                {
                    progressTooltipTrigger = progressFillImage.gameObject.AddComponent<AchievementProgressTooltipTrigger>();
                }
            }

            progressTooltipTrigger.SetProgressText(progressTextValue);
        }

        // color the difficulty icon according to the difficulty value
        if (difficultyIcon != null)
        {
            difficultyIcon.color = GetColorForDifficulty(achievement.difficulty);
            difficultyIcon.enabled = true;
        }

        if (unlockedToggle != null)
        {
            PrepareDisplayOnlyToggle();
            unlockedToggle.SetIsOnWithoutNotify(isUnlocked);
            unlockedToggle.interactable = false;
            UpdateUnlockedBadgeVisual(isUnlocked);
        }

        Image cardBg = GetComponent<Image>();
        if (cardBg != null)
        {
            cardBg.color = isUnlocked
                ? Color.white
                : new Color(0.92f, 0.89f, 0.84f, 0.96f);
        }
    }

    private void ApplyContentHorizontalLayout(bool hasIcon)
    {
        float difficultyX = hasIcon ? 180f : 30f;
        float contentX = hasIcon ? 198f : 50f;
        float contentWidth = hasIcon ? 450f : 596f;

        if (difficultyIcon != null)
        {
            RectTransform diffRect = difficultyIcon.rectTransform;
            diffRect.anchoredPosition = new Vector2(difficultyX, -17f);
        }

        if (titleText != null)
        {
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchoredPosition = new Vector2(contentX, -14f);
            titleRect.sizeDelta = new Vector2(contentWidth - 110f, titleRect.sizeDelta.y);
        }

        if (descriptionText != null)
        {
            RectTransform descRect = descriptionText.rectTransform;
            descRect.anchoredPosition = new Vector2(contentX, -6f);
            descRect.sizeDelta = new Vector2(contentWidth, descRect.sizeDelta.y);
        }

        if (progressFillImage != null && progressFillImage.transform.parent is RectTransform gaugeRect)
        {
            gaugeRect.anchoredPosition = new Vector2(contentX, 34f);
            gaugeRect.sizeDelta = new Vector2(contentWidth, 24f);
        }
    }

    private void EnsureProgressValueLabel()
    {
        if (progressValueLabel != null || progressFillImage == null || progressFillImage.transform.parent == null)
        {
            return;
        }

        Transform gaugeRoot = progressFillImage.transform.parent;
        Transform existing = gaugeRoot.Find("ProgressValueText");
        if (existing != null)
        {
            progressValueLabel = existing.GetComponent<TextMeshProUGUI>();
            return;
        }

        GameObject labelObj = new GameObject("ProgressValueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(gaugeRoot, false);

        RectTransform rect = labelObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        progressValueLabel = labelObj.GetComponent<TextMeshProUGUI>();
        if (titleText != null && titleText.font != null)
        {
            progressValueLabel.font = titleText.font;
        }
        progressValueLabel.fontSize = 16f;
        progressValueLabel.alignment = TextAlignmentOptions.Center;
        progressValueLabel.color = new Color(1f, 0.98f, 0.92f, 0.96f);
        progressValueLabel.raycastTarget = false;
    }

    private void UpdateUnlockedBadgeVisual(bool unlocked)
    {
        if (unlockedToggle == null)
        {
            return;
        }

        // Prevent Unity Toggle from hiding the background graphic when isOn == false
        Graphic toggleGraphic = unlockedToggle.graphic;
        unlockedToggle.graphic = null;

        Transform bgTransform = unlockedToggle.transform.Find("Background");
        Image bgImage = bgTransform != null ? bgTransform.GetComponent<Image>() : (toggleGraphic as Image);
        if (bgImage != null)
        {
            bgImage.enabled = true;
            bgImage.color = unlocked
                ? new Color(0.24f, 0.62f, 0.30f, 0.92f)
                : new Color(0.20f, 0.16f, 0.12f, 0.38f);
        }

        if (unlockedStatusLabel == null)
        {
            Transform existing = unlockedToggle.transform.Find("StatusLabel");
            if (existing != null)
            {
                unlockedStatusLabel = existing.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                GameObject labelObj = new GameObject("StatusLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelObj.transform.SetParent(unlockedToggle.transform, false);

                RectTransform rect = labelObj.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;

                unlockedStatusLabel = labelObj.GetComponent<TextMeshProUGUI>();
                if (titleText != null && titleText.font != null)
                {
                    unlockedStatusLabel.font = titleText.font;
                }
                unlockedStatusLabel.fontSize = 18f;
                unlockedStatusLabel.alignment = TextAlignmentOptions.Center;
                unlockedStatusLabel.raycastTarget = false;
            }
        }

        if (unlockedStatusLabel != null)
        {
            unlockedStatusLabel.text = unlocked ? "달성" : "미달성";
            unlockedStatusLabel.fontSize = unlocked ? 18f : 15f;
            unlockedStatusLabel.color = unlocked
                ? new Color(1f, 0.98f, 0.90f, 1f)
                : new Color(0.92f, 0.88f, 0.82f, 0.78f);
        }
    }

    private void PrepareDisplayOnlyToggle()
    {
        if (unlockedToggle == null)
        {
            return;
        }

        // Keep this toggle fully independent from category filters.
        unlockedToggle.group = null;
        unlockedToggle.toggleTransition = Toggle.ToggleTransition.None;
        unlockedToggle.navigation = new Navigation { mode = Navigation.Mode.None };
        unlockedToggle.interactable = false;
    }

    private Color GetColorForDifficulty(int difficulty)
    {
        // simple palette: 0 (easy) -> green, 1 -> cyan, 2 -> yellow, 3 -> orange, 4+ -> red
        switch (Mathf.Clamp(difficulty, 0, 4))
        {
            case 0: return new Color(0.2f, 0.8f, 0.2f); // green
            case 1: return new Color(0.2f, 0.8f, 0.9f); // cyan
            case 2: return new Color(1f, 0.85f, 0.2f); // yellow
            case 3: return new Color(1f, 0.6f, 0.2f); // orange
            default: return new Color(1f, 0.3f, 0.3f); // red
        }
    }
}