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
                MapManager mapManager = MapManager.Instance;
                if (mapManager != null && mapManager.CurrentNode != null && mapManager.CurrentNode.type != NodeType.Boss)
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

    private void OnGameFlowStateChanged(GameFlowState newState)
    {
        // Battle 상태로 진입할 때 스테이지 설정 (보스전이 아닐 때만)
        if (newState == GameFlowState.Battle)
        {
            // 현재 노드가 보스인지 확인
            MapManager mapManager = MapManager.Instance;
            if (mapManager != null && mapManager.CurrentNode != null && mapManager.CurrentNode.type == NodeType.Boss)
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
        int minDifficulty = clearedStage / 5;
        int maxDifficulty = minDifficulty + 1;

        // 조건: minDifficulty 이상 maxDifficulty 이하의 난이도를 가진 StageData 필터링
        var candidates = allStageData.Where(data => data.difficulty >= minDifficulty && data.difficulty <= maxDifficulty).ToList();

        // 조건에 맞는 스테이지가 없으면, 가장 높은 난이도의 스테이지들을 후보로 사용 (Fallback)
        if (candidates.Count == 0 && allStageData.Count > 0)
        {
            int maxDiff = allStageData.Max(d => d.difficulty);
            candidates = allStageData.Where(data => data.difficulty == maxDiff).ToList();
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
