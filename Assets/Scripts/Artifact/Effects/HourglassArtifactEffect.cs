using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// [AC01] 시간의 모래 (액티브, 즉시 발동)
/// 전장의 상태를 직전 턴 시작 시점(1턴 전)으로 되돌립니다.
/// 지난 턴에 이동한 기물은 원래 위치로 복귀하며, 파괴된 기물은 인장 및 상태를 유지한 채 부활합니다.
/// 지난 턴 동안 사용했던 액티브 유물도 함께 복구되며, 로그는 취소선(<s>)으로 처리됩니다.
/// </summary>
public sealed class HourglassArtifactEffect : ActiveArtifactEffectBase
{
    public override string ArtifactId => "AC01";

    public override ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.None;

    // 되돌린 후 플레이어가 이번 턴에 다시 행동해야 하므로 턴을 소모하지 않습니다.
    public override bool ConsumesTurn => false;

    public struct PieceRecord
    {
        public PieceController pieceInstance; // 살아있는 기물 참조 (파괴 시 null)
        public PieceType pieceType;
        public bool isEnemy;
        public Vector2Int gridPosition;
        public List<SealData> seals;
        public int protectionStacks;
        public int evasionStacks;
        public int counterattackStacks;
        public int invincibilityTurns;
        public int blindnessTurns;
        public int confusionTurns;
    }

    public struct ActiveArtifactRecord
    {
        public string id;
        public int stackCount;
        public int level;
    }

    public class TurnSnapshot
    {
        public List<PieceRecord> pieces = new List<PieceRecord>();
        public List<ActiveArtifactRecord> activeArtifacts = new List<ActiveArtifactRecord>();
        public int logStartIndex = 0;
    }

    private TurnSnapshot previousTurnSnapshot;
    private TurnSnapshot currentTurnSnapshot;

    public bool HasRewindSnapshot => previousTurnSnapshot != null && previousTurnSnapshot.pieces.Count > 0;

    public override bool CanUse()
    {
        // 되돌릴 이전 턴 스냅샷이 존재할 때만 사용 가능 (전투 첫 턴에는 사용 불가)
        return HasRewindSnapshot;
    }

    public void OnBattleStarted()
    {
        previousTurnSnapshot = null;
        currentTurnSnapshot = null;
    }

    public void OnTurnAdvanced(TurnOwner owner)
    {
        if (owner == TurnOwner.Player)
        {
            // 직전 턴 스냅샷 보관
            if (currentTurnSnapshot != null && currentTurnSnapshot.pieces.Count > 0)
            {
                previousTurnSnapshot = currentTurnSnapshot;
            }

            // 이번 플레이어 턴 시작 시점 스냅샷 캡처
            currentTurnSnapshot = CaptureBoardSnapshot();
        }
    }

