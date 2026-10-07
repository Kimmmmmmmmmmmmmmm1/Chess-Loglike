using System;
using UnityEngine;

[Serializable]
public class SettingsData
{
    public enum Language
    {
        Korean,
        English
    }

    public enum TooltipDelay
    {
        Immediate,
        HalfSecond,
        OneSecond
    }

    public enum ScreenMode
    {
        Fullscreen,
        Borderless,
        Windowed
    }

    // Gameplay
    public Language language = Language.Korean;
    public float gameSpeed = 1f; // 1.0, 1.5, 2.0
    public TooltipDelay tooltipDelay = TooltipDelay.Immediate;
    public bool autoEndTurn = true;
    public bool simpleTurnLog = false;

    // Display & Graphics
    public ScreenMode screenMode = ScreenMode.Windowed;
    public string resolution = "1920x1080";
    public bool vSync = true;
    public float screenShake = 1f; // 0..1
    public bool pixelPerfect = true;

    // Audio
    public float masterVolume = 5f;
    public float bgmVolume = 5f;
    public float sfxVolume = 5f;
    public float uiVolume = 5f;
    public bool muteInBackground = true;

    // Controls / Key Bindings (stored as strings for simplicity)
    public string keyEndTurn = "Space";
    public string keyShowThreat = "LeftAlt";
    public string keyCancel = "Escape";
    public string keyShopReroll = "R";
    public string keyShopLock = "F";
    public string keyQuickSell = "E";
    public string keyOpenDeck = "Tab";
    public string keyOpenMap = "M";
    public string keyOpenSettings = "F1";

    public static SettingsData Default()
    {
        return new SettingsData()
        {
            language = Language.Korean,
            gameSpeed = 1f,
            tooltipDelay = TooltipDelay.Immediate,
            autoEndTurn = true,
            simpleTurnLog = false,
            screenMode = ScreenMode.Windowed,
            resolution = "1920x1080",
            vSync = true,
            screenShake = 1f,
            pixelPerfect = true,
            masterVolume = 5f,
            bgmVolume = 5f,
            sfxVolume = 5f,
            uiVolume = 5f,
            muteInBackground = true,
            keyEndTurn = "Space",
            keyShowThreat = "LeftAlt",
            keyCancel = "Escape",
            keyShopReroll = "R",
            keyShopLock = "F",
            keyQuickSell = "E",
            keyOpenDeck = "Tab",
            keyOpenMap = "M",
            keyOpenSettings = "F1"
        };
    }
}
