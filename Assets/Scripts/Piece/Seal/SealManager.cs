using System.Collections.Generic;
using UnityEngine;

public class SealManager : MonoBehaviour
{
    public static SealManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public List<SealBase> GetAttachedSeal(PieceController piece)
    {
        return piece.EquippedSeals;
    }
    // 기물에 인장 장착 시도
    public void TryAttachSealToPiece(PieceController piece, SealData seal)
    {
        // 1. 호환성 체크
        if (seal.compatiblePieces != null && seal.compatiblePieces.Count > 0)
        {
            if (!seal.compatiblePieces.Contains(piece.Type))
            {
                // TODO: 실패 피드백 (사운드, 텍스트 등)
                return;
            }
        }

        // 2. 이미 같은 인장이 있는지, 슬롯이 꽉 찼는지 등 추가 조건 체크 가능
        
        // 3. 인장 장착 (컴포넌트 추가)
        piece.EquipSeal(seal);
    }

    /// <summary>
    /// 기물에서 인장을 장착 해제하고 인벤토리로 반환합니다.
    /// </summary>
    public bool TryDetachSealFromPiece(PieceController piece, SealData sealToDetach)
    {
        if (piece == null || sealToDetach == null) return false;

        // 전투 준비 단계가 아닐 경우 해제 불가
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.Prepare) return false;

        bool success = piece.UnEquipSeal(sealToDetach);
        if (success)
        {
            // SealInventory에 인장 반환
            if (SealInventory.Instance != null)
            {
                SealInventory.Instance.AddSeal(sealToDetach);
            }
        }
        return success;
    }

    /// <summary>
    /// 기물의 인장을 다른 인장으로 교체합니다.
    /// </summary>
    public bool TrySwapSeal(PieceController piece, SealData oldSeal, SealData newSeal)
    {
        if (piece == null || oldSeal == null || newSeal == null) return false;

        // 전투 준비 단계가 아닐 경우 교체 불가
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.Prepare) return false;

        // 1. 기존 인장 해제 시도
        if (TryDetachSealFromPiece(piece, oldSeal))
        {
            // 2. 새 인장 장착 시도
            TryAttachSealToPiece(piece, newSeal);
            return true;
        }
        return false;
    }
}
