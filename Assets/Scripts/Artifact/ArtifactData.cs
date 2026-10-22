using UnityEngine;

public enum ArtifactRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Passive: 보유하는 동안 영구적으로 효과를 발휘합니다.
/// Active: 전투 중 내 턴에 클릭하여 사용합니다. (중복 보유 가능)
/// </summary>
public enum ArtifactType
{
    Passive,
    Active
}

[CreateAssetMenu(fileName = "NewArtifact", menuName = "Chess/Artifact Data")]
public class ArtifactData : ScriptableObject
{
    public string id;
    public string artifactName;
    [TextArea] public string description; // 플레이스홀더 포함 (예: "{0}번 파괴 시...")
    [TextArea] public string flavorText;
    public ArtifactRarity rarity;
    public Sprite icon;
    public int price = 150; // 상점 판매 가격
    public bool isSoldInShop = true; // 상점에서 판매 여부
    public bool isGaugeRequired = false; // 게이지 필요 여부

    [Header("Type")]
    public ArtifactType artifactType = ArtifactType.Passive;

    [Header("Enhancement")]
    [Min(1)] public int maxLevel = 1;
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField] private bool canEnhanceWhenActive;
    [SerializeField] private bool consumesOnUse = true;

    // 런타임 전용: 액티브 유물 중복 보유 개수
    [System.NonSerialized] private int stackCount = 1;

    public bool IsActive => artifactType == ArtifactType.Active;
    public bool IsPassive => artifactType == ArtifactType.Passive;
    public bool CanEnhanceWhenActive => canEnhanceWhenActive;
    public bool ConsumesOnUse => consumesOnUse;
    public int StackCount => Mathf.Max(1, stackCount);

    public int Level => Mathf.Clamp(level, 1, MaxLevel);
    public int MaxLevel => Mathf.Max(1, maxLevel);
    public bool CanEnhance => (IsPassive || canEnhanceWhenActive) && Level < MaxLevel;

    public void ResetStack()
    {
        stackCount = 1;
    }

    public void SetStack(int count)
    {
        stackCount = Mathf.Max(0, count);
    }

    public void AddStack(int amount = 1)
    {
        stackCount = StackCount + Mathf.Max(0, amount);
    }

    /// <summary>
    /// 스택을 1 감소시킵니다. 남은 스택이 없으면 false를 반환합니다.
    /// </summary>
    public bool RemoveStack()
    {
        if (StackCount <= 1)
        {
            stackCount = 0;
            return false;
        }

        stackCount = StackCount - 1;
        return true;
    }

    public bool TryEnhance()
    {
        if (!CanEnhance)
        {
            return false;
        }

        level = Level + 1;
        return true;
    }

    public void ResetLevel()
    {
        level = 1;
    }

    public void SetLevel(int targetLevel)
    {
        level = Mathf.Clamp(targetLevel, 1, MaxLevel);
    }

    public string GetTooltipTitle()
    {
        if (IsActive)
        {
            string stackText = StackCount > 1 ? $" ({StackCount})" : string.Empty;
            string levelText = canEnhanceWhenActive ? $" Lv.{Level}/{MaxLevel}" : string.Empty;
            return $"{artifactName}{stackText}{levelText} [액티브]";
        }

        if (MaxLevel <= 1 || Level <= 1)
        {
            return artifactName;
        }

        return $"{artifactName} (Lv.{Level})";
    }

    public string GetTooltipDescription()
    {
        string desc = GetDescription(Level);
        if (IsActive)
        {
            desc += consumesOnUse
                ? "\n\n<color=#AAAAAA>전투 중 내 턴에 클릭하여 사용 (1회용, 턴 소모)</color>"
                : "\n\n<color=#AAAAAA>전투 중 내 턴에 클릭하여 사용 (재사용 가능, 턴 소모)</color>";
        }
        return desc;
    }

    /// <summary>
    /// 레벨에 맞는 동적 설명을 반환합니다.
    /// ArtifactEffectHandlers에서 실제 게임 로직으로 사용되는 값들을 가져와 description의 
    /// {0}, {1}, {2} 플레이스홀더를 대체합니다.
    /// </summary>
    public string GetDescription(int level)
    {
        if (MaxLevel <= 1)
        {
            return description;
        }

        // ArtifactEffectHandlers에서 해당 아티팩트의 실제 효과값 가져오기
        string result = description;
        
        // ID별로 동적 값을 가져옴
        int value0 = GetDynamicValue(0, level);
        int value1 = GetDynamicValue(1, level);
        int value2 = GetDynamicValue(2, level);

        // {0}, {1}, {2} 플레이스홀더 대체
        if (value0 >= 0) result = result.Replace("{0}", value0.ToString());
        if (value1 >= 0) result = result.Replace("{1}", value1.ToString());
        if (value2 >= 0) result = result.Replace("{2}", value2.ToString());

        return result;
    }

    /// <summary>
    /// 아티팩트 ID와 레벨을 기반으로 ArtifactEffectHandlers에서 실제 효과값을 가져옵니다.
    /// </summary>
    private int GetDynamicValue(int placeholderIndex, int level)
    {
        // ID별로 어느 플레이스홀더에 어떤 값을 넣을지 결정
        return id switch
        {
            "A001" => placeholderIndex == 0 ? ArtifactEffectHandlers.GetSlotBonusForLevel(level) : -1,
            "A002" => placeholderIndex == 0 ? ArtifactEffectHandlers.GetRerollDiscountForLevel(level) : -1,
            "A003" => placeholderIndex == 0 ? (int)ArtifactEffectHandlers.GetSealChanceBonusForLevel(level) : -1,
            "A004" => placeholderIndex == 0 ? ArtifactEffectHandlers.GetGourdRecoveryMaxForLevel(level) : -1,
            "A005" => placeholderIndex == 0 ? ArtifactEffectHandlers.GetTombstoneThresholdForLevel(level) : -1,
            SupportSealArtifactEffect.Id => placeholderIndex == 0 ? SupportSealArtifactEffect.GetCooldownForLevel(level) : -1,
            _ => -1
        };
    }
    
    // 추후 효과 구현 시 여기에 효과 타입이나 수치 등을 추가할 수 있습니다.
}
