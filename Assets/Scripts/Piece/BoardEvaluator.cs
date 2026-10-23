using System.Collections.Generic;
using UnityEngine;

public enum CapturePerspective
{
    PlayerCapturesEnemy,
    EnemyCapturesPlayer
}

public class BoardEvaluator : MonoBehaviour
{
    public const int RULE_STANDARD = 0;
    public const int RULE_OTHELLO = 1;

    // 룰별 실수 확률 배율 (기본 실수 확률에 곱해짐, 1 = 기존과 동일)
    public const float MISTAKE_MULTIPLIER_STANDARD = 1f;
    public const float MISTAKE_MULTIPLIER_OTHELLO = 2.5f;
    // 룰별 최대 실수 확률 상한
    public const float MAX_ERROR_CHANCE_STANDARD = 0.5f;
    public const float MAX_ERROR_CHANCE_OTHELLO = 0.85f;

    public static BoardEvaluator Instance { get; private set; }

    [Header("Scoring")]
    [SerializeField] private float immediateCaptureScore = 1f;
    [SerializeField] private float escapeThreatScore = 0.75f;
    [SerializeField] private float twoTurnCaptureScore = 0.5f;
    [SerializeField] private float threeTurnCaptureScore = 0.25f;
    [SerializeField] private float threatenedPenalty = 0.5f;

    [Header("Lookahead")]
    [SerializeField] private CapturePerspective capturePerspective = CapturePerspective.PlayerCapturesEnemy;

    [Header("AI Settings")]
    [Range(1, 10)]
    public int aiIntelligence = 5;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public static int GetActiveRuleMode()
    {
        if (OthelloArtifactSystem.IsActive)
        {
            return RULE_OTHELLO;
        }

        return RULE_STANDARD;
    }

    private float GetErrorChance(int ruleMode)
    {
        // 기본: 지능 10이면 0%, 1이면 50%
        float baseChance = (10f - aiIntelligence) * (0.5f / 9f);
        float multiplier;
        float maxChance;
        switch (ruleMode)
        {
            case RULE_OTHELLO:
                multiplier = MISTAKE_MULTIPLIER_OTHELLO;
                maxChance = MAX_ERROR_CHANCE_OTHELLO;
                break;

            case RULE_STANDARD:
            default:
                multiplier = MISTAKE_MULTIPLIER_STANDARD;
                maxChance = MAX_ERROR_CHANCE_STANDARD;
                break;
        }

        return Mathf.Min(baseChance * multiplier, maxChance);
    }

    public bool TryPlayBestEnemyMove()
    {
        if (PieceManager.Instance == null)
        {
            return false;
        }

        IReadOnlyList<PieceController> pieces = PieceManager.Instance.Pieces;
        if (pieces == null || pieces.Count == 0)
        {
            return false;
        }

        BoardState baseState = BoardState.FromPieces(pieces, PieceManager.Instance);
        if (baseState.EnemyPieceIds.Count == 0)
        {
            return false;
        }

        BestMove bestMove = BestMove.None;
        List<MoveInfo> allPossibleMoves = new List<MoveInfo>();
        int ruleMode = GetActiveRuleMode();

        for (int i = 0; i < baseState.EnemyPieceIds.Count; i++)
        {
            int pieceId = baseState.EnemyPieceIds[i];
            SimPiece piece = baseState.GetPiece(pieceId);
            List<Vector2Int> legalMoves = GetRootLegalMoves(baseState, piece);
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            {
                int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
                if (cleared == 0 || cleared == 1)
                {
                    legalMoves.RemoveAll(t =>
                    {
                        if (TutorialManager.Instance.IsTargetProtectedFromEnemy(t)) return true;
                        PieceController p = PieceManager.Instance != null ? PieceManager.Instance.GetPieceAt(t) : null;
                        return p != null && !p.IsEnemy;
                    });
                }
                else
                {
                    legalMoves.RemoveAll(t => TutorialManager.Instance.IsTargetProtectedFromEnemy(t));
                }
            }

            for (int m = 0; m < legalMoves.Count; m++)
            {
                Vector2Int target = legalMoves[m];

                allPossibleMoves.Add(new MoveInfo(pieceId, target));

                float score = 0f;
                switch (ruleMode)
                {
                    case RULE_OTHELLO:
                        score = EvaluateOthelloMove(baseState, piece, target, useIntelligenceScaling: false);
                        break;

                    case RULE_STANDARD:
                    default:
                        score = EvaluateStandardMove(baseState, piece, target, useIntelligenceScaling: false);
                        break;
                }

                if (score > bestMove.Score)
                {
                    bestMove = new BestMove(pieceId, target, score);
                }
            }
        }

        // 튜토리얼 1, 2스테이지 전용 착수: 적 기물을 플레이어 사정거리 안으로 유도하여 무조건 패배하도록 유도
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive && GameManager.Instance != null && (GameManager.Instance.ClearedStage == 0 || GameManager.Instance.ClearedStage == 1))
        {
            if (TryGetTutorialEnemyMove(baseState, allPossibleMoves, out MoveInfo tutorialMove))
            {
                PieceController tutorialPiece = baseState.GetActualPiece(tutorialMove.PieceId);
                if (tutorialPiece != null)
                {
                    PieceManager.Instance.TryMovePiece(tutorialPiece, tutorialMove.Target);
                    return true;
                }
            }
        }

        // 실수 확률 계산: 지능 10이면 0%, 1이면 50%
        float errorChance = GetErrorChance(ruleMode);
        bool makeMistake = Random.value < errorChance;

        if (makeMistake && allPossibleMoves.Count > 0)
        {
            MoveInfo randomMove = allPossibleMoves[Random.Range(0, allPossibleMoves.Count)];
            
            PieceController mistakePiece = baseState.GetActualPiece(randomMove.PieceId);
            if (mistakePiece != null)
            {
                PieceManager.Instance.TryMovePiece(mistakePiece, randomMove.Target);
                return true;
            }
        }

