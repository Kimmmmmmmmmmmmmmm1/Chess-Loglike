using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 보스 스테이지를 관리하는 매니저
/// StageManager와 유사하게 Battle 상태에서 BossData를 로드합니다.
/// </summary>
public class BossManager : MonoBehaviour
{
    public static BossManager Instance { get; private set; }

    private List<BossData> allBossData;
    private BossData currentBossData;
    
    public string CurrentBossName => currentBossData != null ? currentBossData.bossName : "";
    public Sprite CurrentBossSprite => currentBossData != null ? currentBossData.bossSprite : null;
    
    private List<BaseSkill> activeSkills = new List<BaseSkill>();
    private SkillExecutionContext skillContext;
    private int opponentTurnCount = 0;
    private bool bossCleared = false;

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
        // Resources/Bosses 폴더에서 모든 BossData 로드
        allBossData = Resources.LoadAll<BossData>("Bosses").ToList();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged += OnGameFlowStateChanged;
            
            // 이미 Battle 상태라면 보스 노드인지 확인 후 초기화 진행
            if (GameManager.Instance.CurrentFlowState == GameFlowState.Battle)
            {
                MapManager mapManager = MapManager.Instance;
                if (mapManager != null && mapManager.CurrentNode != null && mapManager.CurrentNode.type == NodeType.Boss)
                {
                    SetupBoss();
                }
            }
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
        }
        
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnOpponentTurnStarted += ExecuteSkillsForTurn;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= OnGameFlowStateChanged;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
        
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnOpponentTurnStarted -= ExecuteSkillsForTurn;
        }
    }

    private void OnGameFlowStateChanged(GameFlowState newState)
    {
        // Battle 상태로 진입할 때 보스 노드인지 확인
        if (newState == GameFlowState.Battle)
        {
            // 현재 노드가 보스인지 확인
            MapManager mapManager = MapManager.Instance;
            
            if (mapManager != null && mapManager.CurrentNode != null && mapManager.CurrentNode.type == NodeType.Boss)
            {
                SetupBoss();
            }
            else
            {
                ClearSkills();
            }
        }
        else if (newState != GameFlowState.Battle)
        {
            // Battle을 벗어나면 스킬 정리
            ClearSkills();
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.GamePlay)
        {
            opponentTurnCount = 0;
            bossCleared = false;
        }
        else if (newState == GameStateManager.GameState.Win && !bossCleared && currentBossData != null)
        {
            // 보스전 클리어 감지 (currentBossData가 있을 때만 보스 스테이지)
            if (GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.Battle)
            {
                bossCleared = true;
                GameManager.Instance.OnBossCleared();
            }
        }
    }

    private void SetupBoss()
    {
        if (allBossData == null || allBossData.Count == 0) 
        {
            return;
        }

        // 다음 보스의 난이도: clearedBosses + 1 (난이도 1부터 시작)
        int bossDifficulty = GameManager.Instance.ClearedBosses + 1;

        // 현재 난이도의 보스 찾기 (정확히 일치하는 난이도)
        var candidates = allBossData.Where(data => data.difficulty == bossDifficulty).ToList();

        // 난이도에 맞는 보스가 없으면 최대 난이도의 보스 사용
        if (candidates.Count == 0)
        {
            var allOrdered = allBossData.OrderBy(data => data.difficulty).ToList();
            if (allOrdered.Count > 0)
            {
                candidates = new List<BossData> { allOrdered[allOrdered.Count - 1] };
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        currentBossData = candidates[Random.Range(0, candidates.Count)];
        InitializeBoss();
    }

    private void InitializeBoss()
    {
        if (currentBossData == null)
        {
            return;
        }

        // 보스 기물을 PieceSpawner에 설정
        if (PieceSpawner.Instance != null)
        {
            if (currentBossData.bossPieces != null)
            {
                PieceSpawner.Instance.enemyPieces = new List<PieceSpawner.PieceSpawnInfo>(currentBossData.bossPieces);
            }
            else
            {
                PieceSpawner.Instance.enemyPieces = new List<PieceSpawner.PieceSpawnInfo>();
            }
        }

        activeSkills.Clear();
        
        // 스킬 가져오기 및 초기화
        foreach (var skillData in currentBossData.skills)
        {
            if (skillData != null)
            {
                BaseSkill skill = skillData.GetComponent<BaseSkill>();
                if (skill == null)
                {
                    // 프리팹이면 인스턴스화
                    GameObject skillObj = Instantiate(skillData.gameObject);
                    skill = skillObj.GetComponent<BaseSkill>();
                }

                if (skill != null)
                {
                    activeSkills.Add(skill);
                }
            }
        }

        // 스킬 실행 컨텍스트 설정
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        PieceManager pieceManager = PieceManager.Instance;

        if (gridManager == null)
        {
            return;
        }

        if (pieceManager == null)
        {
            return;
        }

        skillContext = new SkillExecutionContext(gridManager, pieceManager, 0);

        // 모든 스킬에 컨텍스트 설정 및 유효성 검사
        foreach (var skill in activeSkills)
        {
            skill.SetExecutionContext(skillContext);
            if (!skill.ValidateSkill())
            {
            }
        }

        // 보스 등장 연출 (보스 전용 시퀀스: 그리드 하향 -> 보스 스프라이트 팝 -> 이름 연출)
        if (PresentationManager.Instance != null)
        {
            PresentationManager.Instance.PlayBossEntrancePresentation(currentBossData.bossName, currentBossData.bossTitle, currentBossData.bossSprite, null);
        }
    }



    /// <summary>
    /// 상대 턴 시작 시 호출되며, 실행될 스킬들을 실행합니다.
    /// </summary>
    private void ExecuteSkillsForTurn()
    {
        if (skillContext == null || GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
        {
            return;
        }

        opponentTurnCount++;
        skillContext.currentTurnCount = opponentTurnCount;

        foreach (var skill in activeSkills)
        {
            if (skill != null && skill.ShouldExecute(opponentTurnCount))
            {
                skill.ExecuteSkill();
            }
        }
    }

    private void ClearSkills()
    {
        activeSkills.Clear();
        skillContext = null;
        opponentTurnCount = 0;
        bossCleared = false;
    }

    /// <summary>
    /// 현재 활성 스킬 목록을 반환합니다.
    /// </summary>
    public List<BaseSkill> GetActiveSkills()
    {
        return new List<BaseSkill>(activeSkills);
    }

    /// <summary>
    /// 현재 적의 턴 카운트를 반환합니다.
    /// </summary>
    public int GetOpponentTurnCount()
    {
        return opponentTurnCount;
    }
}

