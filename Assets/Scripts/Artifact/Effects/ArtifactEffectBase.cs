using UnityEngine;

public abstract class ArtifactEffectBase
{
    public abstract string ArtifactId { get; }

    protected bool TryGetArtifactLevel(out int level)
    {
        level = 0;
        return ArtifactManager.Instance != null && ArtifactManager.Instance.HasArtifact(ArtifactId, out level);
    }

    public virtual void ResetStageLimitedEffects() { }
    public virtual bool IsExhausted() => false;
    public virtual bool HasRemaining() => true;
    public virtual int GetGaugeCurrentCount() => 0;
    public virtual int GetGaugeMaxCount() => 0;
}