    private TurnSnapshot CaptureBoardSnapshot()
    {
        var snapshot = new TurnSnapshot();

        // 1. 턴 시작 시점의 로그 인덱스 기록 (이후 생성된 로그들을 시간 역행 시 취소선 처리하기 위함)
        if (TurnLogUIManager.Instance != null)
        {
            snapshot.logStartIndex = TurnLogUIManager.Instance.GetLogCount();
        }

        // 2. 턴 시작 시점의 액티브 유물 보유 상태 스냅샷 저장
        if (ArtifactManager.Instance != null)
        {
            foreach (var owned in ArtifactManager.Instance.OwnedArtifacts)
            {
                if (owned != null && owned.IsActive)
                {
                    snapshot.activeArtifacts.Add(new ActiveArtifactRecord
                    {
                        id = owned.id,
                        stackCount = owned.StackCount,
                        level = owned.Level
                    });
                }
            }
        }

        if (PieceManager.Instance == null) return snapshot;

        // 3. 보드 위 기물 상태 저장
        foreach (PieceController piece in PieceManager.Instance.Pieces)
        {
            if (piece == null || piece.CurrentLocation != PieceLocation.Board || !piece.GridPosition.HasValue)
            {
                continue;
            }

            var record = new PieceRecord
            {
                pieceInstance = piece,
                pieceType = piece.PieceType,
                isEnemy = piece.IsEnemy,
                gridPosition = piece.GridPosition.Value,
                seals = new List<SealData>(),
                protectionStacks = piece.GetBuffStacks(PieceBuffType.Protection),
                evasionStacks = piece.GetBuffStacks(PieceBuffType.Evasion),
                counterattackStacks = piece.GetBuffStacks(PieceBuffType.Counterattack),
                invincibilityTurns = piece.GetBuffStacks(PieceBuffType.Invincibility),
                blindnessTurns = piece.GetBuffStacks(PieceBuffType.Blindness),
                confusionTurns = piece.GetBuffStacks(PieceBuffType.Confusion)
            };

            if (piece.EquippedSeals != null)
            {
                foreach (var sb in piece.EquippedSeals)
                {
                    if (sb != null && sb.Data != null)
                    {
                        record.seals.Add(sb.Data);
                    }
                }
            }

            snapshot.pieces.Add(record);
        }

        return snapshot;
    }

    public override bool Execute(PieceController target)
    {
        if (!HasRewindSnapshot || PieceManager.Instance == null)
        {
            return false;
        }

        // 1. 선택 상태 및 이동 마커 초기화
        PieceManager.Instance.ClearSelection();
        PieceManager.Instance.HideMoveMarkers();

        // 2. 현재 보드 위의 기물 목록 파악
        var currentPieces = new List<PieceController>(PieceManager.Instance.Pieces);
        var snapshotPieceSet = new HashSet<PieceController>();

        // 3. 스냅샷 기반 기물 복원
        foreach (var record in previousTurnSnapshot.pieces)
        {
            if (record.pieceInstance != null && currentPieces.Contains(record.pieceInstance))
            {
                // [Case A] 살아있는 기물: 원래 위치로 애니메이션 이동 & 상태 복원
                snapshotPieceSet.Add(record.pieceInstance);
                RestoreLivingPiece(record);
            }
            else
            {
                // [Case B] 파괴되었거나 보드에서 사라진 기물: 새로 스폰하여 복원
                SpawnAndRestorePiece(record);
            }
        }

        // 4. 스냅샷에 없었던 기물(이번 턴에 새로 소환/승급된 기물 등) 정리
        foreach (var piece in currentPieces)
        {
            if (piece != null && !snapshotPieceSet.Contains(piece))
            {
                piece.SetLocation(PieceLocation.Inventory);
                PieceManager.Instance.UnregisterPiece(piece);
                Object.Destroy(piece.gameObject);
            }
        }

        // 5. 사용했던 액티브 유물 복원 (스냅샷 상태대로 인벤토리 복구)
        if (ArtifactManager.Instance != null && previousTurnSnapshot.activeArtifacts != null)
        {
            ArtifactManager.Instance.RestoreActiveArtifacts(previousTurnSnapshot.activeArtifacts);
        }

        // 6. 직전 턴 이후 기록된 로그들에 취소선(<s>) 적용하여 무효화 표시
        if (TurnLogUIManager.Instance != null && previousTurnSnapshot != null)
        {
            TurnLogUIManager.Instance.ApplyStrikethroughFromIndex(previousTurnSnapshot.logStartIndex);
        }

        // 7. 3수동형(동형반복) 리셋 및 턴 카운터 되돌리기
        TurnManager.Instance?.RollbackCompletedTurn();
        TurnIndicatorUI.Instance?.UpdateRemainingTurns();

        // 8. 보드 상태 및 위협 갱신
        PieceManager.Instance.MarkPiecePositionCacheDirty();
        PieceManager.Instance.EnsurePositionCacheReady();
        PieceManager.Instance.UpdateThreatenedStatus();
        PieceManager.Instance.UpdatePieceCountUI();

        // 9. 시각 및 사운드 연출
        EffectManager.Instance?.PlayScreenFlash(0.25f, 0.6f);
        EffectManager.Instance?.PlayCameraShake(0.2f, 0.4f);
        TurnLogUIManager.Instance?.AddLog("시간의 모래: 시간을 1턴 전으로 되돌렸습니다!", false);

        // 사용 완료: 이번 턴에는 중복 되돌리기 불가 처리
        currentTurnSnapshot = previousTurnSnapshot;
        previousTurnSnapshot = null;

        return true;
    }

