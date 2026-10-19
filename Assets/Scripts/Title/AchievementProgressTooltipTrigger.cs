using UnityEngine;
using UnityEngine.EventSystems;

public class AchievementProgressTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private string progressText = string.Empty;
    private AchievementData achievementData;

    public void SetProgressText(string value, AchievementData achievement = null)
    {
        progressText = string.IsNullOrEmpty(value) ? string.Empty : value;
        if (achievement != null)
        {
            achievementData = achievement;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance == null || string.IsNullOrEmpty(progressText))
        {
            return;
        }

        TooltipManager.Instance.RegisterTooltipSource(gameObject);
        string title = achievementData != null && !string.IsNullOrEmpty(achievementData.achievementName)
            ? achievementData.achievementName
            : "업적 진행도";
        string desc = achievementData != null && !string.IsNullOrEmpty(achievementData.description)
            ? $"{achievementData.description}\n\n진행도: {progressText}"
            : progressText;
        Sprite icon = achievementData != null ? achievementData.icon : null;
        string subtitle = "<color=#D97706>★</color>  <size=85%>업적 (Achievement)</size>";

        TooltipManager.Instance.ShowTooltip(
            title,
            desc,
            transform.position,
            string.Empty,
            TooltipManager.TooltipPriorityDefault,
            gameObject,
            icon,
            subtitle,
            "★");
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
        TooltipManager.Instance.HideTooltip(gameObject);
    }
}
