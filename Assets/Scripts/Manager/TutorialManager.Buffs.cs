using UnityEngine;

public partial class TutorialManager
{
    /// <summary>
    /// 새로운 유물 획득 시 버프 또는 패시브 튜토리얼을 표시합니다.
    /// </summary>
    private void OnArtifactAdded(ArtifactData artifact)
    {
        if (artifact == null) return;
        if (StageSaveManager.IsRestoring || StageSaveManager.IsContinuePending) return;

        if (IsBuffArtifact(artifact))
        {
            if (PlayerPrefs.GetInt(PrefsKeyBuffStatusShown, 0) == 0 && !hasShownBuffStatus && !IsBuffTutorialRestricted())
            {
                TriggerBuffTutorial();
            }
        }

        if (artifact.IsPassive)
        {
            if (PlayerPrefs.GetInt(PrefsKeyPassiveArtifactShown, 0) == 0 && !hasShownPassiveArtifact)
            {
                if (isDialogShowing)
                {
                    pendingPassiveArtifactTutorial = true;
                    pendingPassiveArtifactData = artifact;
                }
                else
                {
                    ShowPassiveArtifactPhase(artifact);
                }
            }
        }
    }

    /// <summary>
    /// 새로운 인장 인벤토리 추가 시 기물 버프 부여 인장 여부를 확인하여 튜토리얼을 표시합니다.
    /// </summary>
    private void OnSealAdded(SealData seal)
    {
        if (seal == null) return;
        if (StageSaveManager.IsRestoring || StageSaveManager.IsContinuePending) return;

        if (IsBuffSeal(seal))
        {
            if (PlayerPrefs.GetInt(PrefsKeyBuffStatusShown, 0) == 0 && !hasShownBuffStatus && !IsBuffTutorialRestricted())
            {
                TriggerBuffTutorial();
            }
        }
    }