    private void RestoreLivingPiece(PieceRecord record)
    {
        PieceController piece = record.pieceInstance;
        if (piece == null) return;

        piece.gridPosition = record.gridPosition;
        piece.SetLocation(PieceLocation.Board);

        RectTransform rt = piece.GetComponent<RectTransform>();
        if (rt != null && PieceManager.Instance?.gridManager != null)
        {
            Vector2 targetUiPos = PieceManager.Instance.gridManager.GridToUiPosition(record.gridPosition);
            rt.DOKill();
            rt.DOAnchorPos(targetUiPos, 0.35f).SetEase(Ease.OutCubic);
        }

        // 버프 복원
        piece.SetBuffStacks(PieceBuffType.Protection, record.protectionStacks);
        piece.SetBuffStacks(PieceBuffType.Evasion, record.evasionStacks);
        piece.SetBuffStacks(PieceBuffType.Counterattack, record.counterattackStacks);
        piece.SetBuffStacks(PieceBuffType.Invincibility, record.invincibilityTurns);
        piece.SetBuffStacks(PieceBuffType.Blindness, record.blindnessTurns);
        piece.SetBuffStacks(PieceBuffType.Confusion, record.confusionTurns);
        piece.RefreshBuffAuraVisuals();
    }

    private void SpawnAndRestorePiece(PieceRecord record)
    {
        if (PieceSpawner.Instance == null || PieceSpawner.Instance.piecePrefab == null) return;

        Transform parent = PieceSpawner.Instance.piecesParent != null
            ? PieceSpawner.Instance.piecesParent
            : PieceSpawner.Instance.transform;

        GameObject pieceObj = Object.Instantiate(PieceSpawner.Instance.piecePrefab, parent);
        pieceObj.name = $"PiecePrefab(Clone)@HourglassRewind[{(record.isEnemy ? "Enemy" : "Ally")}_{record.pieceType}_{record.gridPosition}]";

        if (pieceObj.TryGetComponent<PieceController>(out var piece))
        {
            piece.gridPosition = record.gridPosition;
            piece.Initialize(record.pieceType, record.isEnemy);

            if (record.seals != null)
            {
                foreach (var seal in record.seals)
                {
                    if (seal != null) piece.EquipSeal(seal);
                }
            }

            piece.SetBuffStacks(PieceBuffType.Protection, record.protectionStacks);
            piece.SetBuffStacks(PieceBuffType.Evasion, record.evasionStacks);
            piece.SetBuffStacks(PieceBuffType.Counterattack, record.counterattackStacks);
            piece.SetBuffStacks(PieceBuffType.Invincibility, record.invincibilityTurns);
            piece.SetBuffStacks(PieceBuffType.Blindness, record.blindnessTurns);
            piece.SetBuffStacks(PieceBuffType.Confusion, record.confusionTurns);
            piece.RefreshBuffAuraVisuals();

            RectTransform rt = piece.GetComponent<RectTransform>();
            if (rt != null && PieceManager.Instance?.gridManager != null)
            {
                rt.anchoredPosition = PieceManager.Instance.gridManager.GridToUiPosition(record.gridPosition);
                rt.localScale = Vector3.zero;
                rt.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
            }

            piece.SetLocation(PieceLocation.Board);
            PieceManager.Instance?.RegisterPiece(piece);
        }
    }
}
