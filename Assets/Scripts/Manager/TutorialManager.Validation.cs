using UnityEngine;

public partial class TutorialManager
{
    /// <summary>
    /// 튜토리얼 중 해당 기물을 드래그할 수 있는지 확인합니다.
    /// (설명 스텝에 도달하기 전이거나, 현재 배치 대상이 아닌 기물은 드래그할 수 없습니다)
    /// </summary>
    public bool IsPieceDragAllowed(PieceType pieceType, PieceLocation location)
    {
        if (!IsTutorialActive) return true;
        if (GameStateManager.Instance == null) return true;

        bool isPrepare = GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;
        bool isGamePlay = GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay;

        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (isPrepare)
        {
            if (cleared == 0)
            {
                if (CurrentPhase == TutorialPhase.PlacementKing)
                {
                    return isKingHighlightAllowed && pieceType == PieceType.King && location == PieceLocation.Inventory;
                }

                if (CurrentPhase == TutorialPhase.PlacementPawn)
                {
                    return isPawnHighlightAllowed && pieceType == PieceType.Pawn && location == PieceLocation.Inventory;
                }

                return false;
            }
            else if (cleared == 1)
            {
                if (CurrentPhase == TutorialPhase.PlacementPawnStage2)
                {
                    return isPawnHighlightAllowed && pieceType == PieceType.Pawn && location == PieceLocation.Inventory;
                }
                // 2스테이지 준비 단계에서는 인장 장착 완료 후에만 기물 위치 조정을 허용
                return CurrentPhase == TutorialPhase.SealEquipComplete;
            }
            else if (cleared == 2)
            {
                return true;
            }
        }
        else if (isGamePlay)
        {
            if (cleared == 1 && !hasShownPromotionComplete)
            {
                if (CurrentPhase == TutorialPhase.SealMovement ||
                    CurrentPhase == TutorialPhase.PromotionAdvance2 ||
                    CurrentPhase == TutorialPhase.PromotionAdvance3)
                {
                    return pieceType == PieceType.Pawn;
                }
            }
            else if (cleared == 2)
            {
                if (CurrentPhase == TutorialPhase.LightningArtifactIntro ||
                    CurrentPhase == TutorialPhase.LightningArtifactTarget)
                {
                    return false;
                }
            }
            else if (cleared == 3)
            {
                if (CurrentPhase == TutorialPhase.ProtectionPotionIntro ||
                    CurrentPhase == TutorialPhase.ProtectionPotionTarget)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 튜토리얼 2스테이지 중 플레이어 폰의 전진 경로(x=1)를 강제 검증합니다.
    /// </summary>
    public bool IsPieceMoveAllowed(PieceController piece, Vector2Int targetPos)
    {
        if (!IsTutorialActive) return true;

        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared == 1 && !hasShownPromotionComplete)
        {
            if (piece == null || piece.IsEnemy) return true;

            // 폰만 이동 가능
            if (piece.Type != PieceType.Pawn) return false;

            if (CurrentPhase == TutorialPhase.SealMovement)
            {
                return targetPos == new Vector2Int(1, 0);
            }
            else if (CurrentPhase == TutorialPhase.PromotionAdvance2)
            {
                return targetPos == new Vector2Int(1, 1);
            }
            else if (CurrentPhase == TutorialPhase.PromotionAdvance3)
            {
                return targetPos == new Vector2Int(1, 2);
            }

            // 페이즈 대기/전환 중이라도 x=1 열로의 전진이면 허용
            if (targetPos.x == 1 && targetPos.y > (piece.gridPosition?.y ?? -2))
            {
                return true;
            }

            return false;
        }
        else if (cleared == 2)
        {
            if (CurrentPhase == TutorialPhase.LightningArtifactIntro ||
                CurrentPhase == TutorialPhase.LightningArtifactTarget)
            {
                return false;
            }
        }
        else if (cleared == 3)
        {
            if (CurrentPhase == TutorialPhase.ProtectionPotionIntro ||
                CurrentPhase == TutorialPhase.ProtectionPotionTarget)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 튜토리얼 중 플레이어의 기물 선택을 허용/제한합니다.
    /// </summary>
    public bool IsPieceSelectionAllowed(PieceController piece)
    {
        if (!IsTutorialActive) return true;
        if (piece == null || piece.IsEnemy) return true;

        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared == 1 && !hasShownPromotionComplete)
        {
            if (CurrentPhase == TutorialPhase.SealMovement ||
                CurrentPhase == TutorialPhase.PromotionAdvance2 ||
                CurrentPhase == TutorialPhase.PromotionAdvance3)
            {
                return piece.Type == PieceType.Pawn;
            }
        }
        else if (cleared == 2)
        {
            if (CurrentPhase == TutorialPhase.LightningArtifactIntro ||
                CurrentPhase == TutorialPhase.LightningArtifactTarget)
            {
                return false;
            }
        }
        else if (cleared == 3)
        {
            if (CurrentPhase == TutorialPhase.ProtectionPotionIntro)
            {
                return false;
            }
            if (CurrentPhase == TutorialPhase.ProtectionPotionTarget)
            {
                return !piece.IsEnemy;
            }
        }

        return true;
    }

    /// <summary>
    /// 튜토리얼 2스테이지 폰 승급 진행 중 x = 1 열 전체를 적 공격 및 침범으로부터 보호합니다.
    /// </summary>
    public bool IsTargetProtectedFromEnemy(Vector2Int target)
    {
        if (!IsTutorialActive || hasShownPromotionComplete) return false;
        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared == 1)
        {
            return target.x == 1;
        }
        return false;
    }

    /// <summary>
    /// 튜토리얼 중 지정된 그리드 칸으로만 기물이 배치되도록 검증합니다.
    /// </summary>
    public bool IsPlacementAllowed(PieceType pieceType, Vector2Int targetPos)
    {
        if (!IsTutorialActive) return true;
        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameStateManager.GameState.Prepare)
        {
            return true;
        }

        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared == 0)
        {
            if (CurrentPhase == TutorialPhase.PlacementKing)
            {
                return pieceType == PieceType.King && targetPos == TargetKingPos;
            }

            if (CurrentPhase == TutorialPhase.PlacementPawn)
            {
                return pieceType == PieceType.Pawn && targetPos == TargetPawnPos;
            }

            return false;
        }
        else if (cleared == 1)
        {
            if (CurrentPhase == TutorialPhase.PlacementPawnStage2)
            {
                return pieceType == PieceType.Pawn && targetPos == TargetPawnPos;
            }
        }

        return true;
    }

