using System;
using UnityEngine;

/// <summary>
/// 선택지(행선지, 이벤트, 보상, 딜레마 등) 카드에 표시할 데이터 및 선택 시 실행될 콜백을 담는 인스턴스 클래스입니다.
/// StageSelectUI.Instance.ShowChoices(...)에 전달하여 어디서든 범용 카드 선택 UI를 띄울 수 있습니다.
/// </summary>
[System.Serializable]
public class StageChoiceOption
{
    public string id;
    public string title;
    public string subtitle;
    [TextArea(2, 5)]
    public string description;
    [TextArea(1, 3)]
    public string rewardText;
    public string buttonText = "선택";
    public Color themeColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    public Sprite iconSprite;
    public StageNodeType? nodeType;
    public Action onSelected;
    public object customData;

    public StageChoiceOption()
    {
    }

    public StageChoiceOption(
        string title,
        string description,
        string rewardText = null,
        Action onSelected = null,
        Color? themeColor = null,
        string buttonText = "선택",
        string subtitle = null,
        Sprite iconSprite = null)
    {
        this.title = title;
        this.description = description;
        this.rewardText = rewardText;
        this.onSelected = onSelected;
        if (themeColor.HasValue) this.themeColor = themeColor.Value;
        if (!string.IsNullOrEmpty(buttonText)) this.buttonText = buttonText;
        this.subtitle = subtitle;
        this.iconSprite = iconSprite;
    }

    /// <summary>
    /// 기존 StageNodeType(일반 전투, 엘리트, 상점, 공방, 보물, 보스)을 기반으로
    /// 기본 타이틀, 설명, 보상 텍스트, 테마 컬러 및 실행 동작이 채워진 인스턴스를 생성합니다.
    /// </summary>
    public static StageChoiceOption FromNodeType(StageNodeType type, Action customAction = null)
    {
        var option = new StageChoiceOption
        {
            id = type.ToString(),
            title = StageNodeHelper.GetTitle(type),
            description = StageNodeHelper.GetDescription(type),
            rewardText = StageNodeHelper.GetRewardText(type),
            themeColor = StageNodeHelper.GetThemeColor(type),
            buttonText = "선택",
            nodeType = type,
            onSelected = customAction ?? (() => StageNodeHelper.ExecuteDefaultAction(type))
        };
        return option;
    }

    // 체이닝 빌더 헬퍼
    public StageChoiceOption SetTitle(string title) { this.title = title; return this; }
    public StageChoiceOption SetSubtitle(string subtitle) { this.subtitle = subtitle; return this; }
    public StageChoiceOption SetDescription(string desc) { this.description = desc; return this; }
    public StageChoiceOption SetRewardText(string reward) { this.rewardText = reward; return this; }
    public StageChoiceOption SetButtonText(string btnText) { this.buttonText = btnText; return this; }
    public StageChoiceOption SetThemeColor(Color color) { this.themeColor = color; return this; }
    public StageChoiceOption SetIcon(Sprite icon) { this.iconSprite = icon; return this; }
    public StageChoiceOption SetOnSelected(Action action) { this.onSelected = action; return this; }
    public StageChoiceOption SetCustomData(object data) { this.customData = data; return this; }
}

/// <summary>
/// 인스펙터/에셋으로 미리 선택지를 정의해 둘 수 있는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "NewChoiceOption", menuName = "ChessLoglike/Stage Choice Option")]
public class StageChoiceData : ScriptableObject
{
    public string id;
    public string title = "선택지 제목";
    public string subtitle;
    [TextArea(2, 5)]
    public string description = "선택지 설명";
    [TextArea(1, 3)]
    public string rewardText;
    public string buttonText = "선택";
    public Color themeColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    public Sprite iconSprite;
    public StageNodeType defaultNodeType = StageNodeType.NormalBattle;
    public bool useNodeTypeDefaults = false;

    public StageChoiceOption ToOption(Action customOnSelected = null)
    {
        if (useNodeTypeDefaults)
        {
            var opt = StageChoiceOption.FromNodeType(defaultNodeType, customOnSelected);
            if (!string.IsNullOrEmpty(title) && title != "선택지 제목") opt.title = title;
            if (!string.IsNullOrEmpty(description) && description != "선택지 설명") opt.description = description;
            if (!string.IsNullOrEmpty(rewardText)) opt.rewardText = rewardText;
            if (!string.IsNullOrEmpty(subtitle)) opt.subtitle = subtitle;
            if (iconSprite != null) opt.iconSprite = iconSprite;
            return opt;
        }

        return new StageChoiceOption
        {
            id = string.IsNullOrEmpty(id) ? name : id,
            title = title,
            subtitle = subtitle,
            description = description,
            rewardText = rewardText,
            buttonText = string.IsNullOrEmpty(buttonText) ? "선택" : buttonText,
            themeColor = themeColor,
            iconSprite = iconSprite,
            nodeType = defaultNodeType,
            onSelected = customOnSelected ?? (() => StageNodeHelper.ExecuteDefaultAction(defaultNodeType))
        };
    }
}

