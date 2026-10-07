using System.Collections.Generic;
using UnityEngine;

public enum CapturePerspective
{
    PlayerCapturesEnemy,
    EnemyCapturesPlayer
}

public class BoardEvaluator : MonoBehaviour
{
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

        for (int i = 0; i < baseState.EnemyPieceIds.Count; i++)
        {
            int pieceId = baseState.EnemyPieceIds[i];
            SimPiece piece = baseState.GetPiece(pieceId);
            List<Vector2Int> legalMoves = baseState.GetLegalMoves(piece);

            for (int m = 0; m < legalMoves.Count; m++)
            {
                Vector2Int target = legalMoves[m];

                allPossibleMoves.Add(new MoveInfo(pieceId, target));

                bool wasThreatened = IsThreatened(baseState, piece, false);

                BoardState after = baseState.Clone();
                SimPiece captured = after.MovePiece(pieceId, target);
                SimPiece movedPiece = after.GetPiece(pieceId);

                float score = 0f;

                if (captured != null && !captured.IsEnemy)
                {
                    score += immediateCaptureScore;
                }

                bool isThreatenedAfter = IsThreatened(after, movedPiece, false);
                if (wasThreatened && !isThreatenedAfter)
                {
                    score += escapeThreatScore;
                }

                if (isThreatenedAfter)
                {
                    score -= threatenedPenalty;
                }

                bool attackerIsEnemy = capturePerspective == CapturePerspective.EnemyCapturesPlayer;
                bool targetIsEnemy = capturePerspective == CapturePerspective.PlayerCapturesEnemy;

                if (CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 2))
                {
                    score += twoTurnCaptureScore;
                }

                if (CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 3))
                {
                    score += threeTurnCaptureScore;
                }

