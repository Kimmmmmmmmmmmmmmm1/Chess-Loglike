using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class TutorialManager
{
    // 페이즈 진행 플래그
    private bool hasShownKingPlacement = false;
    private bool hasShownPawnPlacement = false;
    private bool hasShownPlacementComplete = false;
    private bool hasShownMovement = false;
    private bool hasShownCapture = false;
    private bool hasShownReward = false;
    private bool hasShownStageSelect = false;
    private bool hasShownStageSelect3 = false;
    private bool hasShownPlacementPawnStage2 = false;
    private bool hasShownSealIntro = false;
    private bool hasShownSealEquip = false;
    private bool hasShownSealEquipComplete = false;
    private bool hasShownSealMovement = false;
    private bool hasShownPromotionAdvance2 = false;
    private bool hasShownPromotionAdvance3 = false;
    private bool hasShownPromotionChoice = false;
    private bool hasShownPromotionComplete = false;
    private bool hasShownRewardStage2 = false;
    private bool hasShownLightningIntro = false;
    private bool hasShownLightningTarget = false;
    private bool hasShownLightningComplete = false;
    private bool hasShownPassiveArtifact = false;

    public bool IsPromotionComplete => hasShownPromotionComplete;

    // 기물/버튼/인장 배치 설명 단계 도달 후 강조 허용 여부 플래그
    private bool isKingHighlightAllowed = false;
    private bool isPawnHighlightAllowed = false;
    private bool isStartButtonHighlightAllowed = false;
    private bool isSealHighlightAllowed = false;

    /// <summary>
    /// [1. 기물 배치: 킹 배치] 단계를 시작합니다. (킹 강조는 배치 설명 메시지에 도달했을 때 시작됩니다)
    /// </summary>
    public void ShowPlacementKingPhase()
    {
        if (hasShownKingPlacement) return;
        hasShownKingPlacement = true;
        isKingHighlightAllowed = false;

        var msgs = (messagesData.placementKing != null && messagesData.placementKing.Count > 0)
            ? messagesData.placementKing
            : messagesData.placement;

        ShowPhase(TutorialPhase.PlacementKing, msgs);
    }

    /// <summary>
    /// [2. 기물 배치: 폰 배치] 단계를 시작합니다. (폰 강조는 배치 설명 메시지에 도달했을 때 시작됩니다)
    /// </summary>
    public void ShowPlacementPawnPhase()
    {
        if (hasShownPawnPlacement) return;
        hasShownPawnPlacement = true;
        isPawnHighlightAllowed = false;

        var msgs = (messagesData.placementPawn != null && messagesData.placementPawn.Count > 0)
            ? messagesData.placementPawn
            : messagesData.placement;

        ShowPhase(TutorialPhase.PlacementPawn, msgs);
    }

    /// <summary>
    /// [3. 기물 배치: 배치 완료] 단계를 시작합니다. (버튼 강조는 안내 메시지에 도달했을 때 시작됩니다)
    /// </summary>
    public void ShowPlacementCompletePhase()
    {
        if (hasShownPlacementComplete) return;
        hasShownPlacementComplete = true;
        isStartButtonHighlightAllowed = false;

        ShowPhase(TutorialPhase.PlacementComplete, messagesData.placementComplete);
    }

    /// <summary>
    /// [4. 기물 이동 튜토리얼]을 표시합니다.
    /// </summary>
    public void ShowMovementPhase()
    {
        if (hasShownMovement) return;
        hasShownMovement = true;
        ShowPhase(TutorialPhase.Movement, messagesData.movement);
    }

    /// <summary>
    /// [4. 기물 포획 튜토리얼]을 표시합니다.
    /// </summary>
    public void ShowCapturePhase()
    {
        if (hasShownCapture) return;
        hasShownCapture = true;
        ShowPhase(TutorialPhase.Capture, messagesData.capture);
    }

    /// <summary>
    /// [5. 보상 선택 튜토리얼]을 표시합니다.
    /// </summary>
    public void ShowRewardPhase()
    {
        if (hasShownReward) return;
        hasShownReward = true;
        ShowPhase(TutorialPhase.Reward, messagesData.reward);
    }

    /// <summary>
    /// 스테이지 전투 보상을 선택했을 때 호출됩니다.
    /// 보상 대화창을 닫고 스테이지 행선지 선택 튜토리얼을 준비합니다.
    /// </summary>
    public void OnRewardSelected()
    {
        if (!IsTutorialActive) return;

        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        Debug.Log($"[TutorialManager] {cleared}스테이지 보상 선택 완료 -> 스테이지 선택 튜토리얼 대기");
        if (isDialogShowing)
        {
            CloseCurrentPhaseDialog();
        }
    }

    public void OnStage1RewardSelected() => OnRewardSelected();

    /// <summary>
    /// [6. 스테이지 행선지 선택 튜토리얼]을 표시합니다.
    /// </summary>
    public void ShowStageSelectPhase(int nextStage = 2)
    {
        if (nextStage == 3)
        {
            if (hasShownStageSelect3) return;

            // 패시브 유물 튜토리얼 등 다른 안내가 진행 중이면, 해당 튜토리얼이 완료된 직후 순차적으로 표시되도록 대기
            if (isDialogShowing && CurrentPhase == TutorialPhase.PassiveArtifact)
            {
                return;
            }

            hasShownStageSelect3 = true;
            if (messagesData.stageSelectStage3 == null || messagesData.stageSelectStage3.Count == 0)
            {
                Debug.LogWarning("[TutorialManager] stageSelectStage3 메시지가 비어 있어 stageSelect 메시지를 사용합니다.");
                ShowPhase(TutorialPhase.StageSelect, messagesData.stageSelect);
                return;
            }
            ShowPhase(TutorialPhase.StageSelect, messagesData.stageSelectStage3);
        }
        else
        {
            if (hasShownStageSelect) return;

            // 패시브 유물 튜토리얼 등 다른 안내가 진행 중이면, 해당 튜토리얼이 완료된 직후 순차적으로 표시되도록 대기
            if (isDialogShowing && CurrentPhase == TutorialPhase.PassiveArtifact)
            {
                return;
            }

            hasShownStageSelect = true;
            ShowPhase(TutorialPhase.StageSelect, messagesData.stageSelect);
        }
    }

    /// <summary>
    /// 스테이지 행선지 카드(일반 전투)를 선택했을 때 호출됩니다.
    /// </summary>
    public void OnStageChoiceSelected()
    {
        if (!IsTutorialActive) return;

        Debug.Log("[TutorialManager] 스테이지 선택 완료 -> 2스테이지 준비 단계 진입");
        if (isDialogShowing)
        {
            CloseCurrentPhaseDialog();
        }
    }

    /// <summary>
    /// 2스테이지 준비 단계에서 보드 위에 폰이 없을 경우 인벤토리 내 폰 존재를 보장합니다.
    /// </summary>
    public void EnsurePawnInInventory()
    {
        if (IsPieceOnBoard(PieceType.Pawn) || FindInventoryPiece(PieceType.Pawn) != null)
        {
            return;
        }

        InventorySlot[] slots = FindObjectsByType<InventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        InventorySlot targetSlot = null;
        foreach (var slot in slots)
        {
            if (slot != null && !slot.IsReserved && slot.GetComponentInChildren<PieceController>() == null)
            {
                targetSlot = slot;
                break;
            }
        }

        if (targetSlot != null && PieceSpawner.Instance != null && PieceSpawner.Instance.piecePrefab != null)
        {
            GameObject pieceObj = Instantiate(PieceSpawner.Instance.piecePrefab, targetSlot.transform);
            pieceObj.name = "PiecePrefab(Clone)@TutorialPawn";
            PieceController piece = pieceObj.GetComponent<PieceController>();
            if (piece != null)
            {
                piece.Initialize(PieceType.Pawn, false);
                piece.SetLocation(PieceLocation.Inventory);
                if (PieceManager.Instance != null)
                {
                    PieceManager.Instance.RegisterPiece(piece);
                }
            }
        }
    }

    /// <summary>
    /// [2스테이지 진입 시 보드에 폰이 없는 경우: 폰 재배치 유도] 단계를 시작합니다.
    /// </summary>
    public void ShowPlacementPawnStage2Phase()
    {
        if (hasShownPlacementPawnStage2) return;
        hasShownPlacementPawnStage2 = true;
        isPawnHighlightAllowed = false;

        EnsurePawnInInventory();

        ShowPhase(TutorialPhase.PlacementPawnStage2, messagesData.placementPawnStage2);
    }

    /// <summary>
    /// [6. 2스테이지 인장 소개 및 인벤토리 확인] 단계를 시작합니다.
    /// </summary>
    public void ShowSealIntroPhase()
    {
        if (hasShownSealIntro) return;
        hasShownSealIntro = true;
        isSealHighlightAllowed = false;

        ShowPhase(TutorialPhase.SealIntro, messagesData.sealIntro);
    }

    /// <summary>
    /// [7. 인장 장착 유도: 폰에게 인장 장착] 단계를 시작합니다.
    /// </summary>
    public void ShowSealEquipPhase()
    {
        if (hasShownSealEquip) return;
        hasShownSealEquip = true;

        ShowPhase(TutorialPhase.SealEquip, messagesData.sealEquip);
    }

    /// <summary>
    /// [8. 인장 장착 완료 안내 및 전투 시작 유도] 단계를 시작합니다.
    /// </summary>
    public void ShowSealEquipCompletePhase()
    {
        if (hasShownSealEquipComplete) return;
        hasShownSealEquipComplete = true;
        isStartButtonHighlightAllowed = false;

        ShowPhase(TutorialPhase.SealEquipComplete, messagesData.sealEquipComplete);
    }

    /// <summary>
    /// [9. 2스테이지 전투 시작 후 폰 대각선 이동 확인] 단계를 시작합니다.
    /// </summary>
    public void ShowSealMovementPhase()
    {
        if (hasShownSealMovement) return;
        hasShownSealMovement = true;

        ShowPhase(TutorialPhase.SealMovement, messagesData.sealMovement);
    }

    /// <summary>
    /// [10. 폰 승급 2단계: 2번째 전진 유도] 단계를 시작합니다.
    /// </summary>
    public void ShowPromotionAdvance2Phase()
    {
        if (hasShownPromotionAdvance2) return;
        hasShownPromotionAdvance2 = true;

        ShowPhase(TutorialPhase.PromotionAdvance2, messagesData.promotionAdvance2);
    }

    /// <summary>
    /// [11. 폰 승급 3단계: 프로모션 라인 도달 유도] 단계를 시작합니다.
    /// </summary>
    public void ShowPromotionAdvance3Phase()
    {
        if (hasShownPromotionAdvance3) return;
        hasShownPromotionAdvance3 = true;

        ShowPhase(TutorialPhase.PromotionAdvance3, messagesData.promotionAdvance3);
    }

    /// <summary>
    /// [12. 폰 승급 선택: 퀸 선택 유도] 단계를 시작합니다.
    /// </summary>
    public void ShowPromotionChoicePhase()
    {
        if (hasShownPromotionChoice) return;
        hasShownPromotionChoice = true;

        ClearPieceHighlight();
        ClearTargetGridCellHighlight();

        ShowPhase(TutorialPhase.PromotionChoice, messagesData.promotionChoice);
    }

    /// <summary>
    /// [13. 폰 승급 완료 안내 및 최종 전투 승리 독려] 단계를 시작합니다.
    /// </summary>
    public void ShowPromotionCompletePhase()
    {
        if (hasShownPromotionComplete) return;
        hasShownPromotionComplete = true;

        HighlightQueenButton(false);
        ClearPieceHighlight();
        ClearTargetGridCellHighlight();

        ShowPhase(TutorialPhase.PromotionComplete, messagesData.promotionComplete);
    }

    /// <summary>
    /// [14. 2스테이지 클리어 보상: 낙뢰의 부적 선택 유도] 단계를 시작합니다.
    /// </summary>
    public void ShowRewardStage2Phase()
    {
        if (hasShownRewardStage2) return;
        hasShownRewardStage2 = true;

        ShowPhase(TutorialPhase.RewardStage2, messagesData.rewardStage2);
    }

    /// <summary>
    /// [15. 3스테이지 진입 후 낙뢰의 부적 사용 유도] 단계를 시작합니다.
    /// </summary>
    public void ShowLightningArtifactIntroPhase()
    {
        if (hasShownLightningIntro) return;
        hasShownLightningIntro = true;

        HighlightArtifactSlot("AC02");

        ShowPhase(TutorialPhase.LightningArtifactIntro, messagesData.lightningArtifactIntro);
    }

    /// <summary>
    /// [16. 낙뢰의 부적 타겟팅 단계: 적 기물 클릭 유도] 단계를 시작합니다.
    /// </summary>
    public void ShowLightningArtifactTargetPhase()
    {
        if (hasShownLightningTarget) return;
        hasShownLightningTarget = true;

        ClearArtifactSlotHighlight();

        ShowPhase(TutorialPhase.LightningArtifactTarget, messagesData.lightningArtifactTarget);
    }

    /// <summary>
    /// [17. 낙뢰의 부적 발동 완료 및 튜토리얼 최종 완료 안내] 단계를 시작합니다.
    /// </summary>
    public void ShowLightningArtifactCompletePhase()
    {
        if (hasShownLightningComplete) return;
        hasShownLightningComplete = true;

        ClearArtifactSlotHighlight();
        ClearPieceHighlight();

        ShowPhase(TutorialPhase.LightningArtifactComplete, messagesData.lightningArtifactComplete);
    }

    /// <summary>
    /// 패시브 유물 최초 획득 시 설명 튜토리얼을 표시합니다.
    /// </summary>
    public void ShowPassiveArtifactPhase(ArtifactData artifact)
    {
        hasShownPassiveArtifact = true;
        PlayerPrefs.SetInt(PrefsKeyPassiveArtifactShown, 1);
        PlayerPrefs.Save();

        HighlightArtifactSlot(artifact);

        ShowPhase(TutorialPhase.PassiveArtifact, messagesData.passiveArtifact);
    }

    /// <summary>
    /// 인장이 기물에 성공적으로 장착되었을 때 호출되는 콜백입니다.
    /// </summary>
    public void NotifySealEquipped(PieceController piece, SealData seal)
    {
        if (!IsTutorialActive) return;
        if (CurrentPhase == TutorialPhase.SealEquip && piece != null && piece.Type == PieceType.Pawn)
        {
            ClearSealHighlight();
            ClearPieceHighlight();
            if (isDialogShowing)
            {
                CloseCurrentPhaseDialog();
            }
            ShowSealEquipCompletePhase();
        }
    }

    /// <summary>
    /// 승급 패널이 열렸을 때 PiecePromotionManager에서 호출됩니다.
    /// </summary>
    public void NotifyPromotionPanelOpened()
    {
        if (!IsTutorialActive) return;
        Debug.Log("[TutorialManager] 프로모션 패널 열림 감지 -> 승급 선택 튜토리얼 개시");
        ShowPromotionChoicePhase();
    }

    /// <summary>
    /// 플레이어가 퀸 승급을 선택했을 때 호출됩니다.
    /// </summary>
    public void NotifyPromotionChoiceMade(PieceType promotedType)
    {
        if (!IsTutorialActive) return;
        HighlightQueenButton(false);
        if (isDialogShowing)
        {
            CloseCurrentPhaseDialog();
        }
    }

    /// <summary>
    /// 퀸 승급 연출이 완료되었을 때 호출됩니다.
    /// </summary>
    public void NotifyPromotionEffectCompleted()
    {
        if (!IsTutorialActive) return;
        StartCoroutine(DelayedShowPromotionCompleteRoutine());
    }

    /// <summary>
    /// 세이브 파일에 저장할 튜토리얼 진행 상태 데이터를 내보냅니다.
    /// </summary>
    public SavedTutorialData ExportTutorialSaveData()
    {
        return new SavedTutorialData
        {
            isTutorialActive = IsTutorialActive,
            currentPhase = (int)CurrentPhase,
            isWaitingForQuestion = isWaitingForQuestion,
            hasShownKingPlacement = hasShownKingPlacement,
            hasShownPawnPlacement = hasShownPawnPlacement,
            hasShownPlacementComplete = hasShownPlacementComplete,
            hasShownMovement = hasShownMovement,
            hasShownCapture = hasShownCapture,
            hasShownReward = hasShownReward,
            hasShownStageSelect = hasShownStageSelect,
            hasShownStageSelect3 = hasShownStageSelect3,
            hasShownPlacementPawnStage2 = hasShownPlacementPawnStage2,
            hasShownSealIntro = hasShownSealIntro,
            hasShownSealEquip = hasShownSealEquip,
            hasShownSealEquipComplete = hasShownSealEquipComplete,
            hasShownSealMovement = hasShownSealMovement,
            hasShownPromotionAdvance2 = hasShownPromotionAdvance2,
            hasShownPromotionAdvance3 = hasShownPromotionAdvance3,
            hasShownPromotionChoice = hasShownPromotionChoice,
            hasShownPromotionComplete = hasShownPromotionComplete,
            hasShownRewardStage2 = hasShownRewardStage2,
            hasShownLightningIntro = hasShownLightningIntro,
            hasShownLightningTarget = hasShownLightningTarget,
            hasShownLightningComplete = hasShownLightningComplete,
            hasShownPassiveArtifact = hasShownPassiveArtifact
        };
    }

    /// <summary>
    /// 세이브 데이터로부터 튜토리얼 상태를 사전에 활성화하여 스테이지 로드 및 기물 배치가 튜토리얼 모드로 동작하도록 준비합니다.
    /// </summary>
    public void PrepareTutorialRestore(SavedTutorialData data)
    {
        if (data == null || !data.isTutorialActive) return;

        IsTutorialActive = true;
        CurrentPhase = (TutorialPhase)data.currentPhase;
        isWaitingForQuestion = data.isWaitingForQuestion;
        hasShownKingPlacement = data.hasShownKingPlacement;
        hasShownPawnPlacement = data.hasShownPawnPlacement;
        hasShownPlacementComplete = data.hasShownPlacementComplete;
        hasShownMovement = data.hasShownMovement;
        hasShownCapture = data.hasShownCapture;
        hasShownReward = data.hasShownReward;
        hasShownStageSelect = data.hasShownStageSelect;
        hasShownStageSelect3 = data.hasShownStageSelect3;
        hasShownPlacementPawnStage2 = data.hasShownPlacementPawnStage2;
        hasShownSealIntro = data.hasShownSealIntro;
        hasShownSealEquip = data.hasShownSealEquip;
        hasShownSealEquipComplete = data.hasShownSealEquipComplete;
        hasShownSealMovement = data.hasShownSealMovement;
        hasShownPromotionAdvance2 = data.hasShownPromotionAdvance2;
        hasShownPromotionAdvance3 = data.hasShownPromotionAdvance3;
        hasShownPromotionChoice = data.hasShownPromotionChoice;
        hasShownPromotionComplete = data.hasShownPromotionComplete;
        hasShownRewardStage2 = data.hasShownRewardStage2;
        hasShownLightningIntro = data.hasShownLightningIntro;
        hasShownLightningTarget = data.hasShownLightningTarget;
        hasShownLightningComplete = data.hasShownLightningComplete;
        hasShownPassiveArtifact = data.hasShownPassiveArtifact;

        EnsureDialogUI();
        SubscribeEvents();
    }

    /// <summary>
    /// 세이브 런 복원이 완료된 직후 현재 보드 상태 및 진행 단계에 맞춰 튜토리얼 안내와 하이라이트를 이어서 재개합니다.
    /// </summary>
    public void ResumeTutorialAfterRestore(SavedTutorialData data, StageSaveData stageSave)
    {
        if (data == null || !data.isTutorialActive) return;

        IsTutorialActive = true;
        EnsureDialogUI();
        SubscribeEvents();

        StartCoroutine(DelayedResumeTutorialRoutine(data, stageSave));
    }

    private IEnumerator DelayedResumeTutorialRoutine(SavedTutorialData data, StageSaveData stageSave)
    {
        yield return null;

        // 프레젠테이션(전투 준비 등) 연출이 재생 중이라면 완료될 때까지 대기
        while (PresentationManager.Instance != null && PresentationManager.Instance.IsPresenting)
        {
            yield return null;
        }

        yield return null;

        ExecuteResumeTutorial(data, stageSave);
    }

    private void ExecuteResumeTutorial(SavedTutorialData data, StageSaveData stageSave)
    {
        if (!IsTutorialActive) return;

        // 1. 첫 진입 질문 대기 상태 복원
        if (data.isWaitingForQuestion || (TutorialPhase)data.currentPhase == TutorialPhase.IntroQuestion)
        {
            ShowQuestionStep();
            return;
        }

        isWaitingForQuestion = false;

        // 2. 보상 획득 상태 복원
        if (stageSave != null && stageSave.pendingReward)
        {
            if (stageSave.clearedStage == 0)
            {
                hasShownReward = false;
                ShowRewardPhase();
            }
            else if (stageSave.clearedStage == 1)
            {
                hasShownRewardStage2 = false;
                ShowRewardStage2Phase();
            }
            return;
        }

        // 3. 행선지 선택 상태 복원
        if (stageSave != null && stageSave.currentFlowState == (int)GameFlowState.StageSelect)
        {
            int nextStage = stageSave.clearedStage + 1;
            if (nextStage == 3)
            {
                hasShownStageSelect3 = false;
            }
            else
            {
                hasShownStageSelect = false;
            }
            ShowStageSelectPhase(nextStage);
            return;
        }

        // 4. 전투 준비(Prepare) 및 전투 진행(GamePlay) 상태 복원
        int cleared = stageSave != null ? stageSave.clearedStage : 0;
        bool isPrepare = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare;
        bool isGamePlay = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay;

        if (cleared == 0)
        {
            if (isPrepare)
            {
                if (!IsPieceOnBoard(PieceType.King))
                {
                    hasShownKingPlacement = false;
                    ShowPlacementKingPhase();
                }
                else if (!IsPieceOnBoard(PieceType.Pawn))
                {
                    hasShownKingPlacement = true;
                    hasShownPawnPlacement = false;
                    ShowPlacementPawnPhase();
                }
                else
                {
                    hasShownKingPlacement = true;
                    hasShownPawnPlacement = true;
                    hasShownPlacementComplete = false;
                    ShowPlacementCompletePhase();
                }
            }
            else if (isGamePlay)
            {
                if (!hasShownMovement)
                {
                    StartCoroutine(DelayedShowMovementRoutine());
                }
                else if (!hasShownCapture && CanAnyPlayerPieceCapture())
                {
                    StartCoroutine(DelayedShowCaptureRoutine());
                }
            }
        }
        else if (cleared == 1)
        {
            if (isPrepare)
            {
                if (!IsPieceOnBoard(PieceType.Pawn))
                {
                    hasShownPlacementPawnStage2 = false;
                    ShowPlacementPawnStage2Phase();
                }
                else if (!IsPieceEquippedWithSeal(PieceType.Pawn))
                {
                    hasShownPlacementPawnStage2 = true;
                    if (!hasShownSealIntro)
                    {
                        ShowSealIntroPhase();
                    }
                    else
                    {
                        hasShownSealEquip = false;
                        ShowSealEquipPhase();
                    }
                }
                else
                {
                    hasShownPlacementPawnStage2 = true;
                    hasShownSealIntro = true;
                    hasShownSealEquip = true;
                    hasShownSealEquipComplete = false;
                    ShowSealEquipCompletePhase();
                }
            }
            else if (isGamePlay)
            {
                if (hasShownPromotionComplete)
                {
                    ShowPromotionCompletePhase();
                }
                else
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
        }
        else if (cleared == 2)
        {
            if (isGamePlay)
            {
                if (!hasShownLightningComplete)
                {
                    if (hasShownLightningTarget)
                    {
                        ShowLightningArtifactTargetPhase();
                    }
                    else
                    {
                        hasShownLightningIntro = false;
                        StartCoroutine(DelayedShowLightningIntroRoutine());
                    }
                }
            }
        }
    }
}