        if (!bestMove.HasValue)
        {
            return false;
        }

        PieceController actualPiece = baseState.GetActualPiece(bestMove.PieceId);
        if (actualPiece == null)
        {
            return false;
        }

        PieceManager.Instance.TryMovePiece(actualPiece, bestMove.Target);
        return true;
    }

    public System.Collections.IEnumerator TryPlayBestEnemyMoveAsync(System.Action<bool> onCompleted)
    {
        if (PieceManager.Instance == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        IReadOnlyList<PieceController> pieces = PieceManager.Instance.Pieces;
        if (pieces == null || pieces.Count == 0)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        BoardState baseState = BoardState.FromPieces(pieces, PieceManager.Instance);
        if (baseState.EnemyPieceIds.Count == 0)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        BestMove bestMove = BestMove.None;
        List<MoveInfo> allPossibleMoves = new List<MoveInfo>();
        int ruleMode = GetActiveRuleMode();

        int evaluatedMoves = 0;
        int movesPerFrame = 6;  // 배치 사이즈: 프레임 분산으로 충분하니 6으로 정상화

        // 난이도에 따라 더 많은 계산
        if (aiIntelligence >= 9)
        {
            movesPerFrame = 10;  // 최고 난이도는 더 정교하게
        }
        else if (aiIntelligence >= 8)
        {
            movesPerFrame = 8;
        }

        for (int i = 0; i < baseState.EnemyPieceIds.Count; i++)
        {
            int pieceId = baseState.EnemyPieceIds[i];
            SimPiece piece = baseState.GetPiece(pieceId);
            List<Vector2Int> legalMoves = GetRootLegalMoves(baseState, piece);
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            {
                int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
                if (cleared == 0 || cleared == 1)
                {
                    legalMoves.RemoveAll(t =>
                    {
                        if (TutorialManager.Instance.IsTargetProtectedFromEnemy(t)) return true;
                        PieceController p = PieceManager.Instance != null ? PieceManager.Instance.GetPieceAt(t) : null;
                        return p != null && !p.IsEnemy;
                    });
                }
                else
                {
                    legalMoves.RemoveAll(t => TutorialManager.Instance.IsTargetProtectedFromEnemy(t));
                }
            }

            for (int m = 0; m < legalMoves.Count; m++)
            {
                Vector2Int target = legalMoves[m];

                allPossibleMoves.Add(new MoveInfo(pieceId, target));

                float score = 0f;
                switch (ruleMode)
                {
                    case RULE_OTHELLO:
                        score = EvaluateOthelloMove(baseState, piece, target, useIntelligenceScaling: true);
                        break;

                    case RULE_STANDARD:
                    default:
                        score = EvaluateStandardMove(baseState, piece, target, useIntelligenceScaling: true);
                        break;
                }

                if (score > bestMove.Score)
                {
                    bestMove = new BestMove(pieceId, target, score);
                }

                evaluatedMoves++;
                if (evaluatedMoves % movesPerFrame == 0)
                {
                    yield return null;
                }
            }
        }

        bool moved = false;

        // 튜토리얼 1, 2스테이지 전용 착수: 적 기물을 플레이어 사정거리 안으로 유도하여 무조건 패배하도록 유도
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive && GameManager.Instance != null && (GameManager.Instance.ClearedStage == 0 || GameManager.Instance.ClearedStage == 1))
        {
            if (TryGetTutorialEnemyMove(baseState, allPossibleMoves, out MoveInfo tutorialMove))
            {
                PieceController tutorialPiece = baseState.GetActualPiece(tutorialMove.PieceId);
                if (tutorialPiece != null)
                {
                    moved = PieceManager.Instance.TryMovePiece(tutorialPiece, tutorialMove.Target);
                }
                onCompleted?.Invoke(moved);
                yield break;
            }
        }

        float errorChance = GetErrorChance(ruleMode);
        bool makeMistake = Random.value < errorChance;

        if (makeMistake && allPossibleMoves.Count > 0)
        {
            MoveInfo randomMove = allPossibleMoves[Random.Range(0, allPossibleMoves.Count)];

            PieceController mistakePiece = baseState.GetActualPiece(randomMove.PieceId);
            if (mistakePiece != null)
            {
                moved = PieceManager.Instance.TryMovePiece(mistakePiece, randomMove.Target);
            }
        }
        else if (bestMove.HasValue)
        {
            PieceController actualPiece = baseState.GetActualPiece(bestMove.PieceId);
            if (actualPiece != null)
            {
                moved = PieceManager.Instance.TryMovePiece(actualPiece, bestMove.Target);
            }
        }

        onCompleted?.Invoke(moved);
    }

    public static int TutorialOpponentTurnCount = 0;

    /// <summary>
    /// 튜토리얼 전용 AI 착수 결정:
    /// - 1스테이지: 3턴 전(1~2턴)에는 안전하게 전진하고, 3턴 후 기물이 잡히는 위치로 진입
    /// - 2스테이지: 프로모션 전에는 폰 전진 경로를 방해하지 않고 안전하게 이동하며, 프로모션 완료 후 즉시 퀸/기물 사정거리로 진입하여 무조건 패배
    /// </summary>
    private bool TryGetTutorialEnemyMove(BoardState baseState, List<MoveInfo> allPossibleMoves, out MoveInfo chosenMove)
    {
        chosenMove = default;
        if (allPossibleMoves == null || allPossibleMoves.Count == 0) return false;

        TutorialOpponentTurnCount++;
        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        bool isStage2 = cleared == 1;

        List<MoveInfo> directCaptureOffers = new List<MoveInfo>();
        List<MoveInfo> safeForwardMoves = new List<MoveInfo>();

        foreach (var move in allPossibleMoves)
        {
            PieceController mover = baseState.GetActualPiece(move.PieceId);
            if (mover == null) continue;

            // 튜토리얼 중 플레이어의 기물을 포획하는 상황 원천 차단 (AI 무조건 패배 보장)
            PieceController targetPiece = PieceManager.Instance != null ? PieceManager.Instance.GetPieceAt(move.Target) : null;
            if (targetPiece != null && !targetPiece.IsEnemy)
            {
                continue;
            }

            // 이동한 위치가 플레이어 기물의 포획 범위(공격 가능 타일)인지 검사
            bool isThreatened = IsTileThreatenedByPlayer(move.Target);
            if (isThreatened)
            {
                directCaptureOffers.Add(move);
            }
            else
            {
                safeForwardMoves.Add(move);
            }
        }

        bool shouldOfferCapture = false;
        if (isStage2)
        {
            // 2스테이지: 프로모션이 완료되었거나 3턴 이상 경과한 경우 무조건 잡히는 위치로 진입
            shouldOfferCapture = (TutorialManager.Instance != null && TutorialManager.Instance.IsPromotionComplete) || TutorialOpponentTurnCount >= 3;
        }
        else
        {
            // 1스테이지: 3턴 이상 경과 시 포획 유도
            shouldOfferCapture = TutorialOpponentTurnCount >= 3;
        }

        // 1. 아직 플레이어에게 잡히지 않고 안전하게 움직여야 하는 단계
        if (!shouldOfferCapture)
        {
            if (safeForwardMoves.Count > 0)
            {
                // 폰의 안전한 전진이 있다면 폰을 우선 이동
                List<MoveInfo> pawnSafeMoves = safeForwardMoves.FindAll(m => {
                    PieceController p = baseState.GetActualPiece(m.PieceId);
                    return p != null && p.Type == PieceType.Pawn;
                });

                List<MoveInfo> candidates = (pawnSafeMoves.Count > 0) ? pawnSafeMoves : safeForwardMoves;

                MoveInfo bestSafeMove = candidates[0];
                float minDistance = float.MaxValue;

                foreach (var move in candidates)
                {
                    float dist = GetMinDistanceToPlayerPieces(move.Target);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestSafeMove = move;
                    }
                }

                chosenMove = bestSafeMove;
                return true;
            }

            if (allPossibleMoves.Count > 0)
            {
                chosenMove = allPossibleMoves[0];
                return true;
            }
        }
        else // 2. 플레이어가 잡을 수 있는 위치로 확정 진입하여 무조건 패배!
        {
            if (directCaptureOffers.Count > 0)
            {
                // 1스테이지에서는 폰 우선 포획 유도
                if (!isStage2)
                {
                    List<MoveInfo> pawnOffers = directCaptureOffers.FindAll(m => {
                        PieceController p = baseState.GetActualPiece(m.PieceId);
                        return p != null && p.Type == PieceType.Pawn;
                    });
                    if (pawnOffers.Count > 0)
                    {
                        chosenMove = pawnOffers[Random.Range(0, pawnOffers.Count)];
                        return true;
                    }
                }

                // 2스테이지 또는 1스테이지 잔여 기물: 잡히는 수 중 랜덤 선택하여 헌납
                chosenMove = directCaptureOffers[Random.Range(0, directCaptureOffers.Count)];
                return true;
            }

            if (safeForwardMoves.Count > 0)
            {
                // 아직 사정거리 밖이라도 플레이어에게 가장 가까이 접근하여 다음 턴에 잡히도록 유도
                MoveInfo bestMove = safeForwardMoves[0];
                float minDistance = float.MaxValue;

                foreach (var move in safeForwardMoves)
                {
                    float dist = GetMinDistanceToPlayerPieces(move.Target);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestMove = move;
                    }
                }

                chosenMove = bestMove;
                return true;
            }

            if (allPossibleMoves.Count > 0)
            {
                chosenMove = allPossibleMoves[0];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 해당 타일이 현재 보드 위의 아군(플레이어) 기물들에게 공격/포획 가능한 위치인지 판정합니다.
    /// </summary>
    private bool IsTileThreatenedByPlayer(Vector2Int tile)
    {
        if (PieceManager.Instance == null) return false;

        var pieces = PieceManager.Instance.Pieces;
        if (pieces == null) return false;

        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p == null || p.IsEnemy || p.CurrentLocation != PieceLocation.Board || !p.gridPosition.HasValue)
                continue;

            Vector2Int pos = p.gridPosition.Value;

            switch (p.Type)
            {
                case PieceType.Pawn:
                    // 폰은 전방 대각선 1칸 포획 (플레이어 전방은 y + 1)
                    if (tile == new Vector2Int(pos.x - 1, pos.y + 1) || tile == new Vector2Int(pos.x + 1, pos.y + 1))
                    {
                        return true;
                    }
                    break;

                case PieceType.King:
                    // 킹은 인접 8방향 1칸
                    int kdx = Mathf.Abs(tile.x - pos.x);
                    int kdy = Mathf.Abs(tile.y - pos.y);
                    if (kdx <= 1 && kdy <= 1 && (kdx > 0 || kdy > 0))
                    {
                        return true;
                    }
                    break;

                case PieceType.Knight:
                    int ndx = Mathf.Abs(tile.x - pos.x);
                    int ndy = Mathf.Abs(tile.y - pos.y);
                    if ((ndx == 1 && ndy == 2) || (ndx == 2 && ndy == 1))
                    {
                        return true;
                    }
                    break;

                default:
                    // 퀸, 룩, 비숍 등 기타 기물은 후보 수 목록 검사
                    var moves = p.GetCandidateMoves();
                    if (moves != null && moves.Contains(tile))
                    {
                        return true;
                    }
                    break;
            }
        }

        return false;
    }

    private float GetMinDistanceToPlayerPieces(Vector2Int tile)
    {
        if (PieceManager.Instance == null) return float.MaxValue;

        var pieces = PieceManager.Instance.Pieces;
        if (pieces == null) return float.MaxValue;

        float minDist = float.MaxValue;
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p == null || p.IsEnemy || p.CurrentLocation != PieceLocation.Board || !p.gridPosition.HasValue)
                continue;

            float dist = Vector2Int.Distance(tile, p.gridPosition.Value);
            if (dist < minDist)
            {
                minDist = dist;
            }
        }

        return minDist;
    }

    private static List<Vector2Int> GetRootLegalMoves(BoardState state, SimPiece piece)
    {
        PieceController actualPiece = state.GetActualPiece(piece.Id);
        if (actualPiece != null && (actualPiece.IsBlind || actualPiece.IsConfused))
            return actualPiece.GetCandidateMoves();

        return state.GetLegalMoves(piece);
    }

    private float EvaluateStandardMove(
        BoardState baseState,
        SimPiece piece,
        Vector2Int target,
        bool useIntelligenceScaling)
    {
        bool wasThreatened = IsThreatened(baseState, piece, false);

        BoardState after = baseState.Clone();
        SimPiece captured = after.MovePiece(piece.Id, target);
        SimPiece movedPiece = after.GetPiece(piece.Id);

        int pieceValue = GetPieceValue(piece.Type);
        float score = 0f;

        if (captured != null && !captured.IsEnemy)
        {
            int capturedValue = GetPieceValue(captured.Type);
            score += immediateCaptureScore * capturedValue;
        }

        bool isThreatenedAfter = IsThreatened(after, movedPiece, false);
        if (wasThreatened && !isThreatenedAfter)
        {
            score += escapeThreatScore * pieceValue;
        }

        if (isThreatenedAfter)
        {
            score -= threatenedPenalty * pieceValue;
        }

        // 적 폰 승급(퀸)에 큰 점수 부여
        if (movedPiece != null && movedPiece.IsEnemy && movedPiece.Type == PieceType.Queen && piece.Type == PieceType.Pawn)
        {
            score += 25f;
        }

        bool attackerIsEnemy = capturePerspective == CapturePerspective.EnemyCapturesPlayer;
        bool targetIsEnemy = capturePerspective == CapturePerspective.PlayerCapturesEnemy;

        if (!useIntelligenceScaling)
        {
            if (CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 2))
            {
                score += twoTurnCaptureScore;
            }

            if (CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 3))
            {
                score += threeTurnCaptureScore;
            }
        }
        else
        {
            if (aiIntelligence >= 5 && CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 2))
            {
                score += twoTurnCaptureScore;
            }

            if (aiIntelligence >= 7 && CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 3))
            {
                score += threeTurnCaptureScore;
            }

            if (aiIntelligence >= 9 && CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 4))
            {
                score += 0.125f;
            }
        }

        return score;
    }

    /// <summary>
    /// [RULE_OTHELLO] 오셀로의 성역 규칙:
    /// 기존 일반 체스 AI(EvaluateStandardMove)와 완전히 동일한 판단 수준으로
    /// 1. 자신이 상대 기물을 뒤집을 수 있는 위치에 가산점
    /// 2. 자신이 뒤집힐 위기에서 벗어나면 가산점, 뒤집힐 수 있는 위치로 가면 감산점
    /// 3. 기존 로직과 동일하게 다음 턴(2~4턴) 수읽기 적용
    /// </summary>
    private float EvaluateOthelloMove(
        BoardState baseState,
        SimPiece piece,
        Vector2Int target,
        bool useIntelligenceScaling)
    {
        bool wasThreatened = IsThreatened(baseState, piece, false);

        BoardState after = baseState.Clone();
        int flippedPlayerValue = after.MovePieceAndGetOthelloFlipValue(piece.Id, target);
        SimPiece movedPiece = after.GetPiece(piece.Id);

        int pieceValue = GetPieceValue(piece.Type);
        float score = 0f;

        // 1. 자신이 상대 기물을 뒤집을 수 있는 위치에 가산점
        if (flippedPlayerValue > 0)
        {
            score += immediateCaptureScore * flippedPlayerValue;
        }

        // 2. 자신이 뒤집힐 수 있는 위치에 감산 / 뒤집힐 위치에서 탈출 시 가산
        bool isThreatenedAfter = IsThreatened(after, movedPiece, false);
        if (wasThreatened && !isThreatenedAfter)
        {
            score += escapeThreatScore * pieceValue;
        }

        if (isThreatenedAfter)
        {
            score -= threatenedPenalty * pieceValue;
        }

        // 3. 적 폰 승급(퀸)에 큰 점수 부여
        if (movedPiece != null && movedPiece.IsEnemy && movedPiece.Type == PieceType.Queen && piece.Type == PieceType.Pawn)
        {
            score += 25f;
        }

        // 4. 기존 로직과 동일하게 다음 턴(2~4턴) 수읽기 고려
        bool attackerIsEnemy = capturePerspective == CapturePerspective.EnemyCapturesPlayer;
        bool targetIsEnemy = capturePerspective == CapturePerspective.PlayerCapturesEnemy;

        if (!useIntelligenceScaling)
        {
            if (CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 2))
            {
                score += twoTurnCaptureScore;
            }

            if (CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 3))
            {
                score += threeTurnCaptureScore;
            }
        }
        else
        {
            if (aiIntelligence >= 5 && CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 2))
            {
                score += twoTurnCaptureScore;
            }

            if (aiIntelligence >= 7 && CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 3))
            {
                score += threeTurnCaptureScore;
            }

            if (aiIntelligence >= 9 && CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 4))
            {
                score += 0.125f;
            }
        }

        return score;
    }

    private bool IsThreatened(BoardState state, SimPiece target, bool byEnemy)
    {
        if (target == null)
        {
            return false;
        }

        switch (GetActiveRuleMode())
        {
            case RULE_OTHELLO:
                return state.IsThreatenedByOthello(target, byEnemy);

            case RULE_STANDARD:
            default:
                List<int> attackers = byEnemy ? state.EnemyPieceIds : state.PlayerPieceIds;
                for (int i = 0; i < attackers.Count; i++)
                {
                    SimPiece attacker = state.GetPiece(attackers[i]);
                    List<Vector2Int> moves = state.GetLegalMoves(attacker);
                    for (int m = 0; m < moves.Count; m++)
                    {
                        if (moves[m] == target.Position)
                        {
                            return true;
                        }
                    }
                }

                return false;
        }
    }

    public static int GetPieceValue(PieceType type)
    {
        return PieceData.GetPieceScore(type);
    }

    private bool CanCaptureWithin(BoardState state, bool attackerIsEnemy, bool targetIsEnemy, int maxDepth)
    {
        if (maxDepth <= 0)
        {
            return false;
        }

        List<int> attackers = attackerIsEnemy ? state.EnemyPieceIds : state.PlayerPieceIds;
        for (int i = 0; i < attackers.Count; i++)
        {
            SimPiece attacker = state.GetPiece(attackers[i]);
            List<Vector2Int> moves = state.GetLegalMoves(attacker);
            for (int m = 0; m < moves.Count; m++)
            {
                BoardState next = state.Clone();
                SimPiece captured = next.MovePiece(attacker.Id, moves[m]);
                if (captured != null && captured.IsEnemy == targetIsEnemy)
                {
                    return true;
                }

                if (maxDepth > 1 && CanCaptureWithin(next, attackerIsEnemy, targetIsEnemy, maxDepth - 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private readonly struct BestMove
    {
        public static BestMove None => new BestMove(-1, Vector2Int.zero, float.NegativeInfinity);

        public readonly int PieceId;
        public readonly Vector2Int Target;
        public readonly float Score;
        public bool HasValue => PieceId >= 0;

        public BestMove(int pieceId, Vector2Int target, float score)
        {
            PieceId = pieceId;
            Target = target;
            Score = score;
        }
    }

    private readonly struct MoveInfo
    {
        public readonly int PieceId;
        public readonly Vector2Int Target;

        public MoveInfo(int pieceId, Vector2Int target)
        {
            PieceId = pieceId;
            Target = target;
        }
    }

    private sealed class BoardState
    {
        private readonly Dictionary<Vector2Int, int> positions;
        private readonly Dictionary<int, SimPiece> pieces;
        private readonly Dictionary<int, PieceController> actualPieces;

        private readonly GridManager gridManager;
        private readonly Vector2Int minBounds;
        private readonly int boardWidth;
        private readonly int boardHeight;

        public List<int> EnemyPieceIds { get; }
        public List<int> PlayerPieceIds { get; }

        private BoardState(
            Dictionary<Vector2Int, int> positions,
            Dictionary<int, SimPiece> pieces,
            Dictionary<int, PieceController> actualPieces,
            GridManager gridManager,
            Vector2Int minBounds,
            int boardWidth,
            int boardHeight,
            List<int> enemyPieceIds,
            List<int> playerPieceIds)
        {
            this.positions = positions;
            this.pieces = pieces;
            this.actualPieces = actualPieces;
            this.gridManager = gridManager;
            this.minBounds = minBounds;
            this.boardWidth = boardWidth;
            this.boardHeight = boardHeight;
            EnemyPieceIds = enemyPieceIds;
            PlayerPieceIds = playerPieceIds;
        }

        public static BoardState FromPieces(IReadOnlyList<PieceController> scenePieces, PieceManager manager)
        {
            Dictionary<Vector2Int, int> positions = new Dictionary<Vector2Int, int>();
            Dictionary<int, SimPiece> pieces = new Dictionary<int, SimPiece>();
            Dictionary<int, PieceController> actualPieces = new Dictionary<int, PieceController>();
            List<int> enemyPieceIds = new List<int>();
            List<int> playerPieceIds = new List<int>();

            int id = 0;
            for (int i = 0; i < scenePieces.Count; i++)
            {
                PieceController piece = scenePieces[i];
                if (piece == null)
                {
                    continue;
                }

                // 인벤토리에 있는 기물(좌표가 null)은 시뮬레이션에서 제외
                if (piece.CurrentLocation != PieceLocation.Board ||
                    !piece.gridPosition.HasValue ||
                    !manager.gridManager.IsInBounds(piece.gridPosition.Value))
                {
                    continue;
                }

                SimPiece sim = new SimPiece(id, piece.Type, piece.IsEnemy, piece.gridPosition.Value, piece.IsInvincible);
                pieces.Add(id, sim);
                actualPieces.Add(id, piece);
                positions[sim.Position] = id;

                if (sim.IsEnemy)
                {
                    enemyPieceIds.Add(id);
                }
                else
                {
                    playerPieceIds.Add(id);
                }

                id++;
            }

            GridManager gridManager = manager.gridManager;
            if (gridManager == null)
                throw new System.Exception("GridManager reference is required in PieceManager.");
            return new BoardState(
                positions,
                pieces,
                actualPieces,
                gridManager,
                gridManager.gridMinBounds,
                gridManager.boardWidth,
                gridManager.boardHeight,
                enemyPieceIds,
                playerPieceIds);
        }

        public BoardState Clone()
        {
            Dictionary<Vector2Int, int> newPositions = new Dictionary<Vector2Int, int>(positions);
            Dictionary<int, SimPiece> newPieces = new Dictionary<int, SimPiece>();
            foreach (KeyValuePair<int, SimPiece> entry in pieces)
            {
                newPieces.Add(entry.Key, entry.Value.Clone());
            }

            return new BoardState(
                newPositions,
                newPieces,
                actualPieces,
                gridManager,
                minBounds,
                boardWidth,
                boardHeight,
                new List<int>(EnemyPieceIds),
                new List<int>(PlayerPieceIds));
        }

        public SimPiece GetPiece(int id)
        {
            return pieces[id];
        }

        public PieceController GetActualPiece(int id)
        {
            actualPieces.TryGetValue(id, out PieceController piece);
            return piece;
        }

        public List<Vector2Int> GetLegalMoves(SimPiece piece)
        {
            List<Vector2Int> moves = new List<Vector2Int>();
            switch (piece.Type)
            {
                case PieceType.King:
                    AddStepMoves(moves, piece, kingOffsets);
                    break;
                case PieceType.Queen:
                    AddRayMoves(moves, piece, Vector2Int.up);
                    AddRayMoves(moves, piece, Vector2Int.down);
                    AddRayMoves(moves, piece, Vector2Int.left);
                    AddRayMoves(moves, piece, Vector2Int.right);
                    AddRayMoves(moves, piece, new Vector2Int(1, 1));
                    AddRayMoves(moves, piece, new Vector2Int(1, -1));
                    AddRayMoves(moves, piece, new Vector2Int(-1, 1));
                    AddRayMoves(moves, piece, new Vector2Int(-1, -1));
                    break;
                case PieceType.Rook:
                    AddRayMoves(moves, piece, Vector2Int.up);
                    AddRayMoves(moves, piece, Vector2Int.down);
                    AddRayMoves(moves, piece, Vector2Int.left);
                    AddRayMoves(moves, piece, Vector2Int.right);
                    break;
                case PieceType.Bishop:
                    AddRayMoves(moves, piece, new Vector2Int(1, 1));
                    AddRayMoves(moves, piece, new Vector2Int(1, -1));
                    AddRayMoves(moves, piece, new Vector2Int(-1, 1));
                    AddRayMoves(moves, piece, new Vector2Int(-1, -1));
                    break;
                case PieceType.Knight:
                    AddStepMoves(moves, piece, knightOffsets);
                    break;
                case PieceType.Amazon:
                    AddRayMoves(moves, piece, Vector2Int.up);
                    AddRayMoves(moves, piece, Vector2Int.down);
                    AddRayMoves(moves, piece, Vector2Int.left);
                    AddRayMoves(moves, piece, Vector2Int.right);
                    AddRayMoves(moves, piece, new Vector2Int(1, 1));
                    AddRayMoves(moves, piece, new Vector2Int(1, -1));
                    AddRayMoves(moves, piece, new Vector2Int(-1, 1));
                    AddRayMoves(moves, piece, new Vector2Int(-1, -1));
                    AddStepMoves(moves, piece, knightOffsets);
                    break;
                case PieceType.Chancellor:
                    AddRayMoves(moves, piece, Vector2Int.up);
                    AddRayMoves(moves, piece, Vector2Int.down);
                    AddRayMoves(moves, piece, Vector2Int.left);
                    AddRayMoves(moves, piece, Vector2Int.right);
                    AddStepMoves(moves, piece, knightOffsets);
                    break;
                case PieceType.Archbishop:
                    AddRayMoves(moves, piece, new Vector2Int(1, 1));
                    AddRayMoves(moves, piece, new Vector2Int(1, -1));
                    AddRayMoves(moves, piece, new Vector2Int(-1, 1));
                    AddRayMoves(moves, piece, new Vector2Int(-1, -1));
                    AddStepMoves(moves, piece, knightOffsets);
                    break;
                case PieceType.Centaur:
                    AddStepMoves(moves, piece, kingOffsets);
                    AddStepMoves(moves, piece, knightOffsets);
                    break;
                case PieceType.Cannon:
                    AddCannonRayMoves(moves, piece, Vector2Int.up);
                    AddCannonRayMoves(moves, piece, Vector2Int.down);
                    AddCannonRayMoves(moves, piece, Vector2Int.left);
                    AddCannonRayMoves(moves, piece, Vector2Int.right);
                    break;
                case PieceType.Grasshopper:
                    AddGrasshopperRayMoves(moves, piece, Vector2Int.up);
                    AddGrasshopperRayMoves(moves, piece, Vector2Int.down);
                    AddGrasshopperRayMoves(moves, piece, Vector2Int.left);
                    AddGrasshopperRayMoves(moves, piece, Vector2Int.right);
                    AddGrasshopperRayMoves(moves, piece, new Vector2Int(1, 1));
                    AddGrasshopperRayMoves(moves, piece, new Vector2Int(1, -1));
                    AddGrasshopperRayMoves(moves, piece, new Vector2Int(-1, 1));
                    AddGrasshopperRayMoves(moves, piece, new Vector2Int(-1, -1));
                    break;
                case PieceType.Pawn:
                default:
                    AddPawnMoves(moves, piece);
                    break;
            }

            PieceController actualPiece = GetActualPiece(piece.Id);
            if (actualPiece != null)
            {
                foreach (var seal in actualPiece.EquippedSeals)
                {
                    seal.ModifyMoves(ref moves, piece.Position, piece.IsEnemy, (target) => CanMoveTo(piece, target), (target) => IsOccupied(target));
                }
            }

            switch (GetActiveRuleMode())
            {
                case RULE_OTHELLO:
                    moves.RemoveAll(target => IsOccupied(target));
                    break;

                case RULE_STANDARD:
                default:
                    break;
            }

            return moves;
        }

        public SimPiece MovePiece(int id, Vector2Int target)
        {
            return MovePieceInternal(id, target, out _);
        }

        public int MovePieceAndGetOthelloFlipValue(int id, Vector2Int target)
        {
            MovePieceInternal(id, target, out int flippedValue);
            return flippedValue;
        }

        private SimPiece MovePieceInternal(int id, Vector2Int target, out int flippedValue)
        {
            flippedValue = 0;

            if (!pieces.TryGetValue(id, out SimPiece piece))
            {
                return null;
            }

            positions.Remove(piece.Position);
            SimPiece captured = null;

            switch (GetActiveRuleMode())
            {
                case RULE_OTHELLO:
                    piece.Position = target;
                    positions[target] = id;
                    ApplyPawnPromotion(piece, target);

                    List<SimPiece> flipped = new List<SimPiece>();
                    CollectOthelloFlips(piece, target, flipped, ignoreMoverOldPos: false);
                    for (int i = 0; i < flipped.Count; i++)
                    {
                        SimPiece converted = flipped[i];
                        int value = GetPieceValue(converted.Type);
                        flippedValue += value;

                        if (captured == null || value > GetPieceValue(captured.Type))
                        {
                            // 전향되기 전 원래 진영(IsEnemy) 스냅샷을 보존하여 CanCaptureWithin 판정에 사용
                            captured = converted.Clone();
                        }

                        converted.IsEnemy = piece.IsEnemy;
                        if (piece.IsEnemy)
                        {
                            PlayerPieceIds.Remove(converted.Id);
                            if (!EnemyPieceIds.Contains(converted.Id))
                            {
                                EnemyPieceIds.Add(converted.Id);
                            }
                        }
                        else
                        {
                            EnemyPieceIds.Remove(converted.Id);
                            if (!PlayerPieceIds.Contains(converted.Id))
                            {
                                PlayerPieceIds.Add(converted.Id);
                            }
                        }
                    }
                    break;

                case RULE_STANDARD:
                default:
                    if (positions.TryGetValue(target, out int capturedId))
                    {
                        captured = pieces[capturedId];
                        positions.Remove(target);
                        pieces.Remove(capturedId);
                        EnemyPieceIds.Remove(capturedId);
                        PlayerPieceIds.Remove(capturedId);
                    }

                    piece.Position = target;
                    positions[target] = id;
                    ApplyPawnPromotion(piece, target);
                    break;
            }

            return captured;
        }

        private void ApplyPawnPromotion(SimPiece piece, Vector2Int target)
        {
            if (piece.Type != PieceType.Pawn)
            {
                return;
            }

            if (piece.IsEnemy && target.y <= minBounds.y)
            {
                piece.Type = PieceType.Queen;
            }
            else if (!piece.IsEnemy && target.y >= minBounds.y + boardHeight - 1)
            {
                piece.Type = PieceType.Queen;
            }
        }

        public void CollectOthelloFlips(
            SimPiece mover,
            Vector2Int landingPos,
            List<SimPiece> results,
            bool ignoreMoverOldPos = true)
        {
            List<SimPiece> tempTargets = new List<SimPiece>();

            for (int d = 0; d < kingOffsets.Length; d++)
            {
                Vector2Int dir = kingOffsets[d];
                tempTargets.Clear();
                Vector2Int current = landingPos + dir;

                while (IsInBounds(current) && !IsDestroyedCell(current))
                {
                    if (ignoreMoverOldPos && current == mover.Position)
                    {
                        tempTargets.Clear();
                        break;
                    }

                    SimPiece target = GetPieceAt(current);
                    if (target == null)
                    {
                        tempTargets.Clear();
                        break;
                    }

                    if (target.IsEnemy != mover.IsEnemy)
                    {
                        tempTargets.Add(target);
                    }
                    else
                    {
                        if (tempTargets.Count > 0)
                        {
                            for (int i = 0; i < tempTargets.Count; i++)
                            {
                                if (!results.Contains(tempTargets[i]))
                                {
                                    results.Add(tempTargets[i]);
                                }
                            }
                        }
                        break;
                    }

                    current += dir;
                }
            }
        }

        public bool IsThreatenedByOthello(SimPiece target, bool byEnemy)
        {
            if (target == null)
            {
                return false;
            }

            List<int> attackers = byEnemy ? EnemyPieceIds : PlayerPieceIds;
            List<SimPiece> flips = new List<SimPiece>();

            for (int i = 0; i < attackers.Count; i++)
            {
                SimPiece attacker = GetPiece(attackers[i]);
                List<Vector2Int> moves = GetLegalMoves(attacker);
                for (int m = 0; m < moves.Count; m++)
                {
                    flips.Clear();
                    CollectOthelloFlips(attacker, moves[m], flips);
                    for (int f = 0; f < flips.Count; f++)
                    {
                        if (flips[f].Id == target.Id)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public int GetMaxImmediateOthelloFlipValue(bool byEnemy)
        {
            List<int> attackers = byEnemy ? EnemyPieceIds : PlayerPieceIds;
            List<SimPiece> flips = new List<SimPiece>();
            int maxFlipValue = 0;

            for (int i = 0; i < attackers.Count; i++)
            {
                SimPiece attacker = GetPiece(attackers[i]);
                List<Vector2Int> moves = GetLegalMoves(attacker);
                for (int m = 0; m < moves.Count; m++)
                {
                    flips.Clear();
                    CollectOthelloFlips(attacker, moves[m], flips);
                    if (flips.Count == 0)
                    {
                        continue;
                    }

                    int value = 0;
                    for (int f = 0; f < flips.Count; f++)
                    {
                        value += GetPieceValue(flips[f].Type);
                    }

                    if (value > maxFlipValue)
                    {
                        maxFlipValue = value;
                    }
                }
            }

            return maxFlipValue;
        }

        private void AddStepMoves(List<Vector2Int> moves, SimPiece piece, Vector2Int[] offsets)
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2Int target = piece.Position + offsets[i];
                if (CanMoveTo(piece, target))
                {
                    moves.Add(target);
                }
            }
        }

        private void AddRayMoves(List<Vector2Int> moves, SimPiece piece, Vector2Int direction)
        {
            Vector2Int current = piece.Position + direction;
            while (IsInBounds(current))
            {
                if (IsDestroyedCell(current))
                {
                    break;
                }

                SimPiece pieceAt = GetPieceAt(current);
                if (pieceAt == null)
                {
                    if (CanMoveTo(piece, current))
                    {
                        moves.Add(current);
                    }
                    current += direction;
                    continue;
                }

                if (pieceAt.IsEnemy != piece.IsEnemy && CanMoveTo(piece, current))
                {
                    moves.Add(current);
                }

                break;
            }
        }

        private void AddCannonRayMoves(List<Vector2Int> moves, SimPiece piece, Vector2Int direction)
        {
            Vector2Int current = piece.Position + direction;
            bool jumped = false;

            while (IsInBounds(current))
            {
                if (IsDestroyedCell(current))
                {
                    break;
                }

                SimPiece pieceAt = GetPieceAt(current);

                if (!jumped)
                {
                    if (pieceAt == null)
                    {
                        if (CanMoveTo(piece, current))
                        {
                            moves.Add(current);
                        }
                        current += direction;
                    }
                    else
                    {
                        jumped = true;
                        current += direction;
                    }
                }
                else
                {
                    if (pieceAt == null)
                    {
                        current += direction;
                    }
                    else
                    {
                        if (pieceAt.IsEnemy != piece.IsEnemy && CanMoveTo(piece, current))
                        {
                            moves.Add(current);
                        }
                        break;
                    }
                }
            }
        }

        private void AddGrasshopperRayMoves(List<Vector2Int> moves, SimPiece piece, Vector2Int direction)
        {
            Vector2Int current = piece.Position + direction;

            while (IsInBounds(current))
            {
                if (IsDestroyedCell(current))
                {
                    break;
                }

                SimPiece pieceAt = GetPieceAt(current);
                if (pieceAt != null)
                {
                    Vector2Int landingPos = current + direction;
                    if (IsInBounds(landingPos) && !IsDestroyedCell(landingPos))
                    {
                        SimPiece landingPiece = GetPieceAt(landingPos);
                        if (landingPiece == null)
                        {
                            if (CanMoveTo(piece, landingPos))
                            {
                                moves.Add(landingPos);
                            }
                        }
                        else if (landingPiece.IsEnemy != piece.IsEnemy)
                        {
                            if (CanMoveTo(piece, landingPos))
                            {
                                moves.Add(landingPos);
                            }
                        }
                    }
                    break;
                }

                current += direction;
            }
        }

        private void AddPawnMoves(List<Vector2Int> moves, SimPiece piece)
        {
            int dirY = piece.IsEnemy ? -1 : 1;
            Vector2Int forward = new Vector2Int(0, dirY);

            // 1. 1칸 전진 (빈 칸일 때만)
            Vector2Int oneStep = piece.Position + forward;
            if (IsInBounds(oneStep) && !IsDestroyedCell(oneStep) && !IsOccupied(oneStep))
            {
                moves.Add(oneStep);
            }

            // 2. 대각선 전방 포획
            switch (GetActiveRuleMode())
            {
                case RULE_OTHELLO:
                    break;

                case RULE_STANDARD:
                default:
                    Vector2Int[] captureOffsets =
                    {
                        new Vector2Int(-1, dirY),
                        new Vector2Int(1, dirY)
                    };

                    for (int i = 0; i < captureOffsets.Length; i++)
                    {
                        Vector2Int diagTarget = piece.Position + captureOffsets[i];
                        if (!IsInBounds(diagTarget) || IsDestroyedCell(diagTarget))
                        {
                            continue;
                        }

                        SimPiece pieceAt = GetPieceAt(diagTarget);
                        if (pieceAt != null && pieceAt.IsEnemy != piece.IsEnemy)
                        {
                            moves.Add(diagTarget);
                        }
                    }
                    break;
            }
        }

        private bool IsDestroyedCell(Vector2Int target)
        {
            if (gridManager == null)
            {
                return false;
            }

            GridCell cell = gridManager.GetGridCell(target);
            return cell != null && cell.isDestroyed;
        }

        private bool IsInBounds(Vector2Int position)
        {
            return position.x >= minBounds.x && position.x < minBounds.x + boardWidth &&
                   position.y >= minBounds.y && position.y < minBounds.y + boardHeight;
        }

        private bool CanMoveTo(SimPiece piece, Vector2Int target)
        {
            if (!IsInBounds(target) || IsDestroyedCell(target))
            {
                return false;
            }

            SimPiece pieceAt = GetPieceAt(target);
            if (pieceAt == null)
            {
                return true;
            }

            switch (GetActiveRuleMode())
            {
                case RULE_OTHELLO:
                    return false;

                case RULE_STANDARD:
                default:
                    if (pieceAt.IsInvincible)
                    {
                        return false;
                    }

                    return pieceAt.IsEnemy != piece.IsEnemy;
            }
        }

        private bool IsOccupied(Vector2Int target)
        {
            return GetPieceAt(target) != null;
        }

        private SimPiece GetPieceAt(Vector2Int position)
        {
            if (positions.TryGetValue(position, out int id))
            {
                return pieces[id];
            }

            return null;
        }

        private static readonly Vector2Int[] kingOffsets =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        private static readonly Vector2Int[] knightOffsets =
        {
            new Vector2Int(2, 1),
            new Vector2Int(2, -1),
            new Vector2Int(-2, 1),
            new Vector2Int(-2, -1),
            new Vector2Int(1, 2),
            new Vector2Int(1, -2),
            new Vector2Int(-1, 2),
            new Vector2Int(-1, -2)
        };
    }

    private sealed class SimPiece
    {
        public int Id { get; }
        public PieceType Type { get; set; }
        public bool IsEnemy { get; set; }
        public bool IsInvincible { get; }
        public Vector2Int Position { get; set; }

        public SimPiece(int id, PieceType type, bool isEnemy, Vector2Int position, bool isInvincible)
        {
            Id = id;
            Type = type;
            IsEnemy = isEnemy;
            IsInvincible = isInvincible;
            Position = position;
        }

        public SimPiece Clone()
        {
            return new SimPiece(Id, Type, IsEnemy, Position, IsInvincible);
        }
    }
}
