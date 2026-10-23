using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    private List<StageData> allNormalStageData;
    private List<StageData> allEliteStageData;

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
        LoadAllStages();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged += OnGameFlowStateChanged;
            
            // 이미 Battle 상태라면 초기화 진행 (보스전이 아닐 때)
            if (GameManager.Instance.CurrentFlowState == GameFlowState.Battle)
            {
                if (!IsBossStage())
                {
                    SetupStage();
                }
            }
        }
    }

    private void LoadAllStages()
    {
        var loadedStages = Resources.LoadAll<StageData>("Stages").ToList();
        allEliteStageData = loadedStages.Where(s => s != null && (s.isElite || s.name.Contains("Elite"))).ToList();
        allNormalStageData = loadedStages.Where(s => s != null && !s.isElite && !s.name.Contains("Elite") && !s.name.Contains("Tutorial")).ToList();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= OnGameFlowStateChanged;
        }
    }

    private bool IsBossStage()
    {
        // 선형 진행 구조: 10스테이지마다 (스테이지 10, 20... 즉 clearedStage 9, 19...) 보스전
        if (GameManager.Instance != null && (GameManager.Instance.ClearedStage + 1) % 10 == 0)
        {
            return true;
        }

        return false;
    }

    private void OnGameFlowStateChanged(GameFlowState newState)
    {
        // Battle 상태로 진입할 때 스테이지 설정 (보스전이 아닐 때만)
        if (newState == GameFlowState.Battle)
        {
            if (IsBossStage())
            {
                return;
            }

            SetupStage();
        }
    }

    private void SetupStage()
    {
        if (allNormalStageData == null || allEliteStageData == null)
        {
            LoadAllStages();
        }

        int clearedStage = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        int act = clearedStage / 10;
        int subStage = clearedStage % 10; // 0~9

        bool isElite = GameManager.Instance != null && GameManager.Instance.CurrentBattleType == BattleType.Elite;
        TutorialManager tm = TutorialManager.EnsureInstance();
        bool isTutorial = (tm != null && tm.IsTutorialActive)
                       || (clearedStage == 0 && tm != null && tm.ShouldShowTutorial());
        StageData selectedStage = null;

        if (isTutorial && clearedStage == 0)
        {
            selectedStage = Resources.Load<StageData>("Stages/Stage_Tutorial");
            if (selectedStage != null)
            {
                Debug.Log($"[StageManager] 튜토리얼 1스테이지 고정 로드 완료: {selectedStage.name}, 적 기물 수: {selectedStage.enemyPieces?.Count ?? 0}");
            }
            else
            {
                Debug.LogError("[StageManager] Stages/Stage_Tutorial 에셋 로드 실패! Resources 폴더를 확인하세요.");
            }
        }
        else if (isTutorial && clearedStage == 1)
        {
            StageData baseStage = Resources.Load<StageData>("Stages/Stage_Easy_1");
            if (baseStage != null)
            {
                selectedStage = Instantiate(baseStage);
                // 튜토리얼 2스테이지: 폰 승급 경로(x = 1 열)를 확보하기 위해 적 기물 배치 최적화 (적 킹: (-1, 2), 적 폰: (-1, 1))
                selectedStage.enemyPieces = new List<PieceSpawner.PieceSpawnInfo>
                {
                    new PieceSpawner.PieceSpawnInfo { pieceType = PieceType.King, gridCoordinate = new Vector2Int(-1, 2), seals = new List<SealData>() },
                    new PieceSpawner.PieceSpawnInfo { pieceType = PieceType.Pawn, gridCoordinate = new Vector2Int(-1, 1), seals = new List<SealData>() }
                };
                Debug.Log($"[StageManager] 튜토리얼 2스테이지(프로모션) 고정 로드 완료: {selectedStage.name}, 적 기물 수: {selectedStage.enemyPieces?.Count ?? 0}");
            }
            else
            {
                Debug.LogError("[StageManager] Stages/Stage_Easy_1 에셋 로드 실패!");
            }
        }
        else if (isTutorial && clearedStage == 2)
        {
            StageData baseStage = Resources.Load<StageData>("Stages/Stage_Easy_1");
            if (baseStage != null)
            {
                selectedStage = Instantiate(baseStage);
                // 튜토리얼 3스테이지: 낙뢰의 부적으로 파괴할 적 기물 배치 (적 킹: (0, 2), 적 나이트: (1, 1))
                selectedStage.enemyPieces = new List<PieceSpawner.PieceSpawnInfo>
                {
                    new PieceSpawner.PieceSpawnInfo { pieceType = PieceType.King, gridCoordinate = new Vector2Int(0, 2), seals = new List<SealData>() },
                    new PieceSpawner.PieceSpawnInfo { pieceType = PieceType.Knight, gridCoordinate = new Vector2Int(1, 1), seals = new List<SealData>() }
                };
                Debug.Log($"[StageManager] 튜토리얼 3스테이지(낙뢰의 부적) 고정 로드 완료: {selectedStage.name}, 적 기물 수: {selectedStage.enemyPieces?.Count ?? 0}");
            }
            else
            {
                Debug.LogError("[StageManager] Stages/Stage_Easy_1 에셋 로드 실패!");
            }
        }

        if (selectedStage == null)
        {
            if (isElite)
            {
                if (allEliteStageData != null && allEliteStageData.Count > 0)
                {
                    // 엘리트 스테이지: 액트 및 서브스테이지에 따라 난이도(1~3) 가중치 매칭
                    int targetEliteDiff = Mathf.Clamp(act + (subStage >= 5 ? 2 : 1), 1, 3);
                    var candidates = allEliteStageData.Where(d => d.difficulty == targetEliteDiff).ToList();
                    if (candidates.Count == 0)
                    {
                        candidates = allEliteStageData;
                    }
                    selectedStage = candidates[Random.Range(0, candidates.Count)];
                }
            }
            else
            {
                if (allNormalStageData != null && allNormalStageData.Count > 0)
                {
                    // 10스테이지 주기 일반 전투 난이도 배분 (0: 쉬움, 1: 보통, 2: 어려움, 3: 매우어려움)
                    int step = subStage <= 2 ? 0 : (subStage <= 5 ? 1 : 2);
                    int targetDifficulty = Mathf.Clamp(act + step, 0, 3);

                    var candidates = allNormalStageData.Where(data => data.difficulty == targetDifficulty).ToList();
                    if (candidates.Count == 0)
                    {
                        int minDiff = Mathf.Max(0, targetDifficulty - 1);
                        int maxDiff = Mathf.Min(3, targetDifficulty + 1);
                        candidates = allNormalStageData.Where(data => data.difficulty >= minDiff && data.difficulty <= maxDiff).ToList();
                    }

                    if (candidates.Count == 0 && allNormalStageData.Count > 0)
                    {
                        candidates = allNormalStageData;
                    }

                    if (candidates.Count > 0)
                    {
                        selectedStage = candidates[Random.Range(0, candidates.Count)];
                    }
                }
            }
        }

        if (selectedStage != null)
        {
            // PieceSpawner에 적 기물 정보 전달 (엘리트 전용 스테이지 파일에 설계된 기물과 인장 그대로 적용)
            if (PieceSpawner.Instance != null)
            {
                if (selectedStage.enemyPieces != null)
                {
                    PieceSpawner.Instance.enemyPieces = SanitizeEnemyPieces(selectedStage.enemyPieces);
                }
                else
                {
                    PieceSpawner.Instance.enemyPieces = new List<PieceSpawner.PieceSpawnInfo>();
                }
            }
            else
            {
            }

            GridManager gridManager = PieceManager.Instance != null
                ? PieceManager.Instance.gridManager
                : FindFirstObjectByType<GridManager>();
            if (gridManager == null)
            {
                gridManager = FindFirstObjectByType<GridManager>();
            }

            if (gridManager != null)
            {
                gridManager.SetBoardSize(
                    selectedStage.boardSize,
                    new Vector2Int(selectedStage.minX, selectedStage.minY));
            }

            PieceManager.Instance?.SetHideEnemyTerritoryDuringPrepare(
                selectedStage.hideEnemyTerritoryDuringPrepare);
        }
        else
        {
        }
    }

    /// <summary>
    /// 튜토리얼 스킵 시 현재 스테이지를 일반 스테이지로 재설정하고 적 기물을 재배치합니다.
    /// </summary>
    public void ReloadNormalStage()
    {
        if (PieceManager.Instance != null)
        {
            var enemies = new List<PieceController>(PieceManager.Instance.EnemyPieces);
            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }
        }

        SetupStage();

        if (PieceSpawner.Instance != null)
        {
            PieceSpawner.Instance.SpawnEnemyPieces();
        }

        GridManager gridManager = PieceManager.Instance != null
            ? PieceManager.Instance.gridManager
            : FindFirstObjectByType<GridManager>();
        if (gridManager != null && GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare)
        {
            gridManager.HighlightInvalidZones();
        }
    }

    /// <summary>
    /// 적 기물 목록에서 쓸모없는/중복 인장을 제거하고, 최후의 인장은 스테이지당 최대 1개만 유지하도록 정제합니다.
    /// </summary>
    public static List<PieceSpawner.PieceSpawnInfo> SanitizeEnemyPieces(List<PieceSpawner.PieceSpawnInfo> source)
    {
        if (source == null) return new List<PieceSpawner.PieceSpawnInfo>();

        var result = new List<PieceSpawner.PieceSpawnInfo>();
        int lastStandOwnerIndex = -1;
        int highestPriority = -1;

        for (int i = 0; i < source.Count; i++)
        {
            var original = source[i];
            var copy = original;
            var cleanSeals = new List<SealData>();

            if (original.seals != null)
            {
                foreach (var seal in original.seals)
                {
                    if (seal == null) continue;

                    // 쓸모없는 인장(나이트에게 도약, 비숍에게 부서진 비숍 등) 제거
                    if (seal.IsUselessForPiece(original.pieceType, isEnemy: true))
                    {
                        continue;
                    }

                    bool isLastStand = seal.sealName == "최후의 인장" || seal.name == "LastStand";
                    if (isLastStand)
                    {
                        int priority = GetPieceBossPriority(original.pieceType);
                        if (priority > highestPriority)
                        {
                            highestPriority = priority;
                            lastStandOwnerIndex = i;
                        }
                    }

                    cleanSeals.Add(seal);
                }
            }

            copy.seals = cleanSeals;
            result.Add(copy);
        }

        // 최후의 인장은 스테이지당 단 1개만 허용 (가장 위계가 높은 기물 1개만 보유)
        if (lastStandOwnerIndex >= 0)
        {
            for (int i = 0; i < result.Count; i++)
            {
                if (i != lastStandOwnerIndex && result[i].seals != null)
                {
                    result[i].seals.RemoveAll(s => s != null && (s.sealName == "최후의 인장" || s.name == "LastStand"));
                }
            }
        }

        return result;
    }

    private static int GetPieceBossPriority(PieceType type)
    {
        return type switch
        {
            PieceType.Amazon => 100,
            PieceType.Queen => 90,
            PieceType.King => 85,
            PieceType.Chancellor => 80,
            PieceType.Archbishop => 75,
            PieceType.Rook => 60,
            PieceType.Bishop => 50,
            PieceType.Knight => 40,
            PieceType.Centaur => 35,
            PieceType.Cannon => 30,
            PieceType.Grasshopper => 20,
            PieceType.Pawn => 10,
            _ => 0
        };
    }
}
