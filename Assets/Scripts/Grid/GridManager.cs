using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class GridManager : MonoBehaviour
{
    public Vector2 boardOrigin = Vector2.zero;
    public Vector2 cellSize = new(42f, 42f);
    public int boardWidth = 5;
    public int boardHeight = 6;
    public Vector2Int gridMinBounds = new(-2, -3);

    [Header("Grid Cell")] 
    public RectTransform gridCellsParent;
    private GridCell[,] gridCells;
    public bool IsGridCreated => gridCells != null;
    
    [Header("Grid Points")]
    public RectTransform gridPointsParent;
    private GridPoint[,] gridPoints;
    
    private List<GridLine> gridLines = new List<GridLine>();

    public RectTransform gridLinesParent;
    public Color gridLineColor = Color.black;
    public float gridLineThickness = 2f;

    [Header("Invalid Zone Settings")]
    public Vector2Int invalidRangeMin = new Vector2Int(-100, -1); // X 최소, Y 최소 (기본값: -1행 이상)
    public Vector2Int invalidRangeMax = new Vector2Int(100, 100);   // X 최대, Y 최대 (기본값: 전체)
    public Color invalidZoneColor = new Color(0.5f, 0.5f, 0.5f, 0.3f); // 회색 피드백 색상

    [Header("Animation")]
    public RectTransform boardContainer; // 보드 전체를 감싸는 부모 객체 (권장)
    public float boardShiftX = 240f; // 이동할 거리 (화면 해상도에 맞춰 조정 필요)
    public float boardShiftY = -300f; // 보물상자 열림 시 아래로 이동할 거리
    [SerializeField] private float boardDimMultiplier = 0.68f;
    [SerializeField] private float boardDimShiftY = -48f;

    private readonly Dictionary<Graphic, Color> originalGraphicColors = new();
    private readonly Dictionary<TMP_Text, Color> originalTextColors = new();
    private readonly Dictionary<RectTransform, Vector2> originalRootPositions = new();
    private bool boardPresentationCached = false;

    private void Awake()
    {
    }

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnStateChanged;
            // 이미 Prepare 상태라면 즉시 실행
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
            {
                if (gridCells == null) CreateGridCells();
                StartCoroutine(HighlightInvalidZoneRoutine());
            }
        }
    }

    public void CreateGridCells()
    {
        if (gridCells != null) return;

        if (gridCellsParent == null)
            return;

        gridCells = new GridCell[boardWidth, boardHeight];

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                Vector2Int gridPos = new(gridMinBounds.x + x, gridMinBounds.y + y);
                GameObject cellObj = new($"GridCell_{gridPos.x}_{gridPos.y}");
                cellObj.transform.SetParent(gridCellsParent, false);
                RectTransform rect = cellObj.AddComponent<RectTransform>();
                Image cellImage = cellObj.AddComponent<Image>();
                cellImage.color = new Color(1f, 1f, 1f, 0f); // 투명(혹은 필요시 색상 지정)
                cellImage.raycastTarget = true;
                rect.sizeDelta = cellSize;
                Vector2 anchoredPos = GridToUiPosition(gridPos);
                rect.anchoredPosition = anchoredPos;
                // GridCell 컴포넌트 추가 및 초기화
                GridCell cell = cellObj.AddComponent<GridCell>();
                cell.Initialize(gridPos, rect, anchoredPos, cellSize);
                gridCells[x, y] = cell;

                // 생성 애니메이션: 아래에서 위로 순차적으로 등장
                rect.localScale = Vector3.zero;
                rect.anchoredPosition = anchoredPos - new Vector2(0, 50f);
                rect.localRotation = Quaternion.Euler(0, 0, Random.Range(-90f, 90f));
                float delay = y * 0.05f + x * 0.02f;
                rect.DOAnchorPos(anchoredPos, 0.4f).SetEase(Ease.InExpo).SetDelay(delay);
                rect.DOScale(Vector3.one, 0.4f).SetEase(Ease.InExpo).SetDelay(delay);
                rect.DORotate(Vector3.zero, 0.4f).SetEase(Ease.InExpo).SetDelay(delay);
            }
        }

        DrawGridLines();
        CreateGridPoints();
    }

    public void CreateGridPoints()
    {
        if (gridPoints != null) return;

        // gridPointsParent 자동 생성
        if (gridPointsParent == null)
        {
            GameObject pointsParentObj = new("GridPointsParent");
            pointsParentObj.transform.SetParent(transform, false);
            gridPointsParent = pointsParentObj.AddComponent<RectTransform>();
            gridPointsParent.anchoredPosition = Vector2.zero;
            gridPointsParent.sizeDelta = Vector2.zero;
        }

        // GridPoint는 GridCell과 같은 범위 (boardWidth × boardHeight)
        gridPoints = new GridPoint[boardWidth, boardHeight];

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                // GridPoint의 위치: GridCell과 동일
                Vector2Int gridPos = new(gridMinBounds.x + x, gridMinBounds.y + y);
                GameObject pointObj = new($"GridPoint_{gridPos.x}_{gridPos.y}");
                pointObj.transform.SetParent(gridPointsParent, false);
                RectTransform rect = pointObj.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(10f, 10f); // 포인트 크기
                Vector2 anchoredPos = GridToUiPosition(gridPos);
                rect.anchoredPosition = anchoredPos;
                
                // GridPoint 컴포넌트 추가 및 초기화
                GridPoint point = pointObj.AddComponent<GridPoint>();
                point.Initialize(gridPos, rect, anchoredPos);
                gridPoints[x, y] = point;

                // GridPoint에 연결된 라인 할당
                ConnectPointToLines(point, gridPos);
            }
        }
    }

    /// <summary>
    /// GridPoint에 연결된 GridLine들(위, 아래, 좌, 우)을 할당합니다.
    /// </summary>
    private void ConnectPointToLines(GridPoint point, Vector2Int gridPos)
    {
        // GridLine 컬렉션에서 해당 라인 찾기
        foreach (var line in gridLines)
        {
            if (line == null) continue;

            // 위쪽 라인 (세로 라인 at gridPos)
            if (line.isVertical && line.gridPosition == gridPos)
            {
                point.lineTop = line;
            }
            // 아래쪽 라인 (세로 라인 at gridPos.y - 1)
            else if (line.isVertical && line.gridPosition == new Vector2Int(gridPos.x, gridPos.y - 1))
            {
                point.lineBottom = line;
            }
            // 오른쪽 라인 (가로 라인 at gridPos)
            else if (!line.isVertical && line.gridPosition == gridPos)
            {
                point.lineRight = line;
            }
            // 왼쪽 라인 (가로 라인 at gridPos.x - 1, gridPos.y)
            else if (!line.isVertical && line.gridPosition == new Vector2Int(gridPos.x - 1, gridPos.y))
            {
                point.lineLeft = line;
            }
        }
    }

    public Vector2 GridToUiPosition(Vector2Int gridPosition)
    {
        return boardOrigin + new Vector2(gridPosition.x * cellSize.x, gridPosition.y * cellSize.y);
    }

    public Vector2Int? GetNearestGridPosition(Vector2 localPosition)
    {
        Vector2 relative = localPosition - boardOrigin;
        Vector2Int gridPos = new Vector2Int(
            Mathf.RoundToInt(relative.x / cellSize.x),
            Mathf.RoundToInt(relative.y / cellSize.y)
        );

        if (IsInBounds(gridPos)) return gridPos;
        return null;
    }

    public bool IsInBounds(Vector2Int gridPosition)
    {
        return gridPosition.x >= gridMinBounds.x && gridPosition.x < gridMinBounds.x + boardWidth &&
               gridPosition.y >= gridMinBounds.y && gridPosition.y < gridMinBounds.y + boardHeight;
    }

    /// <summary>
    /// 특정 그리드 위치의 GridCell을 반환합니다.
    /// </summary>
    public GridCell GetGridCell(Vector2Int gridPosition)
    {
        if (!IsInBounds(gridPosition) || gridCells == null)
            return null;

        int x = gridPosition.x - gridMinBounds.x;
        int y = gridPosition.y - gridMinBounds.y;

        if (x >= 0 && x < boardWidth && y >= 0 && y < boardHeight)
        {
            return gridCells[x, y];
        }

        return null;
    }

    /// <summary>
    /// 특정 그리드 위치의 GridPoint를 반환합니다.
    /// </summary>
    public GridPoint GetGridPoint(Vector2Int gridPosition)
    {
        if (gridPoints == null)
            return null;

        int x = gridPosition.x - gridMinBounds.x;
        int y = gridPosition.y - gridMinBounds.y;

        // GridPoint는 GridCell과 같은 범위 (boardWidth × boardHeight)
        if (x >= 0 && x < boardWidth && y >= 0 && y < boardHeight)
        {
            return gridPoints[x, y];
        }

        return null;
    }

    /// <summary>
    /// 모든 GridPoint를 배열로 반환합니다.
    /// </summary>
    public GridPoint[] GetAllGridPoints()
    {
        if (gridPoints == null)
            return new GridPoint[0];

        // 2D 배열을 1D 배열로 변환
        List<GridPoint> points = new List<GridPoint>();
        foreach (var point in gridPoints)
        {
            if (point != null)
            {
                points.Add(point);
            }
        }
        return points.ToArray();
    }

    public void DrawGridLines()
    {
        if (gridLinesParent == null)
        {
            return;
        }

        // 각 칸마다 세로/가로 라인 생성 (이미지 컴포넌트만 가진 오브젝트)
        // 세로 라인: 각 칸의 왼쪽만 생성 (우측 끝 제외)
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight - 1; y++)
            {
                GameObject vLineObj = new($"VLine_{x + gridMinBounds.x}_{y + gridMinBounds.y}");
                vLineObj.transform.SetParent(gridLinesParent, false);
                RectTransform vLineRect = vLineObj.AddComponent<RectTransform>();
                Image vLineImage = vLineObj.AddComponent<Image>();
                vLineImage.color = gridLineColor;
                vLineImage.raycastTarget = false;
                float xPos = boardOrigin.x + (gridMinBounds.x + x) * cellSize.x;
                float yPos = boardOrigin.y + (gridMinBounds.y + y) * cellSize.y + cellSize.y / 2f;
                vLineRect.anchoredPosition = new(xPos, yPos);
                vLineRect.sizeDelta = new(gridLineThickness, cellSize.y);

                Vector2 finalPos = vLineRect.anchoredPosition;
                GridLine line = vLineObj.AddComponent<GridLine>();
                line.Initialize(new Vector2Int(x + gridMinBounds.x, y + gridMinBounds.y), finalPos);
                line.isVertical = true; // 세로 라인
                gridLines.Add(line);

                // 세로 라인 애니메이션
                vLineRect.anchoredPosition = finalPos - new Vector2(0, 50f);
                vLineRect.localScale = Vector3.zero;
                vLineRect.localRotation = Quaternion.Euler(0, 0, Random.Range(-90f, 90f));
                float delay = y * 0.05f + x * 0.02f;
                vLineRect.DOAnchorPos(finalPos, 0.4f).SetEase(Ease.OutBack).SetDelay(delay);
                vLineRect.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetDelay(delay);
                vLineRect.DORotate(Vector3.zero, 0.4f).SetEase(Ease.OutBack).SetDelay(delay);
            }
        }

        // 가로 라인: 각 칸의 하단만 생성 (상단 끝 제외)
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth - 1; x++)
            {
                GameObject hLineObj = new GameObject($"HLine_{x + gridMinBounds.x}_{y + gridMinBounds.y}");
                hLineObj.transform.SetParent(gridLinesParent, false);
                RectTransform hLineRect = hLineObj.AddComponent<RectTransform>();
                Image hLineImage = hLineObj.AddComponent<Image>();
                hLineImage.color = gridLineColor;
                hLineImage.raycastTarget = false;
                float xPos = boardOrigin.x + (gridMinBounds.x + x) * cellSize.x + cellSize.x / 2f;
                float yPos = boardOrigin.y + (gridMinBounds.y + y) * cellSize.y;
                hLineRect.anchoredPosition = new Vector2(xPos, yPos);
                hLineRect.sizeDelta = new Vector2(cellSize.x, gridLineThickness);

                Vector2 finalPos = hLineRect.anchoredPosition;
                GridLine line = hLineObj.AddComponent<GridLine>();
                line.Initialize(new Vector2Int(x + gridMinBounds.x, y + gridMinBounds.y), finalPos);
                line.isVertical = false; // 가로 라인
                gridLines.Add(line);

                // 가로 라인 애니메이션
                hLineRect.anchoredPosition = finalPos - new Vector2(0, 50f);
                hLineRect.localScale = Vector3.zero;
                hLineRect.localRotation = Quaternion.Euler(0, 0, Random.Range(-90f, 90f));
                float delay = y * 0.05f + x * 0.02f;
                hLineRect.DOAnchorPos(finalPos, 0.4f).SetEase(Ease.OutBack).SetDelay(delay);
                hLineRect.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetDelay(delay);
                hLineRect.DORotate(Vector3.zero, 0.4f).SetEase(Ease.OutBack).SetDelay(delay);
            }
        }
    }

    public float GetTotalAnimationDuration()
    {
        // 마지막 셀의 애니메이션 시작 시간(delay) + 지속 시간(duration)
        float maxDelay = (boardHeight - 1) * 0.05f + (boardWidth - 1) * 0.02f;
        return maxDelay + 0.4f; // 0.4f는 트윈 duration
    }

    private void OnStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Prepare)
        {
            if (gridCells == null) CreateGridCells();
            StartCoroutine(HighlightInvalidZoneRoutine());
        }
        else
        {
            ResetZoneHighlights();
        }
    }

    private IEnumerator HighlightInvalidZoneRoutine()
    {
        // 그리드 생성 애니메이션이 끝날 때까지 대기
        float waitTime = GetTotalAnimationDuration();
        yield return new WaitForSeconds(waitTime);

        // 상태가 여전히 Prepare인지 확인
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            HighlightInvalidZones();
        }
    }

    public void HighlightInvalidZones()
    {
        if (gridCells == null) return;

        foreach (var cell in gridCells)
        {
            if (cell != null && IsInvalidZone(cell.gridPosition))
            {
                cell.SetGray(true, invalidZoneColor);
            }
        }

        foreach (var line in gridLines)
        {
            if (line != null && IsInvalidZone(line.gridPosition))
            {
                line.SetGray(true);
            }
        }
    }

    private bool IsInvalidZone(Vector2Int pos)
    {
        if (PieceManager.Instance != null)
        {
            return pos.y > PieceManager.Instance.PlayerPrepareMaxY;
        }

        return pos.x >= invalidRangeMin.x && pos.x <= invalidRangeMax.x &&
               pos.y >= invalidRangeMin.y && pos.y <= invalidRangeMax.y;
    }

    public void ResetZoneHighlights()
    {
        if (gridCells == null) return;
        foreach (var cell in gridCells)
        {
            cell?.SetGray(false, invalidZoneColor);
        }
        foreach (var line in gridLines)
        {
            line?.SetGray(false);
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnStateChanged;
        }
    }

    public void ShiftBoard(bool isOpen, float duration)
    {
        float targetX = isOpen ? boardShiftX : 0f;
        Ease ease = Ease.OutQuad;

        if (boardContainer != null)
        {
            boardContainer.DOAnchorPosX(targetX, duration).SetEase(ease);
        }
    }

    /// <summary>
    /// 보물상자용: 보드를 수직으로 이동 (아래로 밀기 / 원위치 복귀)
    /// </summary>
    public void ShiftBoardVertical(bool isOpen, float duration)
    {
        float targetY = isOpen ? boardShiftY : 0f;
        Ease ease = Ease.OutQuad;

        if (boardContainer != null)
        {
            boardContainer.DOAnchorPosY(targetY, duration).SetEase(ease);
        }
    }

    /// <summary>
    /// 보드와 기물을 함께 어둡게 만들고 아래로 살짝 내립니다.
    /// </summary>
    public void SetBoardPresentation(bool dimmed, float duration)
    {
        if (boardContainer == null)
        {
            return;
        }

        CacheBoardPresentationTargets();

        float targetShiftY = dimmed ? boardDimShiftY : 0f;
        Vector2 targetBoardPos = GetOriginalRootPosition(boardContainer) + new Vector2(0f, targetShiftY);

        boardContainer.DOKill();
        boardContainer.DOAnchorPos(targetBoardPos, duration).SetEase(Ease.OutQuad);

        RectTransform piecesParent = GetPiecesParentRect();
        if (piecesParent != null && piecesParent != boardContainer && !piecesParent.IsChildOf(boardContainer))
        {
            piecesParent.DOKill();
            Vector2 targetPiecesPos = GetOriginalRootPosition(piecesParent) + new Vector2(0f, targetShiftY);
            piecesParent.DOAnchorPos(targetPiecesPos, duration).SetEase(Ease.OutQuad);
        }

        ApplyPresentationTint(dimmed, duration);
    }

    private void CacheBoardPresentationTargets()
    {
        if (boardPresentationCached)
        {
            return;
        }

        if (boardContainer != null)
        {
            originalRootPositions[boardContainer] = boardContainer.anchoredPosition;
        }

        RectTransform piecesParent = GetPiecesParentRect();
        if (piecesParent != null && piecesParent != boardContainer)
        {
            originalRootPositions[piecesParent] = piecesParent.anchoredPosition;
        }

        boardPresentationCached = true;
    }

    private RectTransform GetPiecesParentRect()
    {
        if (PieceSpawner.Instance == null || PieceSpawner.Instance.piecesParent == null)
        {
            return null;
        }

        return PieceSpawner.Instance.piecesParent as RectTransform;
    }

    private Vector2 GetOriginalRootPosition(RectTransform root)
    {
        if (root == null)
        {
            return Vector2.zero;
        }

        if (!originalRootPositions.TryGetValue(root, out Vector2 originalPos))
        {
            originalPos = root.anchoredPosition;
            originalRootPositions[root] = originalPos;
        }

        return originalPos;
    }

    private void ApplyPresentationTint(bool dimmed, float duration)
    {
        float multiplier = dimmed ? boardDimMultiplier : 1f;

        HashSet<Graphic> graphics = new();
        HashSet<TMP_Text> texts = new();

        CollectPresentationTargets(boardContainer, graphics, texts);

        RectTransform piecesParent = GetPiecesParentRect();
        if (piecesParent != null && piecesParent != boardContainer && !piecesParent.IsChildOf(boardContainer))
        {
            CollectPresentationTargets(piecesParent, graphics, texts);
        }

        foreach (Graphic graphic in graphics)
        {
            if (graphic == null)
            {
                continue;
            }

            if (!originalGraphicColors.ContainsKey(graphic))
            {
                originalGraphicColors[graphic] = graphic.color;
            }

            Color original = originalGraphicColors[graphic];
            Color target = new Color(original.r * multiplier, original.g * multiplier, original.b * multiplier, original.a);
            graphic.DOKill();
            graphic.DOColor(target, duration).SetEase(Ease.OutQuad);
        }

        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            if (!originalTextColors.ContainsKey(text))
            {
                originalTextColors[text] = text.color;
            }

            Color original = originalTextColors[text];
            Color target = new Color(original.r * multiplier, original.g * multiplier, original.b * multiplier, original.a);
            text.DOKill();
            text.DOColor(target, duration).SetEase(Ease.OutQuad);
        }
    }

    private void CollectPresentationTargets(RectTransform root, HashSet<Graphic> graphics, HashSet<TMP_Text> texts)
    {
        if (root == null)
        {
            return;
        }

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic != null && !(graphic is TMP_Text))
            {
                graphics.Add(graphic);
            }
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null)
            {
                texts.Add(text);
            }
        }
    }
}
