using System.Collections.Generic;
using UnityEngine;

public abstract class SealBase : MonoBehaviour
{
    protected PieceController owner;
    protected SealData data;

    public SealData Data => data;

    public virtual void Initialize(SealData data, PieceController owner)
    {
        this.data = data;
        this.owner = owner;
        OnEquip();
    }

    public virtual void OnEquip() { }
    public virtual void OnUnequip() { }

    /// <summary>
    /// 인장이 소유 기물에게 무적 상태를 부여하는지 여부
    /// </summary>
    public virtual bool IsInvincible => false;

    /// <summary>
    /// 포획 시 이동하지 않고 제자리에서 포획(저격)하는지 여부
    /// </summary>
    public virtual bool CanSnipeWithoutMoving => false;

    /// <summary>
    /// Case A: 이동 경로 수정 훅
    /// </summary>
    public virtual void ModifyMoves(ref List<Vector2Int> moves, Vector2Int currentPos, bool isEnemy, System.Func<Vector2Int, bool> validator = null, System.Func<Vector2Int, bool> isOccupied = null) { }

    /// <summary>
    /// 툴팁 시각화를 위한 추가 이동범위 오프셋을 반환합니다.
    /// </summary>
    public virtual List<Vector2Int> GetPreviewAdditionalMovementOffsets(PieceType pieceType, bool isEnemy)
    {
        List<Vector2Int> previewMoves = PieceController.GetBaseMovementOffsetsForType(pieceType, isEnemy);
        List<Vector2Int> baseMoves = new List<Vector2Int>(previewMoves);

        Vector2Int origin = Vector2Int.zero;
        ModifyMoves(
            ref previewMoves,
            origin,
            isEnemy,
            target => Mathf.Abs(target.x) <= 3 && Mathf.Abs(target.y) <= 3,
            _ => false);

        List<Vector2Int> additionalOffsets = new List<Vector2Int>();
        foreach (var move in previewMoves)
        {
            if (!baseMoves.Contains(move))
            {
                additionalOffsets.Add(move);
            }
        }

        return additionalOffsets;
    }

    /// <summary>
    /// 툴팁에서 기존 이동범위를 대체하는지 여부입니다.
    /// </summary>
    public virtual bool ReplacesMovementPreview => false;

    /// <summary>
    /// 툴팁에 표시할 최종 이동범위 오프셋입니다.
    /// 기본적으로는 추가분만 반환합니다.
    /// </summary>
    public virtual List<Vector2Int> GetPreviewMovementOffsets(PieceType pieceType, bool isEnemy)
    {
        return GetPreviewAdditionalMovementOffsets(pieceType, isEnemy);
    }

    /// <summary>
    /// Case B: 파괴 직전 거부권 행사 훅 (return false 시 파괴 취소)
    /// </summary>
    public virtual bool OnBeforeDestroy() 
    { 
        return true; 
    }

    /// <summary>
    /// Case F: 기물이 이동하기 직전에 호출되는 훅 (경로/웨이포인트 설정 등)
    /// </summary>
    public virtual void OnBeforeMove(Vector2Int fromPos, Vector2Int toPos) { }

    /// <summary>
    /// Case C: 이동 완료 후 추가 행동 훅
    /// </summary>
    public virtual void OnAfterMove(Vector2Int prevPos, Vector2Int newPos) { }

    /// <summary>
    /// Case C: 이동 완료 후 추가 행동 훅 (포획 여부 포함)
    /// </summary>
    public virtual void OnAfterMove(Vector2Int prevPos, Vector2Int newPos, bool captured)
    {
        OnAfterMove(prevPos, newPos);
    }

    /// <summary>
    /// Case E: 소유 기물이 적 기물을 잡고 이동을 마친 후 호출되는 훅
    /// </summary>
    public virtual void OnAfterCapture(PieceType capturedType) { }

    /// <summary>
    /// Case D: 소유 기물 파괴 시 호출되는 훅
    /// </summary>
    public virtual void OnOwnerDestroyed(PieceController killer, Vector2Int ownerPosition) { }
}
