using UnityEngine.EventSystems;

/// <summary>
/// 인장(Seal)을 드롭받을 수 있는 UI 요소를 위한 인터페이스입니다.
/// </summary>
public interface ISealDropTarget : IDropHandler
{
    /// <summary>
    /// 드롭된 인장을 처리합니다.
    /// </summary>
    void HandleSealDrop(DraggableSeal draggableSeal);
}