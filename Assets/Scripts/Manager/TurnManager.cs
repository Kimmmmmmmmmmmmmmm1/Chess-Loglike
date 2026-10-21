using System.Collections;
using UnityEngine;
using System;

public enum TurnOwner
{
    Player,
    Opponent
}

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }
    
    public event Action OnOpponentTurnStarted;
    public event Action OnPlayerTurnStarted;
    public event Action<TurnOwner> OnTurnAdvanced; // 새 턴의 턴 소유자 전달

    [SerializeField] private TurnOwner currentTurn = TurnOwner.Opponent;

    public TurnOwner CurrentTurn => currentTurn;
    public bool IsPlayerTurn => currentTurn == TurnOwner.Player && (PiecePromotionManager.Instance == null || !PiecePromotionManager.Instance.IsPromoting);
    [SerializeField] private float waitTime = 0.5f;
    private int consecutiveSkips = 0;
    [SerializeField] private int turnsSinceLastCapture = 0;
    [SerializeField] private int completedBattleTurns = 0;
    private bool skipNextOpponentTurn = false;

    public bool IsOpponentTurnSkipPending => skipNextOpponentTurn;
    public int CompletedBattleTurns => completedBattleTurns;

    public void EndTurn()
    {
        AdvanceTurn(true);
    }

    /// <summary>
    /// 상대의 다음 턴을 1회 건너뛰도록 예약합니다. (액티브 유물 등)
    /// </summary>
    public bool RequestSkipNextOpponentTurn()
    {
        if (skipNextOpponentTurn) return false;
        skipNextOpponentTurn = true;
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay)
            {
                OnGameStateChanged(GameStateManager.GameState.GamePlay);
            }
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Prepare)
        {
            currentTurn = TurnOwner.Opponent;
            ClearPositionHistory();
            turnsSinceLastCapture = 0;
            completedBattleTurns = 0;
            consecutiveSkips = 0;
            skipNextOpponentTurn = false;
        }
        else if (newState == GameStateManager.GameState.GamePlay)
        {
            completedBattleTurns = 0;
            ArtifactEffectRegistry.OnBattleStarted();
            if (currentTurn == TurnOwner.Player)
            {
                CheckPlayerTurn();
            }
            else
            {
                StartCoroutine(StartOpponentTurn());
            }
        }
    }

    private System.Collections.Generic.Dictionary<string, int> positionHistory = new System.Collections.Generic.Dictionary<string, int>();

    private void ClearPositionHistory()
    {
        positionHistory.Clear();
    }

    private void RecordBoardPosition()
    {
        if (PieceManager.Instance == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(currentTurn == TurnOwner.Player ? "P:" : "O:");

        var pieces = PieceManager.Instance.Pieces;
        var sortedPieces = new System.Collections.Generic.List<PieceController>();
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p != null && p.CurrentLocation == PieceLocation.Board && p.GridPosition.HasValue)
            {
                sortedPieces.Add(p);
            }
        }

        sortedPieces.Sort((a, b) =>
        {
            int cmpX = a.GridPosition.Value.x.CompareTo(b.GridPosition.Value.x);
            if (cmpX != 0) return cmpX;
            return a.GridPosition.Value.y.CompareTo(b.GridPosition.Value.y);
        });

        for (int i = 0; i < sortedPieces.Count; i++)
        {
            var p = sortedPieces[i];
            sb.Append($"{p.PieceType}{(p.IsEnemy ? "E" : "A")}_{p.GridPosition.Value.x},{p.GridPosition.Value.y};");
        }

        string snapshot = sb.ToString();
        if (positionHistory.TryGetValue(snapshot, out int count))
        {
            positionHistory[snapshot] = count + 1;
            if (positionHistory[snapshot] >= 3)
            {
                if (OthelloArtifactSystem.TryResolveMajorityVictory("동형반복 교착 상태"))
                {
                    return;
                }

                // 3회 동형반복 무승부/교착 상태 처리 -> 교착 판정으로 GameOver 처리
                Debug.LogWarning("[TurnManager] Threefold repetition detected (동형반복 3회 감지: 무승부/교착 상태)");
                GameStateManager.Instance?.ChangeState(GameStateManager.GameState.GameOver);
                return;
            }
        }
        else
        {
            positionHistory[snapshot] = 1;
        }
    }

    public void OnPieceCaptured()
    {
        turnsSinceLastCapture = -1;
        ClearPositionHistory(); // 캡처가 일어나면 보드 국면이 비가역적으로 변경되므로 초기화
    }

    public void AdvanceTurn(bool moveMade = true)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            return;
        }

        if (moveMade)
        {
            consecutiveSkips = 0;
            RecordBoardPosition();
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
            {
                return;
            }
        }
        else
        {
            consecutiveSkips++;
            if (consecutiveSkips >= 2)
            {
                if (OthelloArtifactSystem.TryResolveMajorityVictory("유효 이동 불가(교착 상태)"))
                {
                    return;
                }

                // 양측 모두 이동 불가 (스테일메이트/교착)
                Debug.LogWarning("[TurnManager] Consecutive skips >= 2 (교착 상태/스테일메이트)");
                GameStateManager.Instance?.ChangeState(GameStateManager.GameState.GameOver);
                return;
            }
        }

        if (currentTurn == TurnOwner.Player)
        {
            turnsSinceLastCapture++;
            completedBattleTurns++;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecordPlayerTurnProgress();
            }

            if (OthelloArtifactSystem.IsActive && completedBattleTurns >= OthelloArtifactSystem.MaxTurnLimit)
            {
                if (OthelloArtifactSystem.TryResolveMajorityVictory($"{OthelloArtifactSystem.MaxTurnLimit}턴 종료"))
                {
                    return;
                }
            }
        }

        currentTurn = currentTurn == TurnOwner.Player ? TurnOwner.Opponent : TurnOwner.Player;
        OnTurnAdvanced?.Invoke(currentTurn);
        ArtifactEffectRegistry.OnTurnAdvanced(currentTurn);

        if (currentTurn == TurnOwner.Opponent)
        {
            if (skipNextOpponentTurn)
            {
                skipNextOpponentTurn = false;
                StartCoroutine(SkipOpponentTurnByEffect());
                return;
            }

            StartCoroutine(StartOpponentTurn());
        }
        else
        {
            CheckPlayerTurn();
        }
    }

    /// <summary>
    /// 효과로 상대 턴을 건너뛰고 다시 플레이어 턴으로 돌아옵니다. (교착 판정 카운트에 포함하지 않음)
    /// </summary>
    private IEnumerator SkipOpponentTurnByEffect()
    {
        TurnLogUIManager.Instance?.AddLog("시간의 모래: 상대의 턴을 건너뛰었습니다!", false);

        yield return new WaitForSeconds(waitTime);

        if (GameManager.Instance != null && GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
            yield break;

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
            yield break;

        currentTurn = TurnOwner.Player;
        OnTurnAdvanced?.Invoke(currentTurn);
        ArtifactEffectRegistry.OnTurnAdvanced(currentTurn);
        CheckPlayerTurn();
    }

    private void CheckPlayerTurn()
    {
        // 플레이어 턴 시작 이벤트 발동
        OnPlayerTurnStarted?.Invoke();
        
        // 이동 가능 여부 비동기/안전 확인 (붕괴 연출 및 기물 상태 정리 후 체크)
        StartCoroutine(ValidateAndCheckPlayerMovesRoutine());
    }

    private IEnumerator ValidateAndCheckPlayerMovesRoutine()
    {
        // 보드 상태 및 그리드 파괴 반영을 위해 1프레임 대기
        yield return null;

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            yield break;
        }

        // 보드 위에 아군 기물이 0개라면 무조건 탈락 (GameOver)
        if (PieceManager.Instance != null && PieceManager.Instance.GetPlayerPieceCountOnBoard() <= 0)
        {
            Debug.LogWarning("[TurnManager] 플레이어 턴 검증: 보드에 아군 기물이 없음 -> GameOver");
            GameStateManager.Instance?.ChangeState(GameStateManager.GameState.GameOver);
            yield break;
        }

        // 알까기 유물 보유 시에는 체스 행마법상 이동 가능한 칸이 없어도 알까기로 튕길 수 있으므로 턴을 자동 스킵하지 않음
        if (PieceManager.Instance != null && !PieceFlickLauncher.HasAlkkagiArtifact && !PieceManager.Instance.HasAnyPlayerMoves())
        {
            if (OthelloArtifactSystem.IsActive && !PieceManager.Instance.HasAnyEnemyMoves())
            {
                OthelloArtifactSystem.TryResolveMajorityVictory("양측 유효 이동 불가");
                yield break;
            }

            Debug.LogWarning("[TurnManager] Player has pieces but no legal moves (플레이어 둘 수 있는 수 없음 - 턴 스킵)");
            TurnLogUIManager.Instance?.AddLog("이동 가능한 수가 없어 턴을 넘깁니다.", false);

            yield return new WaitForSeconds(0.6f);
            AdvanceTurn(false);
        }
    }

    private IEnumerator StartOpponentTurn()
    {
        // 상대 턴 시작 시 (AI 이동 전): 보드 상태이나 타일 좌표가 없는 비정상 기물 일괄 제거
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.CleanupInvalidBoardPieces();
            PieceManager.Instance.EnsurePositionCacheReady();
        }

        yield return new WaitForSeconds(waitTime);

        if (GameManager.Instance != null && GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
            yield break;

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
            yield break;

        // 대기 시간 후 AI 기물 평가 및 이동 직전 재검증/정리
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.CleanupInvalidBoardPieces();
            PieceManager.Instance.EnsurePositionCacheReady();
        }
        
        OnOpponentTurnStarted?.Invoke();

        bool moved = false;
        if (BoardEvaluator.Instance != null)
        {
            bool evaluationCompleted = false;
            yield return StartCoroutine(BoardEvaluator.Instance.TryPlayBestEnemyMoveAsync(result =>
            {
                moved = result;
                evaluationCompleted = true;
            }));

            if (!evaluationCompleted)
            {
                moved = false;
            }
        }

        if (!moved)
        {
            AdvanceTurn(false);
        }
    }
}
