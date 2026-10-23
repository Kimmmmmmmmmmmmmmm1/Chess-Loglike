using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public partial class TutorialManager
{
    // 강조 연출 제어 필드
    private PieceController highlightedPiece;
    private Tween highlightScaleTween;
    private Button highlightedStartButton;
    private Tween startButtonTween;
    private DraggableSeal highlightedSeal;
    private Tween sealTween;
    private Button highlightedQueenButton;
    private Tween queenButtonTween;
    private Vector2Int? currentHighlightedCellPos;
    private GameObject tutorialCellHighlightObj;
    private Tween tutorialCellTween;
    private ArtifactSlot highlightedArtifactSlot;
    private Tween artifactSlotTween;

    /// <summary>
    /// 인벤토리에 보관된 특정 타입의 기물을 황금빛 아웃라인 및 펄스 스케일(0.94~1.1)로 강조 표시합니다.
    /// </summary>
    private void HighlightInventoryPiece(PieceType type)
    {
        ClearPieceHighlight();

        PieceController targetPiece = FindInventoryPiece(type);
        if (targetPiece != null)
        {
            ApplyPieceHighlight(targetPiece);
        }
    }

    private PieceController FindInventoryPiece(PieceType type)
    {
        // 1. PieceManager에 등록된 기물 중 검색
        if (PieceManager.Instance != null && PieceManager.Instance.Pieces != null)
        {
            foreach (var p in PieceManager.Instance.Pieces)
            {
                if (p != null && !p.IsEnemy && p.Type == type && p.CurrentLocation == PieceLocation.Inventory)
                {
                    return p;
                }
            }
        }

        // 2. 씬에 존재하는 모든 PieceController 중 검색
        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in allPieces)
        {
            if (p != null && !p.IsEnemy && p.Type == type && p.CurrentLocation == PieceLocation.Inventory)
            {
                return p;
            }
        }

        // 3. InventorySlot 자식 기물 중 검색
        InventorySlot[] slots = FindObjectsByType<InventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            PieceController p = slot.GetComponentInChildren<PieceController>();
            if (p != null && !p.IsEnemy && p.Type == type)
            {
                return p;
            }
        }

        return null;
    }

    private void ApplyPieceHighlight(PieceController piece)
    {
        if (piece == null) return;
        highlightedPiece = piece;

        Outline outline = highlightedPiece.GetComponent<Outline>();
        if (outline == null) outline = highlightedPiece.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
        outline.effectDistance = new Vector2(3.5f, 3.5f);
        outline.enabled = true;

        highlightedPiece.transform.DOKill();
        highlightedPiece.transform.localScale = Vector3.one * 0.94f;
        highlightScaleTween = highlightedPiece.transform.DOScale(Vector3.one * 1.1f, 0.45f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    /// <summary>
    /// 보드 위의 모든 기물 이동 마커 및 선택 상태를 즉시 제거합니다.
    /// </summary>
    public void ClearBoardMoveMarkers()
    {
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.ClearSelection();
            PieceManager.Instance.HideMoveMarkers();
            PieceManager.Instance.ClearMarkers();
        }
    }

    private void ClearPieceHighlight()
    {
        if (highlightScaleTween != null)
        {
            highlightScaleTween.Kill();
            highlightScaleTween = null;
        }

        if (highlightedPiece != null)
        {
            highlightedPiece.transform.DOKill();
            highlightedPiece.transform.localScale = Vector3.one;
            Outline outline = highlightedPiece.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            highlightedPiece = null;
        }
    }

    /// <summary>
    /// 우측 하단의 [전투 시작] 버튼을 부드러운 펄스 스케일(0.94~1.08)로 강조합니다.
    /// </summary>
    private void HighlightStartButton(bool highlight)
    {
        if (startButtonTween != null)
        {
            startButtonTween.Kill();
            startButtonTween = null;
        }

        if (highlightedStartButton != null)
        {
            highlightedStartButton.transform.DOKill();
            highlightedStartButton.transform.localScale = Vector3.one;
            Outline outline = highlightedStartButton.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            highlightedStartButton = null;
        }

        if (highlight && GameStateManager.Instance != null && GameStateManager.Instance.gameStartButton != null)
        {
            highlightedStartButton = GameStateManager.Instance.gameStartButton;

            Outline outline = highlightedStartButton.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;

            highlightedStartButton.transform.DOKill();
            highlightedStartButton.transform.localScale = Vector3.one * 0.94f;
            startButtonTween = highlightedStartButton.transform.DOScale(Vector3.one * 1.08f, 0.45f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// 지정된 그리드 칸에 외곽선 글로우 없이 부드러운 펄스 하이라이트를 표시합니다.
    /// </summary>
    public void HighlightTargetGridCell(Vector2Int targetPos)
    {
        if (currentHighlightedCellPos.HasValue && currentHighlightedCellPos.Value == targetPos && tutorialCellHighlightObj != null)
        {
            return;
        }

        ClearTargetGridCellHighlight();
        currentHighlightedCellPos = targetPos;

        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null) return;
        GridCell cell = PieceManager.Instance.gridManager.GetGridCell(targetPos);
        if (cell == null) return;

        tutorialCellHighlightObj = new GameObject("TutorialTargetCellHighlight", typeof(RectTransform), typeof(Image));
        tutorialCellHighlightObj.transform.SetParent(cell.transform, false);
        tutorialCellHighlightObj.transform.SetAsLastSibling();

        RectTransform rt = tutorialCellHighlightObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = tutorialCellHighlightObj.GetComponent<Image>();
        img.color = new Color(1f, 0.85f, 0.2f, 0.22f);
        img.raycastTarget = false;

        img.DOKill();
        tutorialCellTween = img.DOFade(0.55f, 0.5f)
            .From(0.12f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    /// <summary>
    /// 그리드 칸의 튜토리얼 하이라이트를 해제합니다.
    /// </summary>
    public void ClearTargetGridCellHighlight()
    {
        currentHighlightedCellPos = null;
        if (tutorialCellTween != null)
        {
            tutorialCellTween.Kill();
            tutorialCellTween = null;
        }
        if (tutorialCellHighlightObj != null)
        {
            Destroy(tutorialCellHighlightObj);
            tutorialCellHighlightObj = null;
        }
    }

    /// <summary>
    /// 전장(보드) 위에 배치된 특정 타입의 아군 기물을 부드러운 펄스(0.95~1.08)로 강조합니다.
    /// </summary>
    public void HighlightBoardPiece(PieceType type)
    {
        ClearPieceHighlight();

        PieceController targetPiece = FindBoardPiece(type);
        if (targetPiece != null)
        {
            highlightedPiece = targetPiece;
            Outline outline = highlightedPiece.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;

            highlightedPiece.transform.DOKill();
            highlightedPiece.transform.localScale = Vector3.one * 0.95f;
            highlightScaleTween = highlightedPiece.transform.DOScale(Vector3.one * 1.08f, 0.45f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
    }

    private PieceController FindBoardPiece(PieceType type)
    {
        if (PieceManager.Instance != null && PieceManager.Instance.Pieces != null)
        {
            foreach (var p in PieceManager.Instance.Pieces)
            {
                if (p != null && !p.IsEnemy && p.Type == type && p.CurrentLocation == PieceLocation.Board)
                {
                    return p;
                }
            }
        }

        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in allPieces)
        {
            if (p != null && !p.IsEnemy && p.Type == type && p.CurrentLocation == PieceLocation.Board)
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>
    /// 하단 인장 인벤토리에 보관된 인장을 부드러운 펄스 스케일(0.95~1.08)로 강조합니다.
    /// </summary>
    public void HighlightSealInInventory(bool highlight)
    {
        ClearSealHighlight();
        if (!highlight) return;

        DraggableSeal targetSeal = FindInventorySeal();
        if (targetSeal != null)
        {
            highlightedSeal = targetSeal;
            highlightedSeal.transform.DOKill();
            highlightedSeal.transform.localScale = Vector3.one * 0.95f;
            sealTween = highlightedSeal.transform.DOScale(Vector3.one * 1.08f, 0.45f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
    }

    public void ClearSealHighlight()
    {
        if (sealTween != null)
        {
            sealTween.Kill();
            sealTween = null;
        }

        if (highlightedSeal != null)
        {
            highlightedSeal.transform.DOKill();
            highlightedSeal.transform.localScale = Vector3.one;
            highlightedSeal = null;
        }
    }

    private DraggableSeal FindInventorySeal()
    {
        DraggableSeal[] seals = FindObjectsByType<DraggableSeal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var s in seals)
        {
            if (s != null && s.SourcePiece == null && s.SealData != null)
            {
                if (s.SealData.sealName != null && s.SealData.sealName.Contains("비숍"))
                {
                    return s;
                }
            }
        }
        foreach (var s in seals)
        {
            if (s != null && s.SourcePiece == null && s.SealData != null)
            {
                return s;
            }
        }
        return null;
    }

    /// <summary>
    /// 승급 패널의 퀸 버튼을 부드러운 펄스 스케일(0.95~1.08)로 강조합니다.
    /// </summary>
    public void HighlightQueenButton(bool highlight)
    {
        if (queenButtonTween != null)
        {
            queenButtonTween.Kill();
            queenButtonTween = null;
        }

        if (highlightedQueenButton != null)
        {
            highlightedQueenButton.transform.DOKill();
            highlightedQueenButton.transform.localScale = Vector3.one;
            highlightedQueenButton = null;
        }

        if (highlight && PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.QueenButton != null)
        {
            highlightedQueenButton = PiecePromotionManager.Instance.QueenButton;

            Outline outline = highlightedQueenButton.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;

            highlightedQueenButton.transform.DOKill();
            highlightedQueenButton.transform.localScale = Vector3.one * 0.95f;
            queenButtonTween = highlightedQueenButton.transform.DOScale(Vector3.one * 1.08f, 0.45f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// 상단 유물 보관함에서 새로 획득한 유물 슬롯을 부드러운 펄스 스케일(0.95~1.15)로 강조합니다.
    /// </summary>
    public void HighlightArtifactSlot(ArtifactData artifact)
    {
        if (artifact == null)
        {
            ClearArtifactSlotHighlight();
            return;
        }
        HighlightArtifactSlot(artifact.id);
    }

    /// <summary>
    /// 상단 유물 보관함에서 특정 ID의 유물 슬롯을 부드러운 펄스 스케일(0.95~1.15)로 강조합니다.
    /// </summary>
    public void HighlightArtifactSlot(string artifactId)
    {
        ClearArtifactSlotHighlight();
        if (string.IsNullOrEmpty(artifactId) || ArtifactManager.Instance == null) return;

        var slots = ArtifactManager.Instance.artifactSlots;
        if (slots == null || slots.Count == 0) return;

        ArtifactSlot targetSlot = null;
        foreach (var slot in slots)
        {
            if (slot != null && slot.Artifact != null && slot.Artifact.id == artifactId)
            {
                targetSlot = slot;
                break;
            }
        }

        if (targetSlot != null)
        {
            highlightedArtifactSlot = targetSlot;
            Transform t = targetSlot.transform;
            t.DOKill();
            t.localScale = Vector3.one * 0.95f;
            artifactSlotTween = t.DOScale(Vector3.one * 1.15f, 0.45f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// 유물 슬롯의 강조 펄스 연출을 해제합니다.
    /// </summary>
    public void ClearArtifactSlotHighlight()
    {
        if (artifactSlotTween != null)
        {
            artifactSlotTween.Kill();
            artifactSlotTween = null;
        }

        if (highlightedArtifactSlot != null)
        {
            highlightedArtifactSlot.transform.DOKill();
            highlightedArtifactSlot.transform.localScale = Vector3.one;
            highlightedArtifactSlot = null;
        }
    }
}

