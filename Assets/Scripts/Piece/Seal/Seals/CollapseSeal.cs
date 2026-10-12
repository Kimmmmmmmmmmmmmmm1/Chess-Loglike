using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 붕괴의 인장 (Seal of Collapse)
/// 효과: 장착한 기물이 이동했을 때, 전에 위치해 있던 그리드 셀이 경고 흔들림을 시작하고 1턴 뒤에 붕괴(파괴)됩니다.
/// </summary>
public class CollapseSeal : SealBase
{
    [Header("Collapse Settings")]
    [Tooltip("경고 흔들림 세기")]
    [SerializeField] private float shakeStrength = 5f;
    [Tooltip("경고 흔들림 주기")]
    [SerializeField] private float shakeDuration = 0.5f;

    private class PendingCollapseCell
    {
        public Vector2Int Position;
        public GridCell Cell;
        public int RemainingTurns;
    }

    private readonly List<PendingCollapseCell> pendingCells = new List<PendingCollapseCell>();

    public override void OnEquip()
    {
        base.OnEquip();
        SubscribeEvents();
    }

    public override void OnUnequip()
    {
        base.OnUnequip();
        CancelAllPendingCollapses();
        UnsubscribeEvents();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        CancelAllPendingCollapses();
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= HandleTurnAdvanced;
            TurnManager.Instance.OnTurnAdvanced += HandleTurnAdvanced;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= HandleTurnAdvanced;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameStateManager.GameState newState)
    {
        // 전투 진행 중(GamePlay)이 아닐 때는 대기 중인 붕괴 취소
        if (newState != GameStateManager.GameState.GamePlay)
        {
            CancelAllPendingCollapses();
        }
    }

    /// <summary>
    /// Case C Hook: 기물이 이동 완료된 후 호출됩니다.
    /// 이동 전 위치(prevPos)의 셀을 찾아 경고 흔들림을 시작하고 붕괴 대기열에 등록합니다.
    /// </summary>
    public override void OnAfterMove(Vector2Int prevPos, Vector2Int newPos)
    {
        if (prevPos == newPos) return;

        GridManager gridManager = null;
        if (PieceManager.Instance != null && PieceManager.Instance.gridManager != null)
        {
            gridManager = PieceManager.Instance.gridManager;
        }
        else
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (gridManager == null) return;

        GridCell cell = gridManager.GetGridCell(prevPos);
        if (cell == null || cell.isDestroyed) return;

        // 이미 대기열에 있는지 확인
        for (int i = 0; i < pendingCells.Count; i++)
        {
            if (pendingCells[i].Position == prevPos)
            {
                return; // 이미 등록됨
            }
        }

        // 경고 흔들림 시작
        cell.StartContinuousShake(shakeDuration, shakeStrength);

        // 플레이어 이동 -> 상대 턴 진입(1회 전환, RemainingTurns: 2 -> 1, 흔들림 유지) -> 다시 플레이어 턴 진입(2회 전환, RemainingTurns: 1 -> 0, 붕괴)
        pendingCells.Add(new PendingCollapseCell
        {
            Position = prevPos,
            Cell = cell,
            RemainingTurns = 2
        });
    }

    /// <summary>
    /// 턴이 변경될 때 호출됩니다.
    /// 1턴이 지난 셀을 파괴하고 대기열에서 제거합니다.
    /// </summary>
    private void HandleTurnAdvanced(TurnOwner newTurn)
    {
        if (pendingCells.Count == 0) return;

        List<PendingCollapseCell> toDestroy = new List<PendingCollapseCell>();

        for (int i = pendingCells.Count - 1; i >= 0; i--)
        {
            var pending = pendingCells[i];
            pending.RemainingTurns--;

            if (pending.RemainingTurns <= 0)
            {
                pendingCells.RemoveAt(i);
                toDestroy.Add(pending);
            }
        }

        foreach (var pending in toDestroy)
        {
            if (pending.Cell != null && !pending.Cell.isDestroyed)
            {
                pending.Cell.StopContinuousShake();
                pending.Cell.DestroyCell();
            }
        }
    }

    private void CancelAllPendingCollapses()
    {
        foreach (var pending in pendingCells)
        {
            if (pending.Cell != null && !pending.Cell.isDestroyed)
            {
                pending.Cell.StopContinuousShake();
            }
        }
        pendingCells.Clear();
    }
}

