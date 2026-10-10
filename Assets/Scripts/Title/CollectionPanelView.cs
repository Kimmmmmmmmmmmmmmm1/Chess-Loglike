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
    [SerializeField] private Toggle pieceToggle;

    [Header("Overall Progress")]
    [SerializeField] private TextMeshProUGUI overallProgressText;
    [SerializeField] private Image overallProgressFillImage;

    [Header("Tab Visuals")]
    [SerializeField] private Sprite selectedTabSprite;
    [SerializeField] private Sprite unselectedTabSprite;
    [SerializeField] private Color selectedTabColor = Color.white;
    [SerializeField] private Color unselectedTabColor = new Color(0.88f, 0.84f, 0.78f, 1f);

    private bool isBindingToggles;
    private bool layoutNormalized;
    private readonly List<CollectionEntryData> filteredEntries = new List<CollectionEntryData>();

    private enum EntryKind
    {
        Artifact,
        Seal,
        Piece
    }

    private struct CollectionEntryData
    {
        public ArtifactData Artifact;
        public SealData Seal;
        public PieceData Piece;
        public CollectionDiscoveryState State;
        public EntryKind Kind;
        public bool IsUnlocked => State == CollectionDiscoveryState.Acquired;
    }

    private enum Filter
    {
        All,
        Artifact,
        Seal,
        Piece
    }

    private void Awake()
    {
        EnsurePanelLayoutAndVisuals();
        BindToggles();
        BindPaginationButtons();
        SelectAllWithoutRefresh();
    }

    private void OnEnable()
    {
        EnsurePanelLayoutAndVisuals();
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
            SoundManager.Instance?.PlaySFX(SFXType.Click);
            Refresh();
            ResetScrollToTop();
        }
    }

    public override void GoToNextPage()
    {
        if (TrySetPageIndex(currentPageIndex + 1, filteredEntries.Count))
        {
            SoundManager.Instance?.PlaySFX(SFXType.Click);
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
                    Piece = null,
                    State = collectionManager.GetArtifactState(artifact),
                    Kind = EntryKind.Artifact
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
                    Piece = null,
                    State = collectionManager.GetSealState(seal),
                    Kind = EntryKind.Seal
                });
            }
        }

        if (filter == Filter.All || filter == Filter.Piece)
        {
            foreach (PieceData piece in collectionManager.GetAllPieces())
            {
                filteredEntries.Add(new CollectionEntryData
                {
                    Artifact = null,
                    Seal = null,
                    Piece = piece,
                    State = collectionManager.GetPieceState(piece),
                    Kind = EntryKind.Piece
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

            if (data.Kind == EntryKind.Artifact)
            {
                entry.InitializeArtifact(data.Artifact, data.State);
            }
            else if (data.Kind == EntryKind.Seal)
            {
                entry.InitializeSeal(data.Seal, data.State);
            }
            else if (data.Kind == EntryKind.Piece)
            {
                entry.InitializePiece(data.Piece, data.State);
            }
        }

        AnimateCurrentEntries();
    }

    private void EnsurePanelLayoutAndVisuals()
    {
        entriesPerPage = 10;
        animationEntriesPerRow = 5;

        TMP_FontAsset sharedFont = overallProgressText != null
            ? overallProgressText.font
            : (pageText != null ? pageText.font : null);

        EnsureScrollBackgroundAndSprites();

        if (pieceToggle == null)
        {
            Transform toggleParent = sealToggle != null ? sealToggle.transform.parent : (artifactToggle != null ? artifactToggle.transform.parent : (allToggle != null ? allToggle.transform.parent : transform));
            Transform existing = toggleParent != null ? toggleParent.Find("PieceToggle") : null;
            if (existing != null)
            {
                pieceToggle = existing.GetComponent<Toggle>();
            }
            else if (sealToggle != null)
            {
                GameObject pieceToggleObj = Instantiate(sealToggle.gameObject, toggleParent);
                pieceToggleObj.name = "PieceToggle";
                pieceToggle = pieceToggleObj.GetComponent<Toggle>();
                if (pieceToggle != null)
                {
                    pieceToggle.group = filterToggleGroup != null ? filterToggleGroup : sealToggle.group;
                    pieceToggle.isOn = false;
                    SetToggleListener(pieceToggle, OnFilterToggleChanged);
                }
            }
        }

        Transform maskTransform = transform.Find("Mask");
        if (maskTransform is RectTransform maskRect)
        {
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = new Vector2(0f, -6.5f);
            maskRect.sizeDelta = new Vector2(0f, 13f);
        }

        if (!layoutNormalized)
        {
            layoutNormalized = true;

            SetCenteredRect(allToggle != null ? allToggle.transform as RectTransform : null, new Vector2(-483f, 366.5f), new Vector2(296f, 74f));
            SetCenteredRect(artifactToggle != null ? artifactToggle.transform as RectTransform : null, new Vector2(-161f, 366.5f), new Vector2(296f, 74f));
            SetCenteredRect(sealToggle != null ? sealToggle.transform as RectTransform : null, new Vector2(161f, 366.5f), new Vector2(296f, 74f));
            SetCenteredRect(pieceToggle != null ? pieceToggle.transform as RectTransform : null, new Vector2(483f, 366.5f), new Vector2(296f, 74f));

            Transform closeBtnTransform = maskTransform != null ? maskTransform.Find("CloseBtn") : transform.Find("CloseBtn");
            if (closeBtnTransform is RectTransform closeRect)
            {
                SetCenteredRect(closeRect, new Vector2(795f, 371.5f), new Vector2(42f, 51f));
                EnsureButtonLabel(closeRect, "X", sharedFont, 26f, new Color(0.20f, 0.14f, 0.08f, 1f));
            }

            if (contentRoot is RectTransform pageRect)
            {
                SetCenteredRect(pageRect, new Vector2(0f, 8f), new Vector2(1360f, 580f));

                GridLayoutGroup grid = pageRect.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.padding = new RectOffset(0, 0, 8, 8);
                    grid.childAlignment = TextAnchor.UpperCenter;
                    grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                    grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                    grid.cellSize = new Vector2(240f, 268f);
                    grid.spacing = new Vector2(28f, 20f);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 5;
                }
            }

            if (previousPageButton != null && previousPageButton.transform is RectTransform prevRect)
            {
                SetCenteredRect(prevRect, new Vector2(-736f, 8f), new Vector2(58f, 120f));
                EnsureButtonLabel(prevRect, "<", sharedFont, 36f, new Color(0.96f, 0.92f, 0.84f, 1f));
            }

            if (nextPageButton != null && nextPageButton.transform is RectTransform nextRect)
            {
                SetCenteredRect(nextRect, new Vector2(736f, 8f), new Vector2(58f, 120f));
                EnsureButtonLabel(nextRect, ">", sharedFont, 36f, new Color(0.96f, 0.92f, 0.84f, 1f));
            }

            if (pageText != null && pageText.rectTransform != null)
            {
                SetCenteredRect(pageText.rectTransform, new Vector2(-620f, -366f), new Vector2(220f, 48f));
                pageText.color = new Color(0.16f, 0.11f, 0.06f, 1f);
            }

            if (overallProgressFillImage != null && overallProgressFillImage.transform.parent is RectTransform gaugeParentRect)
            {
                SetCenteredRect(gaugeParentRect, new Vector2(-20f, -366f), new Vector2(820f, 28f));
                RectTransform fillRect = overallProgressFillImage.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.pivot = new Vector2(0.5f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = Vector2.zero;
            }

            if (overallProgressText != null && overallProgressText.rectTransform != null)
            {
                SetCenteredRect(overallProgressText.rectTransform, new Vector2(575f, -366f), new Vector2(280f, 48f));
                overallProgressText.color = new Color(0.16f, 0.11f, 0.06f, 1f);
            }
        }

        EnsureToggleLabel(allToggle, "전체", sharedFont);
        EnsureToggleLabel(artifactToggle, "유물", sharedFont);
        EnsureToggleLabel(sealToggle, "인장", sharedFont);
        EnsureToggleLabel(pieceToggle, "기물", sharedFont);
    }

    private void EnsureScrollBackgroundAndSprites()
    {
        if (transform.parent != null)
        {
            Transform achievementPanel = transform.parent.Find("AchievementPanel");
            if (achievementPanel != null)
            {
                Transform achievementBack = achievementPanel.Find("Back");
                if (achievementBack != null)
                {
                    Image achievementBackImg = achievementBack.GetComponent<Image>();
                    if (achievementBackImg != null && achievementBackImg.sprite != null)
                    {
                        Transform myBack = transform.Find("Back");
                        if (myBack == null)
                        {
                            GameObject backObj = new GameObject("Back", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                            backObj.layer = gameObject.layer;
                            myBack = backObj.transform;
                            myBack.SetParent(transform, false);
                            myBack.SetAsFirstSibling();
                        }

                        RectTransform backRect = myBack as RectTransform;
                        if (backRect != null)
                        {
                            backRect.anchorMin = Vector2.zero;
                            backRect.anchorMax = Vector2.one;
                            backRect.pivot = new Vector2(0.5f, 0.5f);
                            backRect.anchoredPosition = Vector2.zero;
                            backRect.sizeDelta = new Vector2(168f, 192f);
                        }

                        Image myBackImg = myBack.GetComponent<Image>();
                        if (myBackImg != null)
                        {
                            myBackImg.sprite = achievementBackImg.sprite;
                            myBackImg.type = Image.Type.Sliced;
                            myBackImg.color = Color.white;
                            myBackImg.raycastTarget = true;
                        }

                        Image rootImg = GetComponent<Image>();
                        if (rootImg != null)
                        {
                            rootImg.enabled = false;
                        }
                    }
                }

                if (selectedTabSprite == null || unselectedTabSprite == null)
                {
                    Toggle[] achToggles = achievementPanel.GetComponentsInChildren<Toggle>(true);
                    for (int i = 0; i < achToggles.Length; i++)
                    {
                        SpriteState st = achToggles[i].spriteState;
                        if (selectedTabSprite == null && st.selectedSprite != null)
                        {
                            selectedTabSprite = st.selectedSprite;
                        }
                        if (unselectedTabSprite == null && st.disabledSprite != null)
                        {
                            unselectedTabSprite = st.disabledSprite;
                        }
                    }
                }
            }
        }
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;
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
    }

    private void EnsureButtonLabel(RectTransform buttonRect, string labelText, TMP_FontAsset font, float fontSize, Color textColor)
    {
        if (buttonRect == null)
        {
            return;
        }

        Image btnImage = buttonRect.GetComponent<Image>();
        if (btnImage != null)
        {
            btnImage.color = new Color(0.24f, 0.16f, 0.10f, 0.88f);
            textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        }

        TextMeshProUGUI label = buttonRect.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
        {
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(buttonRect, false);

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
        AssignGroup(pieceToggle);

        SetToggleListener(allToggle, OnFilterToggleChanged);
        SetToggleListener(artifactToggle, OnFilterToggleChanged);
        SetToggleListener(sealToggle, OnFilterToggleChanged);
        SetToggleListener(pieceToggle, OnFilterToggleChanged);
    }

    private void UnbindToggles()
    {
        SetToggleListener(allToggle, OnFilterToggleChanged, false);
        SetToggleListener(artifactToggle, OnFilterToggleChanged, false);
        SetToggleListener(sealToggle, OnFilterToggleChanged, false);
        SetToggleListener(pieceToggle, OnFilterToggleChanged, false);
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

        SoundManager.Instance?.PlaySFX(SFXType.Click);
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

        if (pieceToggle != null)
        {
            pieceToggle.SetIsOnWithoutNotify(false);
        }

        isBindingToggles = false;
        UpdateToggleVisuals();
    }

    private void EnsureFilterSelection()
    {
        if ((allToggle == null || !allToggle.isOn) &&
            (artifactToggle == null || !artifactToggle.isOn) &&
            (sealToggle == null || !sealToggle.isOn) &&
            (pieceToggle == null || !pieceToggle.isOn))
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

        if (pieceToggle != null && pieceToggle.isOn)
        {
            return Filter.Piece;
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

    private void UpdateToggleVisuals()
    {
        ApplyToggleVisual(allToggle);
        ApplyToggleVisual(artifactToggle);
        ApplyToggleVisual(sealToggle);
        ApplyToggleVisual(pieceToggle);
    }

    private void ApplyToggleVisual(Toggle toggle)
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
                image.color = Color.white;
            }
            else
            {
                image.color = toggle.isOn ? selectedTabColor : unselectedTabColor;
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
            return;
        }

        if (pieceToggle != null && pieceToggle.group != null)
        {
            filterToggleGroup = pieceToggle.group;
        }
    }
}
