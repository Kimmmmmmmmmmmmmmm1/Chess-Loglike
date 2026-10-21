using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 체스 시계 모티프의 턴 표시 UI 및 게임 오버까지 남은 스테이지 안내 매니저입니다.
/// CanvasGroup 및 스케일 애니메이션을 사용하지 않고 직접 활성화/색상을 제어하여 크기가 항상 일정하게 유지됩니다.
/// 내 기물(흑)과 상대 기물(백)의 턴 상태를 명확한 사각형 상자와 색상으로 표시합니다.
/// </summary>
public class TurnIndicatorUI : MonoBehaviour
{
    public static TurnIndicatorUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private RectTransform rootContainer;
    [SerializeField] private TextMeshProUGUI remainingStagesText;
    [SerializeField] private TurnIndicatorBox playerBox;
    [SerializeField] private TurnIndicatorBox opponentBox;

    [Header("Settings")]
    [SerializeField] private bool hideOutsideBattle = true;

    private bool isSubscribed = false;

    public static TurnIndicatorUI EnsureInstance()
    {
        // 1. TitleScene 등 게임플레이 씬이 아닌 경우 생성하지 않음
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == "TitleScene")
        {
            if (Instance != null)
            {
                Destroy(Instance.gameObject);
                Instance = null;
            }
            return null;
        }

        // 2. 이미 유효한 인스턴스가 현재 씬에 존재하는지 검증
        if (Instance != null)
        {
            if (Instance.gameObject != null && Instance.gameObject.scene == SceneManager.GetActiveScene())
            {
                Instance.EnsureAllSprites();
                return Instance;
            }

            // 다른 씬이나 파괴된 인스턴스인 경우 정리
            if (Instance.gameObject != null)
            {
                Destroy(Instance.gameObject);
            }
            Instance = null;
        }

