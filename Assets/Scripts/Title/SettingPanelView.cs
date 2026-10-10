using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettingPanelView : MonoBehaviour
{
    public static SettingPanelView Instance { get; private set; }

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI panelTitleText;
    [SerializeField] private List<string> pageTitles = new List<string>();

    [Header("Page Navigation")]
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;

    [Header("Settings Views")]
    [SerializeField] private GameplaySettingsView gameplayView;
    [SerializeField] private DisplaySettingsView displayView;
    [SerializeField] private AudioSettingsView audioView;
    [SerializeField] private KeySettingsView keyView;
    [SerializeField] private OtherSettingsView otherView;

    [Header("Buttons")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;

    [Header("Page Animation")]
    [SerializeField] private float pageTransitionDuration = 0.22f;
    [SerializeField] private float pageHideScale = 0.96f;

    private readonly List<GameObject> pageObjects = new List<GameObject>();
    private readonly List<Selectable> currentPageSelectables = new List<Selectable>();
    private int currentPageIndex = 0;
    private int bodySelectionIndex = -1;
    private int footerSelectionIndex = 0;
    private readonly List<Coroutine> transitionCoroutines = new List<Coroutine>();
    private bool isBindingButtons = false;
    private bool blockInputUntilRelease = false;
    private SettingsData staged;
    private PanelAnimator panelAnimator;

    private enum FocusRegion
    {
        Header,
        Body,
        Footer,
        Mouse
    }

    private FocusRegion focusRegion = FocusRegion.Header;

    private bool originalSendNavigationEvents = true;

    private int selectorsEditingCount = 0;

    [Header("Auto Scroll")]
    [SerializeField] private float autoScrollPadding = 84f;

    private string originalSaveText;
    private string originalCancelText;
    private TextMeshProUGUI saveTextComponent;
    private TextMeshProUGUI cancelTextComponent;

    private GameObject persistentCanvasRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        SettingsManager.EnsureInstance();

        EnsurePersistentCanvasSetup();

        panelAnimator = GetComponent<PanelAnimator>();
        if (panelAnimator != null)
        {
            panelAnimator.RecacheTransform();
        }

        if (saveButton != null)
        {
            saveTextComponent = saveButton.GetComponentInChildren<TextMeshProUGUI>();
            if (saveTextComponent != null) originalSaveText = saveTextComponent.text;
        }

        if (cancelButton != null)
        {
            cancelTextComponent = cancelButton.GetComponentInChildren<TextMeshProUGUI>();
            if (cancelTextComponent != null) originalCancelText = cancelTextComponent.text;
        }

        CachePageObjectsFromViews();
        ValidatePages();
        BindButtons();
        BindOtherViewEvents();
        StageFromCurrent();
        ShowPage(0, instant: true);
        SetHeaderFocus();
        UpdateHeaderVisuals();
    }

    private void OnEnable()
    {
        if (!isBindingButtons)
        {
            BindButtons();
        }

        StageFromCurrent();
        ResetKeyboardFocusHierarchy();
        UpdateHeaderVisuals();

        if (EventSystem.current != null)
        {
            originalSendNavigationEvents = EventSystem.current.sendNavigationEvents;
            EventSystem.current.sendNavigationEvents = false;
        }
    }

    private void OnDisable()
    {
        StopTransition();
        UnbindButtons();

        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = originalSendNavigationEvents;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnbindOtherViewEvents();
    }

    private void EnsurePersistentCanvasSetup()
    {
        // ScreenSpaceOverlay 루트 Canvas가 SettingPanel 자체에 붙어있으면 Unity가 매 프레임 RectTransform을 화면 크기에 강제 고정하여
        // PanelAnimator의 위치/너비 트윈이 동작하지 않습니다. 별도의 영속 부모 Canvas 하위로 배치합니다.
        if (persistentCanvasRoot == null)
        {
            persistentCanvasRoot = new GameObject("SettingPanelCanvas", typeof(RectTransform));
            Canvas rootCanvas = persistentCanvasRoot.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 300;

            CanvasScaler scaler = persistentCanvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            persistentCanvasRoot.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(persistentCanvasRoot);
        }

        // SettingPanel 자체에 붙어있을 수 있는 루트 Canvas 관련 컴포넌트 제거
        GraphicRaycaster selfRaycaster = GetComponent<GraphicRaycaster>();
        if (selfRaycaster != null) DestroyImmediate(selfRaycaster);

        CanvasScaler selfScaler = GetComponent<CanvasScaler>();
        if (selfScaler != null) DestroyImmediate(selfScaler);

        Canvas selfCanvas = GetComponent<Canvas>();
        if (selfCanvas != null) DestroyImmediate(selfCanvas);

        transform.SetParent(persistentCanvasRoot.transform, false);

        if (transform is RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // 가로 펼침 애니메이션 중 내부 요소가 찌그러지지 않고 마스킹되도록 설정
            if (GetComponent<RectMask2D>() == null)
            {
                gameObject.AddComponent<RectMask2D>();
            }

            for (int i = 0; i < rt.childCount; i++)
            {
                if (rt.GetChild(i) is RectTransform childRt)
                {
                    float spanX = childRt.anchorMax.x - childRt.anchorMin.x;
                    float spanY = childRt.anchorMax.y - childRt.anchorMin.y;
                    if (Mathf.Abs(spanX) > 0.001f || Mathf.Abs(spanY) > 0.001f)
                    {
                        float w = (1920f * spanX) + childRt.sizeDelta.x;
                        float h = (1080f * spanY) + childRt.sizeDelta.y;
                        Vector2 anchorCenter = (childRt.anchorMin + childRt.anchorMax) * 0.5f;
                        Vector2 posOffset = new Vector2((anchorCenter.x - 0.5f) * 1920f, (anchorCenter.y - 0.5f) * 1080f);
                        childRt.anchorMin = new Vector2(0.5f, 0.5f);
                        childRt.anchorMax = new Vector2(0.5f, 0.5f);
                        childRt.sizeDelta = new Vector2(w, h);
                        childRt.anchoredPosition += posOffset;
                    }
                }
            }
        }
    }

    private void Update()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (ModalManager.IsKeyboardBlocked)
        {
            return;
        }

        if (blockInputUntilRelease)
        {
            if (IsActivationConfirmHeld())
            {
                return;
            }

            blockInputUntilRelease = false;
        }

        if (selectorsEditingCount > 0)
        {
            return;
        }

        switch (focusRegion)
        {
            case FocusRegion.Header:
                UpdateHeaderInput();
                break;
            case FocusRegion.Body:
                UpdateBodyInput();
                break;
            case FocusRegion.Footer:
                UpdateFooterInput();
                break;
            case FocusRegion.Mouse:
                UpdateMouseInput();
                break;
        }
    }

    private void ValidatePages()
    {
        if (pageObjects.Count == 0)
        {
            return;
        }

        for (int i = 0; i < pageObjects.Count; i++)
        {
            if (pageObjects[i] == null)
            {
                continue;
            }

            CanvasGroup canvasGroup = pageObjects[i].GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = pageObjects[i].AddComponent<CanvasGroup>();
            }
        }
    }

    private void CachePageObjectsFromViews()
    {
        pageObjects.Clear();

        AddPageObject(gameplayView);
        AddPageObject(displayView);
        AddPageObject(audioView);
        AddPageObject(keyView);
        AddPageObject(otherView);
    }

    private void AddPageObject(MonoBehaviour view)
    {
        if (view == null)
        {
            return;
        }

        pageObjects.Add(view.gameObject);
    }

    private void BindButtons()
    {
        if (isBindingButtons)
        {
            return;
        }

        isBindingButtons = true;

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnSaveClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        if (previousPageButton != null)
        {
            previousPageButton.onClick.AddListener(OnPreviousPageClicked);
        }

        if (nextPageButton != null)
        {
            nextPageButton.onClick.AddListener(OnNextPageClicked);
        }
    }

    private void UnbindButtons()
    {
        if (!isBindingButtons)
        {
            return;
        }

        isBindingButtons = false;

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(OnSaveClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        if (previousPageButton != null)
        {
            previousPageButton.onClick.RemoveListener(OnPreviousPageClicked);
        }

        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveListener(OnNextPageClicked);
        }
    }

    private void StageFromCurrent()
    {
        SettingsData live = SettingsManager.Instance != null ? SettingsManager.Instance.Settings : SettingsData.Default();
        staged = JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(live));
        PushStageToViews();
    }

    private void PushStageToViews()
    {
        if (gameplayView != null) gameplayView.SetTarget(staged);
        if (displayView != null) displayView.SetTarget(staged);
        if (audioView != null) audioView.SetTarget(staged);
        if (keyView != null) keyView.SetTarget(staged);
        if (otherView != null) otherView.SetTarget(staged);
    }

    private void BindOtherViewEvents()
    {
        if (otherView != null)
        {
            otherView.OnResetAllRequested += OnResetAllRequested;
        }
    }

    private void UnbindOtherViewEvents()
    {
        if (otherView != null)
        {
            otherView.OnResetAllRequested -= OnResetAllRequested;
        }
    }

    private void OnResetAllRequested()
    {
        ConfirmResetAllSettings();
    }

    private async void ConfirmResetAllSettings()
    {
        if (!await ShowConfirmModal("설정을 초기화할까요?\n현재 설정은 모두 기본값으로 되돌아갑니다."))
        {
            return;
        }

        staged = SettingsData.Default();
        PushStageToViews();
    }

    public void OnSaveClicked()
    {
        if (staged == null)
        {
            return;
        }

        if (SettingsManager.Instance == null)
        {
            SettingsManager.EnsureInstance();
        }

        SettingsManager.Instance.ApplySettings(staged, save: true);
        ClosePanel();
    }

    public void OnCancelClicked()
    {
        ConfirmCancelSettings();
    }

    public void OnPreviousPageClicked()
    {
        ShowPreviousPage();
    }

    public void OnNextPageClicked()
    {
        ShowNextPage();
    }

    public void ShowPreviousPage()
    {
        if (pageObjects.Count == 0)
        {
            return;
        }

        int previousPageIndex = currentPageIndex - 1;
        if (previousPageIndex < 0)
        {
            previousPageIndex = pageObjects.Count - 1;
        }

        ShowPage(previousPageIndex);
    }

    public void ShowNextPage()
    {
        if (pageObjects.Count == 0)
        {
            return;
        }

        int nextPageIndex = currentPageIndex + 1;
        if (nextPageIndex >= pageObjects.Count)
        {
            nextPageIndex = 0;
        }

        ShowPage(nextPageIndex);
    }

    private void UpdateHeaderInput()
    {

        if (AnySelectorIsEditing())
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            ShowPreviousPage();
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            ShowNextPage();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePanel();
            return;
        }

        if (WasConfirmPressed())
        {
            EnterBodyFocus(selectFirst: true);
        }
    }

    private void UpdateBodyInput()
    {
        if (currentPageSelectables.Count == 0)
        {
            EnterFooterFocus(selectSaveButton: true);
            return;
        }

        SyncBodySelectionIndex();

        bool anyInEditMode = false;
        if (bodySelectionIndex >= 0 && bodySelectionIndex < currentPageSelectables.Count)
        {
            Selectable selected = currentPageSelectables[bodySelectionIndex];
            SettingsChoiceSelector choiceSelector = selected != null ? selected.GetComponent<SettingsChoiceSelector>() : null;
            SettingsSliderSelector sliderSelector = selected != null ? selected.GetComponent<SettingsSliderSelector>() : null;
            SettingsToggleSelector toggleSelector = selected != null ? selected.GetComponent<SettingsToggleSelector>() : null;
            SettingsButtonSelector buttonSelector = selected != null ? selected.GetComponent<SettingsButtonSelector>() : null;
            anyInEditMode = (choiceSelector != null && choiceSelector.IsEditing) || 
                            (sliderSelector != null && sliderSelector.IsEditing) || 
                            (toggleSelector != null && toggleSelector.IsEditing) ||
                            (buttonSelector != null && buttonSelector.IsEditing);
        }

        if (anyInEditMode)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EnterHeaderFocus();
            return;
        }

        if (WasConfirmPressed())
        {
            if (bodySelectionIndex >= 0 && bodySelectionIndex < currentPageSelectables.Count)
            {
                Selectable selected = currentPageSelectables[bodySelectionIndex];
                
                if (selected != null)
                {
                    EnsureUiSelection(selected);
                    BaseEventData submitEventData = CreateSubmitEventData();

                    SettingsChoiceSelector choiceSelector = selected.GetComponent<SettingsChoiceSelector>();
                    if (choiceSelector != null)
                    {
                        choiceSelector.OnSubmit(submitEventData);
                        return;
                    }

                    SettingsSliderSelector sliderSelector = selected.GetComponent<SettingsSliderSelector>();
                    if (sliderSelector != null)
                    {
                        sliderSelector.OnSubmit(submitEventData);
                        return;
                    }

                    SettingsToggleSelector toggleSelector = selected.GetComponent<SettingsToggleSelector>();
                    if (toggleSelector != null)
                    {
                        toggleSelector.OnSubmit(submitEventData);
                        return;
                    }

                    SettingsButtonSelector buttonSelector = selected.GetComponent<SettingsButtonSelector>();
                    if (buttonSelector != null)
                    {
                        buttonSelector.OnSubmit(submitEventData);
                        return;
                    }

                    SettingsKeyBindingSelector keyBindingSelector = selected.GetComponent<SettingsKeyBindingSelector>();
                    if (keyBindingSelector != null)
                    {
                        keyBindingSelector.OnSubmit(submitEventData);
                        return;
                    }

                    ISubmitHandler submitHandler = selected.GetComponent<ISubmitHandler>();
                    if (submitHandler != null)
                    {
                        submitHandler.OnSubmit(submitEventData);
                    }
                }
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            if (bodySelectionIndex > 0)
            {
                SelectBodyIndex(bodySelectionIndex - 1);
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            if (bodySelectionIndex < currentPageSelectables.Count - 1)
            {
                SelectBodyIndex(bodySelectionIndex + 1);
            }
            else if (bodySelectionIndex == currentPageSelectables.Count - 1)
            {
                EnterFooterFocus(selectSaveButton: true);
            }
            return;
        }
    }

    private void UpdateFooterInput()
    {
        if (AnySelectorIsEditing())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EnterHeaderFocus();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            SetFooterSelectionIndex(footerSelectionIndex == 0 ? 1 : 0);
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            SetFooterSelectionIndex(footerSelectionIndex == 0 ? 1 : 0);
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            EnterBodyFocus(selectLast: true);
            return;
        }

        if (WasConfirmPressed())
        {
            Selectable selectable = footerSelectionIndex == 0 ? saveButton : cancelButton;
            ISubmitHandler submitHandler = selectable != null ? selectable.GetComponent<ISubmitHandler>() : null;
            if (submitHandler != null)
            {
                submitHandler.OnSubmit(null);
            }
            else if (selectable is Button btn)
            {
                btn.onClick.Invoke();
            }
            return;
        }
    }

    private void UpdateMouseInput()
    {
        if (AnySelectorIsEditing())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePanel();
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            EnterHeaderFocus();
        }
    }

    private bool AnySelectorIsEditing()
    {
        if (selectorsEditingCount > 0) return true;

        if (currentPageIndex < 0 || currentPageIndex >= pageObjects.Count) return false;
        GameObject page = pageObjects[currentPageIndex];
        if (page == null) return false;

        foreach (var selector in page.GetComponentsInChildren<SettingsKeyBindingSelector>(true))
        {
            if (selector != null && selector.IsEditing) return true;
        }

        foreach (var selector in page.GetComponentsInChildren<SettingsChoiceSelector>(true))
        {
            if (selector != null && selector.IsEditing) return true;
        }

        foreach (var selector in page.GetComponentsInChildren<SettingsSliderSelector>(true))
        {
            if (selector != null && selector.IsEditing) return true;
        }

        foreach (var selector in page.GetComponentsInChildren<SettingsToggleSelector>(true))
        {
            if (selector != null && selector.IsEditing) return true;
        }

        foreach (var selector in page.GetComponentsInChildren<SettingsButtonSelector>(true))
        {
            if (selector != null && selector.IsEditing) return true;
        }

        return false;
    }

    public void NotifySelectorEditModeChanged(bool isEditing)
    {
        if (isEditing)
        {
            selectorsEditingCount++;
        }
        else
        {
            selectorsEditingCount = Mathf.Max(0, selectorsEditingCount - 1);
        }
    }

    private void SetHeaderFocus()
    {
        focusRegion = FocusRegion.Header;
        bodySelectionIndex = -1;
        UpdateHeaderVisuals();
        ClearUiSelection();
    }

    private void EnterBodyFocus(bool selectFirst = false, bool selectLast = false)
    {
        focusRegion = FocusRegion.Body;
        RefreshCurrentPageSelectableCache();

        if (currentPageSelectables.Count == 0)
        {
            EnterFooterFocus(selectSaveButton: true);
            return;
        }

        if (selectLast)
        {
            SelectBodyIndex(currentPageSelectables.Count - 1);
        }
        else
        {
            SelectBodyIndex(selectFirst ? 0 : Mathf.Clamp(bodySelectionIndex, 0, currentPageSelectables.Count - 1));
        }

        UpdateHeaderVisuals();
    }

    private void EnterHeaderFocus()
    {
        SetHeaderFocus();
    }

    private void EnterFooterFocus(bool selectSaveButton)
    {
        focusRegion = FocusRegion.Footer;
        bodySelectionIndex = -1;
        footerSelectionIndex = selectSaveButton ? 0 : 1;
        UpdateHeaderVisuals();
        SetFooterSelectionIndex(footerSelectionIndex);
    }

    public void NotifyMouseInteraction()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        focusRegion = FocusRegion.Mouse;
        UpdateHeaderVisuals();
    }

    public bool IsMouseInteractionMode => focusRegion == FocusRegion.Mouse;

    private void RefreshCurrentPageSelectableCache()
    {
        currentPageSelectables.Clear();

        if (currentPageIndex < 0 || currentPageIndex >= pageObjects.Count)
        {
            return;
        }

        GameObject page = pageObjects[currentPageIndex];
        if (page == null)
        {
            return;
        }

        // Get all Selectable components directly under the page object.
        // We use GetComponentsInChildren and then filter by parent to ensure we only get direct children
        // that are active and enabled. This is more reliable than iterating transform children.
        var selectables = page.GetComponentsInChildren<Selectable>(false);
        foreach (var selectable in selectables)
        {
            // We only want direct children of the content/scroll view area, not nested selectables.
            // A simple check is to see if its parent's parent is the page itself, assuming a structure like Page -> ScrollView -> Content -> Selectable.
            // For simplicity and robustness, we'll just add all found selectables that are not part of the header/footer.
            if (selectable.gameObject != previousPageButton?.gameObject &&
                selectable.gameObject != nextPageButton?.gameObject &&
                selectable.gameObject != saveButton?.gameObject &&
                selectable.gameObject != cancelButton?.gameObject &&
                selectable.navigation.mode != Navigation.Mode.None)
            {
                currentPageSelectables.Add(selectable);
            }
        }
    }

    private void SelectBodyIndex(int index)
    {
        if (currentPageSelectables.Count == 0)
        {
            return;
        }

        int prevIndex = bodySelectionIndex;
        bodySelectionIndex = Mathf.Clamp(index, 0, currentPageSelectables.Count - 1);
        Selectable selectable = currentPageSelectables[bodySelectionIndex];
        EnsureUiSelection(selectable);

        bool movedUp = bodySelectionIndex < prevIndex;
        ScrollSelectableIntoView(selectable, movedUp);

        UpdateHeaderVisuals();
        Debug.Log($"Selected body index: {bodySelectionIndex}, selectable: {selectable?.name}");
    }

    private void ScrollSelectableIntoView(Selectable selectable, bool movedUp = false)
    {
        if (selectable == null) return;

        RectTransform itemRT = selectable.GetComponent<RectTransform>();
        if (itemRT == null) return;

        ScrollRect scroll = selectable.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null) return;

        RectTransform content = scroll.content;
        RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();

        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float itemPosInContent = -itemRT.anchoredPosition.y;
        float itemTop = itemPosInContent;
        float itemBottom = itemPosInContent + itemRT.rect.height;
        
        float viewportHeight = viewport.rect.height;
        float contentHeight = content.rect.height;
        float scrollableHeight = contentHeight - viewportHeight;
        
        if (scrollableHeight <= 0f) return;

        float scrollPos = (1f - scroll.verticalNormalizedPosition) * scrollableHeight;

        float extraTop = 0f;
        int idx = bodySelectionIndex;
        if (idx > 0 && idx < currentPageSelectables.Count)
        {
            RectTransform prevRT = currentPageSelectables[Mathf.Max(0, idx - 1)]?.GetComponent<RectTransform>();
            if (prevRT != null)
            {
                extraTop = prevRT.rect.height;
            }
        }

        float padding = autoScrollPadding;
        float topThreshold = scrollPos + extraTop + padding;
        float bottomThreshold = scrollPos + viewportHeight - padding;

        float newScrollPos = scrollPos;

        if (movedUp)
        {
            float extra = itemRT.rect.height;
            if (idx > 0 && idx < currentPageSelectables.Count)
            {
                RectTransform prevRT = currentPageSelectables[Mathf.Max(0, idx - 1)]?.GetComponent<RectTransform>();
                if (prevRT != null)
                {
                    extra = prevRT.rect.height;
                }
            }

            newScrollPos = Mathf.Max(0f, itemTop - extra - padding);
        }
        else
        {
            if (itemTop >= topThreshold && itemBottom <= bottomThreshold)
            {
                return;
            }

            if (itemTop < scrollPos)
            {
                float extra = itemRT.rect.height;
                if (idx > 0 && idx < currentPageSelectables.Count)
                {
                    RectTransform prevRT = currentPageSelectables[Mathf.Max(0, idx - 1)]?.GetComponent<RectTransform>();
                    if (prevRT != null)
                    {
                        extra = prevRT.rect.height;
                    }
                }

                newScrollPos = Mathf.Max(0f, itemTop - extra - padding);
            }
            else if (itemBottom > scrollPos + viewportHeight)
            {
                newScrollPos = Mathf.Min(scrollableHeight, itemBottom - viewportHeight + padding);
            }
        }

        float newNormalizedPos = 1f - (newScrollPos / scrollableHeight);
        scroll.verticalNormalizedPosition = Mathf.Clamp01(newNormalizedPos);
    }

    private void EnsureUiSelection(Selectable selectable)
    {
        if (selectable == null || EventSystem.current == null)
        {
            return;
        }

        if (EventSystem.current.currentSelectedGameObject != selectable.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }

    private BaseEventData CreateSubmitEventData()
    {
        return EventSystem.current != null ? new BaseEventData(EventSystem.current) : null;
    }

    private void SyncBodySelectionIndex()
    {
        if (focusRegion != FocusRegion.Body || EventSystem.current == null)
        {
            return;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        for (int i = 0; i < currentPageSelectables.Count; i++)
        {
            Selectable selectable = currentPageSelectables[i];
            if (selectable != null && selectable.gameObject == selectedObject)
            {
                bodySelectionIndex = i;
                return;
            }
        }
    }

    private void SelectFooterIndex(int index)
    {
        footerSelectionIndex = Mathf.Clamp(index, 0, 1);

        Selectable selectable = footerSelectionIndex == 0 ? saveButton : cancelButton;
        if (selectable != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }

    private void SetFooterSelectionIndex(int index)
    {
        SelectFooterIndex(index);
        UpdateFooterVisuals();
    }

    private void ClearUiSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void UpdateHeaderVisuals()
    {
        if (panelTitleText != null)
        {
            panelTitleText.text = GetCurrentPageTitle();
        }

        UpdateHeaderNavigationButtons();
        UpdateFooterVisuals();
    }

    private void UpdateHeaderNavigationButtons()
    {
        bool showHeaderButtons = focusRegion == FocusRegion.Header || focusRegion == FocusRegion.Mouse;

        if (previousPageButton != null)
        {
            previousPageButton.gameObject.SetActive(showHeaderButtons);
        }

        if (nextPageButton != null)
        {
            nextPageButton.gameObject.SetActive(showHeaderButtons);
        }
    }

    private void UpdateFooterVisuals()
    {
        if (saveTextComponent != null && originalSaveText != null)
        {
            saveTextComponent.text = (focusRegion == FocusRegion.Footer && footerSelectionIndex == 0) ? $"> {originalSaveText} <" : originalSaveText;
        }

        if (cancelTextComponent != null && originalCancelText != null)
        {
            cancelTextComponent.text = (focusRegion == FocusRegion.Footer && footerSelectionIndex == 1) ? $"> {originalCancelText} <" : originalCancelText;
        }
    }

    private string GetCurrentPageTitle()
    {
        if (currentPageIndex < 0 || currentPageIndex >= pageObjects.Count)
        {
            return "Settings";
        }

        if (pageTitles != null && currentPageIndex < pageTitles.Count && !string.IsNullOrWhiteSpace(pageTitles[currentPageIndex]))
        {
            return pageTitles[currentPageIndex];
        }

        GameObject page = pageObjects[currentPageIndex];
        if (page == null)
        {
            return "Settings";
        }

        string name = page.name;
        return name.EndsWith("View") ? name.Replace("View", string.Empty) : name;
    }

    private bool WasConfirmPressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
    }

    private bool IsActivationConfirmHeld()
    {
        return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
    }

    private async void ConfirmCancelSettings()
    {
        if (!await ShowConfirmModal("설정을 취소할까요?\n현재 변경 내용은 모두 사라집니다."))
        {
            return;
        }

        StageFromCurrent();
        ClosePanel();
    }

    private Task<bool> ShowConfirmModal(string message)
    {
        if (ModalManager.Instance == null)
        {
            return Task.FromResult(true);
        }

        return ModalManager.Instance.ShowModalAsync(message);
    }

    public void ClosePanel()
    {
        if (panelAnimator == null)
        {
            panelAnimator = GetComponent<PanelAnimator>();
        }

        if (panelAnimator != null)
        {
            panelAnimator.Hide();
            return;
        }

        gameObject.SetActive(false);
    }

    public void OpenPanel()
    {
        if (panelAnimator == null)
        {
            panelAnimator = GetComponent<PanelAnimator>();
        }

        gameObject.SetActive(true);
        ResetKeyboardFocusHierarchy();

        if (panelAnimator != null)
        {
            panelAnimator.Show();
        }
    }

    private void ResetKeyboardFocusHierarchy()
    {
        selectorsEditingCount = 0;
        ExitAllSelectorEditModes();
        bodySelectionIndex = -1;
        footerSelectionIndex = 0;
        blockInputUntilRelease = true;
        RefreshCurrentPageSelectableCache();
        SetHeaderFocus();
    }

    private void ExitAllSelectorEditModes()
    {
        for (int i = 0; i < pageObjects.Count; i++)
        {
            GameObject page = pageObjects[i];
            if (page == null)
            {
                continue;
            }

            foreach (SettingsKeyBindingSelector selector in page.GetComponentsInChildren<SettingsKeyBindingSelector>(true))
            {
                selector.ExitEditMode();
                selector.FocusLabel();
            }
        }
    }

    public static bool OpenSingleton()
    {
        if (Instance == null)
        {
            return false;
        }

        Instance.OpenPanel();
        return true;
    }

    public static bool CloseSingleton()
    {
        if (Instance == null)
        {
            return false;
        }

        Instance.ClosePanel();
        return true;
    }

    private void ShowPage(int pageIndex, bool instant = false)
    {
        if (pageIndex < 0 || pageIndex >= pageObjects.Count)
        {
            return;
        }

        if (currentPageIndex == pageIndex && !instant)
        {
            return;
        }

        StopTransition();

        currentPageIndex = pageIndex;
        RefreshCurrentPageSelectableCache();
        UpdateHeaderVisuals();

        for (int i = 0; i < pageObjects.Count; i++)
        {
            if (i == pageIndex)
            {
                StartPageTransition(pageObjects[i], show: true, instant: instant);
            }
            else
            {
                StartPageTransition(pageObjects[i], show: false, instant: instant);
            }
        }

        ResetPageScrollToTop(pageObjects[pageIndex]);
    }

    private void ResetPageScrollToTop(GameObject page)
    {
        if (page == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        ScrollRect[] scrollRects = page.GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scrollRect = scrollRects[i];
            if (scrollRect == null)
            {
                continue;
            }

            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void StartPageTransition(GameObject page, bool show, bool instant)
    {
        if (page == null) return;

        CanvasGroup canvasGroup = page.GetComponent<CanvasGroup>();
        if (canvasGroup == null) return;

        if (instant)
        {
            canvasGroup.alpha = show ? 1f : 0f;
            RectTransform rectTransform = page.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localScale = show ? Vector3.one : Vector3.one * pageHideScale;
            }

            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
            page.SetActive(show);
            return;
        }

        Coroutine coroutine = StartCoroutine(AnimatePageTransition(page, canvasGroup, show));
        transitionCoroutines.Add(coroutine);
    }

    private IEnumerator AnimatePageTransition(GameObject page, CanvasGroup canvasGroup, bool show)
    {
        RectTransform rectTransform = page.GetComponent<RectTransform>();
        float elapsed = 0f;

        float startAlpha = canvasGroup.alpha;
        float targetAlpha = show ? 1f : 0f;
        Vector3 startScale = rectTransform != null ? rectTransform.localScale : Vector3.one * pageHideScale;
        Vector3 targetScale = show ? Vector3.one : (Vector3.one * pageHideScale);

        if (show)
        {
            page.SetActive(true);
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (pageTransitionDuration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            if (rectTransform != null)
            {
                rectTransform.localScale = targetScale;
            }

            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
            page.SetActive(show);
            yield break;
        }

        while (elapsed < pageTransitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / pageTransitionDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.Lerp(startScale, targetScale, eased);
            }

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (rectTransform != null)
        {
            rectTransform.localScale = targetScale;
        }

        if (!show)
        {
            page.SetActive(false);
        }

        canvasGroup.interactable = show;
        canvasGroup.blocksRaycasts = show;
    }

    private void StopTransition()
    {
        for (int i = 0; i < transitionCoroutines.Count; i++)
        {
            if (transitionCoroutines[i] != null)
            {
                StopCoroutine(transitionCoroutines[i]);
            }
        }

        transitionCoroutines.Clear();
    }

    public int GetCurrentPageIndex()
    {
        return currentPageIndex;
    }

    public int GetPageCount()
    {
        return pageObjects.Count;
    }
}
