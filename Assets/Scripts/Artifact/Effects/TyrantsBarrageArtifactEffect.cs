using System.Collections.Generic;
using UnityEngine;

/// <summary>폭군의 난사: 군단을 숙청하고 왕을 샷건 킹으로 전환합니다.</summary>
public sealed class TyrantsBarrageArtifactEffect : ArtifactEffectBase
{
    public const string Id = "SHOTCUN";
    public override string ArtifactId => Id;

    public static bool IsActive => ArtifactManager.Instance != null && ArtifactManager.Instance.HasArtifact(Id);
    public static int Ammo => ShotgunKingSystem.Ammo;
    // 탄창 파츠 보너스가 더해지기 전의 기본 최대 탄약입니다.
    public const int MaxAmmo = 2;

    public void Activate() => ShotgunKingSystem.Activate();
    public void OnBattleStarted() => ShotgunKingSystem.OnBattleStarted();

    public static int GetAmmo() => IsActive ? ShotgunKingSystem.Ammo : 0;
    public static int GetMaxAmmo() => IsActive ? ShotgunKingSystem.GetMaxAmmo() : 0;
}

/// <summary>폭군의 난사 전투 규칙과 무기 파츠 변환을 관리합니다.</summary>
public static class ShotgunKingSystem
{
    public const string RicochetShot = "도탄 산탄";
    public const string IncendiaryMod = "소이탄 개조";
    public const string ScavengerBarrel = "약탈자 총신";
    public const string JumpShot = "점프 샷";
    public const string GhostSlug = "유령 관통탄";
    public const string LastStand = "결사항전 장전";
    public const string LongBarrel = "장총신";
    public const string ShotSpread = "산탄 증가";
    public const string Dagger = "단검";
    public const string ExtendedMagazine = "확장 탄창";
    public const int BaseShotRange = 1;
    public const int BaseShotPellets = 1;
    public const int PelletsPerSpreadSeal = 2;
    public const int MaxStackablePartCopies = 3;

