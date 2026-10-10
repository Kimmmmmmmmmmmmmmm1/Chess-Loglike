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
                StartCoroutine(HighlightInvalidZoneRoutine());
            }
        }
    }

    public void CreateGridCells()
    {
        if (gridCells != null) return;

        if (gridCellsParent == null)
        {
            GameObject parentObj = new GameObject("GridCellsParent");
            parentObj.transform.SetParent(transform, false);
            gridCellsParent = parentObj.AddComponent<RectTransform>();
            gridCellsParent.anchoredPosition = Vector2.zero;
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

                // 생성 애니메이션
                rect.localScale = Vector3.zero;
                rect.anchoredPosition = anchoredPos - new Vector2(0, 30f);
                float delay = y * 0.04f + x * 0.02f;
                rect.DOAnchorPos(anchoredPos, 0.35f).SetEase(Ease.OutBack).SetDelay(delay);
                rect.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetDelay(delay);
            }
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
            StartCoroutine(HighlightInvalidZoneRoutine());
        }
        else
        {
            ResetZoneHighlights();
        }
    }

    private IEnumerator HighlightInvalidZoneRoutine()
    {
        yield return null;

        if (gridCells == null) yield break;

        foreach (var cell in gridCells)
        {
            if (cell == null) continue;
            bool isInvalid = IsInvalidZone(cell.gridPosition);
            cell.SetGray(isInvalid, invalidZoneColor);
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
            cell?.SetGray(false, invalidZoneColor);
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
