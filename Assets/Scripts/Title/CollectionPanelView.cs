using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CollectionPanelView : PagedAnimatedPanelView
{
    [SerializeField] private CollectionEntryView entryPrefab;

    [Header("Filter")]
    [SerializeField] private ToggleGroup filterToggleGroup;
    [SerializeField] private Toggle allToggle;
    [SerializeField] private Toggle artifactToggle;
    [SerializeField] private Toggle sealToggle;

    [Header("Overall Progress")]
    [SerializeField] private TextMeshProUGUI overallProgressText;
    [SerializeField] private Image overallProgressFillImage;

    [Header("Tab Colors")]
    [SerializeField] private Color selectedTabColor = new Color(0.23f, 0.66f, 1f, 1f);
    [SerializeField] private Color unselectedTabColor = new Color(0.22f, 0.22f, 0.22f, 1f);

    private bool isBindingToggles;
    private readonly List<CollectionEntryData> filteredEntries = new List<CollectionEntryData>();

    private struct CollectionEntryData
    {
        public ArtifactData Artifact;
        public SealData Seal;
        public bool IsUnlocked;
        public bool IsArtifact;
    }

    private enum Filter
    {
        All,
        Artifact,
        Seal
    }

    private void Awake()
    {
        BindToggles();
        BindPaginationButtons();
        SelectAllWithoutRefresh();
    }

    private void OnEnable()
    {
        CollectionManager.EnsureInstance().OnCollectionChanged += Refresh;
        EnsureFilterSelection();
        Refresh();
    }

    private void OnDisable()
    {
        StopEntryAnimations();

        if (CollectionManager.Instance != null)
        {
            CollectionManager.Instance.OnCollectionChanged -= Refresh;
        }
    }

    private void OnDestroy()
    {
        UnbindToggles();
        UnbindPaginationButtons();
    }

    private void LateUpdate()
    {
        EnsureFilterSelection();
        UpdateToggleVisuals();
        UpdateOverallProgressDisplay();
    }

    public void Refresh()
    {
        if (contentRoot == null || entryPrefab == null)
        {
            UpdateOverallProgressDisplay();
            UpdatePaginationControls(filteredEntries.Count);
            return;
        }

        CollectionManager collectionManager = CollectionManager.EnsureInstance();
        if (collectionManager == null)
        {
            ClearContent();
            filteredEntries.Clear();
            ResetPageIndex();
            UpdatePaginationControls(0);
            UpdateOverallProgressDisplay();
            return;
        }

        BuildFilteredEntries(collectionManager);
        TrySetPageIndex(currentPageIndex, filteredEntries.Count);

        ClearContent();
        RenderCurrentPage();

        UpdatePaginationControls(filteredEntries.Count);
        UpdateOverallProgressDisplay();
        UpdateToggleVisuals();
    }

    public override void GoToPreviousPage()
    {
        if (TrySetPageIndex(currentPageIndex - 1, filteredEntries.Count))
        {
            Refresh();
            ResetScrollToTop();
        }
    }

    public override void GoToNextPage()
    {
        if (TrySetPageIndex(currentPageIndex + 1, filteredEntries.Count))
        {
            Refresh();
            ResetScrollToTop();
        }
    }

    private void BuildFilteredEntries(CollectionManager collectionManager)
    {
        filteredEntries.Clear();
        Filter filter = GetSelectedFilter();

        if (filter == Filter.All || filter == Filter.Artifact)
        {
            foreach (ArtifactData artifact in collectionManager.GetAllArtifacts())
            {
                filteredEntries.Add(new CollectionEntryData
                {
                    Artifact = artifact,
                    Seal = null,
                    IsUnlocked = collectionManager.IsArtifactUnlocked(artifact),
                    IsArtifact = true
                });
            }
        }

        if (filter == Filter.All || filter == Filter.Seal)
        {
            foreach (SealData seal in collectionManager.GetAllSeals())
            {
                filteredEntries.Add(new CollectionEntryData
                {
                    Artifact = null,
                    Seal = seal,
                    IsUnlocked = collectionManager.IsSealUnlocked(seal),
                    IsArtifact = false
                });
            }
        }
    }

    private void RenderCurrentPage()
    {
        int pageSize = GetEntriesPerPage();
        int startIndex = currentPageIndex * pageSize;
        int endIndexExclusive = Mathf.Min(startIndex + pageSize, filteredEntries.Count);

        for (int i = startIndex; i < endIndexExclusive; i++)
        {
            CollectionEntryData data = filteredEntries[i];
            Transform entryContainer = CreateEntryContainer($"CollectionEntry_{i}");
            CollectionEntryView entry = Instantiate(entryPrefab, entryContainer, false);

            if (data.IsArtifact)
            {
                entry.InitializeArtifact(data.Artifact, data.IsUnlocked);
            }
            else
            {
                entry.InitializeSeal(data.Seal, data.IsUnlocked);
            }
        }

        AnimateCurrentEntries();
    }

    private void BindToggles()
    {
        ResolveToggleGroup();

        if (filterToggleGroup != null)
        {
            filterToggleGroup.allowSwitchOff = false;
        }

        AssignGroup(allToggle);
        AssignGroup(artifactToggle);
        AssignGroup(sealToggle);

        SetToggleListener(allToggle, OnFilterToggleChanged);
        SetToggleListener(artifactToggle, OnFilterToggleChanged);
        SetToggleListener(sealToggle, OnFilterToggleChanged);
    }

    private void UnbindToggles()
    {
        SetToggleListener(allToggle, OnFilterToggleChanged, false);
        SetToggleListener(artifactToggle, OnFilterToggleChanged, false);
        SetToggleListener(sealToggle, OnFilterToggleChanged, false);
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

    private void OnFilterToggleChanged(bool isOn)
    {
        if (isBindingToggles || !isOn)
        {
            return;
        }

        ResetPageIndex();
        Refresh();
        ResetScrollToTop();
    }

    private void SelectAllWithoutRefresh()
    {
        isBindingToggles = true;

        if (allToggle != null)
        {
            allToggle.SetIsOnWithoutNotify(true);
        }

        if (artifactToggle != null)
        {
            artifactToggle.SetIsOnWithoutNotify(false);
        }

        if (sealToggle != null)
        {
            sealToggle.SetIsOnWithoutNotify(false);
        }

        isBindingToggles = false;
    }

    private void EnsureFilterSelection()
    {
        if ((allToggle == null || !allToggle.isOn) &&
            (artifactToggle == null || !artifactToggle.isOn) &&
            (sealToggle == null || !sealToggle.isOn))
        {
            SelectAllWithoutRefresh();
        }
    }

    private Filter GetSelectedFilter()
    {
        if (artifactToggle != null && artifactToggle.isOn)
        {
            return Filter.Artifact;
        }

        if (sealToggle != null && sealToggle.isOn)
        {
            return Filter.Seal;
        }

        return Filter.All;
    }

    private void UpdateOverallProgressDisplay()
    {
        CollectionManager collectionManager = CollectionManager.EnsureInstance();
        int unlockedCount = collectionManager.GetUnlockedTotalCount();
        int totalCount = collectionManager.GetTotalCount();
        float completionRatio = collectionManager.GetOverallCompletionRatio();
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

    private void UpdateToggleVisuals()
    {
        ApplyToggleColor(allToggle);
        ApplyToggleColor(artifactToggle);
        ApplyToggleColor(sealToggle);
    }

    private void ApplyToggleColor(Toggle toggle)
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
            image.color = toggle.isOn ? selectedTabColor : unselectedTabColor;
        }
    }

    private void AssignGroup(Toggle toggle)
    {
        if (toggle != null && filterToggleGroup != null)
        {
            toggle.group = filterToggleGroup;
        }
    }

    private void ResolveToggleGroup()
    {
        if (filterToggleGroup != null)
        {
            return;
        }

        if (allToggle != null && allToggle.group != null)
        {
            filterToggleGroup = allToggle.group;
            return;
        }

        if (artifactToggle != null && artifactToggle.group != null)
        {
            filterToggleGroup = artifactToggle.group;
            return;
        }

        if (sealToggle != null && sealToggle.group != null)
        {
            filterToggleGroup = sealToggle.group;
        }
    }
}
