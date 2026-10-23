using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 스테이지별 세이브 파일 생성 및 이어하기(Continue)를 총괄 관리하는 매니저입니다.
/// 각 스테이지 통과 및 전환 시점에 스테이지별 세이브(stage_XX.json)와 최신 런 세이브(latest_run.json)를 자동 생성합니다.
/// </summary>
public class StageSaveManager : MonoBehaviour
{
    public static StageSaveManager Instance { get; private set; }

    /// <summary>
    /// 타이틀 화면에서 '이어하기'를 선택했을 때 씬 로드 후 복원을 트리거하기 위한 플래그입니다.
    /// </summary>
    public static bool IsContinuePending { get; set; } = false;

    private static bool isRestoring = false;
    public static bool IsRestoring => isRestoring;
    public static bool HasRestoredThisSession { get; set; } = false;

    public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "StageSaves");
    public static string LatestRunSavePath => Path.Combine(SaveDirectory, "latest_run.json");

    public static string GetStageSavePath(int stageNumber)
    {
        return Path.Combine(SaveDirectory, $"stage_{stageNumber:D2}.json");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitRuntime()
    {
        EnsureInstance();
    }

    public static StageSaveManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        var existing = FindFirstObjectByType<StageSaveManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject obj = new GameObject("StageSaveManager");
        Instance = obj.AddComponent<StageSaveManager>();
        DontDestroyOnLoad(obj);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 현재 이어할 수 있는 활성 런 세이브가 존재하는지 여부를 반환합니다.
    /// </summary>
    public static bool HasActiveRunSave()
    {
        try
        {
            if (!File.Exists(LatestRunSavePath)) return false;
            string json = File.ReadAllText(LatestRunSavePath);
            if (string.IsNullOrWhiteSpace(json)) return false;

            StageSaveData data = JsonUtility.FromJson<StageSaveData>(json);
            return data != null && data.pieces != null && data.pieces.Count > 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StageSaveManager] HasActiveRunSave check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 최신 활성 런의 세이브 데이터를 읽어옵니다.
    /// </summary>
    public static StageSaveData GetLatestRunData()
    {
        try
        {
            if (!File.Exists(LatestRunSavePath)) return null;
            string json = File.ReadAllText(LatestRunSavePath);
            return JsonUtility.FromJson<StageSaveData>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StageSaveManager] Failed to read latest run data: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 현재 활성 런 세이브를 삭제합니다 (게임오버 또는 게임 클리어 시).
    /// </summary>
    public static void ClearActiveRun()
    {
        try
        {
            if (File.Exists(LatestRunSavePath))
            {
                File.Delete(LatestRunSavePath);
                Debug.Log("[StageSaveManager] Cleared active run save file.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StageSaveManager] Failed to clear active run: {ex.Message}");
        }
    }

    /// <summary>
    /// 모든 스테이지 세이브 파일 및 디렉토리를 초기화합니다.
    /// </summary>
    public static void DeleteAllStageSaves()
    {
        try
        {
            if (Directory.Exists(SaveDirectory))
            {
                Directory.Delete(SaveDirectory, true);
                Debug.Log("[StageSaveManager] Deleted all stage save files.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StageSaveManager] Failed to delete all stage saves: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 런 상태를 수집하여 latest_run.json 및 해당 스테이지 번호 파일(stage_XX.json)로 저장합니다.
    /// </summary>
    public void SaveCurrentRun(bool pendingReward = false)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[StageSaveManager] SaveCurrentRun failed: GameManager is null.");
            return;
        }

        try
        {
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }

            StageSaveData saveData = new StageSaveData();
            saveData.clearedStage = GameManager.Instance.ClearedStage;
            saveData.stageNumber = saveData.clearedStage + 1;

            int act = (saveData.clearedStage / 10) + 1;
            int sub = (saveData.clearedStage % 10) + 1;
            saveData.stageDisplayName = $"Stage {act}-{sub}";

            saveData.coin = GameManager.Instance.Coin;
            saveData.enemyPiecesCaptured = GameManager.Instance.EnemyPiecesCaptured;
            saveData.playerPiecesCaptured = GameManager.Instance.PlayerPiecesCaptured;
            saveData.clearedBosses = GameManager.Instance.ClearedBosses;
            saveData.pendingReward = pendingReward;
            saveData.saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            saveData.gameMode = PlayerPrefs.GetInt("PendingGameMode", 0);
            saveData.currentFlowState = GameManager.Instance != null ? (int)GameManager.Instance.CurrentFlowState : (int)GameFlowState.Battle;
            saveData.currentBattleType = GameManager.Instance != null ? (int)GameManager.Instance.CurrentBattleType : (int)BattleType.Normal;

            // 튜토리얼 진행 상태 저장
            TutorialManager tm = TutorialManager.EnsureInstance();
            if (tm != null && tm.IsTutorialActive)
            {
                saveData.tutorialData = tm.ExportTutorialSaveData();
            }

            // 그리드 및 보드 크기 저장
            GridManager gm = PieceManager.Instance != null && PieceManager.Instance.gridManager != null
                ? PieceManager.Instance.gridManager
                : FindFirstObjectByType<GridManager>();
            if (gm != null)
            {
                saveData.boardWidth = gm.boardWidth;
                saveData.boardHeight = gm.boardHeight;
                saveData.gridMinX = gm.gridMinBounds.x;
                saveData.gridMinY = gm.gridMinBounds.y;
            }

            // 1. 아군 기물 저장 (보드 및 인벤토리 중복 방지)
            HashSet<Vector2Int> savedBoardCoords = new HashSet<Vector2Int>();
            HashSet<int> savedSlotIndices = new HashSet<int>();

            PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
            foreach (var piece in allPieces)
            {
                if (piece == null || piece.IsEnemy || piece.HasConversionSeal()) continue;

                PieceType pType = piece.HasPromotionSeal() ? PieceType.Pawn : piece.PieceType;
                bool isBoard = piece.CurrentLocation == PieceLocation.Board && piece.gridPosition.HasValue;

                if (isBoard)
                {
                    Vector2Int pos = piece.gridPosition.Value;
                    if (PieceManager.Instance != null && PieceManager.Instance.HasPlacementSnapshot &&
                        PieceManager.Instance.PlacementSnapshot != null &&
                        PieceManager.Instance.PlacementSnapshot.TryGetValue(piece, out Vector2Int snapPos))
                    {
                        pos = snapPos;
                    }

                    if (!savedBoardCoords.Add(pos)) continue; // 동일 좌표 중복 저장 방지

                    SavedPieceData pieceData = new SavedPieceData
                    {
                        pieceType = pType,
                        location = "Board",
                        gridX = pos.x,
                        gridY = pos.y
                    };

                    if (piece.EquippedSeals != null)
                    {
                        foreach (var sealBase in piece.EquippedSeals)
                        {
                            if (sealBase != null && sealBase.Data != null)
                            {
                                string sealId = CollectionManager.GetSealId(sealBase.Data);
                                if (!string.IsNullOrEmpty(sealId))
                                {
                                    pieceData.equippedSealIds.Add(sealId);
                                }
                            }
                        }
                    }

                    saveData.pieces.Add(pieceData);
                }
                else
                {
                    // 인벤토리 기물 (또는 인벤토리로 비행 중인 기물)
                    int slotIdx = -1;
                    if (piece.transform.parent != null && piece.transform.parent.GetComponent<InventorySlot>() != null)
                    {
                        slotIdx = piece.transform.parent.GetSiblingIndex();
                    }

                    if (slotIdx < 0 || savedSlotIndices.Contains(slotIdx))
                    {
                        for (int s = 0; s < 30; s++)
                        {
                            if (!savedSlotIndices.Contains(s))
                            {
                                slotIdx = s;
                                break;
                            }
                        }
                    }

                    if (slotIdx >= 0)
                    {
                        savedSlotIndices.Add(slotIdx);
                    }

                    SavedPieceData pieceData = new SavedPieceData
                    {
                        pieceType = pType,
                        location = "Inventory",
                        inventorySlotIndex = Mathf.Max(0, slotIdx)
                    };

                    if (piece.EquippedSeals != null)
                    {
                        foreach (var sealBase in piece.EquippedSeals)
                        {
                            if (sealBase != null && sealBase.Data != null)
                            {
                                string sealId = CollectionManager.GetSealId(sealBase.Data);
                                if (!string.IsNullOrEmpty(sealId))
                                {
                                    pieceData.equippedSealIds.Add(sealId);
                                }
                            }
                        }
                    }

                    saveData.pieces.Add(pieceData);
                }
            }

            // 기물이 하나도 수집되지 않았다면 (예: 씬 전환 중) PieceInventory의 데이터를 폴백으로 저장
            if (saveData.pieces.Count == 0 && PieceInventory.Instance != null && PieceInventory.Instance.OwnedPieces != null)
            {
                int slotIdx = 0;
                foreach (var info in PieceInventory.Instance.OwnedPieces)
                {
                    saveData.pieces.Add(new SavedPieceData
                    {
                        pieceType = info.pieceType,
                        location = "Inventory",
                        inventorySlotIndex = slotIdx++
                    });
                }
            }

            // 2. 인벤토리 내 미장착 인장 저장
            if (SealInventory.Instance != null && SealInventory.Instance.OwnedSeals != null)
            {
                var seals = SealInventory.Instance.OwnedSeals;
                for (int i = 0; i < seals.Count; i++)
                {
                    if (seals[i] != null)
                    {
                        string sealId = CollectionManager.GetSealId(seals[i]);
                        if (!string.IsNullOrEmpty(sealId))
                        {
                            saveData.unassignedSeals.Add(new SavedSealData
                            {
                                slotIndex = i,
                                sealId = sealId
                            });
                        }
                    }
                }
            }

            // 3. 소유 유물 저장
            if (ArtifactManager.Instance != null && ArtifactManager.Instance.OwnedArtifacts != null)
            {
                foreach (var art in ArtifactManager.Instance.OwnedArtifacts)
                {
                    if (art != null && !string.IsNullOrEmpty(art.id))
                    {
                        saveData.artifacts.Add(new SavedArtifactData
                        {
                            id = art.id,
                            level = art.Level,
                            stackCount = art.StackCount
                        });
                    }
                }
            }

            // 4. 행선지 선택지 저장 (현재 열려있거나 대기 중인 스테이지 선택지 노드 목록)
            if (StageSelectUI.Instance != null && StageSelectUI.Instance.CurrentPendingNodeTypes.Count > 0)
            {
                foreach (var node in StageSelectUI.Instance.CurrentPendingNodeTypes)
                {
                    saveData.pendingStageNodeTypes.Add((int)node);
                }
            }

            // 5. 보상 선택지 저장 (현재 제시된 보상 카드 목록)
            var rewardManager = FindFirstObjectByType<RewardManager>(FindObjectsInactive.Include);
            if (rewardManager != null && rewardManager.PendingRewardCards != null && rewardManager.PendingRewardCards.Count > 0 && pendingReward)
            {
                saveData.pendingRewardCards = new List<SavedRewardCardData>(rewardManager.PendingRewardCards);
            }

            string json = JsonUtility.ToJson(saveData, true);

            // 최신 런 파일 저장
            File.WriteAllText(LatestRunSavePath, json);

            // 스테이지별 고유 파일 저장
            string stageFilePath = GetStageSavePath(saveData.stageNumber);
            File.WriteAllText(stageFilePath, json);

            Debug.Log($"[StageSaveManager] Successfully saved {saveData.stageDisplayName} to '{stageFilePath}' and '{LatestRunSavePath}'.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StageSaveManager] SaveCurrentRun failed with error: {ex}");
        }
    }

    /// <summary>
    /// 저장된 런 데이터를 읽어와 게임 상태, 아티팩트, 인장, 기물을 순차적으로 복원합니다.
    /// </summary>
    public IEnumerator RestoreRunRoutine()
    {
        if (isRestoring)
        {
            Debug.LogWarning("[StageSaveManager] RestoreRunRoutine is already executing. Ignoring duplicate call.");
            yield break;
        }

        isRestoring = true;
        IsContinuePending = false;
        HasRestoredThisSession = true;

        yield return null;

        StageSaveData saveData = GetLatestRunData();
        if (saveData == null)
        {
            Debug.LogError("[StageSaveManager] No valid save data found to restore!");
            isRestoring = false;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartNextStage();
            }
            yield break;
        }

        Debug.Log($"[StageSaveManager] Restoring Run: {saveData.stageDisplayName} (clearedStage: {saveData.clearedStage}, Coin: {saveData.coin})");

        TutorialManager tm = TutorialManager.EnsureInstance();
        if (saveData.tutorialData != null && saveData.tutorialData.isTutorialActive)
        {
            if (tm != null)
            {
                tm.PrepareTutorialRestore(saveData.tutorialData);
            }
        }

        // 1. 게임 매니저 상태 복원
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestoreRunState(
                saveData.clearedStage,
                saveData.coin,
                saveData.enemyPiecesCaptured,
                saveData.playerPiecesCaptured,
                saveData.clearedBosses
            );
        }

        // 2. 유물 복원
        if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.ClearArtifacts();

            if (saveData.artifacts != null)
            {
                foreach (var savedArt in saveData.artifacts)
                {
                    if (string.IsNullOrEmpty(savedArt.id)) continue;

                    ArtifactData template = ArtifactManager.Instance.GetArtifactDataById(savedArt.id);
                    if (template == null)
                    {
                        template = FindArtifactData(savedArt.id);
                    }

                    if (template != null)
                    {
                        ArtifactData runtime = Instantiate(template);
                        runtime.name = template.name;
                        runtime.SetLevel(savedArt.level);
                        runtime.SetStack(savedArt.stackCount);
                        ArtifactManager.Instance.AddArtifact(runtime);
                    }
                }
            }

            ArtifactManager.Instance.UpdateInventoryUI();
        }

        // 3. 미장착 인장 복원
        if (SealInventory.Instance != null)
        {
            SealInventory.Instance.ClearSeals();

            if (saveData.unassignedSeals != null)
            {
                foreach (var savedSeal in saveData.unassignedSeals)
                {
                    if (string.IsNullOrEmpty(savedSeal.sealId)) continue;
                    SealData seal = FindSealData(savedSeal.sealId);
                    if (seal != null)
                    {
                        SealInventory.Instance.InsertSeal(seal, savedSeal.slotIndex);
                    }
                }
            }

            var sealPanel = FindFirstObjectByType<SealInventoryPanel>();
            sealPanel?.RefreshSealItems();
        }

        // 4. 씬에 남아있는 기존 기물 제거 (슬롯과 즉시 분리 후 제거)
        PieceController[] existingPieces = FindObjectsByType<PieceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in existingPieces)
        {
            if (p != null)
            {
                if (PieceManager.Instance != null)
                {
                    PieceManager.Instance.UnregisterPiece(p);
                }
                p.transform.SetParent(null);
                p.gameObject.SetActive(false);
                Destroy(p.gameObject);
            }
        }

        if (PieceInventory.Instance != null)
        {
            PieceInventory.Instance.ClearPieces();
        }

        yield return null;

        // 4.5. 세이브된 보드 크기 및 그리드 셀 즉시 생성 (기물 배치 전에 그리드가 완성된 상태로 복원)
        GridManager gridManager = PieceManager.Instance != null && PieceManager.Instance.gridManager != null
            ? PieceManager.Instance.gridManager
            : FindFirstObjectByType<GridManager>();

        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (gridManager != null)
        {
            if (PieceManager.Instance != null && PieceManager.Instance.gridManager == null)
            {
                PieceManager.Instance.gridManager = gridManager;
            }

            int bWidth = saveData.boardWidth > 0 ? saveData.boardWidth : gridManager.boardWidth;
            int bHeight = saveData.boardHeight > 0 ? saveData.boardHeight : gridManager.boardHeight;
            int minX = (saveData.boardWidth > 0 && saveData.boardHeight > 0) ? saveData.gridMinX : gridManager.gridMinBounds.x;
            int minY = (saveData.boardWidth > 0 && saveData.boardHeight > 0) ? saveData.gridMinY : gridManager.gridMinBounds.y;

            gridManager.SetBoardSize(new int[] { bWidth, bHeight }, new Vector2Int(minX, minY));
            gridManager.CreateGridCells(immediate: true, forceRecreate: true);
        }

        // 5. 저장된 아군 기물 및 장착 인장 복원 (중복 좌표/슬롯 방지)
        HashSet<Vector2Int> restoredBoardCoords = new HashSet<Vector2Int>();
        HashSet<InventorySlot> usedSlots = new HashSet<InventorySlot>();

        var slots = FindObjectsByType<InventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sortedSlots = slots.OrderBy(s => s.transform.GetSiblingIndex()).ToList();

        PieceSpawner spawner = PieceSpawner.Instance != null ? PieceSpawner.Instance : FindFirstObjectByType<PieceSpawner>();
        if (spawner != null && spawner.piecePrefab != null && saveData.pieces != null)
        {
            Transform pieceParent = spawner.piecesParent != null ? spawner.piecesParent : spawner.transform;

            foreach (var savedPiece in saveData.pieces)
            {
                if (savedPiece.location == "Board")
                {
                    Vector2Int pos = new Vector2Int(savedPiece.gridX, savedPiece.gridY);
                    if (!restoredBoardCoords.Add(pos))
                    {
                        Debug.LogWarning($"[StageSaveManager] Skipping duplicate board piece at {pos}");
                        continue;
                    }

                    GameObject pieceObj = Instantiate(spawner.piecePrefab, pieceParent);
                    pieceObj.name = $"PiecePrefab(Clone)@Restored[Ally_{savedPiece.pieceType}_({savedPiece.gridX},{savedPiece.gridY})]";
                    PieceController piece = pieceObj.GetComponent<PieceController>();

                    if (piece != null)
                    {
                        piece.gridPosition = pos;
                        piece.Initialize(savedPiece.pieceType, false);

                        if (savedPiece.equippedSealIds != null)
                        {
                            foreach (var sealId in savedPiece.equippedSealIds)
                            {
                                SealData seal = FindSealData(sealId);
                                if (seal != null)
                                {
                                    piece.EquipSeal(seal);
                                }
                            }
                        }

                        if (PieceManager.Instance != null && PieceManager.Instance.gridManager != null)
                        {
                            RectTransform rt = piece.GetComponent<RectTransform>();
                            if (rt != null)
                            {
                                rt.anchoredPosition = PieceManager.Instance.gridManager.GridToUiPosition(piece.gridPosition.Value);
                            }
                            PieceManager.Instance.RegisterPiece(piece);
                        }
                    }
                }
                else
                {
                    // 인벤토리 기물 복원
                    InventorySlot targetSlot = null;
                    if (savedPiece.inventorySlotIndex >= 0 && savedPiece.inventorySlotIndex < sortedSlots.Count)
                    {
                        var candidate = sortedSlots[savedPiece.inventorySlotIndex];
                        if (!usedSlots.Contains(candidate) && candidate.GetComponentInChildren<PieceController>() == null)
                        {
                            targetSlot = candidate;
                        }
                    }

                    if (targetSlot == null)
                    {
                        targetSlot = sortedSlots.FirstOrDefault(s => !usedSlots.Contains(s) && s.GetComponentInChildren<PieceController>() == null);
                    }

                    if (targetSlot != null)
                    {
                        usedSlots.Add(targetSlot);
                        GameObject pieceObj = Instantiate(spawner.piecePrefab, targetSlot.transform);
                        pieceObj.name = $"PiecePrefab(Clone)@Restored[Ally_{savedPiece.pieceType}_{targetSlot.name}]";
                        PieceController piece = pieceObj.GetComponent<PieceController>();

                        if (piece != null)
                        {
                            piece.Initialize(savedPiece.pieceType, false);
                            piece.MoveToInventory(targetSlot.transform);

                            if (savedPiece.equippedSealIds != null)
                            {
                                foreach (var sealId in savedPiece.equippedSealIds)
                                {
                                    SealData seal = FindSealData(sealId);
                                    if (seal != null)
                                    {
                                        piece.EquipSeal(seal);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // 6. 보드 배치 스냅샷 갱신 및 인벤토리 데이터 동기화
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.CapturePlacementSnapshot();
        }

        if (PieceInventory.Instance != null)
        {
            PieceInventory.Instance.ResetStageSnapshot();
            PieceInventory.Instance.SavePiecesForNextStage();
        }

        // 7. 선택지 복원 (행선지 및 보상)
        if (saveData.pendingStageNodeTypes != null && saveData.pendingStageNodeTypes.Count > 0)
        {
            List<StageNodeType> nodes = new List<StageNodeType>();
            foreach (var val in saveData.pendingStageNodeTypes)
            {
                nodes.Add((StageNodeType)val);
            }
            if (StageSelectUI.Instance != null)
            {
                StageSelectUI.Instance.SetPendingStageNodes(nodes);
            }
        }
        else if (StageSelectUI.Instance != null)
        {
            StageSelectUI.Instance.SetPendingStageNodes(null);
        }

        var rManager = FindFirstObjectByType<RewardManager>(FindObjectsInactive.Include);
        if (rManager != null)
        {
            if (saveData.pendingRewardCards != null && saveData.pendingRewardCards.Count > 0 && saveData.pendingReward)
            {
                rManager.SetPendingRewardCards(saveData.pendingRewardCards);
            }
            else
            {
                rManager.ClearPendingRewardCards();
            }
        }

        // 8. 스테이지 시작 분기
        if (GameManager.Instance != null)
        {
            if (saveData.pendingReward)
            {
                if (gridManager != null)
                {
                    gridManager.SetBoardPresentation(true, 0f);
                }
                // 보상 선택 전 상태이므로 보상 UI 진입
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.ChangeState(GameStateManager.GameState.Reward);
                }
            }
            else if (saveData.currentFlowState == (int)GameFlowState.Battle || (saveData.clearedStage == 0 && saveData.currentFlowState == 0))
            {
                BattleType bType = (BattleType)saveData.currentBattleType;
                GameManager.Instance.StartBattle(bType);
            }
            else if (saveData.currentFlowState == (int)GameFlowState.Shop)
            {
                GameManager.Instance.OpenShop();
            }
            else if (saveData.currentFlowState == (int)GameFlowState.WorkShop)
            {
                GameManager.Instance.OpenWorkShop();
            }
            else if (saveData.currentFlowState == (int)GameFlowState.Treasure)
            {
                GameManager.Instance.OpenTreasure();
            }
            else
            {
                GameManager.Instance.AdvanceToStageSelect();
            }
        }

        isRestoring = false;

        if (saveData.tutorialData != null && saveData.tutorialData.isTutorialActive)
        {
            if (tm != null)
            {
                tm.ResumeTutorialAfterRestore(saveData.tutorialData, saveData);
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (GameManager.Instance == null) return;
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.GameOver) return;

        // 스테이지 도중(전투 배치 및 진행 중)에는 저장을 수행하지 않습니다.
        // 모든 저장은 스테이지 시작 시점, 보상 획득 시점, 행선지 선택 시점에만 안전하게 저장됩니다.
        bool isMidStage = GameManager.Instance.CurrentFlowState == GameFlowState.Battle ||
                          (GameStateManager.Instance != null &&
                           (GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay ||
                            GameStateManager.Instance.CurrentState == GameStateManager.GameState.Prepare));

        if (isMidStage)
        {
            return;
        }

        bool isPendingReward = GameStateManager.Instance != null &&
            (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Reward ||
             GameStateManager.Instance.CurrentState == GameStateManager.GameState.Win ||
             GameStateManager.Instance.CurrentState == GameStateManager.GameState.Cleanup);

        SaveCurrentRun(pendingReward: isPendingReward);
    }

    /// <summary>
    /// ID 또는 이름으로 SealData를 검색합니다.
    /// </summary>
    public static SealData FindSealData(string sealId)
    {
        if (string.IsNullOrEmpty(sealId)) return null;

        if (CollectionManager.Instance != null)
        {
            var all = CollectionManager.Instance.GetAllSeals();
            if (all != null)
            {
                foreach (var s in all)
                {
                    if (s != null && (CollectionManager.GetSealId(s) == sealId || s.name == sealId || s.sealName == sealId))
                        return s;
                }
            }
        }

        SealData direct = Resources.Load<SealData>("Seal/" + sealId);
        if (direct != null) return direct;

        SealData[] loaded = Resources.LoadAll<SealData>("");
        if (loaded != null)
        {
            foreach (var s in loaded)
            {
                if (s != null && (s.name == sealId || s.sealName == sealId))
                    return s;
            }
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SealData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            SealData s = UnityEditor.AssetDatabase.LoadAssetAtPath<SealData>(path);
            if (s != null && (s.name == sealId || s.sealName == sealId))
                return s;
        }
#endif

        return null;
    }

    /// <summary>
    /// ID로 ArtifactData를 검색합니다.
    /// </summary>
    public static ArtifactData FindArtifactData(string artifactId)
    {
        if (string.IsNullOrEmpty(artifactId)) return null;

        if (ArtifactManager.Instance != null)
        {
            var data = ArtifactManager.Instance.GetArtifactDataById(artifactId);
            if (data != null) return data;
        }

        ArtifactData[] loaded = Resources.LoadAll<ArtifactData>("Artifact");
        if (loaded != null)
        {
            foreach (var a in loaded)
            {
                if (a != null && a.id == artifactId) return a;
            }
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ArtifactData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ArtifactData a = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtifactData>(path);
            if (a != null && a.id == artifactId)
                return a;
        }
#endif

        return null;
    }
}

