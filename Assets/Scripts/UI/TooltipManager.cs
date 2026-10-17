using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class TooltipManager : PersistentManagerBase
{
    public const int TooltipPriorityDefault = 0;
    public const int TooltipPriorityPieceMove = 10;
    public const int TooltipPrioritySeal = 20;

    public static TooltipManager Instance { get; private set; }
    private const string DefaultTooltipViewPrefabPath = "UI/TooltipView";
    private const string DefaultPieceCardTooltipPrefabPath = "UI/PieceCardTooltip";

    [Header("Tooltip UI")]
    [SerializeField] private TooltipView tooltipView;
    [SerializeField] private PieceCardTooltipView pieceCardTooltipView;
    [SerializeField] private GameObject pieceCardTooltipPrefab;
    [SerializeField] private float pieceCardHoverDelay = 1.0f;
    [SerializeField] private Vector3 tooltipOffset = new Vector3(20, 20, 0);
    private enum PendingCardKind
    {
        None,
        Piece,
        Artifact,
        Seal,
        General
    }

    private RectTransform tooltipRect;
    private Canvas parentCanvas;
    private GameObject runtimeTooltipCanvas;
    private readonly HashSet<GameObject> tooltipSources = new HashSet<GameObject>();
    private Coroutine sourceCheckCoroutine;
    private Coroutine pendingTooltipCoroutine;
    private GameObject pendingTooltipSource;
    private int pendingTooltipPriority = int.MinValue;
    private PendingCardKind pendingCardKind = PendingCardKind.None;
    private string pendingTooltipTitle = string.Empty;
    private string pendingTooltipDescription = string.Empty;
    private string pendingTooltipFlavorText = string.Empty;
    private Sprite pendingTooltipIcon;
    private string pendingTooltipSubtitle = string.Empty;
    private string pendingTooltipBadgeText = "★";
    private Color? pendingTooltipIconColor;
    private PieceType pendingPieceType;
    private SealData pendingSealData;
    private ArtifactData pendingArtifactData;
    private int pendingArtifactLevel = -1;
    private bool pendingIsEnemy;
    private bool isTooltipVisible;
    private bool isPieceCardActive;
    private int activeTooltipPriority = int.MinValue;
    private GameObject activeTooltipSource;
    private SettingsData.TooltipDelay currentTooltipDelay = SettingsData.TooltipDelay.Immediate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null) return;

        GameObject managerObj = new GameObject("TooltipManager");
        Instance = managerObj.AddComponent<TooltipManager>();
    }

    protected override void Awake()
    {
        base.Awake();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitTooltipView();
        RefreshTooltipDelay();
        isTooltipVisible = false;
    }

    public override void ResetForNewRun()
    {
        // Hide any visible tooltip and clear runtime sources
        CancelPendingTooltip();
        if (tooltipView != null) tooltipView.SetVisible(false);
        if (pieceCardTooltipView != null) pieceCardTooltipView.Hide();
        isPieceCardActive = false;
        tooltipSources.Clear();
        isTooltipVisible = false;
        activeTooltipPriority = int.MinValue;
        activeTooltipSource = null;
    }

    public void SetTooltipDelay(SettingsData.TooltipDelay delay)
    {
        currentTooltipDelay = delay;
    }

    private void InitTooltipView()
    {
        if (pieceCardTooltipView == null)
        {
            pieceCardTooltipView = FindAnyObjectByType<PieceCardTooltipView>(FindObjectsInactive.Include);
        }

        if (pieceCardTooltipView == null)
        {
            GameObject canvasObj = GetOrCreateTooltipCanvas();
            GameObject prefabToUse = pieceCardTooltipPrefab;
            if (prefabToUse == null)
            {
                prefabToUse = Resources.Load<GameObject>(DefaultPieceCardTooltipPrefabPath);
            }

            if (prefabToUse != null)
            {
                GameObject instance = Instantiate(prefabToUse, canvasObj.transform);
                instance.name = "PieceCardTooltipView";
                pieceCardTooltipView = instance.GetComponent<PieceCardTooltipView>();
            }

            if (pieceCardTooltipView == null)
            {
                GameObject cardObj = new GameObject("PieceCardTooltipView", typeof(RectTransform));
                cardObj.transform.SetParent(canvasObj.transform, false);
                pieceCardTooltipView = cardObj.AddComponent<PieceCardTooltipView>();
            }
        }

        if (pieceCardTooltipView != null)
        {
            if (pieceCardTooltipView.GetComponentInParent<Canvas>() == null)
            {
                GameObject canvasObj = GetOrCreateTooltipCanvas();
                pieceCardTooltipView.transform.SetParent(canvasObj.transform, false);
            }
            pieceCardTooltipView.EnsureUIHierarchy();
            pieceCardTooltipView.gameObject.SetActive(false);
        }

        if (tooltipView == null)
        {
            tooltipView = FindAnyObjectByType<TooltipView>(FindObjectsInactive.Include);
        }

        if (tooltipView == null)
        {
            GameObject prefabObject = Resources.Load<GameObject>(DefaultTooltipViewPrefabPath);
            if (prefabObject == null)
            {
                prefabObject = Resources.Load<GameObject>("UI/Tooltip");
            }

            if (prefabObject != null)
            {
                GameObject canvasObj = GetOrCreateTooltipCanvas();
                GameObject instance = Instantiate(prefabObject, canvasObj.transform);
                tooltipView = instance.GetComponent<TooltipView>();
                if (tooltipView == null)
                {
                    return;
                }
            }
        }

        if (tooltipView == null)
        {
            return;
        }

        if (!tooltipView.gameObject.scene.IsValid() || tooltipView.gameObject.scene.rootCount == 0)
        {
            GameObject canvasObj = GetOrCreateTooltipCanvas();
            tooltipView = Instantiate(tooltipView, canvasObj.transform);
        }

        if (tooltipView.GetComponentInParent<Canvas>() == null)
        {
            GameObject canvasObj = GetOrCreateTooltipCanvas();
            tooltipView.transform.SetParent(canvasObj.transform, false);
        }

        tooltipRect = tooltipView.RectTransform;
        parentCanvas = tooltipView.GetComponentInParent<Canvas>();

        // 툴팁 패널이 레이캐스트를 막아 깜빡이는 현상을 방지하기 위해 CanvasGroup 설정
        CanvasGroup cg = tooltipView.GetComponent<CanvasGroup>();
        if (cg == null) cg = tooltipView.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    private GameObject GetOrCreateTooltipCanvas()
    {
        if (runtimeTooltipCanvas != null)
        {
            return runtimeTooltipCanvas;
        }

        runtimeTooltipCanvas = GameObject.Find("TooltipCanvas");
        if (runtimeTooltipCanvas != null)
        {
            return runtimeTooltipCanvas;
        }

        runtimeTooltipCanvas = new GameObject("TooltipCanvas");
        DontDestroyOnLoad(runtimeTooltipCanvas);

        Canvas canvas = runtimeTooltipCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = runtimeTooltipCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        runtimeTooltipCanvas.AddComponent<GraphicRaycaster>();

        return runtimeTooltipCanvas;
    }

    private void Start()
    {
        if (tooltipView != null) 
        {
            tooltipView.SetVisible(false);
        }
        if (pieceCardTooltipView != null)
        {
            pieceCardTooltipView.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (PieceController.IsAnyDragging)
        {
            if (isTooltipVisible || pendingTooltipCoroutine != null)
            {
                HideTooltip();
            }
            return;
        }

        if (pendingTooltipCoroutine != null && pendingTooltipSource != null)
        {
            if (!pendingTooltipSource.activeInHierarchy || !IsPointerOverSource(pendingTooltipSource))
            {
                CancelPendingTooltip();
            }
        }

        if (isTooltipVisible)
        {
            if (activeTooltipSource != null && (!activeTooltipSource.activeInHierarchy || !IsPointerOverSource(activeTooltipSource)))
            {
                HideTooltip();
                return;
            }
        }

        if (isPieceCardActive)
        {
            return; // 카드 툴팁 활성 시 보드나 대상을 가리지 않도록 위치 고정 유지
        }

        if (tooltipView != null && tooltipView.gameObject.activeSelf)
        {
            UpdatePosition(GetMousePosition());
            ClampToScreen();
        }
    }

    public void ShowTooltip(
        string title,
        string description,
        Vector3 position,
        string flavorText = "",
        int priority = TooltipPriorityDefault,
        GameObject source = null,
        Sprite icon = null,
        string subtitle = "",
        string badgeText = "★",
        Color? iconColor = null)
    {
        if (source != null)
        {
            PieceController pc = source.GetComponent<PieceController>();
            if (pc != null)
            {
                ShowPieceTooltip(pc.PieceType, pc.PrimarySealData, position, source, pc.IsEnemy, priority);
                return;
            }
        }

        if (isTooltipVisible && source != activeTooltipSource && priority < activeTooltipPriority)
        {
            return;
        }

        if (pendingTooltipCoroutine != null && source != pendingTooltipSource && priority < pendingTooltipPriority)
        {
            return;
        }

        if (pieceCardTooltipView == null)
        {
            InitTooltipView();
            if (pieceCardTooltipView == null) return;
        }

        if (icon == null && source != null)
        {
            icon = ExtractIconFromSource(source, out Color extractedColor);
            if (!iconColor.HasValue && icon != null)
            {
                iconColor = extractedColor;
            }
        }

        ScheduleGeneralCardTooltip(title, description, flavorText, position, priority, source, icon, subtitle, badgeText, iconColor);
    }

    /// <summary>
    /// 미니멀 솔리드 흑백(Solid Black & White) 체스 기물 카드 툴팁 표시
    /// </summary>
    public void ShowPieceTooltip(PieceType pieceType, SealData attachedSeal, Vector3 position, GameObject source, bool isEnemy = false, int priority = TooltipPriorityPieceMove)
    {
        if (isTooltipVisible && source != activeTooltipSource && priority < activeTooltipPriority)
        {
            return;
        }

        if (pendingTooltipCoroutine != null && source != pendingTooltipSource && priority < pendingTooltipPriority)
        {
            return;
        }

        if (pieceCardTooltipView == null)
        {
            InitTooltipView();
            if (pieceCardTooltipView == null) return;
        }

        SchedulePieceTooltip(pieceType, attachedSeal, isEnemy, position, priority, source);
    }

    /// <summary>
    /// 아티팩트 데이터를 받아서 일러스트가 있는 유물 카드 툴팁으로 표시하는 메서드
    /// </summary>
    public void ShowArtifactTooltip(ArtifactData artifact, int level = 1)
    {
        ShowArtifactTooltip(artifact, Vector3.zero, null, TooltipPriorityDefault, level);
    }

    public void ShowArtifactTooltip(ArtifactData artifact, Vector3 position, GameObject source = null, int priority = TooltipPriorityDefault, int overrideLevel = -1)
    {
        if (artifact == null)
        {
            return;
        }

        if (isTooltipVisible && source != activeTooltipSource && priority < activeTooltipPriority)
        {
            return;
        }

        if (pendingTooltipCoroutine != null && source != pendingTooltipSource && priority < pendingTooltipPriority)
        {
            return;
        }

        if (pieceCardTooltipView == null)
        {
            InitTooltipView();
            if (pieceCardTooltipView == null) return;
        }

        ScheduleArtifactTooltip(artifact, overrideLevel, position, priority, source);
    }

    /// <summary>
    /// 인장 데이터를 받아서 일러스트가 있는 인장 카드 툴팁으로 표시하는 메서드
    /// </summary>
    public void ShowSealTooltip(SealData seal, Vector3 position, GameObject source = null, int priority = TooltipPrioritySeal)
    {
        if (seal == null)
        {
            return;
        }

        if (isTooltipVisible && source != activeTooltipSource && priority < activeTooltipPriority)
        {
            return;
        }

        if (pendingTooltipCoroutine != null && source != pendingTooltipSource && priority < pendingTooltipPriority)
        {
            return;
        }

        if (pieceCardTooltipView == null)
        {
            InitTooltipView();
            if (pieceCardTooltipView == null) return;
        }

        ScheduleSealTooltip(seal, position, priority, source);
    }

    private static Sprite ExtractIconFromSource(GameObject source, out Color extractedColor)
    {
        extractedColor = Color.white;
        if (source == null)
        {
            return null;
        }

        // 자식 이미지 중 아이콘 역할을 하는 스프라이트를 우선 탐색
        Image[] images = source.GetComponentsInChildren<Image>(true);
        Image fallbackImage = null;
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null || img.sprite == null) continue;

            string objName = img.gameObject.name.ToLowerInvariant();
            if (objName.Contains("icon") || objName.Contains("image") || objName.Contains("portrait") || objName.Contains("piece") || objName.Contains("seal") || objName.Contains("artifact"))
            {
                extractedColor = img.color;
                return img.sprite;
            }

            if (img.gameObject != source && fallbackImage == null)
            {
                fallbackImage = img;
            }
        }

        if (fallbackImage != null)
        {
            extractedColor = fallbackImage.color;
            return fallbackImage.sprite;
        }

        Image rootImg = source.GetComponent<Image>();
        if (rootImg != null && rootImg.sprite != null)
        {
            extractedColor = rootImg.color;
            return rootImg.sprite;
        }

        // 부모 계층(예: 업적 게이지 호버 시 부모 AchievementEntryView의 아이콘)에서도 탐색
        if (source.transform.parent != null)
        {
            Image[] parentImages = source.transform.parent.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < parentImages.Length; i++)
            {
                Image img = parentImages[i];
                if (img == null || img.sprite == null || img.gameObject == source) continue;
                string objName = img.gameObject.name.ToLowerInvariant();
                if (objName.Contains("icon") || objName.Contains("image"))
                {
                    extractedColor = img.color;
                    return img.sprite;
                }
            }
        }

        SpriteRenderer sr = source.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            extractedColor = sr.color;
            return sr.sprite;
        }

        return null;
    }

    private Vector2 GetMousePosition()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
        if (Pointer.current != null)
        {
            return Pointer.current.position.ReadValue();
        }
        try
        {
            return Input.mousePosition;
        }
        catch
        {
            return Vector2.zero;
        }
    }

    private void UpdatePosition(Vector3 screenPos)
    {
        Camera cam = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = parentCanvas.worldCamera;
        }
        
        // 오프셋 적용 (스크린 공간에서 픽셀 단위로 적용)
        Vector2 finalScreenPos = new Vector2(screenPos.x + tooltipOffset.x, screenPos.y + tooltipOffset.y);

        // 최종 위치 설정
        if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            tooltipView.transform.position = finalScreenPos;
        }
        else
        {
            // 카메라 모드라면 스크린 좌표를 다시 월드 좌표로 변환하여 설정
            Vector3 worldPos;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentCanvas.transform as RectTransform, finalScreenPos, cam, out worldPos))
            {
                tooltipView.transform.position = worldPos;
            }
        }
    }

    private void ClampToScreen()
    {
        Camera cam = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = parentCanvas.worldCamera;
        }

        Vector3[] corners = new Vector3[4];
        tooltipRect.GetWorldCorners(corners);
        
        // 코너 좌표를 스크린 공간으로 변환하여 비교
        Vector3[] screenCorners = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            screenCorners[i] = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
        }

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        
        Vector3 shift = Vector3.zero;
        
        // Right edge
        if (screenCorners[2].x > screenWidth)
        {
            shift.x -= (screenCorners[2].x - screenWidth);
        }
        // Left edge
        if (screenCorners[0].x < 0)
        {
            shift.x -= screenCorners[0].x;
        }
        // Top edge
        if (screenCorners[2].y > screenHeight)
        {
            shift.y -= (screenCorners[2].y - screenHeight);
        }
        // Bottom edge
        if (screenCorners[0].y < 0)
        {
            shift.y -= screenCorners[0].y;
        }
        
        // 보정된 위치 적용
        if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            tooltipRect.position += shift;
        }
        else
        {
            Vector2 currentScreenPos = RectTransformUtility.WorldToScreenPoint(cam, tooltipRect.position);
            currentScreenPos += new Vector2(shift.x, shift.y);
            
            Vector3 newWorldPos;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentCanvas.transform as RectTransform, currentScreenPos, cam, out newWorldPos))
            {
                tooltipRect.position = newWorldPos;
            }
        }
    }

    public void HideTooltip(GameObject source = null)
    {
        if (source != null)
        {
            if (pendingTooltipSource == null || pendingTooltipSource == source || pendingTooltipSource.transform.IsChildOf(source.transform) || source.transform.IsChildOf(pendingTooltipSource.transform))
            {
                CancelPendingTooltip();
            }

            if (activeTooltipSource != null && source != activeTooltipSource && !activeTooltipSource.transform.IsChildOf(source.transform) && !source.transform.IsChildOf(activeTooltipSource.transform))
            {
                if (activeTooltipSource.activeInHierarchy && IsPointerOverSource(activeTooltipSource))
                {
                    return;
                }
            }
        }
        else
        {
            CancelPendingTooltip();
        }

        HideActiveCardImmediate();
    }

    private void HideActiveCardImmediate()
    {
        if (tooltipView != null)
        {
            tooltipView.SetVisible(false);
        }
        if (pieceCardTooltipView != null)
        {
            pieceCardTooltipView.Hide();
        }
        isPieceCardActive = false;
        isTooltipVisible = false;
        activeTooltipPriority = int.MinValue;
        activeTooltipSource = null;
    }

    private bool IsPointerOverSource(GameObject source)
    {
        if (source == null)
        {
            return true;
        }

        if (!source.activeInHierarchy)
        {
            return false;
        }

        RectTransform rt = source.GetComponent<RectTransform>();
        if (rt == null)
        {
            return true;
        }

        Canvas sourceCanvas = rt.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (sourceCanvas != null)
        {
            Canvas root = sourceCanvas.rootCanvas;
            if (root != null && root.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = root.worldCamera;
            }
        }

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            if (sp.x < minX) minX = sp.x;
            if (sp.y < minY) minY = sp.y;
            if (sp.x > maxX) maxX = sp.x;
            if (sp.y > maxY) maxY = sp.y;
        }

        // 호버 시 살짝 떠오르는 애니메이션(hoverLiftAmount) 오차 허용
        const float hoverPadding = 16f;
        Vector2 mousePos = GetMousePosition();
        return mousePos.x >= (minX - hoverPadding) &&
               mousePos.x <= (maxX + hoverPadding) &&
               mousePos.y >= (minY - hoverPadding) &&
               mousePos.y <= (maxY + hoverPadding);
    }

    private void RefreshTooltipDelay()
    {
        if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
        {
            currentTooltipDelay = SettingsManager.Instance.Settings.tooltipDelay;
        }
    }

    private float GetCardHoverDelaySeconds()
    {
        return currentTooltipDelay switch
        {
            SettingsData.TooltipDelay.HalfSecond => 0.5f,
            SettingsData.TooltipDelay.OneSecond => 1f,
            _ => pieceCardHoverDelay > 0f ? pieceCardHoverDelay : 0.5f
        };
    }

    private void ScheduleGeneralCardTooltip(
        string title,
        string description,
        string flavorText,
        Vector3 position,
        int priority,
        GameObject source,
        Sprite icon,
        string subtitle,
        string badgeText,
        Color? iconColor)
    {
        if (isTooltipVisible && source != null && source == activeTooltipSource && pieceCardTooltipView != null)
        {
            pieceCardTooltipView.SetGeneralCardData(title, description, flavorText, icon, subtitle, badgeText, string.Empty, null, iconColor);
            activeTooltipPriority = priority;
            return;
        }

        if (isTooltipVisible && source != activeTooltipSource)
        {
            HideActiveCardImmediate();
        }

        if (pendingTooltipCoroutine != null && source != null && source == pendingTooltipSource && pendingCardKind == PendingCardKind.General)
        {
            pendingTooltipTitle = title;
            pendingTooltipDescription = description;
            pendingTooltipFlavorText = flavorText;
            pendingTooltipIcon = icon;
            pendingTooltipSubtitle = subtitle;
            pendingTooltipBadgeText = badgeText;
            pendingTooltipIconColor = iconColor;
            pendingTooltipPriority = priority;
            return;
        }

        CancelPendingTooltip();

        float delaySeconds = GetCardHoverDelaySeconds();
        if (delaySeconds <= 0f)
        {
            ShowGeneralCardTooltipImmediate(title, description, flavorText, position, priority, source, icon, subtitle, badgeText, iconColor);
            return;
        }

        pendingCardKind = PendingCardKind.General;
        pendingTooltipTitle = title;
        pendingTooltipDescription = description;
        pendingTooltipFlavorText = flavorText;
        pendingTooltipIcon = icon;
        pendingTooltipSubtitle = subtitle;
        pendingTooltipBadgeText = badgeText;
        pendingTooltipIconColor = iconColor;
        pendingTooltipSource = source;
        pendingTooltipPriority = priority;
        pendingTooltipCoroutine = StartCoroutine(ShowPendingCardTooltipAfterDelay(delaySeconds, position, source));
    }

    private void ScheduleArtifactTooltip(ArtifactData artifact, int overrideLevel, Vector3 position, int priority, GameObject source)
    {
        if (isTooltipVisible && source != null && source == activeTooltipSource && pieceCardTooltipView != null)
        {
            pieceCardTooltipView.SetArtifactData(artifact, overrideLevel);
            activeTooltipPriority = priority;
            return;
        }

        if (isTooltipVisible && source != activeTooltipSource)
        {
            HideActiveCardImmediate();
        }

        if (pendingTooltipCoroutine != null && source != null && source == pendingTooltipSource && pendingCardKind == PendingCardKind.Artifact)
        {
            pendingArtifactData = artifact;
            pendingArtifactLevel = overrideLevel;
            pendingTooltipPriority = priority;
            return;
        }

        CancelPendingTooltip();

        float delaySeconds = GetCardHoverDelaySeconds();
        if (delaySeconds <= 0f)
        {
            ShowArtifactTooltipImmediate(artifact, overrideLevel, position, priority, source);
            return;
        }

        pendingCardKind = PendingCardKind.Artifact;
        pendingArtifactData = artifact;
        pendingArtifactLevel = overrideLevel;
        pendingTooltipSource = source;
        pendingTooltipPriority = priority;
        pendingTooltipCoroutine = StartCoroutine(ShowPendingCardTooltipAfterDelay(delaySeconds, position, source));
    }

    private void ScheduleSealTooltip(SealData seal, Vector3 position, int priority, GameObject source)
    {
        if (isTooltipVisible && source != null && source == activeTooltipSource && pieceCardTooltipView != null)
        {
            pieceCardTooltipView.SetSealData(seal);
            activeTooltipPriority = priority;
            return;
        }

        if (isTooltipVisible && source != activeTooltipSource)
        {
            HideActiveCardImmediate();
        }

        if (pendingTooltipCoroutine != null && source != null && source == pendingTooltipSource && pendingCardKind == PendingCardKind.Seal)
        {
            pendingSealData = seal;
            pendingTooltipPriority = priority;
            return;
        }

        CancelPendingTooltip();

        float delaySeconds = GetCardHoverDelaySeconds();
        if (delaySeconds <= 0f)
        {
            ShowSealTooltipImmediate(seal, position, priority, source);
            return;
        }

        pendingCardKind = PendingCardKind.Seal;
        pendingSealData = seal;
        pendingTooltipSource = source;
        pendingTooltipPriority = priority;
        pendingTooltipCoroutine = StartCoroutine(ShowPendingCardTooltipAfterDelay(delaySeconds, position, source));
    }

    private void SchedulePieceTooltip(PieceType pieceType, SealData attachedSeal, bool isEnemy, Vector3 position, int priority, GameObject source)
    {
        if (isTooltipVisible && source != null && source == activeTooltipSource && pieceCardTooltipView != null)
        {
            pieceCardTooltipView.SetPieceData(pieceType, attachedSeal, isEnemy);
            activeTooltipPriority = priority;
            return;
        }

        if (isTooltipVisible && source != activeTooltipSource)
        {
            HideActiveCardImmediate();
        }

        if (pendingTooltipCoroutine != null && source != null && source == pendingTooltipSource && pendingCardKind == PendingCardKind.Piece)
        {
            pendingPieceType = pieceType;
            pendingSealData = attachedSeal;
            pendingIsEnemy = isEnemy;
            pendingTooltipPriority = priority;
            return;
        }

        CancelPendingTooltip();

        float delaySeconds = GetCardHoverDelaySeconds();
        if (delaySeconds <= 0f)
        {
            ShowPieceTooltipImmediate(pieceType, attachedSeal, isEnemy, position, priority, source);
            return;
        }

        pendingCardKind = PendingCardKind.Piece;
        pendingPieceType = pieceType;
        pendingSealData = attachedSeal;
        pendingIsEnemy = isEnemy;
        pendingTooltipSource = source;
        pendingTooltipPriority = priority;
        pendingTooltipCoroutine = StartCoroutine(ShowPendingCardTooltipAfterDelay(delaySeconds, position, source));
    }

    private System.Collections.IEnumerator ShowPendingCardTooltipAfterDelay(float delaySeconds, Vector3 position, GameObject source)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);

        if (pendingTooltipCoroutine == null || pendingTooltipSource != source)
        {
            yield break;
        }

        if (PieceController.IsAnyDragging || (source != null && (!source.activeInHierarchy || !IsPointerOverSource(source))))
        {
            CancelPendingTooltip();
            yield break;
        }

        PendingCardKind kind = pendingCardKind;
        int priority = pendingTooltipPriority;

        switch (kind)
        {
            case PendingCardKind.Piece:
                ShowPieceTooltipImmediate(pendingPieceType, pendingSealData, pendingIsEnemy, position, priority, source);
                break;
            case PendingCardKind.Artifact:
                ShowArtifactTooltipImmediate(pendingArtifactData, pendingArtifactLevel, position, priority, source);
                break;
            case PendingCardKind.Seal:
                ShowSealTooltipImmediate(pendingSealData, position, priority, source);
                break;
            case PendingCardKind.General:
                ShowGeneralCardTooltipImmediate(
                    pendingTooltipTitle,
                    pendingTooltipDescription,
                    pendingTooltipFlavorText,
                    position,
                    priority,
                    source,
                    pendingTooltipIcon,
                    pendingTooltipSubtitle,
                    pendingTooltipBadgeText,
                    pendingTooltipIconColor);
                break;
        }

        CancelPendingTooltip();
    }

    private void ShowPieceTooltipImmediate(PieceType pieceType, SealData attachedSeal, bool isEnemy, Vector3 position, int priority, GameObject source)
    {
        if (pieceCardTooltipView == null)
        {
            return;
        }

        if (tooltipView != null)
        {
            tooltipView.SetVisible(false);
        }

        pieceCardTooltipView.SetPieceData(pieceType, attachedSeal, isEnemy);
        pieceCardTooltipView.PositionCard(position, source);
        pieceCardTooltipView.Show();

        isTooltipVisible = true;
        isPieceCardActive = true;
        activeTooltipPriority = priority;
        activeTooltipSource = source;
        pieceCardTooltipView.transform.SetAsLastSibling();
    }

    private void ShowArtifactTooltipImmediate(ArtifactData artifact, int overrideLevel, Vector3 position, int priority, GameObject source)
    {
        if (pieceCardTooltipView == null || artifact == null)
        {
            return;
        }

        if (tooltipView != null)
        {
            tooltipView.SetVisible(false);
        }

        pieceCardTooltipView.SetArtifactData(artifact, overrideLevel);
        pieceCardTooltipView.PositionCard(position, source);
        pieceCardTooltipView.Show();

        isTooltipVisible = true;
        isPieceCardActive = true;
        activeTooltipPriority = priority;
        activeTooltipSource = source;
        pieceCardTooltipView.transform.SetAsLastSibling();
    }

    private void ShowSealTooltipImmediate(SealData seal, Vector3 position, int priority, GameObject source)
    {
        if (pieceCardTooltipView == null || seal == null)
        {
            return;
        }

        if (tooltipView != null)
        {
            tooltipView.SetVisible(false);
        }

        pieceCardTooltipView.SetSealData(seal);
        pieceCardTooltipView.PositionCard(position, source);
        pieceCardTooltipView.Show();

        isTooltipVisible = true;
        isPieceCardActive = true;
        activeTooltipPriority = priority;
        activeTooltipSource = source;
        pieceCardTooltipView.transform.SetAsLastSibling();
    }

    private void ShowGeneralCardTooltipImmediate(
        string title,
        string description,
        string flavorText,
        Vector3 position,
        int priority,
        GameObject source,
        Sprite icon,
        string subtitle,
        string badgeText,
        Color? iconColor)
    {
        if (pieceCardTooltipView == null)
        {
            return;
        }

        if (tooltipView != null)
        {
            tooltipView.SetVisible(false);
        }

        pieceCardTooltipView.SetGeneralCardData(title, description, flavorText, icon, subtitle, badgeText, string.Empty, null, iconColor);
        pieceCardTooltipView.PositionCard(position, source);
        pieceCardTooltipView.Show();

        isTooltipVisible = true;
        isPieceCardActive = true;
        activeTooltipPriority = priority;
        activeTooltipSource = source;
        pieceCardTooltipView.transform.SetAsLastSibling();
    }

    private void CancelPendingTooltip()
    {
        if (pendingTooltipCoroutine != null)
        {
            StopCoroutine(pendingTooltipCoroutine);
            pendingTooltipCoroutine = null;
        }

        pendingTooltipSource = null;
        pendingTooltipPriority = int.MinValue;
        pendingCardKind = PendingCardKind.None;
        pendingTooltipTitle = string.Empty;
        pendingTooltipDescription = string.Empty;
        pendingTooltipFlavorText = string.Empty;
        pendingTooltipIcon = null;
        pendingTooltipSubtitle = string.Empty;
        pendingTooltipBadgeText = "★";
        pendingTooltipIconColor = null;
        pendingSealData = null;
        pendingArtifactData = null;
        pendingArtifactLevel = -1;
    }

    public void RegisterTooltipSource(GameObject source)
    {
        if (source == null)
        {
            return;
        }

        tooltipSources.Add(source);
        EnsureSourceCheckRoutine();
    }

    public void UnregisterTooltipSource(GameObject source)
    {
        if (source == null)
        {
            return;
        }

        tooltipSources.Remove(source);
        if (tooltipSources.Count == 0)
        {
            HideTooltip();
            StopSourceCheckRoutine();
        }
    }

    private void EnsureSourceCheckRoutine()
    {
        if (sourceCheckCoroutine == null)
        {
            sourceCheckCoroutine = StartCoroutine(CheckTooltipSourcesRoutine());
        }
    }

    private void StopSourceCheckRoutine()
    {
        if (sourceCheckCoroutine != null)
        {
            StopCoroutine(sourceCheckCoroutine);
            sourceCheckCoroutine = null;
        }
    }

    private System.Collections.IEnumerator CheckTooltipSourcesRoutine()
    {
        var wait = new WaitForSecondsRealtime(0.15f);

        while (true)
        {
            yield return wait;

            if (!isTooltipVisible)
            {
                if (tooltipSources.Count == 0)
                {
                    sourceCheckCoroutine = null;
                    yield break;
                }

                continue;
            }

            bool hasLiveSource = false;
            List<GameObject> deadSources = null;

            foreach (GameObject source in tooltipSources)
            {
                if (source != null && source.activeInHierarchy)
                {
                    hasLiveSource = true;
                    continue;
                }

                deadSources ??= new List<GameObject>();
                deadSources.Add(source);
            }

            if (deadSources != null)
            {
                for (int i = 0; i < deadSources.Count; i++)
                {
                    tooltipSources.Remove(deadSources[i]);
                }
            }

            if (!hasLiveSource)
            {
                HideTooltip();
                sourceCheckCoroutine = null;
                yield break;
            }
        }
    }

    private void OnDestroy()
    {
        StopSourceCheckRoutine();
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
