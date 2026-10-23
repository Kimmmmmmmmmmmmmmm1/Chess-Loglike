using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 업적 달성 시 화면 우상단에 토스트 알림을 띄우는 영구 매니저입니다.
/// AchievementManager의 OnAchievementUnlocked 이벤트를 자동으로 구독하여
/// 1.5초간 토스트 메시지를 표시하고, 여러 업적이 동시에 달성될 경우 순차적으로 큐잉하여 재생합니다.
/// </summary>
[DisallowMultipleComponent]
public class AchievementToastManager : PersistentManagerBase
{
    public static AchievementToastManager Instance { get; private set; }

    private const int ToastCanvasSortingOrder = 30500;
    private const string CanvasGameObjectName = "AchievementToastCanvas";

    [Header("Toast Configuration")]
    [Tooltip("커스텀 토스트 프리팹 (지정 시 우선 사용).")]
    [SerializeField] private AchievementToastUI toastPrefab;

    [Tooltip("Resources 폴더 내 토스트 프리팹 경로")]
    [SerializeField] private string resourcesPrefabPath = "UI/AchievementToast";

    [Tooltip("토스트 메시지가 화면에 머무는 시간(초). 요구사항: 1.5초")]
    [Range(0.5f, 5.0f)]
    [SerializeField] private float displayDuration = 1.5f;

    [Tooltip("우상단 기준 앵커 오프셋 위치 (X는 오른쪽 여백 음수, Y는 상단 여백 음수)")]
    [SerializeField] private Vector2 toastAnchorPosition = new Vector2(-24f, -24f);

    [Tooltip("업적 달성 토스트 표시 시 효과음 재생 여부")]
    [SerializeField] private bool playSuccessSound = true;

