using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class GridManager : MonoBehaviour
{
    public Vector2 boardOrigin = Vector2.zero;
    public Vector2 cellSize = new(48f, 48f);
    public int boardWidth = 6;
    public int boardHeight = 5;
    public Vector2Int gridMinBounds = new(-3, -2);

    [Header("Grid Cell")] 
    public RectTransform gridCellsParent;
    private GridCell[,] gridCells;
    public bool IsGridCreated => gridCells != null;

    [Header("Tile Styling")]
    public Color lightTileColor = new Color(0.941f, 0.851f, 0.710f, 1f); // #F0D9B5 (정석 클래식 크림/아이보리)
    public Color darkTileColor = new Color(0.710f, 0.533f, 0.388f, 1f);  // #B58863 (정석 클래식 우드 브라운)
    public Color tileBorderColor = new Color(0.38f, 0.26f, 0.17f, 0.35f); // 차분한 우드 테두리

    [Header("Invalid Zone Settings")]
    public Vector2Int invalidRangeMin = new Vector2Int(-100, 0); // X 최소, Y 최소 (기본값: 적진 영역 y >= 0)
    public Vector2Int invalidRangeMax = new Vector2Int(100, 100);
    public Color invalidZoneColor = new Color(0.35f, 0.1f, 0.1f, 0.45f);

    [Header("Animation")]
    public RectTransform boardContainer;
    public float boardShiftX = 240f;
    public float boardShiftY = -300f;
    [SerializeField] private float boardDimMultiplier = 0.68f;
    [SerializeField] private float boardDimShiftY = -48f;

    private readonly Dictionary<Graphic, Color> originalGraphicColors = new();
    private readonly Dictionary<TMP_Text, Color> originalTextColors = new();
    private readonly Dictionary<RectTransform, Vector2> originalRootPositions = new();
    private bool boardPresentationCached = false;

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnStateChanged;
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
            {
                if (gridCells == null) CreateGridCells();
                HighlightInvalidZones();
                StartCoroutine(HighlightInvalidZoneRoutine());
            }
        }
    }

    [Header("Coordinate Labels (File & Rank)")]
    public bool showCoordinateLabels = true;
    public TMP_FontAsset coordinateFont;
    public float coordinateFontSize = 7f;
    public Color lightTileTextColor = new Color(0.710f, 0.533f, 0.388f, 0.9f); // 밝은 타일 위 어두운 글자색
    public Color darkTileTextColor = new Color(0.941f, 0.851f, 0.710f, 0.9f);  // 어두운 타일 위 밝은 글자색
    public Vector2 coordinatePadding = new Vector2(3f, 3f);

    [Header("Last Move Highlight Settings (Chess.com style)")]
    public bool enableLastMoveHighlight = true;
    public Color lightTileLastMoveColor = new Color(0.804f, 0.808f, 0.478f, 1f); // #CDCF7A (밝은 타일용 체스닷컴 시그니처 옐로우/올리브)
    public Color darkTileLastMoveColor = new Color(0.667f, 0.678f, 0.318f, 1f);  // #AAAD51 (어두운 타일용 체스닷컴 시그니처 옐로우/올리브)

    private Vector2Int? lastMoveFrom = null;
    private Vector2Int? lastMoveTo = null;

    public void CreateGridCells()
    {
        CreateGridCells(false, false);
    }

    public void CreateGridCells(bool immediate, bool forceRecreate = false)
    {
        if (gridCells != null && !forceRecreate) return;

        if (forceRecreate && gridCells != null)
        {
            foreach (var c in gridCells)
            {
                if (c != null && c.gameObject != null) Destroy(c.gameObject);
            }
            gridCells = null;
        }

        if (gridCellsParent == null)
        {
            GameObject parentObj = new GameObject("GridCellsParent");
            parentObj.transform.SetParent(transform, false);
            gridCellsParent = parentObj.AddComponent<RectTransform>();
            gridCellsParent.anchoredPosition = Vector2.zero;
        }
        else if (forceRecreate)
        {
            for (int i = gridCellsParent.childCount - 1; i >= 0; i--)
            {
                Destroy(gridCellsParent.GetChild(i).gameObject);
            }
        }

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
                cellImage.raycastTarget = true;
                rect.sizeDelta = cellSize;

                Vector2 anchoredPos = GridToUiPosition(gridPos);
                rect.anchoredPosition = anchoredPos;

                // 6x5 미니 체스판 체크무늬 색상 적용 (우하단 밝은색 = White on right 규칙 준수)
                bool isLight = ((x + y) % 2 != 0);
                Color tileColor = isLight ? lightTileColor : darkTileColor;
                cellImage.color = tileColor;

                // 테두리(아웃라인) 장식 추가
                Outline outline = cellObj.AddComponent<Outline>();
                outline.effectColor = tileBorderColor;
                outline.effectDistance = new Vector2(1f, -1f);

                GridCell cell = cellObj.AddComponent<GridCell>();
                cell.Initialize(gridPos, rect, anchoredPos, cellSize, tileColor);
                gridCells[x, y] = cell;

                // 생성 애니메이션 (immediate가 true이면 대기/트윈 없이 즉시 완성 배치)
                if (immediate)
                {
                    rect.localScale = Vector3.one;
                    rect.anchoredPosition = anchoredPos;
                }
                else
                {
                    rect.localScale = Vector3.zero;
                    rect.anchoredPosition = anchoredPos - new Vector2(0, 30f);
                    float delay = y * 0.04f + x * 0.02f;
                    rect.DOAnchorPos(anchoredPos, 0.35f).SetEase(Ease.OutBack).SetDelay(delay);
                    rect.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetDelay(delay);
                }

                // 체스 좌표 표기 (File: 가로 열 알파벳 a-f / Rank: 세로 행 숫자 1-5)
                if (showCoordinateLabels)
                {
                    Color labelColor = isLight ? lightTileTextColor : darkTileTextColor;
                    float labelDelay = immediate ? 0f : (y * 0.04f + x * 0.02f + 0.15f);

                    // 1. 랭크(Rank): 가장 왼쪽 열(x == 0) 좌측 상단 구석에 행 번호 표기
                    if (x == 0)
                    {
                        int rankNumber = y + 1;
                        CreateCoordinateLabel(rect, rankNumber.ToString(), TextAlignmentOptions.TopLeft, labelColor, labelDelay);
                    }

                    // 2. 파일(File): 가장 아랫쪽 행(y == 0) 우측 하단 구석에 열 알파벳(a, b, c...) 표기
                    if (y == 0)
                    {
                        char fileChar = (char)('a' + x);
                        CreateCoordinateLabel(rect, fileChar.ToString(), TextAlignmentOptions.BottomRight, labelColor, labelDelay);
                    }
                }

            }
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            HighlightInvalidZones();
        }
    }

    public void SetBoardSize(int[] size, Vector2Int minBounds)
    {
        if (size == null || size.Length != 2)
        {
            Debug.LogError("Stage board size must contain exactly [width, height].", this);
            return;
        }

        int width = Mathf.Max(1, size[0]);
        int height = Mathf.Max(1, size[1]);
        bool sizeChanged = boardWidth != width || boardHeight != height || gridMinBounds != minBounds;

        boardWidth = width;
        boardHeight = height;
        gridMinBounds = minBounds;

        if (sizeChanged && gridCells != null)
        {
            ResizeGridCells();
        }
        else if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            HighlightInvalidZones();
        }
    }

    private void ResizeGridCells()
    {
        GridCell[,] previousGrid = gridCells;
        gridCells = new GridCell[boardWidth, boardHeight];

        for (int x = 0; x < previousGrid.GetLength(0); x++)
        {
            for (int y = 0; y < previousGrid.GetLength(1); y++)
            {
                GridCell cell = previousGrid[x, y];
                if (cell == null) continue;

                Vector2Int position = cell.gridPosition;
                if (!IsInBounds(position))
                {
                    Destroy(cell.gameObject);
                    continue;
                }

                int newX = position.x - gridMinBounds.x;
                int newY = position.y - gridMinBounds.y;
                gridCells[newX, newY] = cell;
                Vector2 targetPosition = GridToUiPosition(position);
                cell.gridPosition = position;
                cell.rectTransform.sizeDelta = cellSize;
                cell.SetTileColor(GetTileColor(newX, newY));
                cell.rectTransform.DOKill();
                cell.rectTransform.DOAnchorPos(targetPosition, 0.45f).SetEase(Ease.InOutQuad);
            }
        }

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                if (gridCells[x, y] == null)
                {
                    CreateGridCell(x, y, true);
                }
            }
        }

        RefreshCoordinateLabels();
        lastMoveFrom = null;
        lastMoveTo = null;

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            HighlightInvalidZones();
            StartCoroutine(HighlightInvalidZoneRoutine());
        }
    }

    private void CreateGridCell(int x, int y, bool animate)
    {
        Vector2Int gridPos = new(gridMinBounds.x + x, gridMinBounds.y + y);
        GameObject cellObj = new($"GridCell_{gridPos.x}_{gridPos.y}");
        cellObj.transform.SetParent(gridCellsParent, false);
        RectTransform rect = cellObj.AddComponent<RectTransform>();
        Image cellImage = cellObj.AddComponent<Image>();
        cellImage.raycastTarget = true;
        rect.sizeDelta = cellSize;

        Vector2 anchoredPos = GridToUiPosition(gridPos);
        Color tileColor = GetTileColor(x, y);
        cellImage.color = tileColor;

        Outline outline = cellObj.AddComponent<Outline>();
        outline.effectColor = tileBorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        GridCell cell = cellObj.AddComponent<GridCell>();
        cell.Initialize(gridPos, rect, anchoredPos, cellSize, tileColor);
        gridCells[x, y] = cell;

        if (animate)
        {
            rect.localScale = Vector3.zero;
            rect.anchoredPosition = anchoredPos - new Vector2(0, 30f);
            rect.DOAnchorPos(anchoredPos, 0.45f).SetEase(Ease.OutBack);
            rect.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack);
        }

        CreateCoordinateLabels(rect, x, y, 0.15f);
    }

    private Color GetTileColor(int x, int y)
    {
        return ((x + y) % 2 != 0) ? lightTileColor : darkTileColor;
    }

    private void RefreshCoordinateLabels()
    {
        if (!showCoordinateLabels) return;

        foreach (GridCell cell in gridCells)
        {
            if (cell == null) continue;

            for (int i = cell.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = cell.transform.GetChild(i);
                if (child.name.StartsWith("Coord_"))
                {
                    Destroy(child.gameObject);
                }
            }

            int x = cell.gridPosition.x - gridMinBounds.x;
            int y = cell.gridPosition.y - gridMinBounds.y;
            CreateCoordinateLabels(cell.rectTransform, x, y, 0f);
        }
    }

    private void CreateCoordinateLabels(RectTransform rect, int x, int y, float delay)
    {
        Color labelColor = GetTileColor(x, y) == lightTileColor ? lightTileTextColor : darkTileTextColor;
        if (x == 0)
        {
            CreateCoordinateLabel(rect, (y + 1).ToString(), TextAlignmentOptions.TopLeft, labelColor, delay);
        }

        if (y == 0)
        {
            CreateCoordinateLabel(rect, ((char)('a' + x)).ToString(), TextAlignmentOptions.BottomRight, labelColor, delay);
        }
    }

    private void CreateCoordinateLabel(RectTransform parentCell, string text, TextAlignmentOptions alignment, Color color, float animDelay)
    {
        GameObject labelObj = new GameObject($"Coord_{text}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(parentCell, false);
        labelObj.transform.SetAsLastSibling();

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        TextMeshProUGUI tmp = labelObj.GetComponent<TextMeshProUGUI>();

        if (coordinateFont != null)
        {
            tmp.font = coordinateFont;
        }

        tmp.text = text;
        tmp.fontSize = coordinateFontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;

        // 셀 내부 여백(패딩) 맞춰 stretch 앵커 설정
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = coordinatePadding;
        labelRect.offsetMax = -coordinatePadding;

        // 살짝 떠오르며 페이드인되는 텍스트 애니메이션
        labelRect.localScale = Vector3.one * 0.7f;
        if (animDelay > 0f)
        {
            labelRect.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetDelay(animDelay);
        }
        else
        {
            labelRect.localScale = Vector3.one;
        }
    }

    public Vector2 GridToUiPosition(Vector2Int gridPosition)
    {
        float xOffset = (gridPosition.x - gridMinBounds.x - (boardWidth - 1) * 0.5f) * cellSize.x;
        float yOffset = (gridPosition.y - gridMinBounds.y - (boardHeight - 1) * 0.5f) * cellSize.y;
        return boardOrigin + new Vector2(xOffset, yOffset);
    }

    public Vector2Int? GetNearestGridPosition(Vector2 localPosition)
    {
        Vector2 relative = localPosition - boardOrigin;
        int x = Mathf.RoundToInt((relative.x / cellSize.x) + (boardWidth - 1) * 0.5f) + gridMinBounds.x;
        int y = Mathf.RoundToInt((relative.y / cellSize.y) + (boardHeight - 1) * 0.5f) + gridMinBounds.y;
        Vector2Int gridPos = new Vector2Int(x, y);

        if (IsInBounds(gridPos)) return gridPos;
        return null;
    }

    public Vector2Int? GetNearestCell(Vector2 localPosition)
    {
        return GetNearestGridPosition(localPosition);
    }

    public bool IsInBounds(Vector2Int gridPosition)
    {
        return gridPosition.x >= gridMinBounds.x && gridPosition.x < gridMinBounds.x + boardWidth &&
               gridPosition.y >= gridMinBounds.y && gridPosition.y < gridMinBounds.y + boardHeight;
    }

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

    public GridCell[] GetAllGridCells()
    {
        if (gridCells == null) return new GridCell[0];
        List<GridCell> list = new List<GridCell>();
        foreach (var cell in gridCells)
        {
            if (cell != null) list.Add(cell);
        }
        return list.ToArray();
    }

    public float GetTotalAnimationDuration()
    {
        float maxDelay = boardHeight * 0.04f + boardWidth * 0.02f;
        return maxDelay + 0.35f;
    }

    private void OnStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Prepare)
        {
            if (gridCells == null) CreateGridCells();
            HighlightInvalidZones();
            StartCoroutine(HighlightInvalidZoneRoutine());
        }
        else
        {
            ResetZoneHighlights();
        }
    }

    public void HighlightInvalidZones()
    {
        if (gridCells == null) return;

        foreach (var cell in gridCells)
        {
            if (cell == null) continue;
            bool isInvalid = IsInvalidZone(cell.gridPosition);
            cell.SetGray(isInvalid, invalidZoneColor);
        }
    }

    private IEnumerator HighlightInvalidZoneRoutine()
    {
        yield return null;

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            HighlightInvalidZones();
        }
    }

    public bool IsInvalidZone(Vector2Int pos)
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
            cell?.ResetHighlight();
        }
    }

    /// <summary>
    /// 체스닷컴 스타일: 전 턴에 이동한 기물의 출발 칸(From)과 도착 칸(To)을 하이라이트합니다.
    /// </summary>
    public void HighlightLastMove(Vector2Int from, Vector2Int to)
    {
        if (!enableLastMoveHighlight || gridCells == null) return;

        // 이전 턴의 하이라이트 복원
        ClearLastMoveHighlight();

        lastMoveFrom = from;
        lastMoveTo = to;

        ApplyLastMoveHighlightToCell(from);
        ApplyLastMoveHighlightToCell(to);
    }

    public void ClearLastMoveHighlight()
    {
        if (lastMoveFrom.HasValue)
        {
            GridCell fromCell = GetGridCell(lastMoveFrom.Value);
            fromCell?.SetLastMoveHighlight(false, Color.white);
            lastMoveFrom = null;
        }

        if (lastMoveTo.HasValue)
        {
            GridCell toCell = GetGridCell(lastMoveTo.Value);
            toCell?.SetLastMoveHighlight(false, Color.white);
            lastMoveTo = null;
        }
    }

    private void ApplyLastMoveHighlightToCell(Vector2Int pos)
    {
        GridCell cell = GetGridCell(pos);
        if (cell == null) return;

        int x = pos.x - gridMinBounds.x;
        int y = pos.y - gridMinBounds.y;
        bool isLight = ((x + y) % 2 != 0);
        Color targetColor = isLight ? lightTileLastMoveColor : darkTileLastMoveColor;

        cell.SetLastMoveHighlight(true, targetColor);
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

    public void ShiftBoardVertical(bool isOpen, float duration)
    {
        float targetY = isOpen ? boardShiftY : 0f;
        Ease ease = Ease.OutQuad;

        if (boardContainer != null)
        {
            boardContainer.DOAnchorPosY(targetY, duration).SetEase(ease);
        }
    }

    public void SetBoardPresentation(bool dimmed, float duration)
    {
        if (boardContainer == null) return;

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
        ApplyGridCellsPresentationTint(dimmed, duration);
    }

    private void ApplyGridCellsPresentationTint(bool dimmed, float duration)
    {
        if (gridCells == null) return;

        float multiplier = dimmed ? boardDimMultiplier : 1f;
        bool isPrepare = GameStateManager.Instance != null &&
                         GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;

        foreach (var cell in gridCells)
        {
            if (cell == null || cell.CellImage == null) continue;

            Color baseColor;
            if (isPrepare && IsInvalidZone(cell.gridPosition))
            {
                baseColor = invalidZoneColor;
            }
            else
            {
                baseColor = cell.OriginalColor;
            }

            Color targetColor = new Color(baseColor.r * multiplier, baseColor.g * multiplier, baseColor.b * multiplier, baseColor.a);
            cell.CellImage.DOKill();
            if (duration > 0f)
            {
                cell.CellImage.DOColor(targetColor, duration).SetEase(Ease.OutQuad);
            }
            else
            {
                cell.CellImage.color = targetColor;
            }
        }
    }

    private void CacheBoardPresentationTargets()
    {
        if (boardPresentationCached) return;

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
            return null;

        return PieceSpawner.Instance.piecesParent as RectTransform;
    }

    private Vector2 GetOriginalRootPosition(RectTransform root)
    {
        if (root == null) return Vector2.zero;

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
            if (graphic == null) continue;

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
            if (text == null) continue;

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
        if (root == null) return;

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic != null && !(graphic is TMP_Text))
            {
                if (graphic.GetComponent<GridCell>() != null || graphic.GetComponentInParent<GridCell>() != null)
                {
                    continue;
                }
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
