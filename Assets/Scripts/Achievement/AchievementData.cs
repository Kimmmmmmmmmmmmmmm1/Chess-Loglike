using UnityEngine;

public enum AchievementCategory
{
    Combat,
    Collection,
    Other
}

[CreateAssetMenu(fileName = "NewAchievement", menuName = "Janggi/Achievement Data")]
public class AchievementData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string achievementName;
     public Sprite icon;
    [TextArea] public string description;
    public AchievementCategory category = AchievementCategory.Other;
    [Tooltip("연계 업적의 다음 단계. 비워두면 체인이 종료됩니다.")]
    public AchievementData nextAchievement;

    [Header("Progress")]
    [Min(1)] public int targetCount = 1;
    [Min(0)] public int difficulty = 0;

    [Header("Step Progress")]
    public bool useStepProgress = false;
    [Min(1)] public int stepCount = 5;
    [Min(1)] public int stepGoalInterval = 5;

    public int GetFinalTargetCount()
    {
        if (!useStepProgress)
        {
            return Mathf.Max(1, targetCount);
        }

        return Mathf.Max(1, stepCount) * Mathf.Max(1, stepGoalInterval);
    }

    public int GetCurrentDisplayTarget(int currentCount)
    {
        if (!useStepProgress)
        {
            return Mathf.Max(1, targetCount);
        }

        int safeStepCount = Mathf.Max(1, stepCount);
        int safeInterval = Mathf.Max(1, stepGoalInterval);
        int finalTarget = GetFinalTargetCount();
        int clampedCurrent = Mathf.Clamp(currentCount, 0, finalTarget);

        int completedSteps = Mathf.Clamp(clampedCurrent / safeInterval, 0, safeStepCount);
        int currentStep = completedSteps >= safeStepCount ? safeStepCount : completedSteps + 1;
        return Mathf.Min(finalTarget, currentStep * safeInterval);
    }

    public int GetCurrentStep(int currentCount)
    {
        if (!useStepProgress)
        {
            return 1;
        }

        int safeStepCount = Mathf.Max(1, stepCount);
        int safeInterval = Mathf.Max(1, stepGoalInterval);
        int finalTarget = GetFinalTargetCount();
        int clampedCurrent = Mathf.Clamp(currentCount, 0, finalTarget);

        int completedSteps = Mathf.Clamp(clampedCurrent / safeInterval, 0, safeStepCount);
        return completedSteps >= safeStepCount ? safeStepCount : completedSteps + 1;
    }

    public string GetProgressText(int currentCount)
    {
        int finalTarget = GetFinalTargetCount();
        int safeCurrent = Mathf.Clamp(currentCount, 0, finalTarget);

        if (!useStepProgress)
        {
            return $"{safeCurrent}/{Mathf.Max(1, targetCount)}";
        }

        int displayTarget = GetCurrentDisplayTarget(safeCurrent);
        return $"{safeCurrent}/{displayTarget}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (nextAchievement == this)
        {
            nextAchievement = null;
        }
    }
#endif
}