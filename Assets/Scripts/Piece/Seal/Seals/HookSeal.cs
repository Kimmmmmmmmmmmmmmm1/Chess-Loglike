using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 갈고리의 인장 (Hook Seal)
/// 효과: 이 기물이 적을 포획하지 않고 단순 이동을 마쳤을 때,
/// 이동한 방향의 일직선상에 있는 첫 번째 적 기물을 내 기물 바로 앞 칸으로 강제로 끌어당기고 1턴동안 혼란 상태를 부여합니다.
/// </summary>
public class HookSeal : SealBase
{
    public const string SealName = "갈고리의 인장";

    public override void OnAfterMove(Vector2Int prevPos, Vector2Int newPos, bool captured)
    {
        // 1. 단순 이동(포획하지 않고 이동)인 경우에만 발동
        if (captured) return;
        if (prevPos == newPos) return;
        if (owner == null) return;
        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null) return;

        // 2. 이동한 방향 벡터 계산 (직선 이동인지 검증: 상하좌우 및 대각선 8방향)
        Vector2Int delta = newPos - prevPos;
        int dx = delta.x;
        int dy = delta.y;
        bool isStraight = (dx == 0 && dy != 0) || (dy == 0 && dx != 0) || (Mathf.Abs(dx) == Mathf.Abs(dy));
        if (!isStraight) return;

        Vector2Int dir = new Vector2Int(Math.Sign(dx), Math.Sign(dy));
        if (dir == Vector2Int.zero) return;

        // 3. 내 기물 바로 앞 칸(끌어당길 도착지점) 좌표 검증
        Vector2Int frontPos = newPos + dir;
        GridManager grid = PieceManager.Instance.gridManager;
        if (!grid.IsInBounds(frontPos)) return;

        GridCell frontCell = grid.GetGridCell(frontPos);
        if (frontCell == null || frontCell.isDestroyed) return;

        // 4. 바로 앞 칸 점유 상태 확인
        PieceController pieceAtFront = PieceManager.Instance.GetPieceAt(frontPos);
        if (pieceAtFront != null)
        {
            if (pieceAtFront.IsEnemy == owner.IsEnemy)
            {
                // 아군이 바로 앞 칸을 막고 있으므로 끌어올 공간이 없음
                return;
            }
            else
            {
                // 적 기물이 이미 바로 앞 칸에 있는 경우: 위치 이동 없이 1턴간 혼란 부여
                ApplyHookToAdjacentEnemy(pieceAtFront, newPos, frontPos);
                return;
            }
        }

        // 5. 이동한 방향의 일직선상으로 탐색하여 첫 번째 기물 탐색
        PieceController targetEnemy = null;
        Vector2Int checkPos = frontPos + dir;
        while (grid.IsInBounds(checkPos))
        {
            PieceController occupant = PieceManager.Instance.GetPieceAt(checkPos);
            if (occupant != null)
            {
                if (occupant.IsEnemy != owner.IsEnemy)
                {
                    targetEnemy = occupant;
                }
                // 아군이든 적군이든 첫 번째 기물을 만나면 갈고리의 시야/궤적이 막힘
                break;
            }
            checkPos += dir;
        }

        if (targetEnemy == null) return;

        // 6. 무적 상태 판정 (무적 기물은 갈고리 효과 무시)
        if (targetEnemy.IsInvincible)
        {
            TurnLogUIManager.Instance?.AddLog(
                $"[{targetEnemy.PieceType}] 무적으로 갈고리의 효과를 무시했습니다!",
                targetEnemy.IsEnemy);
            return;
        }

        // 7. 갈고리 사슬 연출 및 강제 끌어당김 실행
        Vector2Int enemyStartPos = targetEnemy.gridPosition.HasValue ? targetEnemy.gridPosition.Value : frontPos;
        PlayHookVisual(newPos, enemyStartPos, frontPos);

        // 기물 보드 위치 강제 이동 (내 바로 앞 칸으로)
        targetEnemy.MoveToGrid(frontPos);

