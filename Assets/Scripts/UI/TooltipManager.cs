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

    [Header("Tooltip UI")]
    [SerializeField] private TooltipView tooltipView;
    [SerializeField] private Vector3 tooltipOffset = new Vector3(20, 20, 0);
    private RectTransform tooltipRect;
    private Canvas parentCanvas;
    private GameObject runtimeTooltipCanvas;
    private readonly HashSet<GameObject> tooltipSources = new HashSet<GameObject>();
    private Coroutine sourceCheckCoroutine;
    private Coroutine pendingTooltipCoroutine;
    private GameObject pendingTooltipSource;
    private int pendingTooltipPriority = int.MinValue;
    private string pendingTooltipTitle = string.Empty;
    private string pendingTooltipDescription = string.Empty;
    private string pendingTooltipFlavorText = string.Empty;
    private bool isTooltipVisible;
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
        if (tooltipView == null)
        {
            tooltipView = FindAnyObjectByType<TooltipView>(FindObjectsInactive.Include);
        }

        if (tooltipView == null)
        {
            GameObject prefabObject = Resources.Load<GameObject>(DefaultTooltipViewPrefabPath);
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
    }

    private void Update()
    {
        if (tooltipView != null && tooltipView.gameObject.activeSelf)
        {
            UpdatePosition(GetMousePosition());
            ClampToScreen();
        }
    }

    public void ShowTooltip(string title, string description, Vector3 position, string flavorText = "", int priority = TooltipPriorityDefault, GameObject source = null)
    {
        if (isTooltipVisible && source != activeTooltipSource && priority < activeTooltipPriority)
        {
            return;
        }

        if (pendingTooltipCoroutine != null && source != pendingTooltipSource && priority < pendingTooltipPriority)
        {
            return;
        }

        if (tooltipView == null)
        {
            InitTooltipView();
            if (tooltipView == null) return;
        }

        ScheduleTooltip(title, description, flavorText, position, priority, source);
    }

    /// <summary>
    /// 아티팩트 데이터를 받아서 레벨에 맞게 동적 설명을 표시하는 메서드
    /// </summary>
    public void ShowArtifactTooltip(ArtifactData artifact, int level = 1)
    {
        if (artifact == null)
        {
            return;
        }

        if (tooltipView == null)
        {
            InitTooltipView();
            if (tooltipView == null) return;
        }

        ScheduleTooltip(artifact.GetTooltipTitle(), artifact.GetDescription(level), artifact.flavorText, Vector3.zero, TooltipPriorityDefault, null);
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
            if (pendingTooltipSource == source)
            {
                CancelPendingTooltip();
            }

            if (activeTooltipSource != null && source != activeTooltipSource)
            {
                return;
            }
        }
        else
        {
            CancelPendingTooltip();
        }

        if (tooltipView != null)
        {
            tooltipView.SetVisible(false);
        }
        isTooltipVisible = false;
        activeTooltipPriority = int.MinValue;
        activeTooltipSource = null;
    }

    private void RefreshTooltipDelay()
    {
        if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
        {
            currentTooltipDelay = SettingsManager.Instance.Settings.tooltipDelay;
        }
    }

    private float GetTooltipDelaySeconds()
    {
        return currentTooltipDelay switch
        {
            SettingsData.TooltipDelay.HalfSecond => 0.5f,
            SettingsData.TooltipDelay.OneSecond => 1f,
            _ => 0f
        };
    }

    private void ScheduleTooltip(string title, string description, string flavorText, Vector3 position, int priority, GameObject source)
    {
        CancelPendingTooltip();

        float delaySeconds = GetTooltipDelaySeconds();
        if (delaySeconds <= 0f)
        {
            ShowTooltipImmediate(title, description, flavorText, position, priority, source);
            return;
        }

        pendingTooltipTitle = title;
        pendingTooltipDescription = description;
        pendingTooltipFlavorText = flavorText;
        pendingTooltipSource = source;
        pendingTooltipPriority = priority;
        pendingTooltipCoroutine = StartCoroutine(ShowTooltipAfterDelay(delaySeconds, position, source));
    }

    private System.Collections.IEnumerator ShowTooltipAfterDelay(float delaySeconds, Vector3 position, GameObject source)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);

        if (pendingTooltipCoroutine == null || pendingTooltipSource != source)
        {
            yield break;
        }

        if (source != null && !source.activeInHierarchy)
        {
            CancelPendingTooltip();
            yield break;
        }

        ShowTooltipImmediate(pendingTooltipTitle, pendingTooltipDescription, pendingTooltipFlavorText, position, pendingTooltipPriority, source);
        CancelPendingTooltip();
    }

    private void ShowTooltipImmediate(string title, string description, string flavorText, Vector3 position, int priority, GameObject source)
    {
        if (tooltipView == null)
        {
            return;
        }

        tooltipView.SetContent(title, description, flavorText);
        tooltipView.SetVisible(true);
        isTooltipVisible = true;
        activeTooltipPriority = priority;
        activeTooltipSource = source;
        tooltipView.transform.SetAsLastSibling();

        UpdatePosition(GetMousePosition());

        if (tooltipRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
            ClampToScreen();
        }
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
        pendingTooltipTitle = string.Empty;
        pendingTooltipDescription = string.Empty;
        pendingTooltipFlavorText = string.Empty;
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
