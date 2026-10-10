using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 체스 타일(GridCell)을 파괴하는 보스 스킬
/// 상대 턴에 무작위 GridCell을 선택하여 흔들림(경고)을 표시
/// 다음 턴이 되면 해당 GridCell을 파괴하고 위에 있던 기물도 함께 제거
/// </summary>
public class GridDestructionSkill : BaseSkill
{
    [SerializeField] private float shakeStrength = 5f;
    [SerializeField] private float shakeDuration = 0.5f;

    [Tooltip("현재 흔들리고 있는 GridCell")]
    private GridCell currentAffectedCell;
    
    private bool isShaking = false;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(skillName))
            skillName = "Grid Destruction";
    }

    /// <summary>
    /// 스킬을 실행합니다. 상대 턴마다 호출됩니다.
    /// 홀수 턴: GridCell 선택 및 흔들기
    /// 짝수 턴: GridCell 파괴
    /// </summary>
    public override void ExecuteSkill()
    {
        // 게임 상태 확인
        if (!IsValidGameState())
        {
            return;
        }

        if (executionContext == null)
        {
            return;
        }

        if (!isShaking)
        {
            // 첫 번째 상대 턴: GridCell 선택 및 흔들기 시작
            SelectAndShakeRandomCell();
            isShaking = true;
        }
        else
        {
            // 두 번째 상대 턴: GridCell 파괴
            DestroyAffectedCell();
            isShaking = false;
        }
    }

    /// <summary>
    /// 게임이 유효한 상태인지 확인합니다.
    /// </summary>
    private bool IsValidGameState()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
        {
            return false;
        }

        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 무작위 GridCell을 선택하고 흔들기를 시작합니다.
    /// </summary>
    private void SelectAndShakeRandomCell()
    {
        if (currentAffectedCell != null)
        {
            currentAffectedCell.StopContinuousShake();
        }

        GridManager gridManager = executionContext.gridManager;
        if (gridManager == null)
        {
            return;
        }

        var allCells = gridManager.GetAllGridCells();
        List<GridCell> validCells = new List<GridCell>();
        for (int i = 0; i < allCells.Length; i++)
        {
            if (allCells[i] != null && !allCells[i].isDestroyed)
            {
                validCells.Add(allCells[i]);
            }
        }
        
        if (validCells.Count == 0)
        {
            return;
        }

        currentAffectedCell = validCells[Random.Range(0, validCells.Count)];
        currentAffectedCell.StartContinuousShake(shakeDuration, shakeStrength);
    }

    /// <summary>
    /// 현재 영향받는 GridCell을 파괴합니다.
    /// </summary>
    private void DestroyAffectedCell()
    {
        if (currentAffectedCell == null)
        {
            return;
        }

        currentAffectedCell.StopContinuousShake();
        currentAffectedCell.DestroyCell();
        currentAffectedCell = null;
    }

    /// <summary>
    /// 특정 위치의 GridCell에 흔들 효과를 적용합니다.
    /// </summary>
    public override void ApplyEffect(Vector2Int gridPosition)
    {
        GridManager gridManager = executionContext.gridManager;
        if (gridManager == null) return;

        GridCell cell = gridManager.GetGridCell(gridPosition);
        if (cell != null && !cell.isDestroyed)
        {
            cell.StartContinuousShake(shakeDuration, shakeStrength);
        }
    }

    /// <summary>
    /// 범위 내 GridCell들에 흔들 효과를 적용합니다.
    /// </summary>
    public override void ApplyEffectInRange(Vector2Int centerPosition, int radius)
    {
        GridManager gridManager = executionContext.gridManager;
        if (gridManager == null) return;

        var allCells = gridManager.GetAllGridCells();
        foreach (var cell in allCells)
        {
            if (cell != null && !cell.isDestroyed && Vector2Int.Distance(cell.gridPosition, centerPosition) <= radius)
            {
                cell.StartContinuousShake(shakeDuration, shakeStrength);
            }
        }
    }

    /// <summary>
    /// 여러 위치의 GridCell에 흔들 효과를 적용합니다.
    /// </summary>
    public override void ApplyEffectToMultiplePositions(List<Vector2Int> positions)
    {
        GridManager gridManager = executionContext.gridManager;
        if (gridManager == null) return;

        foreach (var pos in positions)
        {
            GridCell cell = gridManager.GetGridCell(pos);
            if (cell != null && !cell.isDestroyed)
            {
                cell.StartContinuousShake(shakeDuration, shakeStrength);
            }
        }
    }

    /// <summary>
    /// 스킬의 현재 상태를 반환합니다.
    /// </summary>
    public GridCell GetCurrentAffectedCell() => currentAffectedCell;
}
