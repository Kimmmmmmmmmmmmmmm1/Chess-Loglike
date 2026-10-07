using UnityEngine;
using UnityEngine.EventSystems;

public class CounterTooltipHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum CounterType
    {
        Coin,
        EnemyCaptured
    }

    [Header("Tooltip Target")]
    [SerializeField] private CounterType counterType;

    [Header("Tooltip Labels")]
    [SerializeField] private string coinLabel = "보유 코인:";
    [SerializeField] private string enemyCapturedLabel = "물리친 적의 수:";

    private bool isPointerInside;
    private int lastShownValue = int.MinValue;

    private void Update()
    {
        if (!isPointerInside)
        {
            return;
        }

        int currentValue = GetValue();
        if (currentValue != lastShownValue)
        {
            ShowCounterTooltip(currentValue);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance == null)
        {
            return;
        }

        isPointerInside = true;
        ShowCounterTooltip(GetValue());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        lastShownValue = int.MinValue;
        TooltipManager.Instance?.HideTooltip(gameObject);
    }

    private void OnDisable()
    {
        isPointerInside = false;
        lastShownValue = int.MinValue;
        TooltipManager.Instance?.HideTooltip(gameObject);
    }

    private void ShowCounterTooltip(int value)
    {
        if (TooltipManager.Instance == null)
        {
            return;
        }

        lastShownValue = value;
        TooltipManager.Instance.ShowTooltip(
            "",
            GetLabel() + value.ToString(),
            transform.position,
            string.Empty,
            TooltipManager.TooltipPriorityDefault,
            gameObject);
    }

    private string GetLabel()
    {
        return counterType == CounterType.Coin ? coinLabel : enemyCapturedLabel;
    }

    private int GetValue()
    {
        if (GameManager.Instance == null)
        {
            return 0;
        }

        return counterType == CounterType.Coin
            ? GameManager.Instance.Coin
            : GameManager.Instance.EnemyPiecesCaptured;
    }
}
