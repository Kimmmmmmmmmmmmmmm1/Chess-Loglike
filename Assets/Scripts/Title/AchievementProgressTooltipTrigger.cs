using UnityEngine;
using UnityEngine.EventSystems;

public class AchievementProgressTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private string progressText = string.Empty;

    public void SetProgressText(string value)
    {
        progressText = string.IsNullOrEmpty(value) ? string.Empty : value;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance == null || string.IsNullOrEmpty(progressText))
        {
            return;
        }

        TooltipManager.Instance.RegisterTooltipSource(gameObject);
        TooltipManager.Instance.ShowTooltip(string.Empty, progressText, transform.position, string.Empty);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideProgressTooltip();
    }

    private void OnDisable()
    {
        HideProgressTooltip();
    }

    private void HideProgressTooltip()
    {
        if (TooltipManager.Instance == null)
        {
            return;
        }

        TooltipManager.Instance.UnregisterTooltipSource(gameObject);
        TooltipManager.Instance.HideTooltip();
    }
}