    private GameObject runtimeToastCanvas;
    private AchievementToastUI runtimeToastView;
    private readonly Queue<AchievementData> pendingQueue = new Queue<AchievementData>();
    private bool isShowingToast = false;
    private bool isSubscribed = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        EnsureInstance();
    }

    public static AchievementToastManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        AchievementToastManager existing = FindFirstObjectByType<AchievementToastManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            if (!existing.gameObject.activeSelf)
            {
                existing.gameObject.SetActive(true);
            }
            return Instance != null ? Instance : existing;
        }

        GameObject go = new GameObject("AchievementToastManager");
        return go.AddComponent<AchievementToastManager>();
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

        EnsureToastView();
        TrySubscribeToAchievementManager();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        TrySubscribeToAchievementManager();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnsubscribeFromAchievementManager();
    }

    protected override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        base.OnSceneLoaded(scene, mode);
        TrySubscribeToAchievementManager();
        EnsureToastView();
    }

    public override void ResetForNewRun()
    {
        // 런 재시작 시 잔여 대기 토스트 정리
        pendingQueue.Clear();
        if (runtimeToastView != null)
        {
            runtimeToastView.KillActiveSequence();
            runtimeToastView.gameObject.SetActive(false);
        }
        isShowingToast = false;
    }

    private void TrySubscribeToAchievementManager()
    {
        if (isSubscribed)
        {
            return;
        }

        AchievementManager manager = AchievementManager.Instance;
        if (manager == null)
        {
            manager = AchievementManager.EnsureInstance();
        }

        if (manager != null)
        {
            manager.OnAchievementUnlocked += HandleAchievementUnlocked;
            isSubscribed = true;
        }
    }

    private void UnsubscribeFromAchievementManager()
    {
        if (!isSubscribed)
        {
            return;
        }

        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnAchievementUnlocked -= HandleAchievementUnlocked;
        }

        isSubscribed = false;
    }

    /// <summary>
    /// 업적 달성 이벤트 핸들러
    /// </summary>
    private void HandleAchievementUnlocked(AchievementData achievement)
    {
        if (achievement == null)
        {
            return;
        }

        ShowToast(achievement);
    }

    /// <summary>
    /// 지정된 업적의 달성 토스트를 띄웁니다. 현재 다른 토스트가 표시 중이면 큐에 대기합니다.
    /// </summary>
    public void ShowToast(AchievementData achievement)
    {
        if (achievement == null)
        {
            return;
        }

        pendingQueue.Enqueue(achievement);

        if (!isShowingToast)
        {
            ProcessNextToast();
        }
    }

    private void ProcessNextToast()
    {
        if (pendingQueue.Count == 0)
        {
            isShowingToast = false;
            return;
        }

        AchievementToastUI toastView = EnsureToastView();
        if (toastView == null)
        {
            isShowingToast = false;
            return;
        }

        isShowingToast = true;
        AchievementData nextAchievement = pendingQueue.Dequeue();

        // 효과음 재생
        if (playSuccessSound && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUI(UIType.Success);
        }

        // 토스트 표시 (1.5초 유지 후 퇴장 완료 시 다음 토스트 실행)
        toastView.PlayToast(nextAchievement, displayDuration, toastAnchorPosition, OnToastFinished);
    }

    private void OnToastFinished()
    {
        if (pendingQueue.Count > 0)
        {
            // 연속 토스트 간의 부드러운 전환을 위해 약간의 딜레이 후 실행
            StartCoroutine(WaitAndProcessNext(0.08f));
        }
        else
        {
            isShowingToast = false;
        }
    }

    private IEnumerator WaitAndProcessNext(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        ProcessNextToast();
    }

    /// <summary>
    /// 토스트를 렌더링할 전용 Canvas 및 ToastView를 찾아오거나 자동 생성합니다.
    /// </summary>
    private AchievementToastUI EnsureToastView()
    {
        if (runtimeToastView != null)
        {
            return runtimeToastView;
        }

        GameObject canvasObj = GetOrCreateToastCanvas();

        AchievementToastUI prefabToUse = toastPrefab;
        if (prefabToUse == null && !string.IsNullOrEmpty(resourcesPrefabPath))
        {
            GameObject loadedGo = Resources.Load<GameObject>(resourcesPrefabPath);
            if (loadedGo != null)
            {
                prefabToUse = loadedGo.GetComponent<AchievementToastUI>();
            }
        }

        if (prefabToUse != null)
        {
            runtimeToastView = Instantiate(prefabToUse, canvasObj.transform);
            runtimeToastView.name = "AchievementToast";
            runtimeToastView.ResolveReferences();
        }
        else
        {
            runtimeToastView = AchievementToastUI.CreateDefault(canvasObj.transform);
        }

        return runtimeToastView;
    }

    private GameObject GetOrCreateToastCanvas()
    {
        if (runtimeToastCanvas != null)
        {
            return runtimeToastCanvas;
        }

        runtimeToastCanvas = GameObject.Find(CanvasGameObjectName);
        if (runtimeToastCanvas != null)
        {
            return runtimeToastCanvas;
        }

        runtimeToastCanvas = new GameObject(CanvasGameObjectName);
        DontDestroyOnLoad(runtimeToastCanvas);

        Canvas canvas = runtimeToastCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ToastCanvasSortingOrder;

        CanvasScaler scaler = runtimeToastCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        runtimeToastCanvas.AddComponent<GraphicRaycaster>();

        return runtimeToastCanvas;
    }

    #region Debug Context Menu

    [ContextMenu("Debug/Show Test Toast")]
    private void DebugShowTestToast()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AchievementToastManager] 테스트 토스트는 플레이 모드에서만 실행할 수 있습니다.");
            return;
        }

        AchievementData testData = ScriptableObject.CreateInstance<AchievementData>();
        testData.id = "test_toast_preview";
        testData.achievementName = "테스트 업적 달성!";
        testData.description = "우상단 1.5초 토스트 알림이 정상적으로 작동하고 있습니다.";
        testData.category = AchievementCategory.Other;

        ShowToast(testData);
    }

    #endregion
}