        // 1턴 동안 혼란(Confusion) 상태 부여
        targetEnemy.AddBuff(PieceBuffType.Confusion, 1);
        targetEnemy.PlayBuffActivationEffect(PieceBuffType.Confusion);

        // 보드 상태 및 위협 갱신
        PieceManager.Instance.EnsurePositionCacheReady();
        PieceManager.Instance.UpdateThreatenedStatus();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SFXType.Move);
        }

        TurnLogUIManager.Instance?.AddLog(
            $"[{owner.PieceType}] 갈고리의 인장 발동! [{targetEnemy.PieceType}]을(를) 끌어당기고 1턴간 혼란을 부여했습니다.",
            owner.IsEnemy);
    }

    private void ApplyHookToAdjacentEnemy(PieceController targetEnemy, Vector2Int fromPos, Vector2Int frontPos)
    {
        if (targetEnemy.IsInvincible)
        {
            TurnLogUIManager.Instance?.AddLog(
                $"[{targetEnemy.PieceType}] 무적으로 갈고리의 효과를 무시했습니다!",
                targetEnemy.IsEnemy);
            return;
        }

        PlayHookVisual(fromPos, frontPos, frontPos);

        targetEnemy.AddBuff(PieceBuffType.Confusion, 1);
        targetEnemy.PlayBuffActivationEffect(PieceBuffType.Confusion);

        PieceManager.Instance?.EnsurePositionCacheReady();
        PieceManager.Instance?.UpdateThreatenedStatus();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SFXType.Move);
        }

        TurnLogUIManager.Instance?.AddLog(
            $"[{owner.PieceType}] 갈고리의 인장 발동! 바로 앞의 [{targetEnemy.PieceType}]에게 1턴간 혼란을 부여했습니다.",
            owner.IsEnemy);
    }

    private void PlayHookVisual(Vector2Int fromPos, Vector2Int targetPos, Vector2Int destPos)
    {
        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null) return;
        Transform parent = PieceManager.Instance.markersParent != null ? PieceManager.Instance.markersParent : transform;

        Vector2 startUi = PieceManager.Instance.gridManager.GridToUiPosition(fromPos);
        Vector2 targetUi = PieceManager.Instance.gridManager.GridToUiPosition(targetPos);
        Vector2 destUi = PieceManager.Instance.gridManager.GridToUiPosition(destPos);

        GameObject chainObj = new GameObject("HookChain", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform chainRect = chainObj.GetComponent<RectTransform>();
        chainRect.SetParent(parent, false);
        chainRect.anchorMin = new Vector2(0.5f, 0.5f);
        chainRect.anchorMax = new Vector2(0.5f, 0.5f);
        chainRect.pivot = new Vector2(0f, 0.5f);
        chainRect.anchoredPosition = startUi;

        Vector2 diff = targetUi - startUi;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        chainRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        chainRect.SetAsLastSibling();

        Image img = chainObj.GetComponent<Image>();
        img.color = new Color(0.9f, 0.75f, 0.25f, 0.95f); // 묵직한 쇠사슬 골드/황동
        img.raycastTarget = false;

        float targetLength = Mathf.Max(diff.magnitude, 20f);
        float destLength = Mathf.Max((destUi - startUi).magnitude, 20f);

        chainRect.sizeDelta = new Vector2(0f, 6f);
        Sequence seq = DOTween.Sequence();
        seq.Append(DOTween.To(() => chainRect.sizeDelta, x => chainRect.sizeDelta = x, new Vector2(targetLength, 6f), 0.12f).SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => chainRect.sizeDelta, x => chainRect.sizeDelta = x, new Vector2(destLength, 6f), 0.22f).SetEase(Ease.InQuad));
        seq.Join(img.DOFade(0f, 0.22f));
        seq.OnComplete(() =>
        {
            if (chainObj != null) Destroy(chainObj);
        });
    }
}

