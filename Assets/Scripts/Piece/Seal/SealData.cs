using System.Collections.Generic;
using UnityEngine;

public enum SealEffectType
{
    None,
    AddMove,
    ChangeMove,
    SpecialAbility
}

public enum SealRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

[CreateAssetMenu(fileName = "NewSealData", menuName = "Janggi/Seal Data")]
public class SealData : ScriptableObject
{
    [Header("Basic Info")]
    public string sealName;       // 이름
    public SealRarity rarity;     // 희귀도
    [TextArea]
    public string description;    // 설명
    [TextArea]
    public string flavorText;     // 플레이버 텍스트
    public Sprite icon;           // UI에 표시될 아이콘
    public bool isSoldInShop = true; // 상점에서 판매 여부 (승급 인장 등은 false)

    [Header("Logic")]
    [Tooltip("SealBase를 상속받은 컴포넌트가 붙은 프리팹")]
    public GameObject sealPrefab; 

    [Header("Compatibility")]
    [Tooltip("비어있으면 모든 기물에 장착 가능")]
    public List<PieceType> compatiblePieces; // 장착 가능한 기물 종류

    [Header("Effect (Legacy/Optional)")]
    public SealEffectType effectType; // 효과 타입
    public float effectValue;         // 효과 수치
}
