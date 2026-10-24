using System.Collections;
using UnityEngine;
using DG.Tweening;

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

        SealInventory.OnSealAddedToInventory -= OnSealAdded;
        SealInventory.OnSealAddedToInventory += OnSealAdded;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= OnFlowStateChanged;
            GameManager.Instance.OnFlowStateChanged += OnFlowStateChanged;
        }
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

        SealInventory.OnSealAddedToInventory -= OnSealAdded;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= OnFlowStateChanged;
        }
    }

    private void OnFlowStateChanged(GameFlowState state)
    {
        // 스테이지 선택이나 상점 화면 진입 중에는 버프 튜토리얼을 즉시 띄우지 않고,
        // 각 화면의 등장 연출(상점 기물 등장 등)이 완료된 후 호출되도록 제어합니다.
        if (state == GameFlowState.StageSelect || state == GameFlowState.Shop || state == GameFlowState.Battle)
        {
            return;
        }

        CheckOwnedBuffs();
    }

    private void OnActiveArtifactTargetingBegan(ArtifactData artifact)
    {
        if (!IsTutorialActive) return;
        if (CurrentPhase == TutorialPhase.LightningArtifactIntro && artifact != null && artifact.id == "AC02")
        {
            ShowLightningArtifactTargetPhase();
        }
        else if (CurrentPhase == TutorialPhase.ProtectionPotionIntro && artifact != null && artifact.id == "AC03")
        {
            ShowProtectionPotionTargetPhase();
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
        else if (CurrentPhase == TutorialPhase.ProtectionPotionTarget && !hasShownProtectionPotionComplete)
        {
            hasShownProtectionPotionTarget = false;
            ShowProtectionPotionIntroPhase();
        }
    }

    private void OnArtifactConsumed(ArtifactData artifact)
    {
        if (!IsTutorialActive) return;
        if (CurrentPhase == TutorialPhase.LightningArtifactTarget && artifact != null && artifact.id == "AC02")
        {
            ShowLightningArtifactCompletePhase();
        }
        else if (CurrentPhase == TutorialPhase.ProtectionPotionTarget && artifact != null && artifact.id == "AC03")
        {
            ShowProtectionPotionCompletePhase();
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Prepare)
        {
            DOVirtual.DelayedCall(0.5f, () =>
            {
                if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
                {
                    CheckOwnedBuffs();
                }
            }).SetLink(gameObject);
        }

        if (!IsTutorialActive) return;

        if (newState == GameStateManager.GameState.Prepare)
        {
            int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
            if (cleared == 0)
            {
                if (!hasShownKingPlacement && !isWaitingForQuestion && CurrentPhase != TutorialPhase.IntroQuestion)
                {
                    ShowPlacementKingPhase();
                }
            }
            else if (cleared == 1)
            {
                if (!IsPieceOnBoard(PieceType.Pawn))
                {
                    ShowPlacementPawnStage2Phase();
                }
                else if (!hasShownSealIntro)
                {
                    ShowSealIntroPhase();
                }
            }
        }
        else if (newState == GameStateManager.GameState.GamePlay)
        {
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
                                   CurrentPhase == TutorialPhase.Reward || CurrentPhase == TutorialPhase.RewardStage2 ||
                                   CurrentPhase == TutorialPhase.RewardStage3))
            {
                CloseCurrentPhaseDialog();
            }
        }
        else if (newState == GameStateManager.GameState.Win)
        {
            // 4스테이지 이상 승리 시(GameManager에서 clearedStage++ 된 후 상태) 튜토리얼 확실히 완료 처리
            int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
            if (cleared >= 4 && IsTutorialActive)
            {
                CompleteTutorial();
            }
        }
    }

    private void OnOpponentTurnStarted()
    {
        if (!IsTutorialActive) return;

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
        CheckOwnedBuffs();

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
        else if (cleared == 3)
        {
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay)
            {
                if (!hasShownProtectionPotionIntro)
                {
                    StartCoroutine(DelayedShowProtectionPotionIntroRoutine());
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

    private IEnumerator DelayedShowCaptureRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        if (IsTutorialActive && !hasShownCapture)
        {
            ClearBoardMoveMarkers();
            ShowCapturePhase();
        }
    }

    private IEnumerator DelayedShowSealMovementRoutine()
    {
        yield return new WaitForSeconds(0.3f);
        if (IsTutorialActive && !hasShownSealMovement)
        {
            ClearBoardMoveMarkers();
            ShowSealMovementPhase();
        }
    }

    private IEnumerator DelayedShowPromotionAdvance2Routine()
    {
        yield return new WaitForSeconds(0.3f);
        if (IsTutorialActive && !hasShownPromotionAdvance2)
        {
            ClearBoardMoveMarkers();
            ShowPromotionAdvance2Phase();
        }
    }

    private IEnumerator DelayedShowPromotionAdvance3Routine()
    {
        yield return new WaitForSeconds(0.3f);
        if (IsTutorialActive && !hasShownPromotionAdvance3)
        {
            ClearBoardMoveMarkers();
            ShowPromotionAdvance3Phase();
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

    private IEnumerator DelayedShowProtectionPotionIntroRoutine()
    {
        yield return new WaitForSeconds(0.3f);
        if (IsTutorialActive && !hasShownProtectionPotionIntro)
        {
            ClearBoardMoveMarkers();
            ShowProtectionPotionIntroPhase();
        }
    }

    private void Update()
    {
        if (dialogUI == null || (!IsTutorialActive && !isDialogShowing))
        {
            return;
        }

        UpdateDialogShortcuts();
        UpdatePlacementAndSealHighlights();
        UpdateCombatCaptureDetection();
    }

    private void UpdateDialogShortcuts()
    {
        if (!isDialogShowing || !dialogUI.gameObject.activeInHierarchy) return;

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

    private void UpdatePlacementAndSealHighlights()
    {
        if (!IsTutorialActive || GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameStateManager.GameState.Prepare)
        {
            return;
        }

        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared == 0)
        {
            UpdateStage0PlacementHighlights();
        }
        else if (cleared == 1)
        {
            UpdateStage1PlacementAndSealHighlights();
        }
    }

    private void UpdateStage0PlacementHighlights()
    {
        if (isWaitingForQuestion || CurrentPhase == TutorialPhase.IntroQuestion) return;

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

        if (CurrentPhase == TutorialPhase.PlacementKing && IsPieceOnBoard(PieceType.King))
        {
            ClearPieceHighlight();
            ClearTargetGridCellHighlight();
            isKingHighlightAllowed = false;
            if (isDialogShowing) CloseCurrentPhaseDialog();
            ShowPlacementPawnPhase();
            return;
        }

        if (CurrentPhase == TutorialPhase.PlacementPawn && IsPieceOnBoard(PieceType.Pawn))
        {
            ClearPieceHighlight();
            ClearTargetGridCellHighlight();
            isPawnHighlightAllowed = false;
            if (isDialogShowing) CloseCurrentPhaseDialog();
            ShowPlacementCompletePhase();
            return;
        }
    }

    private void UpdateStage1PlacementAndSealHighlights()
    {
        if (!IsPieceOnBoard(PieceType.Pawn) && CurrentPhase != TutorialPhase.PlacementPawnStage2 &&
            !hasShownSealMovement && !hasShownPromotionComplete)
        {
            if (isDialogShowing) CloseCurrentPhaseDialog();
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

            if (IsPieceOnBoard(PieceType.Pawn))
            {
                ClearPieceHighlight();
                ClearTargetGridCellHighlight();
                isPawnHighlightAllowed = false;
                if (isDialogShowing) CloseCurrentPhaseDialog();
                ShowSealIntroPhase();
                return;
            }
        }
        else if (CurrentPhase == TutorialPhase.SealIntro && isSealHighlightAllowed && highlightedSeal == null)
        {
            HighlightSealInInventory(true);
        }
        else if (CurrentPhase == TutorialPhase.SealEquip)
        {
            if (highlightedSeal == null) HighlightSealInInventory(true);
            if (highlightedPiece == null) HighlightBoardPiece(PieceType.Pawn);

            if (IsPieceEquippedWithSeal(PieceType.Pawn))
            {
                ClearSealHighlight();
                ClearPieceHighlight();
                if (isDialogShowing) CloseCurrentPhaseDialog();
                ShowSealEquipCompletePhase();
                return;
            }
        }
        else if (CurrentPhase == TutorialPhase.SealEquipComplete && isStartButtonHighlightAllowed && highlightedStartButton == null)
        {
            HighlightStartButton(true);
        }
    }

    private void UpdateCombatCaptureDetection()
    {
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

