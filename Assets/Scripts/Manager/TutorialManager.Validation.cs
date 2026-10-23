using System;
using System.Collections;
using UnityEngine;

public partial class TutorialManager
{
    private void SubscribeEvents()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStarted -= OnPlayerTurnStarted;
            TurnManager.Instance.OnPlayerTurnStarted += OnPlayerTurnStarted;
            TurnManager.Instance.OnOpponentTurnStarted -= OnOpponentTurnStarted;
            TurnManager.Instance.OnOpponentTurnStarted += OnOpponentTurnStarted;
        }

        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.OnArtifactAdded -= OnArtifactAdded;
            ArtifactManager.Instance.OnArtifactAdded += OnArtifactAdded;
            ArtifactManager.Instance.OnArtifactConsumed -= OnArtifactConsumed;
            ArtifactManager.Instance.OnArtifactConsumed += OnArtifactConsumed;
        }

        ActiveArtifactController.OnTargetingBegan -= OnActiveArtifactTargetingBegan;
        ActiveArtifactController.OnTargetingBegan += OnActiveArtifactTargetingBegan;
        ActiveArtifactController.OnTargetingEnded -= OnActiveArtifactTargetingEnded;
        ActiveArtifactController.OnTargetingEnded += OnActiveArtifactTargetingEnded;
    }

    private void UnsubscribeEvents()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStarted -= OnPlayerTurnStarted;
            TurnManager.Instance.OnOpponentTurnStarted -= OnOpponentTurnStarted;
        }

        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.OnArtifactAdded -= OnArtifactAdded;
            ArtifactManager.Instance.OnArtifactConsumed -= OnArtifactConsumed;
        }

        ActiveArtifactController.OnTargetingBegan -= OnActiveArtifactTargetingBegan;
        ActiveArtifactController.OnTargetingEnded -= OnActiveArtifactTargetingEnded;
    }

    private void OnActiveArtifactTargetingBegan(ArtifactData artifact)
    {
        if (!IsTutorialActive) return;
        if (CurrentPhase == TutorialPhase.LightningArtifactIntro && artifact != null && artifact.id == "AC02")
        {
            ShowLightningArtifactTargetPhase();
        }
    }

    private void OnActiveArtifactTargetingEnded()
    {
        if (!IsTutorialActive) return;
        if (CurrentPhase == TutorialPhase.LightningArtifactTarget && !hasShownLightningComplete)
        {
            hasShownLightningTarget = false;
            ShowLightningArtifactIntroPhase();
        }
    }

    private void OnArtifactConsumed(ArtifactData artifact)
    {
        if (!IsTutorialActive) return;
        if (CurrentPhase == TutorialPhase.LightningArtifactTarget && artifact != null && artifact.id == "AC02")
        {
            ShowLightningArtifactCompletePhase();
        }
    }

    /// <summary>
    /// 새로운 패시브 유물 획득 시 튜토리얼을 표시합니다.
    /// (액티브 유물 일반 튜토리얼은 제거되고 3스테이지 낙뢰의 부적 전용 튜토리얼로 진행됩니다)
    /// </summary>
    private void OnArtifactAdded(ArtifactData artifact)
    {
        if (artifact == null) return;
        if (StageSaveManager.IsRestoring || StageSaveManager.IsContinuePending) return;

        if (artifact.IsPassive)
        {
            if (PlayerPrefs.GetInt(PrefsKeyPassiveArtifactShown, 0) == 0 && !hasShownPassiveArtifact)
            {
                ShowPassiveArtifactPhase(artifact);
            }
        }
    }

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

        return true;
    }

    /// <summary>
    /// 튜토리얼 2스테이지 중 플레이어의 기물 선택을 아군 폰으로 한정합니다.
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

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (!IsTutorialActive) return;

        if (newState == GameStateManager.GameState.Prepare)
        {
            int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
            if (cleared == 0)
            {
                // 준비 단계 진입 시, 질문이 끝난 이후에만 킹 배치 튜토리얼 시작
                if (!hasShownKingPlacement && !isWaitingForQuestion && CurrentPhase != TutorialPhase.IntroQuestion)
                {
                    ShowPlacementKingPhase();
                }
            }
            else if (cleared == 1)
            {
                // 2스테이지 준비 단계 진입 시: 보드에 폰이 없으면 폰 재배치 튜토리얼을 먼저 진행!
                if (!IsPieceOnBoard(PieceType.Pawn))
                {
                    ShowPlacementPawnStage2Phase();
                }
                else if (!hasShownSealIntro)
                {
                    ShowSealIntroPhase();
                }
            }
            else if (cleared == 2)
            {
                // 3스테이지 준비 단계: 킹/퀸 배치 유지 및 전투 시작 가능
            }
        }
        else if (newState == GameStateManager.GameState.GamePlay)
        {
            // 전투 시작 시 배치 단계 및 인장 단계의 모든 강조 효과 해제 및 대화창 정리, 상대 착수 카운트 초기화
            ClearPieceHighlight();
            ClearTargetGridCellHighlight();
            ClearSealHighlight();
            HighlightStartButton(false);
            HighlightQueenButton(false);
            BoardEvaluator.TutorialOpponentTurnCount = 0;
            if (isDialogShowing && (CurrentPhase == TutorialPhase.PlacementKing || CurrentPhase == TutorialPhase.PlacementPawn ||
                                   CurrentPhase == TutorialPhase.PlacementComplete || CurrentPhase == TutorialPhase.PlacementPawnStage2 ||
                                   CurrentPhase == TutorialPhase.SealIntro || CurrentPhase == TutorialPhase.SealEquip ||
                                   CurrentPhase == TutorialPhase.SealEquipComplete || CurrentPhase == TutorialPhase.StageSelect ||
                                   CurrentPhase == TutorialPhase.Reward || CurrentPhase == TutorialPhase.RewardStage2))
            {
                CloseCurrentPhaseDialog();
            }
        }
    }

    private void OnOpponentTurnStarted()
    {
        if (!IsTutorialActive) return;

        // 플레이어가 이동/포획 행동을 완료하여 상대 턴으로 넘어가면 안내 대화창 닫기 및 하이라이트 정리
        if ((CurrentPhase == TutorialPhase.Movement || CurrentPhase == TutorialPhase.Capture ||
             CurrentPhase == TutorialPhase.SealMovement || CurrentPhase == TutorialPhase.PromotionAdvance2 ||
             CurrentPhase == TutorialPhase.PromotionAdvance3) && isDialogShowing)
        {
            ClearPieceHighlight();
            ClearTargetGridCellHighlight();
            CloseCurrentPhaseDialog();
        }
    }

    private void OnPlayerTurnStarted()
    {
        if (!IsTutorialActive) return;

        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared == 0)
        {
            if (!hasShownMovement && GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay)
            {
                StartCoroutine(DelayedShowMovementRoutine());
            }
            else if (hasShownMovement && !hasShownCapture && !isDialogShowing)
            {
                if (CanAnyPlayerPieceCapture())
                {
                    StartCoroutine(DelayedShowCaptureRoutine());
                }
            }
        }
        else if (cleared == 1 && !hasShownPromotionComplete)
        {
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay)
            {
                PieceController pawn = FindBoardPiece(PieceType.Pawn);
                int pawnY = (pawn != null && pawn.gridPosition.HasValue) ? pawn.gridPosition.Value.y : -2;

                if (pawnY <= -1 && !hasShownSealMovement)
                {
                    StartCoroutine(DelayedShowSealMovementRoutine());
                }
                else if (pawnY == 0 && !hasShownPromotionAdvance2)
                {
                    StartCoroutine(DelayedShowPromotionAdvance2Routine());
                }
                else if (pawnY >= 1 && !hasShownPromotionAdvance3)
                {
                    StartCoroutine(DelayedShowPromotionAdvance3Routine());
                }
            }
        }
        else if (cleared == 2)
        {
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay)
            {
                if (!hasShownLightningIntro)
                {
                    StartCoroutine(DelayedShowLightningIntroRoutine());
                }
            }
        }
    }

    private IEnumerator DelayedShowMovementRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        if (IsTutorialActive && !hasShownMovement)
        {
            ClearBoardMoveMarkers();
            ShowMovementPhase();
        }
    }

    private IEnumerator DelayedShowSealMovementRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        if (IsTutorialActive && !hasShownSealMovement && !hasShownPromotionComplete)
        {
            ClearBoardMoveMarkers();
            ShowSealMovementPhase();
        }
    }

    private IEnumerator DelayedShowPromotionAdvance2Routine()
    {
        yield return new WaitForSeconds(0.25f);
        if (IsTutorialActive && !hasShownPromotionAdvance2 && !hasShownPromotionComplete)
        {
            ClearBoardMoveMarkers();
            ShowPromotionAdvance2Phase();
        }
    }

    private IEnumerator DelayedShowPromotionAdvance3Routine()
    {
        yield return new WaitForSeconds(0.25f);
        if (IsTutorialActive && !hasShownPromotionAdvance3 && !hasShownPromotionComplete)
        {
            ClearBoardMoveMarkers();
            ShowPromotionAdvance3Phase();
        }
    }

    private IEnumerator DelayedShowCaptureRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        if (IsTutorialActive && !hasShownCapture && CanAnyPlayerPieceCapture())
        {
            ClearBoardMoveMarkers();
            ShowCapturePhase();
        }
    }

    private IEnumerator DelayedShowPromotionCompleteRoutine()
    {
        yield return new WaitForSeconds(0.35f);
        if (IsTutorialActive && !hasShownPromotionComplete)
        {
            ShowPromotionCompletePhase();
        }
    }

    private IEnumerator DelayedShowLightningIntroRoutine()
    {
        yield return new WaitForSeconds(0.3f);
        if (IsTutorialActive && !hasShownLightningIntro)
        {
            ClearBoardMoveMarkers();
            ShowLightningArtifactIntroPhase();
        }
    }

    private void Update()
    {
        if (dialogUI == null || (!IsTutorialActive && !isDialogShowing))
        {
            return;
        }

        // 1. 대화창 활성 상태의 단축키 처리
        if (isDialogShowing && dialogUI.gameObject.activeInHierarchy)
        {
            // 질문 단계 단축키 (1/Enter: 진행, 2/Esc: 건너뛰기)
            if (isWaitingForQuestion)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ||
                    Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    OnProceedClicked();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) ||
                    Input.GetKeyDown(KeyCode.Escape))
                {
                    OnSkipClicked();
                    return;
                }

                return;
            }

            // 대화 넘김 단축키 (Enter / Space / 마우스 클릭)
            // 행동 대기 스텝(기물 배치, 카드 선택, 버튼 클릭 등)에서는 대화창이 넘어가지 않고 화면에 유지되어 플레이어가 안내를 보며 조작할 수 있습니다.
            if (!IsCurrentStepAction() && Time.unscaledTime >= canAdvanceTime)
            {
                if (Input.GetKeyDown(KeyCode.Space) ||
                    Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetMouseButtonDown(0))
                {
                    AdvanceStep();
                }
            }
        }

        // 2. 기물 배치 및 인장 장착 실시간 상태 전이 및 하이라이트 (준비 단계)
        if (IsTutorialActive && GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
            if (cleared == 0)
            {
                // 질문 단계 중에는 기물 강조 및 상태 전이를 수행하지 않음
                if (!isWaitingForQuestion && CurrentPhase != TutorialPhase.IntroQuestion)
                {
                    if (highlightedPiece == null)
                    {
                        if (CurrentPhase == TutorialPhase.PlacementKing && isKingHighlightAllowed && !IsPieceOnBoard(PieceType.King))
                        {
                            HighlightInventoryPiece(PieceType.King);
                        }
                        else if (CurrentPhase == TutorialPhase.PlacementPawn && isPawnHighlightAllowed && !IsPieceOnBoard(PieceType.Pawn))
                        {
                            HighlightInventoryPiece(PieceType.Pawn);
                        }
                    }

                    // 타겟 그리드 셀 하이라이트 실시간 유지
                    if (tutorialCellHighlightObj == null)
                    {
                        if (CurrentPhase == TutorialPhase.PlacementKing && isKingHighlightAllowed && !IsPieceOnBoard(PieceType.King))
                        {
                            HighlightTargetGridCell(TargetKingPos);
                        }
                        else if (CurrentPhase == TutorialPhase.PlacementPawn && isPawnHighlightAllowed && !IsPieceOnBoard(PieceType.Pawn))
                        {
                            HighlightTargetGridCell(TargetPawnPos);
                        }
                    }

                    if (highlightedStartButton == null && CurrentPhase == TutorialPhase.PlacementComplete && isStartButtonHighlightAllowed)
                    {
                        HighlightStartButton(true);
                    }

                    // 킹 배치 완료 감지 -> 폰 배치 단계로 전이
                    if (CurrentPhase == TutorialPhase.PlacementKing && IsPieceOnBoard(PieceType.King))
                    {
                        ClearPieceHighlight();
                        ClearTargetGridCellHighlight();
                        isKingHighlightAllowed = false;
                        if (isDialogShowing)
                        {
                            CloseCurrentPhaseDialog();
                        }
                        ShowPlacementPawnPhase();
                        return;
                    }

                    // 폰 배치 완료 감지 -> 배치 완료 단계로 전이 ([전투 시작] 버튼 강조 및 안내)
                    if (CurrentPhase == TutorialPhase.PlacementPawn && IsPieceOnBoard(PieceType.Pawn))
                    {
                        ClearPieceHighlight();
                        ClearTargetGridCellHighlight();
                        isPawnHighlightAllowed = false;
                        if (isDialogShowing)
                        {
                            CloseCurrentPhaseDialog();
                        }
                        ShowPlacementCompletePhase();
                        return;
                    }
                }
            }
            else if (cleared == 1)
            {
                // 폰이 보드에 없는데 아직 폰 재배치 단계가 아니라면 즉시 폰 재배치 단계 개시!
                if (!IsPieceOnBoard(PieceType.Pawn) && CurrentPhase != TutorialPhase.PlacementPawnStage2 &&
                    !hasShownSealMovement && !hasShownPromotionComplete)
                {
                    if (isDialogShowing)
                    {
                        CloseCurrentPhaseDialog();
                    }
                    ShowPlacementPawnStage2Phase();
                    return;
                }

                if (CurrentPhase == TutorialPhase.PlacementPawnStage2)
                {
                    if (highlightedPiece == null && !IsPieceOnBoard(PieceType.Pawn) && isPawnHighlightAllowed)
                    {
                        HighlightInventoryPiece(PieceType.Pawn);
                    }
                    if (tutorialCellHighlightObj == null && !IsPieceOnBoard(PieceType.Pawn) && isPawnHighlightAllowed)
                    {
                        HighlightTargetGridCell(TargetPawnPos);
                    }

                    // 폰 배치 완료 감지 -> 바로 인장 소개 단계(SealIntro)로 전이!
                    if (IsPieceOnBoard(PieceType.Pawn))
                    {
                        ClearPieceHighlight();
                        ClearTargetGridCellHighlight();
                        isPawnHighlightAllowed = false;
                        if (isDialogShowing)
                        {
                            CloseCurrentPhaseDialog();
                        }
                        ShowSealIntroPhase();
                        return;
                    }
                }
                // 2스테이지 인장 장착 실시간 감지 및 하이라이트 유지
                else if (CurrentPhase == TutorialPhase.SealIntro && isSealHighlightAllowed && highlightedSeal == null)
                {
                    HighlightSealInInventory(true);
                }
                else if (CurrentPhase == TutorialPhase.SealEquip)
                {
                    if (highlightedSeal == null)
                    {
                        HighlightSealInInventory(true);
                    }
                    if (highlightedPiece == null)
                    {
                        HighlightBoardPiece(PieceType.Pawn);
                    }

                    // 폰에 인장 장착 완료 실시간 감지
                    if (IsPieceEquippedWithSeal(PieceType.Pawn))
                    {
                        ClearSealHighlight();
                        ClearPieceHighlight();
                        if (isDialogShowing)
                        {
                            CloseCurrentPhaseDialog();
                        }
                        ShowSealEquipCompletePhase();
                        return;
                    }
                }
                else if (CurrentPhase == TutorialPhase.SealEquipComplete && isStartButtonHighlightAllowed && highlightedStartButton == null)
                {
                    HighlightStartButton(true);
                }
            }
        }

        // 3. 전투 중 기물 포획 타이밍 실시간 감지
        if (IsTutorialActive && hasShownMovement && !hasShownCapture && !isDialogShowing)
        {
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay &&
                TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn)
            {
                if (!Input.GetMouseButton(0) && CanAnyPlayerPieceCapture())
                {
                    ShowCapturePhase();
                }
            }
        }
    }
}
