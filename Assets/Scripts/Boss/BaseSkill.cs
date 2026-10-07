using UnityEngine;

public abstract class BaseSkill : MonoBehaviour, ISkillEffect
{
    [Tooltip("스킬 이름")]
    public string skillName;

    [Tooltip("스킬 설명")]
    [TextArea(3, 5)]
    public string skillDescription;

    [Tooltip("스킬이 실행될 턴 간격 (예: 3이면 3턴마다 실행)")]
    public int executionInterval = 1;

    protected SkillExecutionContext executionContext;

    public virtual void SetExecutionContext(SkillExecutionContext context)
    {
        executionContext = context;
    }

    public virtual bool ShouldExecute(int currentTurnCount)
    {
        return executionInterval > 0 && currentTurnCount % executionInterval == 0;
    }

    public abstract void ExecuteSkill();

    public abstract void ApplyEffect(Vector2Int gridPosition);

    public abstract void ApplyEffectInRange(Vector2Int centerPosition, int radius);

    public abstract void ApplyEffectToMultiplePositions(System.Collections.Generic.List<Vector2Int> positions);

    public virtual bool ValidateSkill()
    {
        if (string.IsNullOrEmpty(skillName))
        {
            return false;
        }

        if (executionInterval < 1)
        {
            return false;
        }

        return true;
    }

    protected bool IsGameActive()
    {
        return GameManager.Instance != null && 
               GameManager.Instance.CurrentFlowState == GameFlowState.Battle &&
               GameStateManager.Instance != null &&
               GameStateManager.Instance.CurrentState == GameStateManager.GameState.GamePlay;
    }
}
