using System.Collections.Generic;
using UnityEngine;

public sealed class OthelloArtifact : ArtifactBase
{
    public const string Id = "OTHELLO";
    public override string ArtifactId => Id;
}

/// <summary>
/// 전설 패시브 유물 [오셀로의 성역 (Othello's Domain)] 핵심 시스템.
/// 1. 빈 타일 전용 이동 (직접 포획 금지)
/// 2. 착지 시 8방향 샌드위치 포위 판정 및 적 기물 아군 전향 ([전향의 인장] 부여 + 180도 뒤집기 연출)
/// 3. 전투 승리 후 [전향의 인장] 기물 소멸로 원본 덱 밸런스 보존
/// 4. 15턴 제한 또는 유효 이동 불가(교착) 시 필드 기물 수 비교 다수결 판정승(Majority Victory)
/// </summary>
public class OthelloArtifactSystem : MonoBehaviour
{
    public const int MaxTurnLimit = 15;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),   new Vector2Int(0, -1),
        new Vector2Int(-1, 0),  new Vector2Int(1, 0),
        new Vector2Int(1, 1),   new Vector2Int(1, -1),
        new Vector2Int(-1, 1),  new Vector2Int(-1, -1)
    };

    public static bool IsActive =>
        ArtifactManager.Instance != null &&
        ArtifactManager.Instance.HasArtifact(OthelloArtifact.Id);

    /// <summary>
    /// 기물이 빈 타일에 착지한 직후 호출되어 8방향 샌드위치 전향 판정을 수행합니다.
    /// </summary>
    public static int OnPieceLanded(PieceController landedPiece)
    {
        if (!IsActive || landedPiece == null || !landedPiece.GridPosition.HasValue)
        {
            return 0;
        }

        if (PieceManager.Instance == null || PieceManager.Instance.gridManager == null)
        {
            return 0;
        }

        GridManager gridManager = PieceManager.Instance.gridManager;
        Vector2Int startPos = landedPiece.GridPosition.Value;
        List<PieceController> piecesToConvert = new List<PieceController>();

        foreach (Vector2Int dir in Directions)
        {
            List<PieceController> tempTargets = new List<PieceController>();
            Vector2Int currentPos = startPos + dir;

            while (gridManager.IsInBounds(currentPos))
            {
                GridCell cell = gridManager.GetGridCell(currentPos);
                if (cell != null && cell.isDestroyed)
                {
                    tempTargets.Clear();
                    break;
                }

                PieceController target = PieceManager.Instance.GetPieceAt(currentPos);

                // 빈 칸이거나 준비 단계 은신 기물이면 샌드위치 불성립
                if (target == null || target.IsHidden)
                {
                    tempTargets.Clear();
                    break;
                }

                // 다른 팀(상대) 기물이면 후보군에 추가
                if (target.IsEnemy != landedPiece.IsEnemy)
                {
                    tempTargets.Add(target);
                }
                // 같은 팀(자신과 같은 진영) 기물을 만나면 후보군을 최종 전향 리스트에 확정하고 해당 방향 탐색 종료
                else
                {
                    if (tempTargets.Count > 0)
                    {
                        for (int i = 0; i < tempTargets.Count; i++)
                        {
                            if (!piecesToConvert.Contains(tempTargets[i]))
                            {
                                piecesToConvert.Add(tempTargets[i]);
                            }
                        }
                    }
                    break;
                }

                currentPos += dir;
            }
        }

        if (piecesToConvert.Count == 0)
        {
            return 0;
        }

        // 포위된 모든 상대 기물을 착지한 기물의 진영으로 전향 실행 (포획 보상 카운트 반영)
        bool toEnemy = landedPiece.IsEnemy;
        foreach (PieceController targetPiece in piecesToConvert)
        {
            if (targetPiece == null)
            {
                continue;
            }

            bool wasEnemyPiece = targetPiece.IsEnemy;
            PieceType convertedType = targetPiece.Type;

            ConvertPieceToTeam(targetPiece, toEnemy);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecordCapture(wasEnemyPiece, convertedType);
            }

            landedPiece.OnCapturedPiece(convertedType);
        }

        PlayOthelloArtifactPopup();

        string logMessage = toEnemy
            ? $"[오셀로의 성역] 아군 기물 {piecesToConvert.Count}기 적군으로 전향!"
            : $"[오셀로의 성역] 적 기물 {piecesToConvert.Count}기 전향!";
        TurnLogUIManager.Instance?.AddLog(logMessage, toEnemy);

        if (!toEnemy && piecesToConvert.Count > 0)
        {
            AchievementManager.Instance?.AddProgress("combat_othello_first_flip", 1);
            AchievementManager.Instance?.AddProgress("combat_othello_flip_20", piecesToConvert.Count);
            if (piecesToConvert.Count >= 3)
            {
                AchievementManager.Instance?.UnlockAchievement("combat_othello_multi_flip");
            }
        }

        // 전향이 발생하면 보드 국면이 비가역적으로 변경되므로 동형반복 기록 초기화
        TurnManager.Instance?.OnPieceCaptured();

        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.MarkPiecePositionCacheDirty();
            PieceManager.Instance.UpdatePieceCountUI();
            PieceManager.Instance.UpdateThreatenedStatus();
            PieceManager.Instance.CheckPieceCounts();
        }

        return piecesToConvert.Count;
    }

    private static void ConvertPieceToTeam(PieceController piece, bool toEnemy)
    {
        if (piece == null)
        {
            return;
        }

        piece.ConvertToTeamWithOthelloSeal(toEnemy);
    }

    private static void PlayOthelloArtifactPopup()
    {
        ArtifactData artifact = ArtifactManager.Instance != null
            ? ArtifactManager.Instance.GetOwnedArtifact(OthelloArtifact.Id)
            : null;

        if (artifact != null && artifact.icon != null)
        {
            ArtifactPopupEffect.Play(artifact);
        }
        else
        {
            ArtifactPopupEffect.Play(ConversionSeal.GetOrCreateFallbackIcon());
        }
    }

    /// <summary>
    /// 유효 이동 불가(교착) 또는 15턴 종료 시 필드 위 기물 수를 비교하여 다수결 판정승(Majority Victory)을 처리합니다.
    /// </summary>
    public static bool TryResolveMajorityVictory(string reason)
    {
        if (!IsActive || PieceManager.Instance == null || GameStateManager.Instance == null)
        {
            return false;
        }

        if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            return false;
        }

        int allyCount = PieceManager.Instance.GetPlayerPieceCountOnBoard();
        int enemyCount = PieceManager.Instance.GetEnemyPieceCountOnBoard();

        PlayOthelloArtifactPopup();

        if (allyCount > enemyCount)
        {
            AchievementManager.Instance?.UnlockAchievement("combat_othello_majority_win");
            TurnLogUIManager.Instance?.AddLog(
                $"[오셀로의 성역] {reason} — 다수결 판정승! (아군 {allyCount} : 적군 {enemyCount})",
                false);
            Debug.Log($"[OthelloArtifactSystem] Majority Victory ({reason}): Ally {allyCount} > Enemy {enemyCount}");
            GameStateManager.Instance.ChangeState(GameStateManager.GameState.Win);
            PieceManager.Instance.RemoveAllEnemyPieces();
        }
        else
        {
            TurnLogUIManager.Instance?.AddLog(
                $"[오셀로의 성역] {reason} — 다수결 판정패 (아군 {allyCount} : 적군 {enemyCount})",
                true);
            Debug.Log($"[OthelloArtifactSystem] Majority Defeat ({reason}): Ally {allyCount} <= Enemy {enemyCount}");
            GameStateManager.Instance.ChangeState(GameStateManager.GameState.GameOver);
            PieceManager.Instance.RemoveAllEnemyPieces();
        }

        return true;
    }
}

