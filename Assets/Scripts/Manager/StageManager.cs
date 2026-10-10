using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StageManager : MonoBehaviour
{
    private List<StageData> allStageData;

    private void Start()
    {
        allStageData = Resources.LoadAll<StageData>("Stages").ToList();

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

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= OnGameFlowStateChanged;
        }
    }

    private bool IsBossStage()
    {
        // 선형 진행 구조: 5스테이지마다(스테이지 5, 10, 15... 즉 clearedStage 4, 9, 14...) 보스전
        if (GameManager.Instance != null && (GameManager.Instance.ClearedStage + 1) % 5 == 0)
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
        if (allStageData == null || allStageData.Count == 0) return;

        int clearedStage = GameManager.Instance.ClearedStage;
        int act = clearedStage / 5;
        int subStage = clearedStage % 5; // 0, 1, 2, 3 (4번째는 보스)

        // 0: 쉬움, 1: 보통, 2: 어려움, 3: 매우어려움
        // Act 1: 1~2스테이지 쉬움(0), 3~4스테이지 보통(1)
        // Act 2: 1~2스테이지 보통(1), 3~4스테이지 어려움(2)
        // Act 3: 1~2스테이지 어려움(2), 3~4스테이지 매우어려움(3)
        // Act 4+: 매우어려움(3)
        int targetDifficulty = Mathf.Clamp(act + (subStage >= 2 ? 1 : 0), 0, 3);

        var candidates = allStageData.Where(data => data.difficulty == targetDifficulty).ToList();
        if (candidates.Count == 0)
        {
            int minDiff = Mathf.Max(0, targetDifficulty - 1);
            int maxDiff = Mathf.Min(3, targetDifficulty + 1);
            candidates = allStageData.Where(data => data.difficulty >= minDiff && data.difficulty <= maxDiff).ToList();
        }

        if (candidates.Count == 0 && allStageData.Count > 0)
        {
            candidates = allStageData;
        }

        if (candidates.Count > 0)
        {
            // 랜덤으로 하나 선택
            StageData selectedStage = candidates[Random.Range(0, candidates.Count)];
            
            // PieceSpawner에 적 기물 정보 전달
            if (PieceSpawner.Instance != null)
            {
                if (selectedStage.enemyPieces != null)
                {
                    // 리스트를 새로 생성하여 전달 (참조로 인한 원본 데이터 수정 방지)
                    PieceSpawner.Instance.enemyPieces = new List<PieceSpawner.PieceSpawnInfo>(selectedStage.enemyPieces);
                }
                else
                {
                    PieceSpawner.Instance.enemyPieces = new List<PieceSpawner.PieceSpawnInfo>();
                }
            }
            else
            {
            }
        }
        else
        {
        }
    }
}
