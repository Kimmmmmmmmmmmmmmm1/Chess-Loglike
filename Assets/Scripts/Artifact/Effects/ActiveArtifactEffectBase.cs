using UnityEngine;

/// <summary>
/// 액티브 유물 사용 시 필요한 대상 종류
/// </summary>
public enum ActiveArtifactTargetType
{
    None,       // 클릭 즉시 발동
    AllyPiece,  // 보드 위 아군 기물 선택
    EnemyPiece, // 보드 위 적 기물 선택
    AnyPiece    // 보드 위 아무 기물 선택
}

/// <summary>
/// 사용하면 소모되는 액티브 유물 효과의 베이스 클래스.
/// 새 액티브 유물을 만들 때 이 클래스를 상속하고 ArtifactEffectRegistry에 등록하세요.
/// </summary>
public abstract class ActiveArtifactEffectBase : ArtifactEffectBase
{
    public virtual ActiveArtifactTargetType TargetType => ActiveArtifactTargetType.None;

    /// <summary>
    /// 액티브 유물 사용 시 플레이어의 턴을 소모하는지 여부 (기본값: true)
    /// </summary>
    public virtual bool ConsumesTurn => true;

    /// <summary>
    /// 현재 사용 가능한지 (턴/상태 조건은 ActiveArtifactController에서 공통으로 검사)
    /// </summary>
    public virtual bool CanUse() => true;

    /// <summary>
    /// 대상 선택형 유물에서 해당 기물이 유효한 대상인지
    /// </summary>
    public virtual bool IsValidTarget(PieceController piece)
    {
        if (piece == null || piece.CurrentLocation != PieceLocation.Board || !piece.GridPosition.HasValue)
        {
            return false;
        }

        return TargetType switch
        {
            ActiveArtifactTargetType.AllyPiece => !piece.IsEnemy,
            ActiveArtifactTargetType.EnemyPiece => piece.IsEnemy,
            ActiveArtifactTargetType.AnyPiece => true,
            _ => false
        };
    }

    /// <summary>
    /// 효과를 실행합니다. true를 반환하면 유물이 1개 소모됩니다.
    /// TargetType이 None이면 target은 null입니다.
    /// </summary>
    public abstract bool Execute(PieceController target);
}

