using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지별 진행 상태 및 런 복원 데이터를 담는 직렬화 클래스입니다.
/// </summary>
[Serializable]
public class StageSaveData
{
    public int saveVersion = 1;
    public string saveTimestamp;
    public int clearedStage;
    public int stageNumber;
    public string stageDisplayName;
    public int coin;
    public int enemyPiecesCaptured;
    public int playerPiecesCaptured;
    public int clearedBosses;
    public int gameMode;
    public bool pendingReward;
    public int currentFlowState = (int)GameFlowState.Battle;
    public int currentBattleType = (int)BattleType.Normal;

    // 보드 및 그리드 데이터
    public int boardWidth = 6;
    public int boardHeight = 5;
    public int gridMinX = -3;
    public int gridMinY = -2;

    public List<SavedPieceData> pieces = new List<SavedPieceData>();
    public List<SavedSealData> unassignedSeals = new List<SavedSealData>();
    public List<SavedArtifactData> artifacts = new List<SavedArtifactData>();

    // 고정된 선택지 데이터
    public List<int> pendingStageNodeTypes = new List<int>(); // StageNodeType int values
    public List<SavedRewardCardData> pendingRewardCards = new List<SavedRewardCardData>();

    // 튜토리얼 진행 데이터
    public SavedTutorialData tutorialData;
}

[Serializable]
public class SavedTutorialData
{
    public bool isTutorialActive;
    public int currentPhase;
    public bool isWaitingForQuestion;
    public bool hasShownKingPlacement;
    public bool hasShownPawnPlacement;
    public bool hasShownPlacementComplete;
    public bool hasShownMovement;
    public bool hasShownCapture;
    public bool hasShownReward;
    public bool hasShownStageSelect;
    public bool hasShownStageSelect3;
    public bool hasShownPlacementPawnStage2;
    public bool hasShownSealIntro;
    public bool hasShownSealEquip;
    public bool hasShownSealEquipComplete;
    public bool hasShownSealMovement;
    public bool hasShownPromotionAdvance2;
    public bool hasShownPromotionAdvance3;
    public bool hasShownPromotionChoice;
    public bool hasShownPromotionComplete;
    public bool hasShownRewardStage2;
    public bool hasShownLightningIntro;
    public bool hasShownLightningTarget;
    public bool hasShownLightningComplete;
    public bool hasShownRewardStage3;
    public bool hasShownStageSelect4;
    public bool hasShownProtectionPotionIntro;
    public bool hasShownProtectionPotionTarget;
    public bool hasShownProtectionPotionComplete;
    public bool hasShownPassiveArtifact;
    public bool hasShownBuffStatus;
}

[Serializable]
public class SavedRewardCardData
{
    public int rewardType; // RewardButton.RewardType int value
    public int coinAmount;
    public PieceType pieceType;
    public string artifactId;
    public string attachedSealId;
}

[Serializable]
public class SavedPieceData
{
    public PieceType pieceType;
    public string location; // "Board" or "Inventory"
    public int gridX;
    public int gridY;
    public int inventorySlotIndex;
    public List<string> equippedSealIds = new List<string>();
}

[Serializable]
public class SavedSealData
{
    public int slotIndex;
    public string sealId;
}

[Serializable]
public class SavedArtifactData
{
    public string id;
    public int level;
    public int stackCount;
}

