using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelfDestructSeal : SealBase
{
    [Header("Explosion Timing")]
    [SerializeField] private float explosionDelayAfterOwnerDeath = 0.5f;

    public override void OnOwnerDestroyed(PieceController killer, Vector2Int ownerPosition)
    {
        if (owner == null || owner.Type != PieceType.Pawn)
        {
            return;
        }

        if (PieceManager.Instance == null)
        {
            return;
        }

        List<PieceController> targets = new List<PieceController>();

        // 1. 사망 위치 중심 주변 8칸 내의 모든 기물을 폭발 타겟으로 등록
        Vector2Int[] surroundingOffsets = new Vector2Int[]
        {
            new Vector2Int(-1,  1), new Vector2Int(0,  1), new Vector2Int(1,  1),
            new Vector2Int(-1,  0),                         new Vector2Int(1,  0),
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1)
        };

        for (int i = 0; i < surroundingOffsets.Length; i++)
        {
            Vector2Int checkPos = ownerPosition + surroundingOffsets[i];
            if (PieceManager.Instance.gridManager != null && !PieceManager.Instance.gridManager.IsInBounds(checkPos))
            {
                continue;
            }

            PieceController nearbyPiece = PieceManager.Instance.GetPieceAt(checkPos);
            if (nearbyPiece != null && !targets.Contains(nearbyPiece))
            {
                targets.Add(nearbyPiece);
            }
        }

        // 2. 잡은 기물(killer)에 대한 판정:
        // - 직접 이동하여 ownerPosition(폭발 진원지)으로 진입 중이거나,
        // - 현재 위치가 폭발 범위(주변 8칸) 내에 있는 경우 폭발 타겟에 포함
        if (killer != null && killer.CurrentLocation == PieceLocation.Board && killer.gridPosition.HasValue && !targets.Contains(killer))
        {
            bool isLandingOnCenter = PieceManager.Instance.IsPieceMovingToCapture(killer);
            bool isWithinBlastRadius = Mathf.Abs(killer.gridPosition.Value.x - ownerPosition.x) <= 1 &&
                                       Mathf.Abs(killer.gridPosition.Value.y - ownerPosition.y) <= 1;

            if (isLandingOnCenter || isWithinBlastRadius)
            {
                targets.Add(killer);
            }
        }

        if (targets.Count == 0)
        {
            return;
        }

        // 스테이지 종료 시 버프 소멸(ClearAllPieceBuffs)이 실행되기 전에
        // 즉시 무적/보호/회피/역공 방어 판정을 처리합니다.
        List<PieceController> doomedTargets = new List<PieceController>();

        for (int i = 0; i < targets.Count; i++)
        {
            PieceController target = targets[i];
            if (target == null || target.CurrentLocation != PieceLocation.Board || !target.GridPosition.HasValue)
            {
                continue;
            }

            // 1. 무적: 폭발 피해 완전 무시
            if (target.IsInvincible)
            {
                TurnLogUIManager.Instance?.AddLog(
                    $"[{target.PieceType}] 무적으로 자폭 피해를 무시했습니다!",
                    target.IsEnemy);
                continue;
            }

            // 2. 보호 / 회피 / 역공 판정 처리
            // - 보호: 스택 차감 & 피해 무효화
            // - 회피: 스택 차감 & 인접 빈 칸으로 회피 이동
            // - 역공: 스택 차감 & 자폭 공격 방어
            PieceManager.AttackResponse response = PieceManager.Instance.ResolveAttackResponse(null, target);
            if (response != PieceManager.AttackResponse.None)
            {
                continue;
            }

            // 3. 파괴 전 거부권 확인
            if (!target.CanBeDestroyed())
            {
                continue;
            }

            // 4. 방어에 실패한 기물만 파괴 예정 목록에 등록
            doomedTargets.Add(target);
        }

        if (doomedTargets.Count == 0)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            for (int i = 0; i < doomedTargets.Count; i++)
            {
                PieceController target = doomedTargets[i];
                if (target != null)
                {
                    PieceManager.Instance.RemovePiece(target);
                }
            }

            return;
        }

        PieceManager.Instance.StartCoroutine(ExplodeAfterDelay(doomedTargets));
    }

    private IEnumerator ExplodeAfterDelay(List<PieceController> doomedTargets)
    {
        if (explosionDelayAfterOwnerDeath > 0f)
        {
            yield return new WaitForSecondsRealtime(explosionDelayAfterOwnerDeath);
        }

        if (PieceManager.Instance == null)
        {
            yield break;
        }

        for (int i = 0; i < doomedTargets.Count; i++)
        {
            PieceController target = doomedTargets[i];
            if (target == null || target.CurrentLocation != PieceLocation.Board || !target.GridPosition.HasValue)
            {
                continue;
            }

            PieceManager.Instance.RemovePiece(target);
        }
    }
}
