using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 그리드 포인트를 파괴하는 스킬
/// 상대 턴에 무작위 GridPoint를 선택하여 흔들린다고 표시
/// 플레이어 턴이 되면 해당 GridPoint를 이루는 GridLine들을 무너뜨림
/// </summary>
public class GridDestructionSkill : BaseSkill
{
    [SerializeField] private float shakeStrength = 5f;
    [SerializeField] private float shakeDuration = 0.5f;

    [Tooltip("현재 흔들리고 있는 GridPoint")]
    private GridPoint currentAffectedPoint;
    
    private bool isShaking = false;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(skillName))
            skillName = "Grid Destruction";
    }

    /// <summary>
    /// 스킬을 실행합니다. 상대 턴마다 호출됩니다.
    /// 홀수 턴: GridPoint 선택 및 흔들기
    /// 짝수 턴: GridLine 무너짐
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
            // 첫 번째 상대 턴: GridPoint 선택 및 흔들기 시작
            SelectAndShakeRandomPoint();
            isShaking = true;
        }
        else
        {
            // 두 번째 상대 턴: GridLine 무너짐
            DestroyAffectedPoint();
            isShaking = false;
        }
    }

    /// <summary>
    /// 게임이 유효한 상태인지 확인합니다.
    /// </summary>
    private bool IsValidGameState()
    {
        // Battle 상태가 아니면 실행 불가
        if (GameManager.Instance == null || GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
        {
            return false;
        }

        // GamePlay 상태가 아니면 실행 불가
        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 무작위 GridPoint를 선택하고 흔들기를 시작합니다.
    /// </summary>
    private void SelectAndShakeRandomPoint()
    {
        // 이전 GridPoint의 흔들기 중지
        if (currentAffectedPoint != null)
        {
            currentAffectedPoint.StopContinuousShake();
        }

        GridManager gridManager = executionContext.gridManager;
        if (gridManager == null)
        {
            return;
        }

        // 모든 GridPoint 조회
        var allGridPoints = gridManager.GetAllGridPoints();
        
        if (allGridPoints.Length == 0)
        {
            return;
        }

        // 무작위 GridPoint 선택
        currentAffectedPoint = allGridPoints[Random.Range(0, allGridPoints.Length)];
        currentAffectedPoint.StartContinuousShake(shakeDuration, shakeStrength);

        // 이 GridPoint와 관련된 Piece들에 시각적 피드백 제공 (선택적)
        if (PieceManager.Instance != null)
        {
            var affectedPieces = PieceManager.Instance.GetPiecesAtGridPoint(currentAffectedPoint.gridPosition);
        }
    }

    /// <summary>
    /// 현재 영향받는 GridPoint의 라인들을 파괴합니다.
    /// 라인 파괴 후 연결이 끊긴 다른 GridPoint들도 함께 제거합니다.
    /// </summary>
    private void DestroyAffectedPoint()
    {
        if (currentAffectedPoint == null)
        {
            return;
        }

        GridManager gridManager = executionContext.gridManager;
        if (gridManager == null)
        {
            return;
        }

        // 무너지는 GridPoint 위의 기물 제거
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.RemovePieceAtPositionByCollapse(currentAffectedPoint.gridPosition);
        }

        // 파괴될 라인들과 연결된 주변 GridPoint들 수집
        HashSet<GridPoint> affectedPoints = CollectAffectedGridPoints(currentAffectedPoint);
        
        // 라인 파괴
        currentAffectedPoint.DestroyIntersectingLines();
        
        // 주변 GridPoint들 중 모든 라인이 파괴된 것들 제거
        CheckAndDestroyIsolatedPoints(affectedPoints, gridManager);
        
        currentAffectedPoint = null;
    }

    /// <summary>
    /// 파괴될 라인들과 연결된 주변 GridPoint들을 수집합니다.
    /// </summary>
    private HashSet<GridPoint> CollectAffectedGridPoints(GridPoint destroyedPoint)
    {
        HashSet<GridPoint> points = new HashSet<GridPoint>();
        GridManager gridManager = executionContext.gridManager;
        
        if (gridManager == null) return points;

        // 파괴될 GridPoint의 라인들과 연결된 다른 GridPoint들을 찾음
        // 위쪽 라인의 반대편 GridPoint (y+1)
        if (destroyedPoint.lineTop != null && destroyedPoint.lineTop.gameObject.activeInHierarchy)
        {
            GridPoint neighbor = gridManager.GetGridPoint(destroyedPoint.gridPosition + Vector2Int.up);
            if (neighbor != null && !neighbor.isDestroyed) points.Add(neighbor);
        }
        
        // 아래쪽 라인의 반대편 GridPoint (y-1)
        if (destroyedPoint.lineBottom != null && destroyedPoint.lineBottom.gameObject.activeInHierarchy)
        {
            GridPoint neighbor = gridManager.GetGridPoint(destroyedPoint.gridPosition + Vector2Int.down);
            if (neighbor != null && !neighbor.isDestroyed) points.Add(neighbor);
        }
        
        // 왼쪽 라인의 반대편 GridPoint (x-1)
        if (destroyedPoint.lineLeft != null && destroyedPoint.lineLeft.gameObject.activeInHierarchy)
        {
            GridPoint neighbor = gridManager.GetGridPoint(destroyedPoint.gridPosition + Vector2Int.left);
            if (neighbor != null && !neighbor.isDestroyed) points.Add(neighbor);
        }
        
        // 오른쪽 라인의 반대편 GridPoint (x+1)
        if (destroyedPoint.lineRight != null && destroyedPoint.lineRight.gameObject.activeInHierarchy)
        {
            GridPoint neighbor = gridManager.GetGridPoint(destroyedPoint.gridPosition + Vector2Int.right);
            if (neighbor != null && !neighbor.isDestroyed) points.Add(neighbor);
        }

        return points;
    }

    /// <summary>
    /// 영향받은 GridPoint들 중 모든 라인이 파괴된 고립된 포인트들을 제거합니다.
    /// </summary>
    private void CheckAndDestroyIsolatedPoints(HashSet<GridPoint> affectedPoints, GridManager gridManager)
    {
        foreach (GridPoint point in affectedPoints)
        {
            if (point == null || point.isDestroyed) continue;

            // 4개의 라인 중 활성화된 것이 있는지 확인
            bool hasActiveLine = false;

            if (point.lineTop != null && point.lineTop.gameObject.activeInHierarchy)
                hasActiveLine = true;
            else if (point.lineBottom != null && point.lineBottom.gameObject.activeInHierarchy)
                hasActiveLine = true;
            else if (point.lineLeft != null && point.lineLeft.gameObject.activeInHierarchy)
                hasActiveLine = true;
            else if (point.lineRight != null && point.lineRight.gameObject.activeInHierarchy)
                hasActiveLine = true;

            // 활성화된 라인이 없으면 GridPoint 파괴
            if (!hasActiveLine)
            {
                // 기물 제거
                if (PieceManager.Instance != null)
                {
                    PieceManager.Instance.RemovePieceAtPositionByCollapse(point.gridPosition);
                }
                
                // GridPoint를 파괴된 상태로 표시 (라인은 이미 파괴되었으므로 DestroyIntersectingLines 호출 불필요)
                point.StopContinuousShake();
                // isDestroyed를 public setter가 없으므로, DestroyIntersectingLines를 호출하되 라인은 이미 파괴됨
                point.DestroyIntersectingLines();
            }
        }
    }

    /// <summary>
    /// 특정 GridPoint에 흔들 효과를 적용합니다.
    /// </summary>
    public override void ApplyEffect(Vector2Int gridPosition)
    {
        GridManager gridManager = executionContext.gridManager;
        if (gridManager == null) return;

        var allGridPoints = gridManager.GetAllGridPoints();
        foreach (var point in allGridPoints)
        {
            if (point != null && point.gridPosition == gridPosition)
            {
                point.StartContinuousShake(shakeDuration, shakeStrength);
                break;
            }
        }
    }
    /// <summary>
    /// 범위 내 GridPoint들에 흔들 효과를 적용합니다.
    /// </summary>
    public override void ApplyEffectInRange(Vector2Int centerPosition, int radius)
    {
        GridManager gridManager = executionContext.gridManager;
        if (gridManager == null) return;

        var allGridPoints = gridManager.GetAllGridPoints();
        foreach (var point in allGridPoints)
        {
            if (point != null && Vector2Int.Distance(point.gridPosition, centerPosition) <= radius)
            {
                point.StartContinuousShake(shakeDuration, shakeStrength);
            }
        }
    }

    /// <summary>
    /// 여러 GridPoint에 효과를 적용합니다.
    /// </summary>
    public override void ApplyEffectToMultiplePositions(List<Vector2Int> positions)
    {
        GridManager gridManager = executionContext.gridManager;
        if (gridManager == null) return;

        var allGridPoints = gridManager.GetAllGridPoints();
        foreach (var position in positions)
        {
            foreach (var point in allGridPoints)
            {
                if (point != null && point.gridPosition == position)
                {
                    point.StartContinuousShake(shakeDuration, shakeStrength);
                }
            }
        }
    }

    /// <summary>
    /// 스킬의 현재 상태를 반환합니다.
    /// </summary>
    public GridPoint GetCurrentAffectedPoint() => currentAffectedPoint;
}
