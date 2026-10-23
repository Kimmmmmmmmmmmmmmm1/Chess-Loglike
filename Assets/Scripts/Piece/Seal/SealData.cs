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

[CreateAssetMenu(fileName = "NewSealData", menuName = "Chess/Seal Data")]
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

    /// <summary>
    /// 해당 기물 타입에 이 인장을 장착하는 것이 무의미하거나 불필요한지(중복 이동 능력 등) 여부를 판별합니다.
    /// </summary>
    public bool IsUselessForPiece(PieceType pieceType, bool isEnemy = false)
    {
        // 1. 호환 기물 목록이 명시적으로 설정되어 있고, 해당 기물이 포함되지 않은 경우
        if (compatiblePieces != null && compatiblePieces.Count > 0 && !compatiblePieces.Contains(pieceType))
        {
            return true;
        }

        string sName = !string.IsNullOrEmpty(sealName) ? sealName : name;

        // 2. 도약의 인장: 이미 나이트 이동 능력을 보유한 기물
        if (sName == "도약의 인장" || name == "KnightLeap")
        {
            if (pieceType == PieceType.Knight ||
                pieceType == PieceType.Chancellor ||
                pieceType == PieceType.Archbishop ||
                pieceType == PieceType.Centaur ||
                pieceType == PieceType.Amazon)
            {
                return true;
            }
        }

        // 3. 부서진 비숍 인장: 이미 비숍 대각선 슬라이딩 이동 능력을 보유한 기물
        if (sName == "부서진 비숍 인장" || name == "Diagnal")
        {
            if (pieceType == PieceType.Bishop ||
                pieceType == PieceType.Queen ||
                pieceType == PieceType.Archbishop ||
                pieceType == PieceType.Amazon)
            {
                return true;
            }
        }

        // 4. 폰의 인장: 이미 앞 대각선 포획 능력을 보유한 기물
        if (sName == "폰의 인장" || name == "Pawn" || name == "PawnSeal")
        {
            if (pieceType == PieceType.Pawn ||
                pieceType == PieceType.King ||
                pieceType == PieceType.Bishop ||
                pieceType == PieceType.Queen ||
                pieceType == PieceType.Archbishop ||
                pieceType == PieceType.Centaur ||
                pieceType == PieceType.Amazon)
            {
                return true;
            }
        }

        // 5. 측면의 인장: 이미 측면(좌우) 이동이 가능한 기물
        if (sName == "측면의 인장" || name == "LateralAdvance")
        {
            if (pieceType == PieceType.Rook ||
                pieceType == PieceType.Queen ||
                pieceType == PieceType.King ||
                pieceType == PieceType.Chancellor ||
                pieceType == PieceType.Centaur ||
                pieceType == PieceType.Amazon)
            {
                return true;
            }
        }

        // 6. 환영의 인장: 아군 기물 통과. 나이트 등 도약 기물이나 슬라이딩하지 않는 기물에는 무의미
        if (sName == "환영의 인장" || name == "Phantom")
        {
            if (pieceType == PieceType.Knight ||
                pieceType == PieceType.Grasshopper ||
                pieceType == PieceType.Pawn ||
                pieceType == PieceType.King ||
                pieceType == PieceType.Centaur)
            {
                return true;
            }
        }

        // 7. 자폭의 인장: 폰(Pawn) 전용
        if (sName == "자폭의 인장" || name == "Destruction")
        {
            if (pieceType != PieceType.Pawn)
            {
                return true;
            }
        }

        // 8. 승급의 인장: 폰(Pawn) 전용
        if (sName == "승급의 인장" || sName == "승급자의 인장" || name == "Promotion")
        {
            if (pieceType != PieceType.Pawn)
            {
                return true;
            }
        }

        // 9. 전향의 인장: 적군 스테이지 배치 시 부착 불가
        if (sName == "전향의 인장" || name == "Conversion")
        {
            if (isEnemy)
            {
                return true;
            }
        }

        // 10. 샷건 킹 무기 파츠: 적군은 사용 불가
        if (isEnemy)
        {
            if (sName == "점프 샷" || sName == "확장 탄창" || sName == "유령 슬러그" ||
                sName == "화염 탄약" || sName == "장총열" || sName == "도탄 사격" ||
                sName == "스캐빈저 총열" || sName == "산탄 확산" || sName == "최후의 탄환" ||
                sName == "단검" ||
                name == "JumpShot" || name == "ExtendedMagazine" || name == "GhostSlug" ||
                name == "IncendiaryMod" || name == "LongBarrel" || name == "RicochetShot" ||
                name == "ScavengerBarrel" || name == "ShotSpread" || name == "ShotgunLastStand" ||
                name == "Dagger")
            {
                return true;
            }
        }

        // 11. 저격의 인장: 장거리 기물만 장착 가능
        if (sName == "저격의 인장" || name == "Sniper" || name == "SniperSeal")
        {
            if (!PieceController.IsLongRangePiece(pieceType))
            {
                return true;
            }
        }

        // 12. 갈고리의 인장: 기본 이동이 직선이 아닌 기물 (Knight)
        if (sName == "갈고리의 인장" || name == "Hook" || name == "HookSeal")
        {
            if (pieceType == PieceType.Knight)
            {
                return true;
            }
        }

        return false;
    }
}