        // 3. 현재 씬에 이미 배치된 TurnIndicatorUI가 있는지 확인
        TurnIndicatorUI[] existingList = FindObjectsByType<TurnIndicatorUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TurnIndicatorUI existing in existingList)
        {
            if (existing != null && existing.gameObject.scene == SceneManager.GetActiveScene())
            {
                Instance = existing;
                Instance.EnsureAllSprites();
                return Instance;
            }
        }

        // 4. GameScene 전용 Canvas (640x320) 찾기
        Canvas targetCanvas = FindTargetCanvas();
        if (targetCanvas != null)
        {
            GameObject obj = CreateRuntimeUI(targetCanvas.transform);
            obj.name = "TurnIndicatorUI";
            obj.transform.SetAsLastSibling();
            Instance = obj.GetComponent<TurnIndicatorUI>();
            if (Instance != null) Instance.EnsureAllSprites();
            return Instance;
        }

        return null;
    }

    private static Canvas FindTargetCanvas()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        // 1순위: 현재 씬 내의 이름이 "Canvas"인 오브젝트 (GameScene 기본 인게임 캔버스)
        GameObject mainCanvasObj = GameObject.Find("Canvas");
        if (mainCanvasObj != null && mainCanvasObj.scene == activeScene)
        {
            Canvas c = mainCanvasObj.GetComponent<Canvas>();
            if (c != null) return c;
        }

        // 2순위: 현재 씬 내에서 SettingPanelCanvas, ModalCanvas, TooltipCanvas가 아닌 인게임 캔버스 탐색
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c == null || c.gameObject.scene != activeScene) continue;
            string name = c.gameObject.name;
            if (name.Contains("Modal") || name.Contains("Tooltip") || name.Contains("Setting")) continue;

            CanvasScaler scaler = c.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.referenceResolution.x <= 800f)
            {
                return c;
            }
        }

        // 3순위: 현재 씬의 첫 번째 Canvas (타이틀/모달/툴팁 제외)
        foreach (Canvas c in canvases)
        {
            if (c == null || c.gameObject.scene != activeScene) continue;
            string name = c.gameObject.name;
            if (name.Contains("Modal") || name.Contains("Tooltip") || name.Contains("Setting")) continue;
            return c;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureAllSprites();
    }

    public void EnsureAllSprites()
    {
        if (playerBox != null) playerBox.EnsureSprites();
        if (opponentBox != null) opponentBox.EnsureSprites();
    }

    private void Start()
    {
        EnsureAllSprites();
        SubscribeEvents();
        UpdateRemainingStages();

        GameStateManager.GameState currentState = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameStateManager.GameState.GamePlay;
        UpdateVisibility(currentState, immediate: true);

        if (currentState == GameStateManager.GameState.Prepare)
        {
            SetPlacementState();
        }
        else
        {
            TurnOwner initialTurn = TurnManager.Instance != null ? TurnManager.Instance.CurrentTurn : TurnOwner.Player;
            UpdateTurnState(initialTurn, immediate: true);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SubscribeEvents()
    {
        if (isSubscribed) return;
        isSubscribed = true;

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced += HandleTurnAdvanced;
            TurnManager.Instance.OnPlayerTurnStarted += HandlePlayerTurnStarted;
            TurnManager.Instance.OnOpponentTurnStarted += HandleOpponentTurnStarted;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStageProgressUpdated += HandleStageProgressUpdated;
        }
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed) return;
        isSubscribed = false;

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= HandleTurnAdvanced;
            TurnManager.Instance.OnPlayerTurnStarted -= HandlePlayerTurnStarted;
            TurnManager.Instance.OnOpponentTurnStarted -= HandleOpponentTurnStarted;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStageProgressUpdated -= HandleStageProgressUpdated;
        }
    }

    private void HandleTurnAdvanced(TurnOwner turn)
    {
        UpdateTurnState(turn, immediate: false);
    }

    private void HandlePlayerTurnStarted()
    {
        UpdateTurnState(TurnOwner.Player, immediate: false);
    }

    private void HandleOpponentTurnStarted()
    {
        UpdateTurnState(TurnOwner.Opponent, immediate: false);
    }

    private void HandleGameStateChanged(GameStateManager.GameState newState)
    {
        UpdateVisibility(newState, immediate: false);
        UpdateRemainingStages();

        if (newState == GameStateManager.GameState.Prepare)
        {
            SetPlacementState();
        }
        else if (newState == GameStateManager.GameState.GamePlay)
        {
            TurnOwner turn = TurnManager.Instance != null ? TurnManager.Instance.CurrentTurn : TurnOwner.Player;
            UpdateTurnState(turn, immediate: true);
        }
    }

    private void HandleStageProgressUpdated(int cleared, int remaining)
    {
        UpdateRemainingStages();
    }

    public void UpdateRemainingStages()
    {
        if (remainingStagesText == null) return;

        int remaining = GameManager.Instance != null ? GameManager.Instance.RemainingStagesUntilGameOver : 30;
        remainingStagesText.text = $"남은 스테이지: {remaining}";
    }

    /// <summary>
    /// 기물 배치(Prepare) 중일 때 양측 모두 '대기중' 및 비활성 상태로 표시합니다.
    /// </summary>
    public void SetPlacementState()
    {
        if (playerBox != null)
        {
            playerBox.SetState(false, "대기중");
        }

        if (opponentBox != null)
        {
            opponentBox.SetState(false, "대기중");
        }
    }

    public void UpdateTurnState(TurnOwner currentTurn, bool immediate = false)
    {
        // 기물 배치 중(Prepare)인 경우 양측 모두 대기중 상태를 유지
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            SetPlacementState();
            return;
        }

        bool isPlayer = currentTurn == TurnOwner.Player;

        if (playerBox != null)
        {
            playerBox.SetState(isPlayer, isPlayer ? "내 턴" : "대기중");
        }

        if (opponentBox != null)
        {
            opponentBox.SetState(!isPlayer, !isPlayer ? "상대 턴" : "대기중");
        }
    }

    private void UpdateVisibility(GameStateManager.GameState state, bool immediate = false)
    {
        if (!hideOutsideBattle) return;

        bool shouldShow = state == GameStateManager.GameState.Prepare ||
                          state == GameStateManager.GameState.GamePlay;

        if (rootContainer != null)
        {
            rootContainer.gameObject.SetActive(shouldShow);
        }
        else
        {
            gameObject.SetActive(shouldShow);
        }
    }

    #region Runtime UI Generator

    private static Sprite cachedSolidSprite = null;

    public static Sprite GetOrCreateBoxSprite()
    {
        if (cachedSolidSprite != null && cachedSolidSprite.texture != null)
        {
            return cachedSolidSprite;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "TurnClock_SolidQuad";
        texture.filterMode = FilterMode.Point;
        Color[] pixels = new Color[] { Color.white, Color.white, Color.white, Color.white };
        texture.SetPixels(pixels);
        texture.Apply();

        cachedSolidSprite = Sprite.Create(
            texture,
            new Rect(0, 0, 2, 2),
            new Vector2(0.5f, 0.5f)
        );

        return cachedSolidSprite;
    }

    private static GameObject CreateRuntimeUI(Transform parent)
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium SDF");
        if (font == null && GameManager.Instance != null && GameManager.Instance.coinText != null)
        {
            font = GameManager.Instance.coinText.font;
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;

        Sprite boxSprite = GetOrCreateBoxSprite();

        // 루트 오브젝트 생성 (CanvasGroup 제거, 순수 RectTransform)
        GameObject root = new GameObject("TurnIndicatorUI", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -6f);
        rootRect.sizeDelta = new Vector2(210f, 48f);
        rootRect.localScale = Vector3.one;

        // 1. 남은 스테이지 뱃지 (상단)
        GameObject stageBadgeObj = new GameObject("RemainingStageBadge", typeof(RectTransform));
        stageBadgeObj.transform.SetParent(root.transform, false);
        RectTransform stageBadgeRect = stageBadgeObj.GetComponent<RectTransform>();
        stageBadgeRect.anchorMin = new Vector2(0.5f, 1f);
        stageBadgeRect.anchorMax = new Vector2(0.5f, 1f);
        stageBadgeRect.pivot = new Vector2(0.5f, 1f);
        stageBadgeRect.anchoredPosition = new Vector2(0f, 0f);
        stageBadgeRect.sizeDelta = new Vector2(130f, 16f);
        stageBadgeRect.localScale = Vector3.one;

        Image stageBadgeBorder = stageBadgeObj.AddComponent<Image>();
        stageBadgeBorder.sprite = boxSprite;
        stageBadgeBorder.type = Image.Type.Simple;
        stageBadgeBorder.color = new Color(0.40f, 0.40f, 0.40f, 0.9f);
        stageBadgeBorder.raycastTarget = false;

        GameObject stageBadgeFace = new GameObject("Face", typeof(RectTransform));
        stageBadgeFace.transform.SetParent(stageBadgeObj.transform, false);
        RectTransform stageFaceRect = stageBadgeFace.GetComponent<RectTransform>();
        stageFaceRect.anchorMin = Vector2.zero;
        stageFaceRect.anchorMax = Vector2.one;
        stageFaceRect.offsetMin = new Vector2(1.5f, 1.5f);
        stageFaceRect.offsetMax = new Vector2(-1.5f, -1.5f);
        stageFaceRect.localScale = Vector3.one;

        Image stageBadgeBg = stageBadgeFace.AddComponent<Image>();
        stageBadgeBg.sprite = boxSprite;
        stageBadgeBg.type = Image.Type.Simple;
        stageBadgeBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        stageBadgeBg.raycastTarget = false;

        GameObject stageTextObj = new GameObject("StageText", typeof(RectTransform));
        stageTextObj.transform.SetParent(stageBadgeFace.transform, false);
        RectTransform stageRect = stageTextObj.GetComponent<RectTransform>();
        stageRect.anchorMin = Vector2.zero;
        stageRect.anchorMax = Vector2.one;
        stageRect.anchoredPosition = Vector2.zero;
        stageRect.sizeDelta = Vector2.zero;
        stageRect.localScale = Vector3.one;

        TextMeshProUGUI stageText = stageTextObj.AddComponent<TextMeshProUGUI>();
        if (font != null) stageText.font = font;
        stageText.text = "남은 스테이지: 30";
        stageText.fontSize = 10f;
        stageText.fontStyle = FontStyles.Bold;
        stageText.alignment = TextAlignmentOptions.Center;
        stageText.color = new Color(0.95f, 0.90f, 0.78f, 1f);
        stageText.raycastTarget = false;

        // 2. 턴 박스 컨테이너 (하단, 수평 배치)
        GameObject containerObj = new GameObject("BoxesContainer", typeof(RectTransform));
        containerObj.transform.SetParent(root.transform, false);
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0f);
        containerRect.anchorMax = new Vector2(0.5f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0f, 0f);
        containerRect.sizeDelta = new Vector2(204f, 26f);
        containerRect.localScale = Vector3.one;

        HorizontalLayoutGroup hlg = containerObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // 3. Player Box (내 기물: 흑)
        TurnIndicatorBox pBox = CreateClockBox(
            containerObj.transform,
            "PlayerBox",
            "흑",
            "대기중",
            boxSprite,
            font,
            new Color(0.88f, 0.88f, 0.88f, 1f),   // activeBorder (은백색 테두리)
            new Color(0.25f, 0.25f, 0.25f, 0.8f), // inactiveBorder (어두운 회색)
            new Color(0.08f, 0.08f, 0.08f, 1f),   // activeBg (흑색)
            new Color(0.12f, 0.12f, 0.12f, 0.8f), // inactiveBg (비활성 어두운 회색)
            new Color(1f, 1f, 1f, 1f),            // activeText (화이트)
            new Color(0.45f, 0.45f, 0.45f, 1f),   // inactiveText
            new Color(0f, 0f, 0f, 0.5f)           // shadowColor
        );

        // 4. Opponent Box (상대 기물: 백)
        TurnIndicatorBox oBox = CreateClockBox(
            containerObj.transform,
            "OpponentBox",
            "백",
            "대기중",
            boxSprite,
            font,
            new Color(0.28f, 0.18f, 0.10f, 1f),   // activeBorder (진한 목재 테두리)
            new Color(0.20f, 0.14f, 0.10f, 0.8f), // inactiveBorder
            new Color(0.94f, 0.85f, 0.71f, 1f),   // activeBg (따뜻한 체스판 백색/아이보리)
            new Color(0.48f, 0.43f, 0.36f, 0.8f), // inactiveBg (채도 낮춘 어두운 베이지)
            new Color(0.10f, 0.06f, 0.03f, 1f),   // activeText (짙은 흑갈색)
            new Color(0.28f, 0.22f, 0.18f, 1f),   // inactiveText
            new Color(0f, 0f, 0f, 0.5f)           // shadowColor
        );

        TurnIndicatorUI uiComp = root.AddComponent<TurnIndicatorUI>();
        uiComp.rootContainer = rootRect;
        uiComp.remainingStagesText = stageText;
        uiComp.playerBox = pBox;
        uiComp.opponentBox = oBox;

        return root;
    }

    private static TurnIndicatorBox CreateClockBox(
        Transform parent,
        string name,
        string label,
        string status,
        Sprite boxSprite,
        TMP_FontAsset font,
        Color activeBorder,
        Color inactiveBorder,
        Color activeBg,
        Color inactiveBg,
        Color activeText,
        Color inactiveText,
        Color shadowColor)
    {
        GameObject boxObj = new GameObject(name, typeof(RectTransform));
        boxObj.transform.SetParent(parent, false);

        RectTransform boxRect = boxObj.GetComponent<RectTransform>();
        boxRect.sizeDelta = new Vector2(96f, 24f);
        boxRect.localScale = Vector3.one;

        LayoutElement le = boxObj.AddComponent<LayoutElement>();
        le.preferredWidth = 96f;
        le.preferredHeight = 24f;
        le.minWidth = 96f;
        le.minHeight = 24f;

        // 1. Shadow (하단 그림자)
        GameObject shadowObj = new GameObject("Shadow", typeof(RectTransform));
        shadowObj.transform.SetParent(boxObj.transform, false);
        RectTransform shadowRect = shadowObj.GetComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.offsetMin = new Vector2(0f, -2f);
        shadowRect.offsetMax = new Vector2(0f, -2f);
        shadowRect.localScale = Vector3.one;

        Image shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = boxSprite;
        shadowImg.type = Image.Type.Simple;
        shadowImg.color = shadowColor;
        shadowImg.raycastTarget = false;

        // 2. Outer Border (외곽 테두리 사각형)
        GameObject borderObj = new GameObject("Border", typeof(RectTransform));
        borderObj.transform.SetParent(boxObj.transform, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        borderRect.localScale = Vector3.one;

        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.sprite = boxSprite;
        borderImg.type = Image.Type.Simple;
        borderImg.color = activeBorder;
        borderImg.raycastTarget = false;

        // 3. Inner Face (내부 바탕 사각형 - 2px 안쪽으로 오프셋)
        GameObject faceObj = new GameObject("Face", typeof(RectTransform));
        faceObj.transform.SetParent(borderObj.transform, false);
        RectTransform faceRect = faceObj.GetComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.offsetMin = new Vector2(2f, 2f);
        faceRect.offsetMax = new Vector2(-2f, -2f);
        faceRect.localScale = Vector3.one;

        Image faceImg = faceObj.AddComponent<Image>();
        faceImg.sprite = boxSprite;
        faceImg.type = Image.Type.Simple;
        faceImg.color = activeBg;
        faceImg.raycastTarget = false;

        // 4. Label ("흑" / "백")
        GameObject labelObj = new GameObject("LabelText", typeof(RectTransform));
        labelObj.transform.SetParent(faceObj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0.4f, 1f);
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = new Vector2(0f, 0f);
        labelRect.localScale = Vector3.one;

        TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        if (font != null) labelTmp.font = font;
        labelTmp.text = label;
        labelTmp.fontSize = 11f;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        labelTmp.color = activeText;
        labelTmp.raycastTarget = false;

        // 5. Status ("내 턴" / "상대 턴" / "대기")
        GameObject statusObj = new GameObject("StatusText", typeof(RectTransform));
        statusObj.transform.SetParent(faceObj.transform, false);
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.35f, 0f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.offsetMin = new Vector2(0f, 0f);
        statusRect.offsetMax = new Vector2(-6f, 0f);
        statusRect.localScale = Vector3.one;

        TextMeshProUGUI statusTmp = statusObj.AddComponent<TextMeshProUGUI>();
        if (font != null) statusTmp.font = font;
        statusTmp.text = status;
        statusTmp.fontSize = 10f;
        statusTmp.fontStyle = FontStyles.Bold;
        statusTmp.alignment = TextAlignmentOptions.MidlineRight;
        statusTmp.color = activeText;
        statusTmp.raycastTarget = false;

        TurnIndicatorBox boxComp = boxObj.AddComponent<TurnIndicatorBox>();
        boxComp.Initialize(
            boxRect,
            borderImg,
            faceImg,
            shadowImg,
            labelTmp,
            statusTmp,
            activeBorder,
            inactiveBorder,
            activeBg,
            inactiveBg,
            activeText,
            inactiveText
        );

        return boxComp;
    }

    #endregion
}
