using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementPanelView : MonoBehaviour
{
    [SerializeField] private AchievementEntryView entryPrefab;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Category Filter")]
    [SerializeField] private ToggleGroup categoryToggleGroup;
    [SerializeField] private Toggle allToggle;
    [SerializeField] private Toggle combatToggle;
    [SerializeField] private Toggle collectionToggle;
    [SerializeField] private Toggle otherToggle;

    [Header("Overall Progress")]
    [SerializeField] private TextMeshProUGUI overallProgressText;
    [SerializeField] private Image overallProgressFillImage;

    [Header("Category Toggle Sprites")]
    [SerializeField] private Sprite selectedTabSprite;
    [SerializeField] private Sprite unselectedTabSprite;

    private bool isBindingToggles;
    private readonly List<AchievementData> filteredAchievements = new List<AchievementData>();

    private void Awake()
    {
        BindToggles();
        SelectAllCategoryWithoutRefresh();
    }

    private void OnEnable()
    {
        SubscribeAchievementEvents();

        if (EnsureCategorySelection())
        {
            Refresh();
        }

        Refresh();
        UpdateCategoryToggleVisuals();
        UpdateOverallProgressDisplay();
    }

    private void LateUpdate()
    {
        if (EnsureCategorySelection())
        {
            Refresh();
        }

        UpdateCategoryToggleVisuals();
        UpdateOverallProgressDisplay();
    }

    private void OnDestroy()
    {
        UnsubscribeAchievementEvents();
        UnbindToggles();
    }

    private void OnDisable()
    {
        UnsubscribeAchievementEvents();
    }

    public void Refresh()
    {
        if (contentRoot == null || entryPrefab == null)
        {
            return;
        }

        AchievementManager achievementManager = AchievementManager.Instance;
        if (achievementManager == null)
        {
            ClearContent();
            filteredAchievements.Clear();
            UpdateOverallProgressDisplay();
            return;
        }

        BuildFilteredAchievements(achievementManager);

        ClearContent();
        RenderCurrentPage(achievementManager);

        UpdateOverallProgressDisplay();
    }

    private void BuildFilteredAchievements(AchievementManager achievementManager)
    {
        filteredAchievements.Clear();
        IReadOnlyList<AchievementData> achievements = achievementManager.GetVisibleAchievements(GetSelectedCategoryFilter());
        for (int i = 0; i < achievements.Count; i++)
        {
            AchievementData achievement = achievements[i];
            if (achievement == null)
            {
                continue;
            }

            filteredAchievements.Add(achievement);
        }
    }

    private void RenderCurrentPage(AchievementManager achievementManager)
    {
        int achievementCount = filteredAchievements.Count;

        for (int i = 0; i < achievementCount; i++)
        {
            AchievementData achievement = filteredAchievements[i];
            if (achievement == null)
            {
                continue;
            }

            AchievementEntryView entry = Instantiate(entryPrefab, contentRoot, false);
            int currentCount = achievementManager.GetCurrentCount(achievement.id);
            bool isUnlocked = achievementManager.IsUnlocked(achievement.id);
            string displayProgressText = achievementManager.GetDisplayProgressText(achievement.id);
            string clearTimeText = achievementManager.GetClearTimeText(achievement.id);
            entry.Initialize(achievement, currentCount, isUnlocked, displayProgressText, clearTimeText);
        }
    }

    private void ClearContent()
    {
        if (contentRoot == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject childObj = contentRoot.GetChild(i).gameObject;
            childObj.SetActive(false);
            Destroy(childObj);
        }
    }

    private void OnFilterChanged()
    {
        if (isBindingToggles) return;

        Refresh();
        UpdateCategoryToggleVisuals();
        ResetScrollToTop();
    }

    private void BindToggles()
    {
        ResolveCategoryToggleGroup();

        if (categoryToggleGroup != null)
        {
            categoryToggleGroup.allowSwitchOff = false;
        }

        AssignGroup(allToggle);
        AssignGroup(combatToggle);
        AssignGroup(collectionToggle);
        AssignGroup(otherToggle);

        SetToggleListener(allToggle, (isOn) => { if(isOn) OnFilterChanged(); });
        SetToggleListener(combatToggle, (isOn) => { if(isOn) OnFilterChanged(); });
        SetToggleListener(collectionToggle, (isOn) => { if(isOn) OnFilterChanged(); });
        SetToggleListener(otherToggle, (isOn) => { if(isOn) OnFilterChanged(); });
    }

    private void UnbindToggles()
    {
        SetToggleListener(allToggle, (isOn) => { if(isOn) OnFilterChanged(); }, false);
        SetToggleListener(combatToggle, (isOn) => { if(isOn) OnFilterChanged(); }, false);
        SetToggleListener(collectionToggle, (isOn) => { if(isOn) OnFilterChanged(); }, false);
        SetToggleListener(otherToggle, (isOn) => { if(isOn) OnFilterChanged(); }, false);
    }

    private void SetToggleListener(Toggle toggle, UnityEngine.Events.UnityAction<bool> callback, bool add = true)
    {
        if (toggle == null)
        {
            return;
        }

        if (add)
        {
            toggle.onValueChanged.AddListener(callback);
        }
        else
        {
            toggle.onValueChanged.RemoveListener(callback);
        }
    }

    private void AssignGroup(Toggle toggle)
    {
        if (toggle == null || categoryToggleGroup == null)
        {
            return;
        }

        toggle.group = categoryToggleGroup;
    }

    private void ResolveCategoryToggleGroup()
    {
        if (categoryToggleGroup != null)
        {
            return;
        }

        if (allToggle != null && allToggle.group != null)
        {
            categoryToggleGroup = allToggle.group;
            return;
        }

        if (combatToggle != null && combatToggle.group != null)
        {
            categoryToggleGroup = combatToggle.group;
            return;
        }

        if (collectionToggle != null && collectionToggle.group != null)
        {
            categoryToggleGroup = collectionToggle.group;
            return;
        }

        if (otherToggle != null && otherToggle.group != null)
        {
            categoryToggleGroup = otherToggle.group;
            return;
        }

        categoryToggleGroup = GetComponentInChildren<ToggleGroup>(true);
    }

    private void SelectAllCategoryWithoutRefresh()
    {
        isBindingToggles = true;

        if (allToggle != null)
        {
            allToggle.isOn = true;
        }

        if (combatToggle != null)
        {
            combatToggle.isOn = false;
        }

        if (collectionToggle != null)
        {
            collectionToggle.isOn = false;
        }

        if (otherToggle != null)
        {
            otherToggle.isOn = false;
        }

        isBindingToggles = false;
        UpdateCategoryToggleVisuals();
    }

    private bool EnsureCategorySelection()
    {
        bool hasSelection =
            (allToggle != null && allToggle.isOn) ||
            (combatToggle != null && combatToggle.isOn) ||
            (collectionToggle != null && collectionToggle.isOn) ||
            (otherToggle != null && otherToggle.isOn);

        if (!hasSelection)
        {
            SelectAllCategoryWithoutRefresh();
            return true;
        }

        return false;
    }

    private AchievementCategory? GetSelectedCategoryFilter()
    {
        if (combatToggle != null && combatToggle.isOn)
        {
            return AchievementCategory.Combat;
        }

        if (collectionToggle != null && collectionToggle.isOn)
        {
            return AchievementCategory.Collection;
        }

        if (otherToggle != null && otherToggle.isOn)
        {
            return AchievementCategory.Other;
        }

        return null;
    }

    private void UpdateOverallProgressDisplay()
    {
        AchievementManager achievementManager = AchievementManager.Instance;
        if (achievementManager == null)
        {
            if (overallProgressText != null)
            {
                overallProgressText.text = "0 / 0 (0%)";
            }

            if (overallProgressFillImage != null)
            {
                overallProgressFillImage.type = Image.Type.Filled;
                overallProgressFillImage.fillMethod = Image.FillMethod.Horizontal;
                overallProgressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                overallProgressFillImage.fillClockwise = true;
                overallProgressFillImage.fillAmount = 0f;
            }

            return;
        }

        int totalCount = achievementManager.GetTotalAchievementCount();
        int unlockedCount = achievementManager.GetUnlockedAchievementCount();
        float completionRatio = achievementManager.GetOverallCompletionRatio();
        int completionPercent = Mathf.RoundToInt(completionRatio * 100f);

        if (overallProgressText != null)
        {
            overallProgressText.text = $"{unlockedCount} / {totalCount} ({completionPercent}%)";
        }

        if (overallProgressFillImage != null)
        {
            overallProgressFillImage.type = Image.Type.Filled;
            overallProgressFillImage.fillMethod = Image.FillMethod.Horizontal;
            overallProgressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            overallProgressFillImage.fillClockwise = true;
            overallProgressFillImage.fillAmount = completionRatio;
        }
    }

    private void UpdateCategoryToggleVisuals()
    {
        ApplyToggleSprite(allToggle);
        ApplyToggleSprite(combatToggle);
        ApplyToggleSprite(collectionToggle);
        ApplyToggleSprite(otherToggle);
    }

    private void ApplyToggleSprite(Toggle toggle)
    {
        if (toggle == null)
        {
            return;
        }

        Image image = toggle.targetGraphic as Image;
        if (image == null)
        {
            image = toggle.GetComponent<Image>();
        }

        if (image != null)
        {
            image.sprite = toggle.isOn ? selectedTabSprite : unselectedTabSprite;
        }
    }

    private void ResetScrollToTop()
    {
        ScrollRect targetScrollRect = ResolveScrollRect();
        if (targetScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        targetScrollRect.StopMovement();
        targetScrollRect.verticalNormalizedPosition = 1f;
    }

    private ScrollRect ResolveScrollRect()
    {
        if (scrollRect != null)
        {
            return scrollRect;
        }

        if (contentRoot != null)
        {
            scrollRect = contentRoot.GetComponentInParent<ScrollRect>();
        }

        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        return scrollRect;
    }

    private void SubscribeAchievementEvents()
    {
        AchievementManager manager = AchievementManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.OnAchievementUnlocked -= OnAchievementStateChanged;
        manager.OnAchievementUnlocked += OnAchievementStateChanged;
        manager.OnAchievementProgressChanged -= OnAchievementProgressChanged;
        manager.OnAchievementProgressChanged += OnAchievementProgressChanged;
    }

    private void UnsubscribeAchievementEvents()
    {
        AchievementManager manager = AchievementManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.OnAchievementUnlocked -= OnAchievementStateChanged;
        manager.OnAchievementProgressChanged -= OnAchievementProgressChanged;
    }

    private void OnAchievementStateChanged(AchievementData _)
    {
        Refresh();
    }

    private void OnAchievementProgressChanged(AchievementData _, int __, int ___)
    {
        Refresh();
    }
}
