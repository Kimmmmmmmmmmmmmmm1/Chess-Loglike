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
            List<Vector2Int> legalMoves = baseState.GetLegalMoves(piece);

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
            List<Vector2Int> legalMoves = baseState.GetLegalMoves(piece);

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

                // 2. 초기 2칸 전진
                int minY = minBounds.y;
                int maxY = minY + boardHeight - 1;
                bool isStartRank = piece.IsEnemy
                    ? (piece.Position.y >= maxY - 1)
                    : (piece.Position.y <= minY + 1);

                Vector2Int twoStep = piece.Position + (forward * 2);
                if (isStartRank && IsInBounds(twoStep) && !IsDestroyedCell(twoStep) && !IsOccupied(twoStep))
                {
                    moves.Add(twoStep);
                }
            }

            // 3. 대각선 전방 포획
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
