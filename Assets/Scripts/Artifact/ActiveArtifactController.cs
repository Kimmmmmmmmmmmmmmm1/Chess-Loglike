using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 액티브 유물의 사용 흐름(즉시 발동 / 대상 선택)을 관리합니다.
/// 씬 배치가 필요 없으며 최초 사용 시 자동 생성됩니다.
/// </summary>
public class ActiveArtifactController : MonoBehaviour
{
    public static ActiveArtifactController Instance { get; private set; }

    public static bool IsTargeting => Instance != null && Instance.pendingArtifact != null;

    private ArtifactData pendingArtifact;
    private ActiveArtifactEffectBase pendingEffect;
    private ArtifactSlot pendingSlot;

    private readonly Dictionary<PieceController, Vector3> highlightedTargets = new Dictionary<PieceController, Vector3>();
    private const string HighlightTweenId = "ActiveArtifactTargetHighlight";

    public static ActiveArtifactController EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject(nameof(ActiveArtifactController));
        Instance = go.AddComponent<ActiveArtifactController>();
        return Instance;
    }

    public static void CancelIfTargeting()
    {
        if (IsTargeting) Instance.CancelTargeting();
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ClearHighlights();
            Instance = null;
        }
    }

    private void Update()
    {
        if (!IsTargeting) return;

        // 사용 조건이 깨지면(턴 종료, 전투 종료 등) 자동 취소
        if (!CanUseNow() || pendingSlot == null)
        {
            CancelTargeting();
            return;
        }

        if (IsCancelInputPressed())
        {
            CancelTargeting();
        }
    }

    /// <summary>
    /// 전투 중이고 내 턴이며 연출(승급 등) 중이 아닐 때만 사용 가능
    /// </summary>
    public static bool CanUseNow()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
            return false;
        if (GameManager.Instance != null && GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
            return false;
        if (TurnManager.Instance == null || !TurnManager.Instance.IsPlayerTurn)
            return false;
        if (PiecePromotionManager.Instance != null && PiecePromotionManager.Instance.IsPromoting)
            return false;
        return true;
    }

    /// <summary>
    /// 유물 슬롯 클릭 시 호출. 즉시 발동형은 바로 사용하고, 대상 선택형은 선택 모드로 진입합니다.
    /// </summary>
    public bool TryActivate(ArtifactData artifact, ArtifactSlot slot)
    {
        if (artifact == null || !artifact.IsActive) return false;

        // 이미 선택 모드면: 같은 유물 재클릭 시 취소, 다른 유물이면 교체
        if (IsTargeting)
        {
            bool isSame = pendingArtifact == artifact;
            CancelTargeting();
            if (isSame) return false;
        }

        ActiveArtifactEffectBase effect = ArtifactEffectRegistry.GetActiveEffect(artifact.id);
        if (effect == null)
        {
            Debug.LogWarning($"[ActiveArtifact] '{artifact.id}'에 등록된 액티브 효과가 없습니다. ArtifactEffectRegistry를 확인하세요.");
            slot?.PlayDeniedFeedback();
            return false;
        }

        if (!CanUseNow() || !effect.CanUse())
        {
            slot?.PlayDeniedFeedback();
            return false;
        }

        if (effect.TargetType == ActiveArtifactTargetType.None)
        {
            if (effect.Execute(null))
            {
                Consume(artifact, slot, effect);
                return true;
            }

            slot?.PlayDeniedFeedback();
            return false;
        }

        BeginTargeting(artifact, effect, slot);
        return true;
    }

    /// <summary>
    /// 대상 선택 모드에서 기물 클릭 시 호출. 선택 모드였다면 true(클릭 소비)를 반환합니다.
    /// </summary>
    public static bool TryHandlePieceClick(PieceController piece)
    {
        if (!IsTargeting) return false;
        Instance.HandlePieceClick(piece);
        return true;
    }

    private void HandlePieceClick(PieceController piece)
    {
        if (!CanUseNow())
        {
            CancelTargeting();
            return;
        }

        if (piece == null || !pendingEffect.IsValidTarget(piece))
        {
            if (piece != null && piece.PieceImage != null)
            {
                Transform t = piece.PieceImage.transform;
                t.DOComplete();
                t.DOShakePosition(0.3f, new Vector3(6f, 0f, 0f), 20, 90, false, true);
            }
            return;
        }

        ArtifactData artifact = pendingArtifact;
        ArtifactSlot slot = pendingSlot;
        ActiveArtifactEffectBase effect = pendingEffect;

        if (effect.Execute(piece))
        {
            EndTargeting();
            Consume(artifact, slot, effect);
        }
        else if (effect.ContinueTargetingAfterFailedExecution)
        {
            HighlightValidTargets();
        }
        else
        {
            EndTargeting();
            slot?.PlayDeniedFeedback();
        }
    }

    private void BeginTargeting(ArtifactData artifact, ActiveArtifactEffectBase effect, ArtifactSlot slot)
    {
        pendingArtifact = artifact;
        pendingEffect = effect;
        pendingSlot = slot;

        PieceManager.Instance?.ClearSelection();
        PieceManager.Instance?.HideMoveMarkers();
        slot?.SetTargetingVisual(true);
        HighlightValidTargets();
    }

    public void CancelTargeting()
    {
        EndTargeting();
    }

    private void EndTargeting()
    {
        pendingEffect?.CancelSelection();
        pendingSlot?.SetTargetingVisual(false);
        ClearHighlights();
        pendingArtifact = null;
        pendingEffect = null;
        pendingSlot = null;
    }

    private void Consume(ArtifactData artifact, ArtifactSlot slot, ActiveArtifactEffectBase effect)
    {
        if (slot != null)
        {
            slot.PlayUseFeedback();
        }

        ArtifactManager.Instance?.ConsumeArtifact(artifact);

        // 턴 소모 처리
        if (effect != null && effect.ConsumesTurn)
        {
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay)
            {
                if (TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn)
                {
                    TurnManager.Instance.AdvanceTurn(true);
                }
            }
        }
    }

    private void HighlightValidTargets()
    {
        ClearHighlights();
        if (PieceManager.Instance == null || pendingEffect == null) return;

        foreach (PieceController piece in PieceManager.Instance.Pieces)
        {
            if (piece == null || !pendingEffect.IsValidTarget(piece) || piece.PieceImage == null) continue;

            Transform t = piece.PieceImage.transform;
            highlightedTargets[piece] = t.localScale;
            t.DOScale(t.localScale * 1.15f, 0.35f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetId(HighlightTweenId);
        }
    }

    private void ClearHighlights()
    {
        DOTween.Kill(HighlightTweenId);
        foreach (var pair in highlightedTargets)
        {
            if (pair.Key != null && pair.Key.PieceImage != null)
            {
                pair.Key.PieceImage.transform.localScale = pair.Value;
            }
        }
        highlightedTargets.Clear();
    }

    private static bool IsCancelInputPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) return true;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
