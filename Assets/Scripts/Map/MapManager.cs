using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MapManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private MapConfig mapConfig;
    public MapConfig MapConfig => mapConfig;
    [SerializeField] private int mapSeed = -1; // -1이면 랜덤 시드 사용

    [Header("References")]
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private ScrollRect mapScrollView;
    [SerializeField] private GameObject linePrefab; // UI Image Prefab으로 변경


    [Header("Assets")]
    // In a real project, use a ScriptableObject dictionary for Type->Sprite
    [SerializeField] private Sprite battleIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite treasureIcon;
    [SerializeField] private Sprite bossIcon;
    [SerializeField] private Sprite mysteryIcon;
    [SerializeField] private Sprite eventIcon;
    [SerializeField] private Sprite workShopIcon;

    [Header("Scroll Preview")]
    [SerializeField] private float scrollPreviewDuration = 3f; // 맵 전체 스크롤 소요 시간
    [SerializeField] private float scrollPreviewDelay = 0.3f;   // 맵 열린 후 스크롤 시작까지 대기
    [SerializeField] private Ease scrollPreviewEase = Ease.InCubic; // 가속도: 천천히 시작 → 빠르게

    [Header("Panel Animation")]
    [SerializeField] private Image panelAnimationImage;
    [SerializeField] private RectTransform panelAnimationTransform;
    [SerializeField] private float collapsedPanelWidth = 72f;
    [SerializeField] private float expandedPanelWidth = 512f;
    [SerializeField] private float collapsedTransformWidth = 8f;
    [SerializeField] private float expandedTransformWidth = 360f;
    [SerializeField] private float panelWidthDuration = 0.4f;

    private MapData currentMapData;
    private List<MapNodeView> spawnedNodes = new List<MapNodeView>();
    private MapNodeData currentNode;
    private MapNodeData pendingNodeProgress;
    public MapNodeData CurrentNode => currentNode;
    public IEnumerable<MapNodeView> ActiveNodes => spawnedNodes.Where(node => node != null && node.IsSelectable);
    private List<string> visitedNodeIds = new List<string>();
    public int VisitedNodeCount => visitedNodeIds.Count;
    private Dictionary<string, Image> connectionLines = new Dictionary<string, Image>();
    [SerializeField]
    [Tooltip("Multiplier applied to vertical spacing only (distance between floors).")]
    private float verticalSpacingMultiplier = 1/30f;
    private Vector2 mapBoundsMin;
    private Vector2 mapBoundsMax;
    private List<MapNodeView> cachedActiveNodes = new List<MapNodeView>();
    private Sequence mapPanelSequence;
    private Dictionary<RectTransform, Vector2> panelAnimationOpenPositions = new Dictionary<RectTransform, Vector2>();

    private void Update()
    {
        HandleNodeKeyboardInput();
    }

    private void HandleNodeKeyboardInput()
    {
        if (!IsMapFlowState())
        {
            return;
        }

        if (ModalManager.IsKeyboardBlocked)
        {
            return;
        }

        if (KeyManager.Instance == null)
        {
            return;
        }

        ShortcutAction[] nodeShortcutActions = new ShortcutAction[]
        {
            ShortcutAction.first,
            ShortcutAction.second,
            ShortcutAction.third,
            ShortcutAction.fourth,
            ShortcutAction.fifth,
            ShortcutAction.sixth,
            ShortcutAction.seventh,
            ShortcutAction.eighth,
            ShortcutAction.ninth,
            ShortcutAction.zero,
        };

        for (int i = 0; i < nodeShortcutActions.Length; i++)
        {
            if (KeyManager.Instance.IsPressed(nodeShortcutActions[i]))
            {
                if (i < cachedActiveNodes.Count)
                {
                    var activeNode = cachedActiveNodes[i];
                    if (activeNode != null && activeNode.IsSelectable)
                    {
                        activeNode.GetComponent<Button>().onClick.Invoke();
                    }
                }
            }
        }
    }

    private RectTransform GetViewportRect()
    {
        if (mapScrollView == null)
        {
            return null;
        }

        if (mapScrollView.viewport != null)
        {
            return mapScrollView.viewport;
        }

        return mapScrollView.GetComponent<RectTransform>();
    }
    
    public static MapManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        GenerateAndDrawMap();
    }

    public void GenerateAndDrawMap()
    {
        // 1. Generate Data
        // 시드가 -1이면 랜덤 시드 생성
        int seed = mapSeed < 0 ? UnityEngine.Random.Range(0, int.MaxValue) : mapSeed;
        currentMapData = MapGenerator.GenerateMap(mapConfig, seed);

        RectTransform viewportRect = GetViewportRect();
        float viewportHeight = viewportRect != null ? viewportRect.rect.height : 0f;
        float viewportWidth = viewportRect != null ? viewportRect.rect.width : 0f;

        float baseMapHeight = Mathf.Max(1f, (mapConfig.height - 1) * mapConfig.nodeSpacingY * verticalSpacingMultiplier);

        mapBoundsMin = new Vector2(float.MaxValue, float.MaxValue);
        mapBoundsMax = new Vector2(float.MinValue, float.MinValue);
        foreach (var nodeData in currentMapData.nodes)
        {
            float scaledX = nodeData.renderPosX * mapConfig.nodeSpacingX;
            float scaledY = nodeData.renderPosY * mapConfig.nodeSpacingY * verticalSpacingMultiplier;
            mapBoundsMin.x = Mathf.Min(mapBoundsMin.x, scaledX);
            mapBoundsMin.y = Mathf.Min(mapBoundsMin.y, scaledY);
            mapBoundsMax.x = Mathf.Max(mapBoundsMax.x, scaledX);
            mapBoundsMax.y = Mathf.Max(mapBoundsMax.y, scaledY);
        }

        if (mapBoundsMin.x == float.MaxValue)
        {
            mapBoundsMin = Vector2.zero;
            mapBoundsMax = Vector2.zero;
        }

        Vector2 mapBoundsCenter = (mapBoundsMin + mapBoundsMax) * 0.5f;
        Vector2 mapBoundsSize = mapBoundsMax - mapBoundsMin;

        // 2. Clear Old Map
        foreach (Transform child in mapContainer)
        {
            Destroy(child.gameObject);
        }
        spawnedNodes.Clear();
        visitedNodeIds.Clear();
        connectionLines.Clear();

        // 2.5 Resize Container (Scroll View Content)
        // 맵 크기에 맞춰 컨테이너 크기 조절 (여유 공간 포함)
        // Container Anchor는 (0.5, 1) = 상단 중앙으로 유지

        mapContainer.pivot = new Vector2(0.5f, 1f);
        // Anchor는 건드리지 않음 - 씬에 이미 (0.5, 1)로 설정됨
        
        // 노드 1개 크기만큼 패딩 추가
        float nodePadding = mapConfig.nodeSize;
        float containerHeight = Mathf.Max(1f, mapBoundsSize.y + mapConfig.nodeSpacingY * verticalSpacingMultiplier * 0.5f + nodePadding * 2f);
        float containerWidth = Mathf.Max(1f, mapBoundsSize.x + mapConfig.nodeSpacingX * 0.5f + nodePadding * 2f);
        
        mapContainer.sizeDelta = new Vector2(containerWidth, containerHeight);

        // 3. Instantiate Nodes
        Dictionary<string, MapNodeView> viewLookup = new Dictionary<string, MapNodeView>();

        foreach (var nodeData in currentMapData.nodes)
        {
            GameObject obj = Instantiate(nodePrefab, mapContainer);
            Vector2 nodePosition = new Vector2(nodeData.renderPosX * mapConfig.nodeSpacingX, nodeData.renderPosY * mapConfig.nodeSpacingY * verticalSpacingMultiplier) - mapBoundsCenter;

            // UI 환경에 맞춰 RectTransform의 anchoredPosition 사용
            RectTransform nodeRect = obj.GetComponent<RectTransform>();
            if (nodeRect != null)
            {
                nodeRect.pivot = new Vector2(0.5f, 0.5f); // 노드 피벗도 중앙으로 강제
                nodeRect.anchoredPosition = nodePosition;
            }
            else
            {
                obj.transform.localPosition = new Vector3(nodePosition.x, nodePosition.y, 0);
            }

            obj.transform.localScale = Vector3.one * mapConfig.nodeSize; // 설정된 크기 적용

            MapNodeView view = obj.GetComponent<MapNodeView>();
            view.Initialize(nodeData, GetIconForType(nodeData.type), OnNodeClicked);

            spawnedNodes.Add(view);
            viewLookup.Add(nodeData.id, view);
        }

        // 4. Draw Connections (Edges)
        foreach (var nodeData in currentMapData.nodes)
        {
            foreach (var nextId in nodeData.nextNodeIds)
            {
                if (viewLookup.TryGetValue(nextId, out MapNodeView nextView))
                {
                    RectTransform startRect = viewLookup[nodeData.id].GetComponent<RectTransform>();
                    RectTransform endRect = nextView.GetComponent<RectTransform>();
                    if (startRect == null || endRect == null)
                    {
                        continue;
                    }

                    GameObject line = CreateConnection(startRect.anchoredPosition, endRect.anchoredPosition);
                    Image lineImage = line.GetComponent<Image>();
                    if (lineImage != null)
                    {
                        Color c = Color.white;
                        c.a = 0.2f; // 기본은 반투명
                        lineImage.color = c;
                        connectionLines.Add($"{nodeData.id}-{nextId}", lineImage);
                    }
                }
            }
        }

        // 5. Initialize Game State (Start at floor 0)
        UpdateMapState(null);

        // 6. Scroll to start position (Initial View)
    }

    private GameObject CreateConnection(Vector3 start, Vector3 end)
    {
        GameObject line = Instantiate(linePrefab, mapContainer);
        line.transform.SetAsFirstSibling(); // 노드보다 뒤에 그려지도록 순서 변경 (가장 먼저 그리기)

        RectTransform rect = line.GetComponent<RectTransform>();
        Vector3 dir = end - start;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rect.anchoredPosition = start + dir / 2.0f; // 두 점의 중간 위치
        rect.localRotation = Quaternion.Euler(0, 0, angle); // 방향에 맞춰 회전

        float drawDistance = Mathf.Max(0, distance - mapConfig.lineGap * 2); // 양쪽 끝에서 간격만큼 뺌
        rect.sizeDelta = new Vector2(drawDistance, mapConfig.lineThickness); // 길이는 거리만큼, 두께는 설정값 적용

        // 1. RawImage 사용: 실선 렌더링 (타일링 비활성화)
        RawImage lineRawImage = line.GetComponent<RawImage>();
        if (lineRawImage != null)
        {
            lineRawImage.uvRect = new Rect(0, 0, 1, 1);
        }
        // 2. 기존 Image 사용 - RawImage가 없을 경우의 폴백
        else
        {
            Image lineImage = line.GetComponent<Image>();
            if (lineImage != null)
            {
                lineImage.type = Image.Type.Simple;
                lineImage.pixelsPerUnitMultiplier = 1f;
            }
        }

        return line;
    }

    private Sprite GetIconForType(NodeType type)
    {
        switch (type)
        {
            case NodeType.Battle: return battleIcon;
            case NodeType.Shop: return shopIcon;
            case NodeType.Treasure: return treasureIcon;
            case NodeType.Boss: return bossIcon;
            case NodeType.Mystery: return mysteryIcon;
            case NodeType.Event: return eventIcon;
            case NodeType.WorkShop: return workShopIcon;
            default: return null;
        }
    }

    private void OnNodeClicked(MapNodeData data)
    {
        if (!IsMapFlowState())
        {
            return;
        }

        if (!visitedNodeIds.Contains(data.id))
        {
            visitedNodeIds.Add(data.id);
        }

        currentNode = data;

        // Mystery 노드인 경우 랜덤으로 타입 결정
        if (data.type == NodeType.Mystery)
        {
            List<NodeType> possibleTypes = new List<NodeType>
            {
                NodeType.Battle,
                NodeType.Shop,
                NodeType.Treasure,
                NodeType.Event
            };

            // 연속 상점/보물 방지 (직전 현재 노드 기준)
            if (currentNode != null)
            {
                if (currentNode.type == NodeType.Shop)
                {
                    possibleTypes.Remove(NodeType.Shop);
                }
                if (currentNode.type == NodeType.Treasure)
                {
                    possibleTypes.Remove(NodeType.Treasure);
                }
            }

            data.type = possibleTypes[UnityEngine.Random.Range(0, possibleTypes.Count)];

            // 아이콘 업데이트
            MapNodeView view = spawnedNodes.Find(v => v.NodeData.id == data.id);
            if (view != null)
            {
                view.Initialize(data, GetIconForType(data.type), OnNodeClicked);
                view.UpdateVisualState(true, true, false);
            }

        }

        // 다음 노드 활성화는 클리어 후 맵 복귀 시점에 반영
        pendingNodeProgress = data;

        // 맵이 닫히기 전 중복 입력을 막기 위해 즉시 모든 노드를 비활성화
        foreach (var view in spawnedNodes)
        {
            view.SetInteractable(false);
            bool isCurrent = view.NodeData.id == data.id;
            bool isVisited = visitedNodeIds.Contains(view.NodeData.id) && !isCurrent;
            view.UpdateVisualState(isCurrent, false, isVisited);
        }
        cachedActiveNodes.Clear();

        GameFlowState newState = GameFlowState.None;
        switch (data.type)
        {
            case NodeType.Battle:
            case NodeType.Boss:
                newState = GameFlowState.Battle;
                break;
            case NodeType.Shop:
                newState = GameFlowState.Shop;
                break;
            case NodeType.Treasure:
                newState = GameFlowState.Treasure;
                break;
            case NodeType.Event:
                newState = GameFlowState.Event;
                break;
            case NodeType.WorkShop:
                newState = GameFlowState.WorkShop;
                break;
        }

        if (newState != GameFlowState.None && GameManager.Instance != null)
        {
            GameManager.Instance.ChangeFlowState(newState);
        }
    }

    private void UpdateMapState(MapNodeData currentNode)
    {
        this.currentNode = currentNode;
        bool canSelectNodes = IsMapFlowState();

        // 활성화된 노드들을 순서대로 수집
        List<MapNodeView> activeNodesList = new List<MapNodeView>();

        foreach (var view in spawnedNodes)
        {
            bool isInteractable = false;
            bool isCurrent = false;
            bool isVisited = false;

            // 아직 시작하지 않은 경우 (currentNode가 null), 1층(floor 0) 노드만 활성화
            if (currentNode == null)
            {
                if (view.NodeData.floor == 0) isInteractable = true;
            }
            else
            {
                if (view.NodeData.id == currentNode.id) isCurrent = true;

                if (visitedNodeIds.Contains(view.NodeData.id) && !isCurrent) isVisited = true;

                // 현재 노드와 연결된 다음 노드들만 활성화
                if (currentNode.nextNodeIds.Contains(view.NodeData.id))
                {
                    isInteractable = true;
                }
            }

            if (!canSelectNodes)
            {
                isInteractable = false;
            }

            view.SetInteractable(isInteractable);
            view.UpdateVisualState(isCurrent, isInteractable, isVisited);

            if (isInteractable)
            {
                activeNodesList.Add(view);
            }
        }

        // 캐시에 활성화된 노드들 저장 (키보드 입력 처리에서 사용)
        cachedActiveNodes = activeNodesList;

        // 모든 노드에 표시할 순서 할당 (활성화되지 않은 노드는 -1)
        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            int displayIndex = activeNodesList.IndexOf(spawnedNodes[i]);
            spawnedNodes[i].SetDisplayIndex(displayIndex);
        }

        // 연결선 색상 업데이트
        if (visitedNodeIds.Count > 1)
        {
            for (int i = 0; i < visitedNodeIds.Count - 1; i++)
            {
                string from = visitedNodeIds[i];
                string to = visitedNodeIds[i + 1];
                if (connectionLines.TryGetValue($"{from}-{to}", out Image lineImg))
                {
                    lineImg.color = Color.gray; // 지나온 길은 회색
                }
            }
        }
    }

    public void Open()
    {
        ApplyPendingNodeProgress();

        SoundManager.Instance?.PlaySFX(SFXType.OpenPanel);

        // 보스 클리어 후 새 맵 생성
        bool bossJustCleared = GameManager.Instance != null && GameManager.Instance.BossJustCleared;
        if (bossJustCleared)
        {
            // 기존 노드들 정리 후 새 맵 생성
            foreach (Transform child in mapContainer)
            {
                Destroy(child.gameObject);
            }
            spawnedNodes.Clear();
            visitedNodeIds.Clear();
            connectionLines.Clear();
            currentNode = null;
            GenerateAndDrawMap();
        }
        // 아직 맵이 생성되지 않았다면 생성
        else if (spawnedNodes.Count == 0) GenerateAndDrawMap();

        gameObject.SetActive(true);

        if (mapScrollView != null)
        {
            mapScrollView.gameObject.SetActive(true);
            
            // 슬라이드 애니메이션: 위에서 내려오기
            RectTransform scrollViewRect = mapScrollView.GetComponent<RectTransform>();
            if (scrollViewRect != null)
            {
                mapPanelSequence?.Kill();
                mapPanelSequence = null;

                List<RectTransform> panelAnimationTargets = GetPanelAnimationTargets(scrollViewRect);
                SetPanelAnimationTargetsActive(true);
                PreparePanelAnimationTargets(panelAnimationTargets, scrollViewRect, true, true);
                
                // 패널 애니메이션 후 스테이지 이름 표시 및 프리뷰 시작
                mapPanelSequence = DOTween.Sequence();
                AppendPanelMoveTweens(mapPanelSequence, panelAnimationTargets, 0f, 0.4f, Ease.OutQuad, false, scrollViewRect);
                AppendPanelWidthTweens(mapPanelSequence, panelAnimationTargets, false, Ease.OutQuad);

                mapPanelSequence.OnComplete(() =>
                {
                    mapPanelSequence = null;

                    // Ensure node interactability is refreshed when the panel finishes opening.
                    UpdateMapState(currentNode);

                    if (currentNode == null)
                    {
                        DisplayStageNameAndStartPreview();
                    }
                });
            }
            else
            {
            }
            
            mapScrollView.enabled = true;
            mapScrollView.StopMovement();
            mapScrollView.velocity = Vector2.zero;

            // 스크롤 애니메이션 추가
            if (currentNode != null)
            {
                // 노드 클리어 후 맵 오픈 -> 다음 노드들 평균점으로 스크롤
                float targetScrollPos = CalculateTargetScrollPosition();
                DOTween.To(
                    () => mapScrollView.verticalNormalizedPosition,
                    value => mapScrollView.verticalNormalizedPosition = value,
                    Mathf.Clamp01(targetScrollPos),
                    0.5f
                ).SetEase(Ease.InOutQuad);
            }
        }
    }

    private void ApplyPendingNodeProgress()
    {
        if (pendingNodeProgress == null)
        {
            return;
        }

        UpdateMapState(pendingNodeProgress);
        pendingNodeProgress = null;
    }

    private bool IsMapFlowState()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.Map;
    }

    private List<RectTransform> GetPanelAnimationTargets(RectTransform fallbackRect)
    {
        List<RectTransform> targets = new List<RectTransform>();

        if (panelAnimationImage != null)
        {
            AddPanelAnimationTarget(targets, panelAnimationImage.rectTransform);
        }

        AddPanelAnimationTarget(targets, panelAnimationTransform);

        if (targets.Count == 0)
        {
            AddPanelAnimationTarget(targets, fallbackRect);
        }

        return targets;
    }

    private void AddPanelAnimationTarget(List<RectTransform> targets, RectTransform target)
    {
        if (target != null && !targets.Contains(target))
        {
            targets.Add(target);
        }
    }

    private void PreparePanelAnimationTargets(List<RectTransform> targets, RectTransform fallbackRect, bool startAboveOpenPosition, bool startCollapsed)
    {
        foreach (RectTransform target in targets)
        {
            Vector2 openPosition = CachePanelAnimationOpenPosition(target);
            float startOffsetY = startAboveOpenPosition ? GetPanelAnimationHeight(target, fallbackRect) : 0f;
            target.anchoredPosition = new Vector2(openPosition.x, openPosition.y + startOffsetY);
            SetPanelWidth(target, startCollapsed ? GetCollapsedPanelWidth(target) : GetExpandedPanelWidth(target));
        }
    }

    private Vector2 CachePanelAnimationOpenPosition(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return Vector2.zero;
        }

        if (!panelAnimationOpenPositions.TryGetValue(rectTransform, out Vector2 openPosition))
        {
            openPosition = rectTransform.anchoredPosition;
            panelAnimationOpenPositions.Add(rectTransform, openPosition);
        }

        return openPosition;
    }

    private float GetPanelAnimationHeight(RectTransform rectTransform, RectTransform fallbackRect)
    {
        float height = rectTransform != null ? rectTransform.rect.height : 0f;
        if (height <= 0f && fallbackRect != null)
        {
            height = fallbackRect.rect.height;
        }

        return height;
    }

    private void SetPanelAnimationTargetsActive(bool active)
    {
        if (panelAnimationImage != null)
        {
            panelAnimationImage.gameObject.SetActive(active);
        }

        if (panelAnimationTransform != null)
        {
            panelAnimationTransform.gameObject.SetActive(active);
        }
    }

    private void SetPanelWidth(RectTransform rectTransform, float width)
    {
        if (rectTransform == null)
        {
            return;
        }

        Vector2 size = rectTransform.sizeDelta;
        size.x = width;
        rectTransform.sizeDelta = size;
    }

    private Tween CreatePanelWidthTween(RectTransform rectTransform, float targetWidth)
    {
        return DOTween.To(
            () => rectTransform.sizeDelta.x,
            value => SetPanelWidth(rectTransform, value),
            targetWidth,
            panelWidthDuration
        );
    }

    private void AppendPanelMoveTweens(
        Sequence sequence,
        List<RectTransform> targets,
        float offsetY,
        float duration,
        Ease ease,
        bool useTargetHeightAsOffset,
        RectTransform fallbackRect)
    {
        bool appended = false;
        foreach (RectTransform target in targets)
        {
            Vector2 openPosition = CachePanelAnimationOpenPosition(target);
            float targetOffsetY = useTargetHeightAsOffset ? GetPanelAnimationHeight(target, fallbackRect) : offsetY;
            Tween tween = DOTween.To(
                () => target.anchoredPosition.y,
                value => target.anchoredPosition = new Vector2(openPosition.x, value),
                openPosition.y + targetOffsetY,
                duration
            ).SetEase(ease);

            if (appended)
            {
                sequence.Join(tween);
            }
            else
            {
                sequence.Append(tween);
                appended = true;
            }
        }
    }

    private float GetCollapsedPanelWidth(RectTransform target)
    {
        return target == panelAnimationTransform ? collapsedTransformWidth : collapsedPanelWidth;
    }

    private float GetExpandedPanelWidth(RectTransform target)
    {
        return target == panelAnimationTransform ? expandedTransformWidth : expandedPanelWidth;
    }

    private void AppendPanelWidthTweens(Sequence sequence, List<RectTransform> targets, bool collapse, Ease ease)
    {
        bool appended = false;
        foreach (RectTransform target in targets)
        {
            float targetWidth = collapse ? GetCollapsedPanelWidth(target) : GetExpandedPanelWidth(target);
            Tween tween = CreatePanelWidthTween(target, targetWidth).SetEase(ease);
            if (appended)
            {
                sequence.Join(tween);
            }
            else
            {
                sequence.Append(tween);
                appended = true;
            }
        }
    }

    private void StartScrollPreview()
    {
        if (mapScrollView == null) return;

        // 초기 위치에서 시작
        mapScrollView.verticalNormalizedPosition = 1f;
        
        // 딜레이 후 위에서 아래로 스크롤 (1.0 → 0.0) - 올라오지 않음
        DOTween.To(
            () => mapScrollView.verticalNormalizedPosition,
            value => mapScrollView.verticalNormalizedPosition = value,
            0f,
            scrollPreviewDuration
        ).SetDelay(scrollPreviewDelay).SetEase(scrollPreviewEase);
    }

    private void DisplayStageNameAndStartPreview()
    {
        // 현재 난이도에 맞는 스테이지 이름 가져오기
        int difficulty = GameManager.Instance != null ? GameManager.Instance.ClearedBosses : 0;
        string stageName = GetStageNameForDifficulty(difficulty);
        
        // PresentationManager를 사용하여 연출 재생
        if (PresentationManager.Instance != null)
        {
            PresentationManager.Instance.PlayCustomPresentation(
                stageName,
                null,
                () => StartScrollPreview()
            );
        }
        else
        {
            // PresentationManager가 없으면 바로 프리뷰 시작
            StartScrollPreview();
        }
    }

    private string GetStageNameForDifficulty(int difficulty)
    {
        if (mapConfig == null || mapConfig.stageNames == null || mapConfig.stageNames.Count == 0)
        {
            return "Stage " + (difficulty + 1);
        }
        
        if (difficulty < mapConfig.stageNames.Count)
        {
            return mapConfig.stageNames[difficulty];
        }
        
        // 범위를 벗어나면 마지막 이름 재사용
        return mapConfig.stageNames[mapConfig.stageNames.Count - 1];
    }

    /// <summary>
    /// Calculate the target scroll position to center on next available nodes
    /// </summary>
    private float CalculateTargetScrollPosition()
    {
        if (mapScrollView == null)
        {
            return 0.5f;
        }

        if (currentNode == null)
        {
            return 1f;
        }

        if (currentNode.nextNodeIds.Count == 0)
        {
            return 0.5f;
        }

        // Find Y range of next nodes
        float minNextY = float.MaxValue;
        float maxNextY = float.MinValue;

        foreach (var nextNodeId in currentNode.nextNodeIds)
        {
            var nextView = spawnedNodes.Find(v => v.NodeData.id == nextNodeId);
            if (nextView != null)
            {
                RectTransform nextRect = nextView.GetComponent<RectTransform>();
                float nodeY = nextRect.anchoredPosition.y;
                minNextY = Mathf.Min(minNextY, nodeY);
                maxNextY = Mathf.Max(maxNextY, nodeY);
            }
        }

        if (minNextY == float.MaxValue)
        {
            return 0.5f;
        }

        // Center on next nodes (평균점)
        float nextNodesCenter = (minNextY + maxNextY) / 2f;
        
        float result = NormalizeScrollPosition(nextNodesCenter);
        return result;
    }

    private float CalculateScrollPositionForNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return 0.5f;

        var nodeView = spawnedNodes.Find(v => v != null && v.NodeData != null && v.NodeData.id == nodeId);
        if (nodeView == null)
        {
            return 1f;
        }

        RectTransform nextRect = nodeView.GetComponent<RectTransform>();
        if (nextRect == null)
        {
            return 1f;
        }

        return NormalizeScrollPosition(nextRect.anchoredPosition.y);
    }

    /// <summary>
    /// Convert map space Y coordinate to normalized scroll position (0~1)
    /// </summary>
    private float NormalizeScrollPosition(float mapSpaceY)
    {
        if (mapScrollView == null)
        {
            return 0.5f;
        }

        RectTransform viewportRect = GetViewportRect();
        if (viewportRect == null)
        {
            return 0.5f;
        }

        // Use the container's rect HEIGHT SCALED by localScale to get actual rendered height
        float contentHeight = mapContainer.rect.height * mapContainer.localScale.y;
        float viewportHeight = viewportRect.rect.height;

        float scrollableHeight = contentHeight - viewportHeight;

        if (scrollableHeight <= 0)
        {
            // Content fits in viewport entirely - center it
            return 0.5f;
        }

        // mapContainer.pivot = (0.5, 1.0) means pivot is at top-center
        // Top of content at local space: Y = +contentHeight/2
        // Bottom of content at local space: Y = -contentHeight/2
        
        float distanceFromTop = (mapContainer.rect.height / 2f) - mapSpaceY;
        // Convert viewportHeight to unscaled units to match distanceFromTop calculation
        float viewportHeightUnscaled = viewportHeight / mapContainer.localScale.y;
        // Offset to center the node in the viewport middle
        distanceFromTop -= (viewportHeightUnscaled / 2f);
        float normalizedPos = distanceFromTop / (scrollableHeight / mapContainer.localScale.y);
        // Invert direction: ScrollRect uses 0=bottom, 1=top, but our calculation assumes 0=top, 1=bottom
        normalizedPos = 1f - normalizedPos;

        return Mathf.Clamp01(normalizedPos);
    }

    public void Close()
    {
        SoundManager.Instance?.PlaySFX(SFXType.ClosePanel);

        // 슬라이드 애니메이션: 위로 올라가기
        if (mapScrollView != null)
        {
            RectTransform scrollViewRect = mapScrollView.GetComponent<RectTransform>();
            if (scrollViewRect != null)
            {
                mapPanelSequence?.Kill();
                mapPanelSequence = null;

                List<RectTransform> panelAnimationTargets = GetPanelAnimationTargets(scrollViewRect);
                SetPanelAnimationTargetsActive(true);
                PreparePanelAnimationTargets(panelAnimationTargets, scrollViewRect, false, false);

                mapPanelSequence = DOTween.Sequence();
                AppendPanelWidthTweens(mapPanelSequence, panelAnimationTargets, true, Ease.OutQuad);
                AppendPanelMoveTweens(mapPanelSequence, panelAnimationTargets, 0f, 0.4f, Ease.OutQuad, true, scrollViewRect);
                mapPanelSequence.OnComplete(() =>
                {
                    mapPanelSequence = null;
                    SetPanelAnimationTargetsActive(false);
                    gameObject.SetActive(false);
                    mapScrollView.gameObject.SetActive(false);
                });
            }
            else
            {
                gameObject.SetActive(false);
                mapScrollView.gameObject.SetActive(false);
            }
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 마패(치트): 맵을 닫고 재생성한 후 처음 들어갔을 때의 훑어보기를 실행합니다.
    /// R키로 호출됩니다.
    /// </summary>
    public void ReloadMap()
    {
        Close();
        
        // 맵 상태 초기화 (처음 상태로)
        currentNode = null;
        visitedNodeIds.Clear();
        connectionLines.Clear();
        
        // DOTween이 완료될 때까지 약간의 딜레이 후 재생성 및 오픈
        DOVirtual.DelayedCall(0.5f, () =>
        {
            GenerateAndDrawMap();
            Open();
        });
    }

    // 디버그: 노드 간 실제 거리 확인
    [ContextMenu("Debug Node Distances")]
    private void DebugNodeDistances()
    {
        if (spawnedNodes.Count < 2) return;

        // 첫 두 노드 간 거리 측정
        var node1 = spawnedNodes[0].GetComponent<RectTransform>();
        var node2 = spawnedNodes[1].GetComponent<RectTransform>();

        // 로컬 좌표 기준 거리 (컨테이너 내부 좌표)
        float localDistance = Vector2.Distance(node1.anchoredPosition, node2.anchoredPosition);

        // 월드 좌표 기준 거리 (실제 화면 좌표)
        Vector3[] corners1 = new Vector3[4];
        Vector3[] corners2 = new Vector3[4];
        node1.GetWorldCorners(corners1);
        node2.GetWorldCorners(corners2);
        float worldDistance = Vector3.Distance(corners1[0], corners2[0]);
    }
}
