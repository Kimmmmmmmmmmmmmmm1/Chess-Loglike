/// <summary>
/// BGM (Background Music) types
/// Place audio files in: Resources/Audio/BGM/
/// </summary>
public enum BGMType
{
    TitleBGM,
    GameplayBGM,
    BattleBGM,
    BossBGM,
    ShopBGM,
    WorkshopBGM,
    TreasureBGM,
    DefeatBGM,
}

/// <summary>
/// SFX (Sound Effects) types
/// Place audio files in: Resources/Audio/SFX/
/// </summary>
public enum SFXType
{
    Click,
    Confirm,
    Cancel,
    Hover,
    OpenPanel,
    ClosePanel,
    Move,
    Destroy,
    Purchase,
    Sell,
}

/// <summary>
/// UI Sound types
/// Place audio files in: Resources/Audio/UI/
/// </summary>
public enum UIType
{
    ButtonClick,
    ButtonHover,
    ButtonPress,
    ToggleOn,
    ToggleOff,
    Open,
    Close,
    Back,
    Success,
    Error,
}