                if (score > bestMove.Score)
                {
                    bestMove = new BestMove(pieceId, target, score);
                }
            }
        }

        // 실수 확률 계산: 지능 10이면 0%, 1이면 50%
        float errorChance = (10f - aiIntelligence) * (0.5f / 9f);
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

                bool wasThreatened = IsThreatened(baseState, piece, false);

                BoardState after = baseState.Clone();
                SimPiece captured = after.MovePiece(pieceId, target);
                SimPiece movedPiece = after.GetPiece(pieceId);

                float score = 0f;

                if (captured != null && !captured.IsEnemy)
                {
                    score += immediateCaptureScore;
                }

                bool isThreatenedAfter = IsThreatened(after, movedPiece, false);
                if (wasThreatened && !isThreatenedAfter)
                {
                    score += escapeThreatScore;
                }

                if (isThreatenedAfter)
                {
                    score -= threatenedPenalty;
                }

                bool attackerIsEnemy = capturePerspective == CapturePerspective.EnemyCapturesPlayer;
                bool targetIsEnemy = capturePerspective == CapturePerspective.PlayerCapturesEnemy;

                // 난이도별로 lookahead 깊이 조정
                if (aiIntelligence >= 5)
                {
                    if (CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 2))
                    {
                        score += twoTurnCaptureScore;
                    }
                }

                if (aiIntelligence >= 7)
                {
                    if (CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 3))
                    {
                        score += threeTurnCaptureScore;
                    }
                }

                // 최고 난이도: 4턴 이상 계획 능력
                if (aiIntelligence >= 9)
                {
                    if (CanCaptureWithin(after, attackerIsEnemy, targetIsEnemy, 4))
                    {
                        score += 0.125f;  // 4턴 계획 점수
                    }
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

        float errorChance = (10f - aiIntelligence) * (0.5f / 9f);
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

    private bool IsThreatened(BoardState state, SimPiece target, bool byEnemy)
    {
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

        private readonly Vector2Int minBounds;
        private readonly int boardWidth;
        private readonly int boardHeight;

        public List<int> EnemyPieceIds { get; }
        public List<int> PlayerPieceIds { get; }

        private BoardState(
            Dictionary<Vector2Int, int> positions,
            Dictionary<int, SimPiece> pieces,
            Dictionary<int, PieceController> actualPieces,
            Vector2Int minBounds,
            int boardWidth,
            int boardHeight,
            List<int> enemyPieceIds,
            List<int> playerPieceIds)
        {
            this.positions = positions;
            this.pieces = pieces;
            this.actualPieces = actualPieces;
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
                if (!piece.gridPosition.HasValue)
                {
                    continue;
                }

                SimPiece sim = new SimPiece(id, piece.Type, piece.IsEnemy, piece.gridPosition.Value);
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
                case PieceType.Chariot:
                    AddRayMoves(moves, piece, Vector2Int.up);
                    AddRayMoves(moves, piece, Vector2Int.down);
                    AddRayMoves(moves, piece, Vector2Int.left);
                    AddRayMoves(moves, piece, Vector2Int.right);
                    break;
                case PieceType.Horse:
                    AddHorseMoves(moves, piece);
                    break;
                case PieceType.Elephant:
                    AddElephantMoves(moves, piece);
                    break;
                case PieceType.Cannon:
                    AddCannonMoves(moves, piece);
                    break;
                case PieceType.Soldier:
                default:
                    Vector2Int[] currentOffsets = piece.IsEnemy ? enemySoldierOffsets : playerSoldierOffsets;
                    AddStepMoves(moves, piece, currentOffsets);
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

            return moves;
        }

        public SimPiece MovePiece(int id, Vector2Int target)
        {
            if (!pieces.TryGetValue(id, out SimPiece piece))
            {
                return null;
            }

            positions.Remove(piece.Position);
            SimPiece captured = null;

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
            return captured;
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

        private void AddCannonMoves(List<Vector2Int> moves, SimPiece piece)
        {
            AddCannonRayMoves(moves, piece, Vector2Int.up);
            AddCannonRayMoves(moves, piece, Vector2Int.down);
            AddCannonRayMoves(moves, piece, Vector2Int.left);
            AddCannonRayMoves(moves, piece, Vector2Int.right);
        }

        private void AddCannonRayMoves(List<Vector2Int> moves, SimPiece piece, Vector2Int direction)
        {
            Vector2Int current = piece.Position + direction;
            bool hasScreen = false;

            while (IsInBounds(current))
            {
                SimPiece pieceAt = GetPieceAt(current);

                if (!hasScreen)
                {
                    if (pieceAt != null)
                    {
                        // [규칙 1] 포는 다른 포를 넘을 수 없다.
                        if (pieceAt.Type == PieceType.Cannon)
                        {
                            break;
                        }

                        hasScreen = true;
                    }

                    current += direction;
                    continue;
                }

                if (pieceAt != null)
                {
                    if (pieceAt.IsEnemy != piece.IsEnemy)
                    {
                        // [규칙 2] 포는 다른 포를 잡을 수 없다.
                        if (pieceAt.Type != PieceType.Cannon && CanMoveTo(piece, current))
                        {
                            moves.Add(current);
                        }
                    }

                    break;
                }

                // 파괴된 GridPoint로는 이동 불가
                if (CanMoveTo(piece, current))
                {
                    moves.Add(current);
                }
                current += direction;
            }
        }

        private void AddHorseMoves(List<Vector2Int> moves, SimPiece piece)
        {
            for (int i = 0; i < horseOffsets.Length; i++)
            {
                Vector2Int offset = horseOffsets[i];
                int stepX = offset.x == 0 ? 0 : (int)Mathf.Sign(offset.x);
                int stepY = offset.y == 0 ? 0 : (int)Mathf.Sign(offset.y);

                Vector2Int block = Mathf.Abs(offset.x) == 2 ?
                    new Vector2Int(stepX, 0) :
                    new Vector2Int(0, stepY);

                if (IsOccupied(piece.Position + block))
                {
                    continue;
                }

                Vector2Int target = piece.Position + offset;
                if (CanMoveTo(piece, target))
                {
                    moves.Add(target);
                }
            }
        }

        private void AddElephantMoves(List<Vector2Int> moves, SimPiece piece)
        {
            for (int i = 0; i < elephantOffsets.Length; i++)
            {
                Vector2Int offset = elephantOffsets[i];
                int stepX = offset.x == 0 ? 0 : (int)Mathf.Sign(offset.x);
                int stepY = offset.y == 0 ? 0 : (int)Mathf.Sign(offset.y);

                Vector2Int step1 = Mathf.Abs(offset.x) == 3 ?
                    new Vector2Int(stepX, 0) :
                    new Vector2Int(0, stepY);
                Vector2Int step2 = step1 + new Vector2Int(stepX, stepY);

                if (IsOccupied(piece.Position + step1) || IsOccupied(piece.Position + step2))
                {
                    continue;
                }

                Vector2Int target = piece.Position + offset;
                if (CanMoveTo(piece, target))
                {
                    moves.Add(target);
                }
            }
        }

        private bool IsInBounds(Vector2Int position)
        {
            return position.x >= minBounds.x && position.x < minBounds.x + boardWidth &&
                   position.y >= minBounds.y && position.y < minBounds.y + boardHeight;
        }

        private bool CanMoveTo(SimPiece piece, Vector2Int target)
        {
            if (!IsInBounds(target))
            {
                return false;
            }

            // 파괴된 GridPoint로는 이동 불가
            GridManager gridManager = FindFirstObjectByType<GridManager>();
            if (gridManager != null)
            {
                GridPoint gridPoint = gridManager.GetGridPoint(target);
                if (gridPoint != null && gridPoint.isDestroyed)
                {
                    return false;
                }
            }

            SimPiece pieceAt = GetPieceAt(target);
            if (pieceAt == null)
            {
                return true;
            }

            return pieceAt.IsEnemy != piece.IsEnemy;
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

        private static readonly Vector2Int[] playerSoldierOffsets =
        {
            Vector2Int.up,
            Vector2Int.left,
            Vector2Int.right
        };

        private static readonly Vector2Int[] enemySoldierOffsets =
        {
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        private static readonly Vector2Int[] horseOffsets =
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

        private static readonly Vector2Int[] elephantOffsets =
        {
            new Vector2Int(3, 2),
            new Vector2Int(3, -2),
            new Vector2Int(-3, 2),
            new Vector2Int(-3, -2),
            new Vector2Int(2, 3),
            new Vector2Int(2, -3),
            new Vector2Int(-2, 3),
            new Vector2Int(-2, -3)
        };
    }

    private sealed class SimPiece
    {
        public int Id { get; }
        public PieceType Type { get; }
        public bool IsEnemy { get; }
        public Vector2Int Position { get; set; }

        public SimPiece(int id, PieceType type, bool isEnemy, Vector2Int position)
        {
            Id = id;
            Type = type;
            IsEnemy = isEnemy;
            Position = position;
        }

        public SimPiece Clone()
        {
            return new SimPiece(Id, Type, IsEnemy, Position);
        }
    }
}