    /// <summary>
    /// 해당 유물이 기물 버프/디버프(보호, 회피, 반격, 무적, 실명, 혼란 등)를 부여하는지 판별합니다.
    /// </summary>
    public static bool IsBuffArtifact(ArtifactData artifact)
    {
        if (artifact == null) return false;
        if (artifact.id == "AC03" || artifact.id == "AC04" || artifact.id == "AC05" ||
            artifact.id == "AC06" || artifact.id == "AC09" || artifact.id == "AC10" ||
            artifact.id == "AP04" || artifact.id == "SHOTCUN")
        {
            return true;
        }
        if (!string.IsNullOrEmpty(artifact.description))
        {
            if (artifact.description.Contains("보호") || artifact.description.Contains("회피") ||
                artifact.description.Contains("역공") || artifact.description.Contains("반격") ||
                artifact.description.Contains("무적") || artifact.description.Contains("실명") ||
                artifact.description.Contains("혼란"))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 해당 인장이 기물 버프/디버프(보호, 회피, 반격, 무적, 실명, 혼란 등)를 부여하는지 판별합니다.
    /// </summary>
    public static bool IsBuffSeal(SealData seal)
    {
        if (seal == null) return false;
        if (!string.IsNullOrEmpty(seal.sealName))
        {
            if (seal.sealName.Contains("갈고리") || seal.sealName.Contains("최후") || seal.sealName.Contains("결사항전"))
            {
                return true;
            }
        }
        if (!string.IsNullOrEmpty(seal.description))
        {
            if (seal.description.Contains("보호") || seal.description.Contains("회피") ||
                seal.description.Contains("반격") || seal.description.Contains("역공") ||
                seal.description.Contains("무적") || seal.description.Contains("실명") ||
                seal.description.Contains("혼란"))
            {
                return true;
            }
        }
        if (seal.sealPrefab != null)
        {
            if (seal.sealPrefab.GetComponent<HookSeal>() != null ||
                seal.sealPrefab.GetComponent<LastStandSeal>() != null)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 3스테이지까지(1~3스테이지 전투 및 3스테이지 클리어 보상 화면 포함) 버프 상태 튜토리얼이 나타나지 않도록 제한합니다.
    /// 3스테이지 클리어 후(4스테이지 진입 이후)부터 나타나도록 제한합니다.
    /// </summary>
    public bool IsBuffTutorialRestricted()
    {
        // 기본 튜토리얼 진행 중에는 항상 억제
        if (IsTutorialActive) return true;

        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;

        // 1, 2, 3스테이지 진행 중 (cleared < 3: 1스테이지, 1스테이지 보상, 2스테이지, 2스테이지 보상, 3스테이지 전투)
        if (cleared < 3)
        {
            return true;
        }

        // 3스테이지 클리어 직후 보상 선택 단계 (cleared == 3이면서 보상창이 열려 있거나 GameState가 Reward인 경우)
        if (cleared == 3)
        {
            if (RewardManager.Instance != null && RewardManager.Instance.rewardPanel != null && RewardManager.Instance.rewardPanel.activeSelf)
            {
                return true;
            }

            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.Reward)
            {
                return true;
            }
        }

        // 스테이지 선택 화면(노드 선택 중)에는 버프 튜토리얼을 띄우지 않음
        if (GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.StageSelect)
        {
            return true;
        }
        if (StageSelectUI.Instance != null && StageSelectUI.Instance.IsOpen)
        {
            return true;
        }

        // 상점 상태에서는 상점 패널이 열리고 기물/아이템 등장 연출이 끝날 때까지 억제
        if (GameManager.Instance != null && GameManager.Instance.CurrentFlowState == GameFlowState.Shop)
        {
            if (ShopManager.Instance == null || !ShopManager.Instance.AreItemsAppeared)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsInEarlyTutorialReward() => IsBuffTutorialRestricted();

    /// <summary>
    /// 상점 패널이 열리고 상점 기물 및 아이템의 등장 애니메이션이 완전히 끝났을 때 호출됩니다.
    /// </summary>
    public void OnShopItemsAppeared()
    {
        if (StageSaveManager.IsRestoring || StageSaveManager.IsContinuePending) return;
        if (PlayerPrefs.GetInt(PrefsKeyBuffStatusShown, 0) != 0 || hasShownBuffStatus) return;
        if (IsBuffTutorialRestricted()) return;

        if (pendingBuffStatusTutorial)
        {
            pendingBuffStatusTutorial = false;
            ShowBuffStatusPhase();
            return;
        }

        // 상점에 등장한 아이템 또는 소유 중인 버프 확인
        CheckOwnedBuffs();
        CheckShopItemsForBuffs();
    }

    /// <summary>
    /// 현재 상점 슬롯에 진열된 기물이나 인장, 유물 중 버프 관련 요소가 있는지 확인합니다.
    /// </summary>
    public void CheckShopItemsForBuffs()
    {
        if (StageSaveManager.IsRestoring || StageSaveManager.IsContinuePending) return;
        if (PlayerPrefs.GetInt(PrefsKeyBuffStatusShown, 0) != 0 || hasShownBuffStatus) return;
        if (IsBuffTutorialRestricted()) return;
        if (ShopManager.Instance == null || ShopManager.Instance.shopItemsContainer == null) return;

        foreach (Transform child in ShopManager.Instance.shopItemsContainer)
        {
            if (child == null) continue;
            var slot = child.GetComponent<ShopPieceSlot>();
            if (slot == null) continue;

            if (slot.IsArtifact && IsBuffArtifact(slot.ArtifactData))
            {
                TriggerBuffTutorial();
                return;
            }
            if (slot.IsStandaloneSeal && IsBuffSeal(slot.StandaloneSeal))
            {
                TriggerBuffTutorial();
                return;
            }
            if (slot.AttachedSeal != null && IsBuffSeal(slot.AttachedSeal))
            {
                TriggerBuffTutorial();
                return;
            }
        }
    }

    /// <summary>
    /// 보유 중인 유물이나 인장, 기물에 장착된 인장 중 버프 부여 요소가 있는지 확인하여 튜토리얼을 트리거합니다.
    /// (1~3스테이지 보상에서 획득한 후 4스테이지에 진입했을 때 등의 상황 대비)
    /// </summary>
    public void CheckOwnedBuffs()
    {
        if (StageSaveManager.IsRestoring || StageSaveManager.IsContinuePending) return;
        if (PlayerPrefs.GetInt(PrefsKeyBuffStatusShown, 0) != 0 || hasShownBuffStatus) return;
        if (IsBuffTutorialRestricted()) return;

        // 1. 소유 중인 유물 검사
        if (ArtifactManager.Instance != null && ArtifactManager.Instance.OwnedArtifacts != null)
        {
            foreach (var art in ArtifactManager.Instance.OwnedArtifacts)
            {
                if (IsBuffArtifact(art))
                {
                    TriggerBuffTutorial();
                    return;
                }
            }
        }

        // 2. 소유 중인 인장 검사
        if (SealInventory.Instance != null && SealInventory.Instance.OwnedSeals != null)
        {
            foreach (var seal in SealInventory.Instance.OwnedSeals)
            {
                if (IsBuffSeal(seal))
                {
                    TriggerBuffTutorial();
                    return;
                }
            }
        }

        // 3. 기물에 장착된 인장 검사
        if (PieceManager.Instance != null && PieceManager.Instance.PlayerPieces != null)
        {
            foreach (var piece in PieceManager.Instance.PlayerPieces)
            {
                if (piece == null) continue;
                var seals = piece.GetAllEquippedSealData();
                if (seals != null)
                {
                    foreach (var seal in seals)
                    {
                        if (IsBuffSeal(seal))
                        {
                            TriggerBuffTutorial();
                            return;
                        }
                    }
                }
            }
        }
    }

    private void TriggerBuffTutorial()
    {
        if (IsTutorialActive) return;
        if (isDialogShowing || IsBuffTutorialRestricted())
        {
            pendingBuffStatusTutorial = true;
        }
        else
        {
            ShowBuffStatusPhase();
        }
    }

    /// <summary>
    /// 보상/상점/도감 등에서 버프 유물이 최초 발견되었을 때 튜토리얼을 트리거합니다.
    /// </summary>
    public void CheckBuffDiscovered(ArtifactData artifact)
    {
        if (artifact == null) return;
        if (IsTutorialActive) return;
        if (StageSaveManager.IsRestoring || StageSaveManager.IsContinuePending) return;
        if (PlayerPrefs.GetInt(PrefsKeyBuffStatusShown, 0) != 0 || hasShownBuffStatus) return;

        // 3스테이지까지는 버프 안내를 띄우지 않고 4스테이지부터 표시
        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared < 3) return;

        if (IsBuffArtifact(artifact))
        {
            if (IsBuffTutorialRestricted() || isDialogShowing)
            {
                pendingBuffStatusTutorial = true;
            }
            else
            {
                TriggerBuffTutorial();
            }
        }
    }

    /// <summary>
    /// 보상/상점/도감 등에서 버프 인장이 최초 발견되었을 때 튜토리얼을 트리거합니다.
    /// </summary>
    public void CheckBuffDiscovered(SealData seal)
    {
        if (seal == null) return;
        if (IsTutorialActive) return;
        if (StageSaveManager.IsRestoring || StageSaveManager.IsContinuePending) return;
        if (PlayerPrefs.GetInt(PrefsKeyBuffStatusShown, 0) != 0 || hasShownBuffStatus) return;

        // 3스테이지까지는 버프 안내를 띄우지 않고 4스테이지부터 표시
        int cleared = GameManager.Instance != null ? GameManager.Instance.ClearedStage : 0;
        if (cleared < 3) return;

        if (IsBuffSeal(seal))
        {
            if (IsBuffTutorialRestricted() || isDialogShowing)
            {
                pendingBuffStatusTutorial = true;
            }
            else
            {
                TriggerBuffTutorial();
            }
        }
    }

    /// <summary>
    /// 전투 중 기물에 버프가 실제로 부여되었을 때 최초 1회 튜토리얼을 트리거합니다.
    /// </summary>
    public void NotifyPieceBuffApplied()
    {
        if (IsTutorialActive) return;
        if (StageSaveManager.IsRestoring || StageSaveManager.IsContinuePending) return;
        if (PlayerPrefs.GetInt(PrefsKeyBuffStatusShown, 0) != 0 || hasShownBuffStatus) return;
        if (IsBuffTutorialRestricted()) return;

        TriggerBuffTutorial();
    }
}

