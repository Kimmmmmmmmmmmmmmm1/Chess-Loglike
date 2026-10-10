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

    [SerializeField] private TurnOwner currentTurn = TurnOwner.Opponent;

    public TurnOwner CurrentTurn => currentTurn;
    public bool IsPlayerTurn => currentTurn == TurnOwner.Player && (PiecePromotionManager.Instance == null || !PiecePromotionManager.Instance.IsPromoting);
    [SerializeField] private float waitTime = 0.5f;
    private int consecutiveSkips = 0;
    [SerializeField] private int turnsSinceLastCapture = 0;
    private const int MaxTurnsWithoutCapture = 15;

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
            consecutiveSkips = 0;
        }
        else if (newState == GameStateManager.GameState.GamePlay)
        {
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
                // 양측 모두 이동 불가 (스테일메이트/교착)
                Debug.LogWarning("[TurnManager] Consecutive skips >= 2 (교착 상태/스테일메이트)");
                GameStateManager.Instance?.ChangeState(GameStateManager.GameState.GameOver);
                return;
            }
        }

        if (currentTurn == TurnOwner.Player)
        {
            turnsSinceLastCapture++;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecordPlayerTurnProgress();
            }
            int turnsLeft = MaxTurnsWithoutCapture - turnsSinceLastCapture;

            if (turnsLeft <= 0)
            {
                // 턴 제한 초과 (교착 상태)
                Debug.LogWarning("[TurnManager] Max turns without capture reached (턴 제한 교착 상태)");
                GameStateManager.Instance?.ChangeState(GameStateManager.GameState.GameOver);
                return;
            }
            else if (turnsLeft <= 5)
            {
            }
        }

        currentTurn = currentTurn == TurnOwner.Player ? TurnOwner.Opponent : TurnOwner.Player;

        if (currentTurn == TurnOwner.Opponent)
        {
            StartCoroutine(StartOpponentTurn());
        }
        else
        {
            CheckPlayerTurn();
        }
    }

    private void CheckPlayerTurn()
    {
        // 플레이어 턴 시작 이벤트 발동
        OnPlayerTurnStarted?.Invoke();
        
        // 플레이어 턴일 때 움직일 수 있는 기물이 없으면 (스테일메이트 가능성)
        if (PieceManager.Instance != null && !PieceManager.Instance.HasAnyPlayerMoves())
        {
            // 플레이어 기물이 남아있는데 이동이 불가능한 경우 스테일메이트 처리
            int playerPieceCount = 0;
            foreach (var p in PieceManager.Instance.PlayerPieces)
            {
                if (p != null && p.CurrentLocation == PieceLocation.Board) playerPieceCount++;
            }

            if (playerPieceCount > 0)
            {
                Debug.LogWarning("[TurnManager] Player has pieces but no legal moves (플레이어 스테일메이트)");
            }

            StartCoroutine(SkipPlayerTurn());
        }
    }

    private IEnumerator SkipPlayerTurn()
    {
        yield return new WaitForSeconds(0.5f);
        AdvanceTurn(false);
    }

    private IEnumerator StartOpponentTurn()
    {
        // 캐시 프리워밍: 적 턴 시작 전에 위치 캐시를 미리 구축하여 BoardEvaluator의 성능 개선
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.EnsurePositionCacheReady();
        }

        yield return new WaitForSeconds(waitTime);

        if (GameManager.Instance != null && GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
            yield break;

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
            yield break;
        
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
