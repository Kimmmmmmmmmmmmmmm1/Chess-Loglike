using System.Collections;
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

    [Header("Entry Animation")]
    [SerializeField] private PanelAnimator panelAnimator;
    [SerializeField] private bool animateEntriesOnRefresh = true;
    [SerializeField] private float entryFadeDuration = 0.2f;
    [SerializeField] private float entryStaggerInterval = 0.03f;
    [SerializeField] private float entryStartScale = 0.94f;

    private bool isBindingToggles;
    private readonly List<AchievementData> filteredAchievements = new List<AchievementData>();
    private readonly List<RectTransform> spawnedEntries = new List<RectTransform>();
    private readonly List<Coroutine> activeEntryAnimations = new List<Coroutine>();

    private void Awake()
    {
        if (panelAnimator == null)
        {
            panelAnimator = GetComponent<PanelAnimator>();
        }

        EnsureTabAndButtonLabels();
        BindToggles();
        SelectAllCategoryWithoutRefresh();
    }

    private void OnEnable()
    {
        EnsureTabAndButtonLabels();
        AchievementManager.EnsureInstance();
        SubscribeAchievementEvents();

        EnsureCategorySelection();
        Refresh();
        UpdateCategoryToggleVisuals();
        UpdateOverallProgressDisplay();
        ResetScrollToTop();
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
        StopEntryAnimations();
        UnsubscribeAchievementEvents();
        UnbindToggles();
    }

    private void OnDisable()
    {
        StopEntryAnimations();
        UnsubscribeAchievementEvents();
    }

    public void Refresh()
    {
        if (contentRoot == null || entryPrefab == null)
        {
            return;
        }

        AchievementManager achievementManager = AchievementManager.Instance != null
            ? AchievementManager.Instance
            : AchievementManager.EnsureInstance();
        if (achievementManager == null)
        {
            ClearContent();
            filteredAchievements.Clear();
            UpdateOverallProgressDisplay();
            return;
        }

        BuildFilteredAchievements(achievementManager);

        StopEntryAnimations();
        ClearContent();
        RenderCurrentPage(achievementManager);
        AnimateSpawnedEntries();

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

            if (entry.transform is RectTransform entryRect)
            {
                spawnedEntries.Add(entryRect);
            }
        }
    }

    private void ClearContent()
    {
        spawnedEntries.Clear();

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

    private void AnimateSpawnedEntries()
    {
        if (!animateEntriesOnRefresh || !isActiveAndEnabled || spawnedEntries.Count == 0)
        {
            return;
        }

        float baseDelay = (panelAnimator != null && panelAnimator.IsOpening)
            ? panelAnimator.TotalOpenDuration * 0.72f
            : 0f;

        int maxAnimatedCount = Mathf.Min(spawnedEntries.Count, 12);
        for (int i = 0; i < maxAnimatedCount; i++)
        {
            RectTransform entryRect = spawnedEntries[i];
            if (entryRect == null)
            {
                continue;
            }

            float delay = baseDelay + i * Mathf.Max(0f, entryStaggerInterval);
            Coroutine routine = StartCoroutine(AnimateEntryRoutine(entryRect, delay));
            activeEntryAnimations.Add(routine);
        }
    }

    private IEnumerator AnimateEntryRoutine(RectTransform entryRect, float delay)
    {
        if (entryRect == null)
        {
            yield break;
        }

        CanvasGroup cg = entryRect.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = entryRect.gameObject.AddComponent<CanvasGroup>();
        }

        cg.alpha = 0f;
        entryRect.localScale = Vector3.one * entryStartScale;

        if (delay > 0f)
        {
            float waited = 0f;
            while (waited < delay)
            {
                if (entryRect == null)
                {
                    yield break;
                }
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        float duration = Mathf.Max(0.01f, entryFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (entryRect == null)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            cg.alpha = eased;
            entryRect.localScale = Vector3.LerpUnclamped(Vector3.one * entryStartScale, Vector3.one, eased);
            yield return null;
        }

        if (entryRect != null)
        {
            cg.alpha = 1f;
            entryRect.localScale = Vector3.one;
        }
    }

    private void StopEntryAnimations()
    {
        for (int i = 0; i < activeEntryAnimations.Count; i++)
        {
            if (activeEntryAnimations[i] != null)
            {
                StopCoroutine(activeEntryAnimations[i]);
            }
        }
        activeEntryAnimations.Clear();

        for (int i = 0; i < spawnedEntries.Count; i++)
        {
            RectTransform entryRect = spawnedEntries[i];
            if (entryRect == null)
            {
                continue;
            }

            CanvasGroup cg = entryRect.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
            }
            entryRect.localScale = Vector3.one;
        }
    }

    private void EnsureTabAndButtonLabels()
    {
        TMP_FontAsset sharedFont = overallProgressText != null ? overallProgressText.font : null;

        EnsureToggleLabel(allToggle, "전체", sharedFont);
        EnsureToggleLabel(combatToggle, "전투", sharedFont);
        EnsureToggleLabel(collectionToggle, "수집", sharedFont);
        EnsureToggleLabel(otherToggle, "기타", sharedFont);

        Transform closeBtnTransform = transform.Find("Mask/CloseBtn");
        if (closeBtnTransform == null)
        {
            closeBtnTransform = transform.Find("CloseBtn");
        }
        if (closeBtnTransform != null)
        {
            EnsureButtonLabel(closeBtnTransform, "X", sharedFont, 26f, new Color(0.20f, 0.14f, 0.08f, 1f));
        }
    }

    private void EnsureToggleLabel(Toggle toggle, string labelText, TMP_FontAsset font)
    {
        if (toggle == null)
        {
            return;
        }

        TextMeshProUGUI label = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
        {
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(toggle.transform, false);

            RectTransform rect = labelObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            label = labelObj.GetComponent<TextMeshProUGUI>();
        }

        if (font != null && label.font == null)
        {
            label.font = font;
        }

        label.text = labelText;
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.color = toggle.isOn
            ? new Color(0.16f, 0.10f, 0.05f, 1f)
            : new Color(0.88f, 0.82f, 0.72f, 0.90f);
    }

    private void EnsureButtonLabel(Transform buttonTransform, string labelText, TMP_FontAsset font, float fontSize, Color textColor)
    {
        if (buttonTransform == null)
        {
            return;
        }

        Image buttonImage = buttonTransform.GetComponent<Image>();
        if (buttonImage != null && buttonImage.sprite == null)
        {
            buttonImage.color = new Color(0.24f, 0.16f, 0.10f, 0.88f);
            textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        }

        TextMeshProUGUI label = buttonTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
        {
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(buttonTransform, false);

            RectTransform rect = labelObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            label = labelObj.GetComponent<TextMeshProUGUI>();
        }

        if (font != null && label.font == null)
        {
            label.font = font;
        }

        label.text = labelText;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = textColor;
        label.raycastTarget = false;
    }

    private void OnFilterChanged()
    {
        if (isBindingToggles) return;

        SoundManager.Instance?.PlaySFX(SFXType.Click);
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
            overallProgressText.color = new Color(0.16f, 0.11f, 0.06f, 1f);
        }

        if (overallProgressFillImage != null)
        {
            overallProgressFillImage.type = Image.Type.Filled;
            overallProgressFillImage.fillMethod = Image.FillMethod.Horizontal;
            overallProgressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            overallProgressFillImage.fillClockwise = true;
            overallProgressFillImage.fillAmount = completionRatio;
            overallProgressFillImage.color = new Color(0.78f, 0.56f, 0.20f, 0.95f);

            if (overallProgressFillImage.transform.parent != null)
            {
                Image bg = overallProgressFillImage.transform.parent.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = new Color(0.18f, 0.14f, 0.10f, 0.85f);
                }
            }
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
            Sprite targetSprite = toggle.isOn ? selectedTabSprite : unselectedTabSprite;
            if (targetSprite != null)
            {
                image.sprite = targetSprite;
            }
        }

        TextMeshProUGUI label = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.color = toggle.isOn
                ? new Color(0.16f, 0.10f, 0.05f, 1f)
                : new Color(0.88f, 0.82f, 0.72f, 0.90f);
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
