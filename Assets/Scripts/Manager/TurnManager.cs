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
    public bool IsPlayerTurn => currentTurn == TurnOwner.Player;
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

    public void OnPieceCaptured()
    {
        turnsSinceLastCapture = -1;
    }

    public void AdvanceTurn(bool moveMade = true)
    {
        if (moveMade)
        {
            consecutiveSkips = 0;
        }
        else
        {
            consecutiveSkips++;
            if (consecutiveSkips >= 2)
            {
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
        
        // 플레이어 턴일 때 움직일 수 있는 기물이 없으면 턴 스킵
        if (PieceManager.Instance != null && !PieceManager.Instance.HasAnyPlayerMoves())
        {
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
