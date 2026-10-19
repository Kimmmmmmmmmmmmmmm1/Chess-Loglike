using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StageManager : MonoBehaviour
{
    private List<StageData> allNormalStageData;
    private List<StageData> allEliteStageData;

    private void Start()
    {
        LoadAllStages();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged += OnGameFlowStateChanged;
            
            // 이미 Battle 상태라면 초기화 진행 (보스전이 아닐 때)
            if (GameManager.Instance.CurrentFlowState == GameFlowState.Battle)
            {
                if (!IsBossStage())
                {
                    SetupStage();
                }
            }
        }
    }

    private void LoadAllStages()
    {
        var loadedStages = Resources.LoadAll<StageData>("Stages").ToList();
        allEliteStageData = loadedStages.Where(s => s != null && (s.isElite || s.name.Contains("Elite"))).ToList();
        allNormalStageData = loadedStages.Where(s => s != null && !s.isElite && !s.name.Contains("Elite")).ToList();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= OnGameFlowStateChanged;
        }
    }

    private bool IsBossStage()
    {
        // 선형 진행 구조: 10스테이지마다 (스테이지 10, 20... 즉 clearedStage 9, 19...) 보스전
        if (GameManager.Instance != null && (GameManager.Instance.ClearedStage + 1) % 10 == 0)
        {
            return true;
        }

        return false;
    }

    private void OnGameFlowStateChanged(GameFlowState newState)
    {
        // Battle 상태로 진입할 때 스테이지 설정 (보스전이 아닐 때만)
        if (newState == GameFlowState.Battle)
        {
            if (IsBossStage())
            {
                return;
            }

            SetupStage();
        }
    }

    private void SetupStage()
    {
        if (allNormalStageData == null || allEliteStageData == null)
        {
            LoadAllStages();
        }

        int clearedStage = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        int act = clearedStage / 10;
        int subStage = clearedStage % 10; // 0~9

        bool isElite = GameManager.Instance != null && GameManager.Instance.CurrentBattleType == BattleType.Elite;
        StageData selectedStage = null;

        if (isElite)
        {
            if (allEliteStageData != null && allEliteStageData.Count > 0)
            {
                // 엘리트 스테이지: 액트 및 서브스테이지에 따라 난이도(1~3) 가중치 매칭
                int targetEliteDiff = Mathf.Clamp(act + (subStage >= 5 ? 2 : 1), 1, 3);
                var candidates = allEliteStageData.Where(d => d.difficulty == targetEliteDiff).ToList();
                if (candidates.Count == 0)
                {
                    candidates = allEliteStageData;
                }
                selectedStage = candidates[Random.Range(0, candidates.Count)];
            }
        }
        else
        {
            if (allNormalStageData != null && allNormalStageData.Count > 0)
            {
                // 10스테이지 주기 일반 전투 난이도 배분 (0: 쉬움, 1: 보통, 2: 어려움, 3: 매우어려움)
                int step = subStage <= 2 ? 0 : (subStage <= 5 ? 1 : 2);
                int targetDifficulty = Mathf.Clamp(act + step, 0, 3);

                var candidates = allNormalStageData.Where(data => data.difficulty == targetDifficulty).ToList();
                if (candidates.Count == 0)
                {
                    int minDiff = Mathf.Max(0, targetDifficulty - 1);
                    int maxDiff = Mathf.Min(3, targetDifficulty + 1);
                    candidates = allNormalStageData.Where(data => data.difficulty >= minDiff && data.difficulty <= maxDiff).ToList();
                }

                if (candidates.Count == 0 && allNormalStageData.Count > 0)
                {
                    candidates = allNormalStageData;
                }

                if (candidates.Count > 0)
                {
                    selectedStage = candidates[Random.Range(0, candidates.Count)];
                }
            }
        }

        if (selectedStage != null)
        {
            // PieceSpawner에 적 기물 정보 전달 (엘리트 전용 스테이지 파일에 설계된 기물과 인장 그대로 적용)
            if (PieceSpawner.Instance != null)
            {
                if (selectedStage.enemyPieces != null)
                {
                    var enemyList = new List<PieceSpawner.PieceSpawnInfo>();
                    foreach (var original in selectedStage.enemyPieces)
                    {
                        var copy = original;
                        copy.seals = original.seals != null ? new List<SealData>(original.seals) : new List<SealData>();
                        enemyList.Add(copy);
                    }

                    PieceSpawner.Instance.enemyPieces = enemyList;
                }
                else
                {
                    PieceSpawner.Instance.enemyPieces = new List<PieceSpawner.PieceSpawnInfo>();
                }
            }
            else
            {
            }

            GridManager gridManager = PieceManager.Instance != null
                ? PieceManager.Instance.gridManager
                : FindFirstObjectByType<GridManager>();
            if (gridManager == null)
            {
                gridManager = FindFirstObjectByType<GridManager>();
            }

            if (gridManager != null)
            {
                gridManager.SetBoardSize(
                    selectedStage.boardSize,
                    new Vector2Int(selectedStage.minX, selectedStage.minY));
            }

            PieceManager.Instance?.SetHideEnemyTerritoryDuringPrepare(
                selectedStage.hideEnemyTerritoryDuringPrepare);
        }
        else
        {
        }
    }
}
