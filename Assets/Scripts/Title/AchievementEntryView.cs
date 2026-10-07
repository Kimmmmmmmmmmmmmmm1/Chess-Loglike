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

    private void Awake()
    {
        PrepareDisplayOnlyToggle();
    }

    private void OnEnable()
    {
        PrepareDisplayOnlyToggle();
    }

    public void Initialize(AchievementData achievement, int currentCount, bool isUnlocked, string displayProgressText = null, string clearTimeTextValue = null)
    {
        if (achievement == null)
        {
            return;
        }

        currentAchievement = achievement;
        this.currentCount = currentCount;
        this.isUnlocked = isUnlocked;

        if (achievementIcon != null)
        {
            achievementIcon.sprite = achievement.icon;
            achievementIcon.enabled = achievement.icon != null;
        }

        if (titleText != null)
        {
            titleText.text = achievement.achievementName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = achievement.description;
        }

        string progressTooltipText = achievement.GetProgressText(currentCount);

        if (clearTimeText != null)
        {
            clearTimeText.text = isUnlocked ? (clearTimeTextValue ?? string.Empty) : string.Empty;
        }

        if (progressFillImage != null)
        {
            int finalTargetCount = achievement.GetFinalTargetCount();
            float progressRatio = finalTargetCount <= 0
                ? 0f
                : Mathf.Clamp01((float)Mathf.Clamp(currentCount, 0, finalTargetCount) / finalTargetCount);

            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFillImage.fillClockwise = true;
            progressFillImage.fillAmount = progressRatio;
            progressFillImage.raycastTarget = true;

            if (progressTooltipTrigger == null)
            {
                progressTooltipTrigger = progressFillImage.GetComponent<AchievementProgressTooltipTrigger>();
                if (progressTooltipTrigger == null)
                {
                    progressTooltipTrigger = progressFillImage.gameObject.AddComponent<AchievementProgressTooltipTrigger>();
                }
            }

            progressTooltipTrigger.SetProgressText(progressTooltipText);
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