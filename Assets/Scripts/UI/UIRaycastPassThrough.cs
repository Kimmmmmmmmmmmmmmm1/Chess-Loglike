using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycastPassThrough : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private bool passOnlyTopmostBehindTarget = true;

    private bool isPointerOver;
    private GameObject currentBehindTarget;

    // 1. 앞 오브젝트 자신이 감지했을 때의 로직
    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        RefreshBehindTarget();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        ClearBehindTarget(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PassEvent(eventData, ExecuteEvents.pointerClickHandler);
    }

    private void Update()
    {
        if (!isPointerOver)
        {
            return;
        }

        RefreshBehindTarget();
    }

    // 마우스 아래에 있는 모든 UI를 찾아 이벤트를 통과시키는 핵심 메서드
    private void PassEvent<T>(PointerEventData data, ExecuteEvents.EventFunction<T> function) where T : IEventSystemHandler
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);

        foreach (RaycastResult result in results)
        {
            // 자기 자신은 무시 (무한루프 방지)
            if (result.gameObject == gameObject || result.gameObject.transform.IsChildOf(transform)) continue;

            // 뒤에 있는 오브젝트의 실제 핸들러가 부모에 붙어 있을 수 있으므로 hierarchy로 전달
            if (ExecuteEvents.ExecuteHierarchy(result.gameObject, data, function))
            {
                if (passOnlyTopmostBehindTarget)
                {
                    break;
                }
            }

            // 만약 바로 뒤에 있는 오브젝트 '하나'에만 넘기고 싶다면 여기서 break; 하시면 됩니다.
        }
    }

    private void RefreshBehindTarget()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        GameObject nextTarget = GetTopmostBehindTarget(pointerData);
        if (nextTarget == currentBehindTarget)
        {
            return;
        }

        if (currentBehindTarget != null)
        {
            ExecuteOnEntireHierarchy(currentBehindTarget, pointerData, ExecuteEvents.pointerExitHandler);
        }

        currentBehindTarget = nextTarget;

        if (currentBehindTarget != null)
        {
            ExecuteOnEntireHierarchy(currentBehindTarget, pointerData, ExecuteEvents.pointerEnterHandler);
        }
    }

    private void ClearBehindTarget(PointerEventData pointerData)
    {
        if (currentBehindTarget == null)
        {
            return;
        }

        ExecuteOnEntireHierarchy(currentBehindTarget, pointerData, ExecuteEvents.pointerExitHandler);
        currentBehindTarget = null;
    }

    private void ExecuteOnEntireHierarchy<T>(GameObject target, PointerEventData data, ExecuteEvents.EventFunction<T> function)
        where T : IEventSystemHandler
    {
        if (target == null)
        {
            return;
        }

        Transform current = target.transform;
        while (current != null)
        {
            ExecuteEvents.Execute(current.gameObject, data, function);
            current = current.parent;
        }
    }

    private GameObject GetTopmostBehindTarget(PointerEventData data)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == gameObject || result.gameObject.transform.IsChildOf(transform))
            {
                continue;
            }

            return result.gameObject;
        }

        return null;
    }
}