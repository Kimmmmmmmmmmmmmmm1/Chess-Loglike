using UnityEngine;

/// <summary>
/// 2지선다 행선지 선택 카드 타입
/// </summary>
public enum StageNodeType
{
    NormalBattle,   // ⚔️ 일반 전투
    EliteBattle,    // 🔥 엘리트 전투
    Shop,           // 🛒 상점
    WorkShop,       // ⚒️ 공방
    Treasure,       // 🎁 보물
    BossBattle      // 💀 보스 전투
}

/// <summary>
/// 전투 세부 타입
/// </summary>
public enum BattleType
{
    Normal,
    Elite,
    Boss
}

public static class StageNodeHelper
{
    public static string GetTitle(StageNodeType type)
    {
        switch (type)
        {
            case StageNodeType.NormalBattle: return "⚔️ 일반 전투";
            case StageNodeType.EliteBattle:  return "🔥 엘리트 전투";
            case StageNodeType.Shop:         return "🛒 상점";
            case StageNodeType.WorkShop:     return "⚒️ 공방";
            case StageNodeType.Treasure:     return "🎁 보물";
            case StageNodeType.BossBattle:   return "💀 보스 전투";
            default: return "전투";
        }
    }

    public static string GetDescription(StageNodeType type)
    {
        switch (type)
        {
            case StageNodeType.NormalBattle:
                return "기본 난이도의 적 AI 기물과 전멸전을 진행합니다.";
            case StageNodeType.EliteBattle:
                return "인장이나 변형 기물로 무장한 강적이 등장합니다.";
            case StageNodeType.Shop:
                return "골드를 소모하여 기물, 인장, 유물을 확정 구매합니다.";
            case StageNodeType.WorkShop:
                return "기물 합성 및 유물 강화를 통해 덱을 정비합니다.";
            case StageNodeType.Treasure:
                return "상자를 열어 유물, 인장 또는 대량의 골드를 무료로 획득합니다.";
            case StageNodeType.BossBattle:
                return "특수 기믹과 패시브를 가진 강력한 보스와 결전을 치릅니다.";
            default:
                return "";
        }
    }

    public static string GetRewardText(StageNodeType type)
    {
        switch (type)
        {
            case StageNodeType.NormalBattle:
                return "<color=#FFD700>기본 골드</color> + 일반 3지선다 보상";
            case StageNodeType.EliteBattle:
                return "<color=#FFD700>2배 골드</color> + <color=#4169E1>고급(Rare 이상)</color> 확정 보상";
            case StageNodeType.Shop:
                return "<color=#A0A0A0>전투 없음 (골드 소비처)</color>";
            case StageNodeType.WorkShop:
                return "<color=#A0A0A0>전투 없음 (덱 압축 및 성장)</color>";
            case StageNodeType.Treasure:
                return "<color=#FFD700>무료 특별 보상 1종</color> 즉시 획득";
            case StageNodeType.BossBattle:
                return "<color=#FFD700>대량의 골드</color> + <color=#FF4500>전설/에픽급</color> 확정 보상";
            default:
                return "";
        }
    }

    public static Color GetThemeColor(StageNodeType type)
    {
        switch (type)
        {
            case StageNodeType.NormalBattle:
                return new Color(0.85f, 0.85f, 0.85f, 1f);
            case StageNodeType.EliteBattle:
                return new Color(1f, 0.45f, 0.2f, 1f); // 오렌지/레드
            case StageNodeType.Shop:
                return new Color(0.4f, 0.8f, 1f, 1f);  // 스카이블루
            case StageNodeType.WorkShop:
                return new Color(0.9f, 0.75f, 0.3f, 1f); // 골드/황동
            case StageNodeType.Treasure:
                return new Color(0.6f, 1f, 0.5f, 1f);  // 에메랄드
            case StageNodeType.BossBattle:
                return new Color(0.9f, 0.2f, 0.25f, 1f); // 크림슨 레드
            default:
                return Color.white;
        }
    }

    public static void ExecuteDefaultAction(StageNodeType type)
    {
        if (GameManager.Instance == null) return;

        switch (type)
        {
            case StageNodeType.NormalBattle:
                GameManager.Instance.StartBattle(BattleType.Normal);
                break;
            case StageNodeType.EliteBattle:
                GameManager.Instance.StartBattle(BattleType.Elite);
                break;
            case StageNodeType.BossBattle:
                GameManager.Instance.StartBattle(BattleType.Boss);
                break;
            case StageNodeType.Shop:
                GameManager.Instance.OpenShop();
                break;
            case StageNodeType.WorkShop:
                GameManager.Instance.OpenWorkShop();
                break;
            case StageNodeType.Treasure:
                GameManager.Instance.OpenTreasure();
                break;
        }
    }
}