    /// <summary>
    /// 전장의 특정 기물에 인장이 하나라도 장착되어 있는지 검사합니다.
    /// </summary>
    public bool IsPieceEquippedWithSeal(PieceType type)
    {
        if (PieceManager.Instance != null && PieceManager.Instance.Pieces != null)
        {
            foreach (var p in PieceManager.Instance.Pieces)
            {
                if (p != null && !p.IsEnemy && p.Type == type && p.EquippedSeals != null && p.EquippedSeals.Count > 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 튜토리얼 중 인장을 드래그할 수 있는지 확인합니다. (설명 스텝 도달 전 드래그 방지)
    /// </summary>
    public bool IsSealDragAllowed(DraggableSeal draggableSeal)
    {
        if (!IsTutorialActive) return true;
        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared == 1)
        {
            return CurrentPhase == TutorialPhase.SealEquip;
        }
        return true;
    }

    /// <summary>
    /// 튜토리얼 중 해당 기물에 인장을 장착할 수 있는지 확인합니다. (아군 폰에만 장착 허용)
    /// </summary>
    public bool IsSealEquipAllowed(PieceController targetPiece)
    {
        if (!IsTutorialActive) return true;
        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared == 1 && CurrentPhase == TutorialPhase.SealEquip)
        {
            return targetPiece != null && !targetPiece.IsEnemy && targetPiece.Type == PieceType.Pawn;
        }
        return true;
    }

    /// <summary>
    /// [전투 시작] 버튼을 누를 수 있는 상태인지 검증합니다.
    /// </summary>
    public bool IsGameStartAllowed()
    {
        if (!IsTutorialActive) return true;
        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared == 0)
        {
            return CurrentPhase == TutorialPhase.PlacementComplete;
        }
        else if (cleared == 1)
        {
            return CurrentPhase == TutorialPhase.SealEquipComplete;
        }
        return true;
    }

    /// <summary>
    /// 현재 전장(보드) 위에 특정 타입의 아군 기물이 배치되어 있는지 확인합니다.
    /// </summary>
    public bool IsPieceOnBoard(PieceType type)
    {
        if (PieceManager.Instance == null) return false;
        var pieces = PieceManager.Instance.Pieces;
        if (pieces == null) return false;

        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p != null && !p.IsEnemy && p.Type == type && p.CurrentLocation == PieceLocation.Board)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 튜토리얼 중 승급 선택 시 퀸만 선택 가능하도록 제한합니다.
    /// </summary>
    public bool IsPromotionChoiceAllowed(PieceType promotedType)
    {
        if (!IsTutorialActive) return true;
        if (CurrentPhase == TutorialPhase.PromotionChoice)
        {
            return promotedType == PieceType.Queen;
        }
        return true;
    }

    /// <summary>
    /// 전장에 배치된 아군 기물 중 적 기물을 포획 가능한 수가 존재하는지 판정합니다.
    /// </summary>
    public bool CanAnyPlayerPieceCapture()
    {
        if (PieceManager.Instance == null) return false;

        var playerPieces = PieceManager.Instance.PlayerPieces;
        if (playerPieces == null || playerPieces.Count == 0) return false;

        for (int i = 0; i < playerPieces.Count; i++)
        {
            var piece = playerPieces[i];
            if (piece == null || piece.IsEnemy || piece.CurrentLocation != PieceLocation.Board)
            {
                continue;
            }

            var candidateMoves = piece.GetCandidateMoves();
            if (candidateMoves == null || candidateMoves.Count == 0)
            {
                continue;
            }

            for (int m = 0; m < candidateMoves.Count; m++)
            {
                Vector2Int targetPos = candidateMoves[m];
                PieceController targetPiece = PieceManager.Instance.GetPieceAt(targetPos);
                if (targetPiece != null && targetPiece.IsEnemy && !targetPiece.IsInvincible)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
