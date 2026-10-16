using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 최후의 인장 (Last Stand Seal)
/// 효과: 이 기물은 같은 진영에 다른 아군 기물이 남아있는 한 **무적**입니다.
/// 혼자 남았을 때 비로소 무적이 해제되어 공격 및 처치가 가능해집니다.
/// </summary>
public class LastStandSeal : SealBase
{
    private bool cachedIsInvincible = false;

    public override bool IsInvincible => cachedIsInvincible;

    public override void OnEquip()
    {
        base.OnEquip();
        SubscribeEvents();
        UpdateInvincibilityState();
    }

    public override void OnUnequip()
    {
        base.OnUnequip();
        UnsubscribeEvents();
        cachedIsInvincible = false;
        if (owner != null)
        {
            owner.RefreshBuffAuraVisuals();
        }
    }

    private void OnEnable()
    {
        SubscribeEvents();
        UpdateInvincibilityState();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.OnPieceRegistered -= HandlePieceCountChanged;
            PieceManager.Instance.OnPieceRegistered += HandlePieceCountChanged;
            PieceManager.Instance.OnPieceUnregistered -= HandlePieceCountChanged;
            PieceManager.Instance.OnPieceUnregistered += HandlePieceCountChanged;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= HandleTurnAdvanced;
            TurnManager.Instance.OnTurnAdvanced += HandleTurnAdvanced;
        }
    }

    private void UnsubscribeEvents()
    {
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.OnPieceRegistered -= HandlePieceCountChanged;
            PieceManager.Instance.OnPieceUnregistered -= HandlePieceCountChanged;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= HandleTurnAdvanced;
        }
    }

    private void HandlePieceCountChanged(PieceController piece)
    {
        UpdateInvincibilityState();
    }

    private void HandleGameStateChanged(GameStateManager.GameState newState)
    {
        UpdateInvincibilityState();
    }

    private void HandleTurnAdvanced(TurnOwner newTurn)
    {
        UpdateInvincibilityState();
    }

    private void UpdateInvincibilityState()
    {
        if (owner == null || PieceManager.Instance == null)
        {
            cachedIsInvincible = false;
            return;
        }

        // 같은 진영의 살아있는 기물 목록 확인
        IReadOnlyList<PieceController> allies = owner.IsEnemy 
            ? PieceManager.Instance.EnemyPieces 
            : PieceManager.Instance.PlayerPieces;

        int activeAlliesOnBoard = 0;
        for (int i = 0; i < allies.Count; i++)
        {
            PieceController ally = allies[i];
            if (ally != null && ally.CurrentLocation == PieceLocation.Board && ally.GridPosition.HasValue)
            {
                activeAlliesOnBoard++;
            }
        }

        // 혼자 남으면 (activeAlliesOnBoard <= 1) 무적 해제, 다른 아군이 있으면 (>= 2) 무적!
        bool newInvincible = activeAlliesOnBoard > 1;

        if (cachedIsInvincible != newInvincible)
        {
            cachedIsInvincible = newInvincible;
            owner.RefreshBuffAuraVisuals();

            if (!cachedIsInvincible)
            {
                // 최후의 1인이 되어 무적이 해제되었을 때 로그 또는 알림
                string sideName = owner.IsEnemy ? "적" : "아군";
                TurnLogUIManager.Instance?.AddLog($"[{owner.PieceType}] {sideName} 기물이 홀로 남아 최후의 인장의 무적이 해제되었습니다!", owner.IsEnemy);
            }
        }
    }

    /// <summary>
    /// Case B Hook: 무적 상태일 때 파괴 거부권 행사
    /// </summary>
    public override bool OnBeforeDestroy()
    {
        UpdateInvincibilityState();
        if (cachedIsInvincible)
        {
            return false; // 무적이므로 파괴 거부
        }
        return true;
    }
}