    private static readonly string[] partNames =
    {
        RicochetShot, IncendiaryMod, ScavengerBarrel, JumpShot, GhostSlug, LastStand,
        LongBarrel, ShotSpread, Dagger, ExtendedMagazine
    };
    private static readonly Vector2Int[] shotDirections =
    {
        Vector2Int.right, new Vector2Int(1, 1), Vector2Int.up, new Vector2Int(-1, 1),
        Vector2Int.left, new Vector2Int(-1, -1), Vector2Int.down, new Vector2Int(1, -1)
    };
    private static readonly Vector2Int[] shotRangeJumps =
    {
        new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(-1, 2), new Vector2Int(-2, 1),
        new Vector2Int(1, -2), new Vector2Int(2, -1), new Vector2Int(-1, -2), new Vector2Int(-2, -1),
        new Vector2Int(2, 0), new Vector2Int(-2, 0), new Vector2Int(0, 2), new Vector2Int(0, -2)
    };
    private static readonly Vector2Int[] incendiaryOrthogonalOffsets =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };
    private static readonly Vector2Int[] incendiaryDiagonalOffsets =
    {
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    private static int ammo = TyrantsBarrageArtifactEffect.MaxAmmo;
    private static PieceType? scavengedPieceType;
    private static SealData[] cachedParts;

    public static bool IsActive => TyrantsBarrageArtifactEffect.IsActive;
    public static int Ammo => Mathf.Clamp(ammo, 0, GetMaxAmmo());
    public static bool IsShotgunKing(PieceController piece) => IsActive && IsPlayerKing(piece);

    public static int GetMaxAmmo()
    {
        PieceController king = FindKing();
        return TyrantsBarrageArtifactEffect.MaxAmmo +
               (IsActive && HasPart(king, ExtendedMagazine) ? 1 : 0);
    }

    public static int GetShotRange(PieceController king)
    {
        int rangeBonuses = 0;
        if (king != null && king.EquippedSeals != null)
        {
            foreach (SealBase seal in king.EquippedSeals)
            {
                if (seal != null && seal.Data != null && seal.Data.sealName == LongBarrel)
                    rangeBonuses++;
            }
        }
        return BaseShotRange + rangeBonuses;
    }

    public static int GetShotPelletCount(PieceController king)
    {
        return BaseShotPellets + GetEquippedPartCount(king, ShotSpread) * PelletsPerSpreadSeal;
    }

    public static int GetEquippedPartCount(PieceController king, string partName)
    {
        if (king == null || king.EquippedSeals == null) return 0;
        int count = 0;
        foreach (SealBase seal in king.EquippedSeals)
        {
            if (seal != null && seal.Data != null && seal.Data.sealName == partName) count++;
        }
        return count;
    }

    public static bool IsStackablePart(SealData seal)
    {
        return seal != null && (seal.sealName == LongBarrel || seal.sealName == ShotSpread);
    }

    public static bool CanEquipStackablePart(PieceController king, SealData seal)
    {
        return IsShotgunKing(king) && IsStackablePart(seal) &&
               GetEquippedPartCount(king, seal.sealName) < MaxStackablePartCopies;
    }

    public static void NotifyKingPartsChanged(PieceController king)
    {
        if (!IsActive || !IsPlayerKing(king)) return;
        ammo = Mathf.Min(ammo, GetMaxAmmo());
        RefreshArtifactUI();
        CheckFullModAchievement(king);
    }

    private static void CheckFullModAchievement(PieceController king)
    {
        if (king == null || king.EquippedSeals == null) return;
        int count = 0;
        foreach (SealBase seal in king.EquippedSeals)
        {
            if (seal != null && seal.Data != null && IsWeaponPart(seal.Data))
                count++;
        }
        if (count >= 3)
        {
            AchievementManager.Instance?.UnlockAchievement("combat_shotgun_full_mod");
        }
    }

    public static void Activate()
    {
        ammo = TyrantsBarrageArtifactEffect.MaxAmmo;
        scavengedPieceType = null;
        SealInventory.Instance?.ConvertOwnedSealsToShotgunParts();
        PurgeArmy();
        PieceController king = FindKing();
        if (king != null) king.SetBuffStacks(PieceBuffType.Protection, 2);
        EnsureKingWeaponParts();
        ammo = GetMaxAmmo();
        RefreshArtifactUI();
        CheckFullModAchievement(king);
        AchievementManager.Instance?.UnlockAchievement("combat_shotgun_awaken");
    }

    public static void OnBattleStarted()
    {
        if (!IsActive) return;
        AchievementManager.Instance?.UnlockAchievement("combat_shotgun_awaken");
        scavengedPieceType = null;
        SealInventory.Instance?.ConvertOwnedSealsToShotgunParts();
        PurgeArmy();
        PieceController king = FindKing();
        if (king != null) king.SetBuffStacks(PieceBuffType.Protection, 2);
        EnsureKingWeaponParts();
        ammo = GetMaxAmmo();
        RefreshArtifactUI();
    }

    public static void Reset()
    {
        ammo = TyrantsBarrageArtifactEffect.MaxAmmo;
        scavengedPieceType = null;
        RefreshArtifactUI();
    }

    private static void PurgeArmy()
    {
        if (!IsActive) return;

        PieceController[] scenePieces = Object.FindObjectsByType<PieceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PieceController king = null;
        foreach (PieceController piece in scenePieces)
        {
            if (piece != null && !piece.IsEnemy && piece.Type == PieceType.King)
            {
                king = piece;
                if (piece.CurrentLocation == PieceLocation.Board) break;
            }
        }

        bool kingWasOnBoard = king != null && king.CurrentLocation == PieceLocation.Board && king.GridPosition.HasValue;
        Vector2Int kingPosition = kingWasOnBoard ? king.GridPosition.Value : default;
        Transform kingInventorySlot = king != null && king.CurrentLocation == PieceLocation.Inventory ? king.transform.parent : null;
        List<SealData> kingSeals = king != null ? king.GetAllEquippedSealData() : new List<SealData>();

        int sellValue = 0;
        if (PieceInventory.Instance != null)
        {
            foreach (PieceInventory.PieceInfo piece in PieceInventory.Instance.OwnedPieces)
            {
                if (piece.pieceType != PieceType.King) sellValue += GetSellPrice(piece.pieceType);
            }
        }
        foreach (PieceController piece in scenePieces)
        {
            if (piece != null && !piece.IsEnemy && piece.Type != PieceType.King && piece.CurrentLocation == PieceLocation.Board)
                sellValue += GetSellPrice(piece.Type);
        }

        PieceInventory.Instance?.ClearPieces();
        if (!kingWasOnBoard) PieceInventory.Instance?.AddPiece(PieceType.King);

        PieceController replacementKing = CreateReplacementKing(kingWasOnBoard, kingPosition, kingInventorySlot, kingSeals);
        if (replacementKing == null) replacementKing = king;
        PieceManager.Instance?.PurgePlayerArmyExceptKing(replacementKing, king);

        foreach (PieceController piece in scenePieces)
        {
            if (piece == null || piece.IsEnemy || piece == replacementKing) continue;
            Object.Destroy(piece.gameObject);
        }

        if (sellValue > 0) GameManager.Instance?.AddCoin(sellValue);
    }

    private static PieceController CreateReplacementKing(bool placeOnBoard, Vector2Int boardPosition, Transform inventorySlot, List<SealData> seals)
    {
        PieceSpawner spawner = PieceSpawner.Instance;
        if (spawner == null || spawner.piecePrefab == null) return null;

        if (!placeOnBoard && inventorySlot == null)
        {
            InventorySlot[] slots = Object.FindObjectsByType<InventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (slots.Length > 0) inventorySlot = slots[0].transform;
        }

        Transform parent = placeOnBoard
            ? (spawner.piecesParent != null ? spawner.piecesParent : spawner.transform)
            : (inventorySlot != null ? inventorySlot : spawner.transform);
        GameObject kingObject = Object.Instantiate(spawner.piecePrefab, parent);
        PieceController newKing = kingObject.GetComponent<PieceController>();
        if (newKing == null)
        {
            Object.Destroy(kingObject);
            return null;
        }

        if (placeOnBoard) newKing.gridPosition = boardPosition;
        newKing.Initialize(PieceType.King, false);
        if (!placeOnBoard && inventorySlot != null) newKing.MoveToInventory(inventorySlot, false);

        foreach (SealData seal in seals)
        {
            if (seal != null) newKing.EquipSeal(seal);
        }

        return newKing;
    }

    public static int GetSellPrice(PieceType pieceType)
    {
        ShopPieceData data = ShopManager.Instance != null ? ShopManager.Instance.shopPieceData : null;
        if (data == null && RewardService.Instance != null) data = RewardService.Instance.pieceData;
        if (data != null && data.pieceList != null)
        {
            ShopPieceData.PieceInfo info = data.pieceList.Find(piece => piece.pieceType == pieceType);
            if (info != null) return Mathf.Max(0, info.Price / 2);
        }
        return Mathf.Max(0, PieceData.GetPieceScore(pieceType) * 5);
    }

    public static void EnsureKingWeaponParts()
    {
        if (!IsActive) return;
        PieceController king = FindKing();
        if (king == null) return;

        List<SealData> oldSeals = new List<SealData>();
        foreach (SealBase seal in king.EquippedSeals)
        {
            if (seal != null && seal.Data != null && !IsWeaponPart(seal.Data)) oldSeals.Add(seal.Data);
        }

        foreach (SealData seal in oldSeals)
        {
            king.UnEquipSeal(seal);
            king.EquipSeal(seal);
        }
    }

    private static PieceController FindKing()
    {
        if (PieceManager.Instance != null)
        {
            foreach (PieceController piece in PieceManager.Instance.PlayerPieces)
            {
                if (piece != null && piece.Type == PieceType.King && piece.CurrentLocation == PieceLocation.Board) return piece;
            }
        }

        PieceController[] pieces = Object.FindObjectsByType<PieceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PieceController piece in pieces)
        {
            if (piece != null && !piece.IsEnemy && piece.Type == PieceType.King) return piece;
        }
        return null;
    }

    public static bool IsWeaponPart(SealData seal)
    {
        if (seal == null) return false;
        foreach (string partName in partNames)
        {
            if (seal.sealName == partName) return true;
        }
        return false;
    }

    public static SealData ConvertSeal(SealData seal)
    {
        if (!IsActive || seal == null || IsWeaponPart(seal)) return seal;
        SealData[] parts = LoadParts();
        return parts.Length > 0 ? parts[Random.Range(0, parts.Length)] : seal;
    }

    private static SealData[] LoadParts()
    {
        if (cachedParts == null || cachedParts.Length == 0)
        {
            List<SealData> parts = new List<SealData>();
            SealData[] loaded = Resources.LoadAll<SealData>("Seal");
            foreach (SealData seal in loaded)
            {
                if (IsWeaponPart(seal)) parts.Add(seal);
            }
            cachedParts = parts.ToArray();
        }
        return cachedParts;
    }

    public static SealData GetRandomWeaponPart()
    {
        SealData[] parts = LoadParts();
        return parts.Length > 0 ? parts[Random.Range(0, parts.Length)] : null;
    }

    public static bool HasPart(PieceController king, string partName)
    {
        if (king == null || king.EquippedSeals == null) return false;
        foreach (SealBase seal in king.EquippedSeals)
        {
            if (seal != null && seal.Data != null && seal.Data.sealName == partName) return true;
        }
        return false;
    }

    public static bool IsPlayerAimingTurn
    {
        get
        {
            PieceController king = FindKing();
            return IsActive && GameStateManager.Instance != null &&
                   GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay &&
                   TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn &&
                   IsPlayerKing(king) && !king.IsBlind &&
                   king.CurrentLocation == PieceLocation.Board && king.GridPosition.HasValue;
        }
    }

    public static bool IsValidAimPosition(Vector2Int aimPosition)
    {
        return IsValidAimPosition(FindKing(), aimPosition);
    }

    private static bool IsValidAimPosition(PieceController king, Vector2Int aimPosition)
    {
        if (!IsPlayerAimingTurn || Ammo <= 0 || !IsPlayerKing(king) || !king.GridPosition.HasValue ||
            PieceManager.Instance?.gridManager == null || !PieceManager.Instance.gridManager.IsInBounds(aimPosition))
            return false;

        return IsInsideShotRange(king, aimPosition);
    }

    private static bool IsInsideShotRange(PieceController king, Vector2Int aimPosition)
    {
        GridManager grid = PieceManager.Instance?.gridManager;
        if (grid == null || king == null || !king.GridPosition.HasValue) return false;

        Vector2Int origin = king.GridPosition.Value;
        int maxJumps = GetShotRange(king);
        Queue<Vector2Int> positions = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int> { [origin] = 0 };
        positions.Enqueue(origin);

        while (positions.Count > 0)
        {
            Vector2Int current = positions.Dequeue();
            int nextDistance = distances[current] + 1;
            if (nextDistance > maxJumps) continue;

            foreach (Vector2Int jump in shotRangeJumps)
            {
                Vector2Int next = current + jump;
                if (!grid.IsInBounds(next) || distances.ContainsKey(next)) continue;
                if (next == aimPosition) return true;
                distances[next] = nextDistance;
                positions.Enqueue(next);
            }
        }

        return false;
    }

    private static HashSet<Vector2Int> GetShotAimPositions(PieceController king)
    {
        HashSet<Vector2Int> positions = new HashSet<Vector2Int>();
        GridManager grid = PieceManager.Instance?.gridManager;
        if (grid == null || king == null || !king.GridPosition.HasValue) return positions;

        Vector2Int origin = king.GridPosition.Value;
        int maxJumps = GetShotRange(king);
        Queue<Vector2Int> pending = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int> { [origin] = 0 };
        pending.Enqueue(origin);

        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();
            int nextDistance = distances[current] + 1;
            if (nextDistance > maxJumps) continue;

            foreach (Vector2Int jump in shotRangeJumps)
            {
                Vector2Int next = current + jump;
                if (!grid.IsInBounds(next) || distances.ContainsKey(next)) continue;
                distances[next] = nextDistance;
                positions.Add(next);
                pending.Enqueue(next);
            }
        }

        return positions;
    }

    public static void UpdateAimPreview(Vector2Int? aimPosition)
    {
        PieceManager manager = PieceManager.Instance;
        if (manager == null) return;
        PieceController king = FindKing();
        if (!IsPlayerAimingTurn || Ammo <= 0 || !IsPlayerKing(king) || !king.GridPosition.HasValue)
        {
            manager.ClearShotgunAimMarkers();
            return;
        }

        List<Vector2Int> highlightedPositions = new List<Vector2Int>();
        if (aimPosition.HasValue && IsValidAimPosition(king, aimPosition.Value))
            highlightedPositions = GetIncendiaryPreviewPositions(manager, king, aimPosition.Value);
        manager.UpdateShotgunAimMarkers(GetShotAimPositions(king), highlightedPositions);
    }

    public static bool TryHandleShotAimClick(Vector2Int aimPosition, UnityEngine.EventSystems.PointerEventData.InputButton button)
    {
        if (button != UnityEngine.EventSystems.PointerEventData.InputButton.Left || !IsValidAimPosition(aimPosition)) return false;
        PieceManager manager = PieceManager.Instance;
        PieceController king = FindKing();
        if (manager == null || king == null || !TryShoot(manager, king, aimPosition)) return false;
        manager.ClearShotgunAimMarkers();
        TurnManager.Instance?.EndTurn();
        return true;
    }

    public static bool TryHandleDaggerAttack(PieceController target, UnityEngine.EventSystems.PointerEventData.InputButton button)
    {
        if (button != UnityEngine.EventSystems.PointerEventData.InputButton.Left || target == null || !target.IsEnemy ||
            !IsActive || GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay ||
            TurnManager.Instance == null || !TurnManager.Instance.IsPlayerTurn ||
            (PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsPromoting))
            return false;

        PieceController king = FindKing();
        if (!IsShotgunKing(king) || king.IsBlind || king.CurrentLocation != PieceLocation.Board || !HasPart(king, Dagger) ||
            !king.GridPosition.HasValue || !target.GridPosition.HasValue ||
            target.CurrentLocation != PieceLocation.Board || target.IsInvincible)
            return false;

        Vector2Int delta = target.GridPosition.Value - king.GridPosition.Value;
        if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) != 1) return false;

        PieceManager manager = PieceManager.Instance;
        if (manager == null) return false;

        if (manager.TryCapturePieceWithAttack(king, target, out _, out _))
        {
            AchievementManager.Instance?.UnlockAchievement("combat_shotgun_dagger");
        }
        TurnLogUIManager.Instance?.AddLog("단검으로 인접한 적을 공격했습니다.", false);
        manager.UpdateThreatenedStatus();
        TurnManager.Instance?.EndTurn();
        return true;
    }

    public static bool TryShoot(PieceManager manager, PieceController king, Vector2Int aimPosition)
    {
        if (manager == null || manager.gridManager == null || !IsValidAimPosition(king, aimPosition)) return false;

        manager.PlayShotgunProjectile(king.GridPosition.Value, aimPosition);
        ammo--;
        scavengedPieceType = null;
        HashSet<PieceController> hitPieces = new HashSet<PieceController>();
        foreach (Vector2Int position in GetShotTargetPositions(king, aimPosition))
        {
            PieceController piece = manager.GetPieceAt(position);
            if (piece != null && piece.IsEnemy) CaptureShotTarget(manager, king, piece, hitPieces);
        }

        int killCount = hitPieces.Count;
        if (killCount > 0)
        {
            AchievementManager.Instance?.AddProgress("combat_shotgun_kill", killCount);
            AchievementManager.Instance?.AddProgress("combat_shotgun_kill_20", killCount);
            if (killCount >= 2)
            {
                AchievementManager.Instance?.UnlockAchievement("combat_shotgun_multi_kill");
            }
        }

        TurnLogUIManager.Instance?.AddLog($"폭군의 난사 발사 (탄약 {Ammo}/{GetMaxAmmo()})", false);
        RefreshArtifactUI();
        manager.UpdateThreatenedStatus();
        return true;
    }

    private static Vector2Int GetShotDirection(PieceController king, Vector2Int aimPosition)
    {
        Vector2Int delta = aimPosition - king.GridPosition.Value;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        int sector = Mathf.RoundToInt(angle / 45f);
        return shotDirections[(sector % shotDirections.Length + shotDirections.Length) % shotDirections.Length];
    }

    private static List<Vector2Int> GetShotTargetPositions(PieceController king, Vector2Int aimPosition)
    {
        List<Vector2Int> targets = new List<Vector2Int>();
        PieceManager manager = PieceManager.Instance;
        if (manager?.gridManager == null || king == null || !king.GridPosition.HasValue)
            return targets;

        Vector2Int direction = GetShotDirection(king, aimPosition);
        Vector2Int spreadDirection = new Vector2Int(-direction.y, direction.x);
        int halfSpread = GetShotPelletCount(king) / 2;
        for (int offset = -halfSpread; offset <= halfSpread; offset++)
        {
            Vector2Int position = aimPosition + spreadDirection * offset;
            if (position != king.GridPosition.Value && manager.gridManager.IsInBounds(position))
                targets.Add(position);
        }

        if (targets.Count == 0 && aimPosition != king.GridPosition.Value && manager.gridManager.IsInBounds(aimPosition))
            targets.Add(aimPosition);

        return targets;
    }

    private static List<Vector2Int> GetIncendiaryPreviewPositions(PieceManager manager, PieceController king, Vector2Int aimPosition)
    {
        List<Vector2Int> previewPositions = new List<Vector2Int>();
        if (!HasPart(king, IncendiaryMod) || manager?.gridManager == null) return previewPositions;

        foreach (Vector2Int targetPosition in GetShotTargetPositions(king, aimPosition))
        {
            PieceController target = manager.GetPieceAt(targetPosition);
            if (target == null || !target.IsEnemy) continue;

            foreach (Vector2Int offset in GetIncendiaryBlastOffsets(king, targetPosition))
            {
                Vector2Int blastPosition = targetPosition + offset;
                if (manager.gridManager.IsInBounds(blastPosition) && !previewPositions.Contains(blastPosition))
                    previewPositions.Add(blastPosition);
            }
        }

        return previewPositions;
    }

    private static Vector2Int[] GetIncendiaryBlastOffsets(PieceController king, Vector2Int center)
    {
        Vector2Int direction = GetShotDirection(king, center);
        return direction.x == 0 || direction.y == 0
            ? incendiaryOrthogonalOffsets
            : incendiaryDiagonalOffsets;
    }

    private static void CaptureShotTarget(PieceManager manager, PieceController king, PieceController target, HashSet<PieceController> hitPieces, bool triggerIncendiary = true)
    {
        if (target == null || !target.IsEnemy || !hitPieces.Add(target)) return;
        PieceType capturedType;
        Vector2Int position;
        if (!manager.TryCapturePieceWithAttack(king, target, out capturedType, out position)) return;

        if (HasPart(king, ScavengerBarrel)) scavengedPieceType = capturedType;
        if (triggerIncendiary && HasPart(king, IncendiaryMod)) SpreadIncendiaryBlast(manager, king, position, hitPieces);
    }

    private static void SpreadIncendiaryBlast(PieceManager manager, PieceController king, Vector2Int center, HashSet<PieceController> hitPieces)
    {
        foreach (Vector2Int offset in GetIncendiaryBlastOffsets(king, center))
        {
            PieceController nearby = manager.GetPieceAt(center + offset);
            // 폭발로 처치된 기물은 다시 폭발하지 않도록 연쇄를 한 단계로 제한합니다.
            if (nearby != null && nearby.IsEnemy) CaptureShotTarget(manager, king, nearby, hitPieces, false);
        }
    }

    private static PieceController FindRicochetTarget(PieceManager manager, Vector2Int origin, Vector2Int direction, HashSet<PieceController> alreadyHit)
    {
        GridManager grid = manager.gridManager;
        Vector2Int position = origin + direction;
        Vector2Int lastInside = origin;
        int steps = 0;
        while (grid.IsInBounds(position) && steps++ < 128)
        {
            lastInside = position;
            position += direction;
        }

        Vector2Int reflected = direction;
        if (position.x < grid.gridMinBounds.x || position.x >= grid.gridMinBounds.x + grid.boardWidth) reflected.x = -reflected.x;
        if (position.y < grid.gridMinBounds.y || position.y >= grid.gridMinBounds.y + grid.boardHeight) reflected.y = -reflected.y;
        position = lastInside + reflected;
        steps = 0;
        while (grid.IsInBounds(position) && steps++ < 128)
        {
            PieceController piece = manager.GetPieceAt(position);
            if (piece != null)
            {
                if (!piece.IsEnemy) return null;
                if (!alreadyHit.Contains(piece)) return piece;
            }
            position += reflected;
        }
        return null;
    }

    private static List<Vector2Int> GetRayPositions(GridManager grid, Vector2Int origin, Vector2Int direction, int maxRange)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        Vector2Int position = origin + direction;
        int steps = 0;
        while (grid != null && grid.IsInBounds(position) && steps++ < maxRange)
        {
            positions.Add(position);
            position += direction;
        }
        return positions;
    }

    private static bool IsPlayerKing(PieceController piece) => piece != null && !piece.IsEnemy && piece.Type == PieceType.King;

    public static void OnKingMoveFinished(PieceController king, Vector2Int from, Vector2Int to, PieceType? capturedType, bool usedScavengedMove)
    {
        if (!IsActive || !IsPlayerKing(king)) return;
        bool knightMove = IsKnightMove(to - from);
        if (HasPart(king, JumpShot) && knightMove)
        {
            AddAmmo(GetMaxAmmo());
        }
        else if (!capturedType.HasValue || usedScavengedMove)
        {
            AddAmmo(1);
        }
        scavengedPieceType = null;
    }

    private static bool IsKnightMove(Vector2Int delta) =>
        (Mathf.Abs(delta.x) == 2 && Mathf.Abs(delta.y) == 1) ||
        (Mathf.Abs(delta.x) == 1 && Mathf.Abs(delta.y) == 2);

    private static void AddAmmo(int amount)
    {
        ammo = Mathf.Clamp(ammo + Mathf.Max(0, amount), 0, GetMaxAmmo());
        RefreshArtifactUI();
    }

    public static void OnKingProtectionBroken(PieceController king)
    {
        if (!IsActive || !IsPlayerKing(king) || !HasPart(king, LastStand)) return;
        AddAmmo(1);
        bool dodged = PieceManager.Instance != null && PieceManager.Instance.TryEvadePieceImmediately(king);
        TurnLogUIManager.Instance?.AddLog(dodged ? "결사항전 장전: 탄약 1발 장전, 즉시 회피" : "결사항전 장전: 탄약 1발 장전", false);
    }

    public static List<Vector2Int> GetScavengedMoveTargets(PieceController king, Vector2Int currentPosition)
    {
        List<Vector2Int> targets = new List<Vector2Int>();
        if (!IsActive || !IsPlayerKing(king) || !scavengedPieceType.HasValue || PieceManager.Instance == null || PieceManager.Instance.gridManager == null) return targets;
        PieceType type = scavengedPieceType.Value;
        GridManager grid = PieceManager.Instance.gridManager;
        switch (type)
        {
            case PieceType.Rook:
                AddSlidingTargets(king, currentPosition, targets, true, false);
                break;
            case PieceType.Bishop:
                AddSlidingTargets(king, currentPosition, targets, false, true);
                break;
            case PieceType.Queen:
                AddSlidingTargets(king, currentPosition, targets, true, true);
                break;
            case PieceType.Amazon:
                AddSlidingTargets(king, currentPosition, targets, true, true);
                AddKnightTargets(king, currentPosition, targets);
                break;
            case PieceType.Chancellor:
                AddSlidingTargets(king, currentPosition, targets, true, false);
                AddKnightTargets(king, currentPosition, targets);
                break;
            case PieceType.Archbishop:
                AddSlidingTargets(king, currentPosition, targets, false, true);
                AddKnightTargets(king, currentPosition, targets);
                break;
            case PieceType.Centaur:
                AddKingTargets(king, currentPosition, targets);
                AddKnightTargets(king, currentPosition, targets);
                break;
            case PieceType.Knight:
                AddKnightTargets(king, currentPosition, targets);
                break;
            case PieceType.Cannon:
                AddCannonTargets(king, currentPosition, targets);
                break;
            case PieceType.Grasshopper:
                AddGrasshopperTargets(king, currentPosition, targets);
                break;
            case PieceType.Pawn:
                AddPawnTargets(king, currentPosition, targets);
                break;
            default:
                AddKingTargets(king, currentPosition, targets);
                break;
        }
        return targets;
    }

    public static bool IsScavengedMove(PieceController king, Vector2Int targetPosition)
    {
        if (!HasPart(king, ScavengerBarrel) || king == null || king.GridPosition == null) return false;
        return GetScavengedMoveTargets(king, king.GridPosition.Value).Contains(targetPosition);
    }

    private static void AddSlidingTargets(PieceController king, Vector2Int origin, List<Vector2Int> targets, bool orthogonal, bool diagonal)
    {
        GridManager grid = PieceManager.Instance.gridManager;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                bool isOrthogonal = dx == 0 || dy == 0;
                if ((isOrthogonal && !orthogonal) || (!isOrthogonal && !diagonal)) continue;
                Vector2Int position = origin + new Vector2Int(dx, dy);
                while (grid.IsInBounds(position) && !IsDestroyedCell(grid, position))
                {
                    PieceController occupant = PieceManager.Instance.GetPieceAt(position);
                    if (occupant != null)
                    {
                        if (occupant.IsEnemy && king.CanMoveTo(position)) targets.Add(position);
                        break;
                    }
                    targets.Add(position);
                    position += new Vector2Int(dx, dy);
                }
            }
        }
    }

    private static void AddKnightTargets(PieceController king, Vector2Int origin, List<Vector2Int> targets)
    {
        Vector2Int[] offsets =
        {
            new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(-2, 1), new Vector2Int(-2, -1),
            new Vector2Int(1, 2), new Vector2Int(1, -2), new Vector2Int(-1, 2), new Vector2Int(-1, -2)
        };
        foreach (Vector2Int offset in offsets) AddScavengerTarget(king, origin + offset, targets);
    }

    private static void AddKingTargets(PieceController king, Vector2Int origin, List<Vector2Int> targets)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0) AddScavengerTarget(king, origin + new Vector2Int(dx, dy), targets);
    }

    private static void AddPawnTargets(PieceController king, Vector2Int origin, List<Vector2Int> targets)
    {
        GridManager grid = PieceManager.Instance.gridManager;
        Vector2Int forward = origin + Vector2Int.up;
        if (grid.IsInBounds(forward) && !IsDestroyedCell(grid, forward) && PieceManager.Instance.GetPieceAt(forward) == null)
        {
            targets.Add(forward);
        }
        foreach (int dx in new[] { -1, 1 })
        {
            Vector2Int diagonal = origin + new Vector2Int(dx, 1);
            PieceController occupant = PieceManager.Instance.GetPieceAt(diagonal);
            if (occupant != null && occupant.IsEnemy && king.CanMoveTo(diagonal)) targets.Add(diagonal);
        }
    }

    private static void AddCannonTargets(PieceController king, Vector2Int origin, List<Vector2Int> targets)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        GridManager grid = PieceManager.Instance.gridManager;
        foreach (Vector2Int direction in directions)
        {
            bool jumped = false;
            Vector2Int position = origin + direction;
            while (grid.IsInBounds(position) && !IsDestroyedCell(grid, position))
            {
                PieceController occupant = PieceManager.Instance.GetPieceAt(position);
                if (!jumped)
                {
                    if (occupant == null) targets.Add(position);
                    else jumped = true;
                }
                else if (occupant != null)
                {
                    if (occupant.IsEnemy && king.CanMoveTo(position)) targets.Add(position);
                    break;
                }
                position += direction;
            }
        }
    }

    private static void AddGrasshopperTargets(PieceController king, Vector2Int origin, List<Vector2Int> targets)
    {
        GridManager grid = PieceManager.Instance.gridManager;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int direction = new Vector2Int(dx, dy);
                Vector2Int position = origin + direction;
                while (grid.IsInBounds(position) && !IsDestroyedCell(grid, position) && PieceManager.Instance.GetPieceAt(position) == null)
                    position += direction;
                if (grid.IsInBounds(position) && !IsDestroyedCell(grid, position)) AddScavengerTarget(king, position + direction, targets);
            }
        }
    }

    private static void AddScavengerTarget(PieceController king, Vector2Int target, List<Vector2Int> targets)
    {
        GridManager grid = PieceManager.Instance.gridManager;
        if (grid.IsInBounds(target) && !IsDestroyedCell(grid, target) && king.CanMoveTo(target) && !targets.Contains(target)) targets.Add(target);
    }

    private static bool IsDestroyedCell(GridManager grid, Vector2Int position)
    {
        GridCell cell = grid.GetGridCell(position);
        return cell != null && cell.isDestroyed;
    }

    private static void RefreshArtifactUI()
    {
        ArtifactManager.Instance?.UpdateInventoryUI();

        PieceController king = IsActive ? FindKing() : null;
        PieceController[] pieces = Object.FindObjectsByType<PieceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PieceController piece in pieces)
        {
            ShotgunKingAmmoBar bar = piece.GetComponent<ShotgunKingAmmoBar>();
            if (bar != null) bar.SetAmmoVisible(piece == king, Ammo, GetMaxAmmo());
        }

        if (king != null)
        {
            ShotgunKingAmmoBar bar = king.GetComponent<ShotgunKingAmmoBar>();
            if (bar == null) bar = king.gameObject.AddComponent<ShotgunKingAmmoBar>();
            bar.SetAmmoVisible(true, Ammo, GetMaxAmmo());
        }
    }
}
