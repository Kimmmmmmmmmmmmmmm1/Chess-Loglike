using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class KeySettingsView : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] private SettingsKeyBindingSelector keyEndTurnSelector;
    [SerializeField] private SettingsKeyBindingSelector keyShowThreatSelector;
    [SerializeField] private SettingsKeyBindingSelector keyCancelSelector;
    [SerializeField] private SettingsKeyBindingSelector keyShopRerollSelector;
    [SerializeField] private SettingsKeyBindingSelector keyShopLockSelector;
    [SerializeField] private SettingsKeyBindingSelector keyQuickSellSelector;
    [SerializeField] private SettingsKeyBindingSelector keyOpenDeckSelector;
    [SerializeField] private SettingsKeyBindingSelector keyOpenMapSelector;
    [SerializeField] private SettingsKeyBindingSelector keyOpenSettingsSelector;
    [SerializeField] private Button restoreDefaultsButton;

    private enum BindingTarget
    {
        None,
        EndTurn,
        ShowThreat,
        Cancel,
        ShopReroll,
        ShopLock,
        QuickSell,
        OpenDeck,
        OpenMap,
        OpenSettings
    }

    private SettingsData staged;
    private BindingTarget activeBinding = BindingTarget.None;
    private SettingsKeyBindingSelector activeSelector;
    private float rebindIgnoreUntil = 0f;

    private void OnEnable()
    {
        UpdateSelectionVisuals();
    }

    private void Start()
    {
        SettingsManager.EnsureInstance();
        if (restoreDefaultsButton != null) restoreDefaultsButton.onClick.AddListener(OnRestoreDefaultsClicked);

        if (keyOpenMapSelector != null) keyOpenMapSelector.gameObject.SetActive(false);

        BindSelectors();
        UpdateSelectionVisuals();
    }

    private void OnDestroy()
    {
        if (restoreDefaultsButton != null) restoreDefaultsButton.onClick.RemoveListener(OnRestoreDefaultsClicked);
        UnbindSelectors();
    }

    public void SetTarget(SettingsData target)
    {
        staged = target;
        ApplyToUI();
        UpdateSelectionVisuals();
    }

    private void ApplyToUI()
    {
        var s = staged ?? (SettingsManager.Instance != null ? SettingsManager.Instance.Settings : null);
        if (s == null) return;

        // Set labels and key texts for all selectors
        SetSelectorKeyText(keyEndTurnSelector, BindingTarget.EndTurn, s.keyEndTurn);
        SetSelectorKeyText(keyShowThreatSelector, BindingTarget.ShowThreat, s.keyShowThreat);
        SetSelectorKeyText(keyCancelSelector, BindingTarget.Cancel, s.keyCancel);
        SetSelectorKeyText(keyShopRerollSelector, BindingTarget.ShopReroll, s.keyShopReroll);
        SetSelectorKeyText(keyShopLockSelector, BindingTarget.ShopLock, s.keyShopLock);
        SetSelectorKeyText(keyQuickSellSelector, BindingTarget.QuickSell, s.keyQuickSell);
        SetSelectorKeyText(keyOpenDeckSelector, BindingTarget.OpenDeck, s.keyOpenDeck);
        SetSelectorKeyText(keyOpenMapSelector, BindingTarget.OpenMap, s.keyOpenMap);
        SetSelectorKeyText(keyOpenSettingsSelector, BindingTarget.OpenSettings, s.keyOpenSettings);

        UpdateSelectionVisuals();
    }

    private void SetSelectorKeyText(SettingsKeyBindingSelector selector, BindingTarget binding, string keyName)
    {
        if (selector == null) return;

        // Set label (action name) and key text
        selector.SetLabel(GetActionLabel(binding));
        selector.SetKeyText(FormatKey(keyName));
    }

    private void OnRestoreDefaultsClicked()
    {
        ConfirmResetBindings();
    }

    private async void ConfirmResetBindings()
    {
        if (!await ShowConfirmModal("키 설정을 초기화할까요?\n현재 키 설정은 모두 기본값으로 되돌아갑니다."))
        {
            return;
        }

        staged = SettingsData.Default();
        ApplyToUI();
    }

    private Task<bool> ShowConfirmModal(string message)
    {
        if (ModalManager.Instance == null)
        {
            return Task.FromResult(true);
        }

        return ModalManager.Instance.ShowModalAsync(message);
    }

    private void BindSelectors()
    {
        if (keyEndTurnSelector != null)
        {
            keyEndTurnSelector.OnEditModeEntered += BeginEndTurnRebind;
            keyEndTurnSelector.OnEditModeExited += OnSelectorEditModeExited;
        }
        if (keyShowThreatSelector != null)
        {
            keyShowThreatSelector.OnEditModeEntered += BeginShowThreatRebind;
            keyShowThreatSelector.OnEditModeExited += OnSelectorEditModeExited;
        }
        if (keyCancelSelector != null)
        {
            keyCancelSelector.OnEditModeEntered += BeginCancelRebind;
            keyCancelSelector.OnEditModeExited += OnSelectorEditModeExited;
        }
        if (keyShopRerollSelector != null)
        {
            keyShopRerollSelector.OnEditModeEntered += BeginShopRerollRebind;
            keyShopRerollSelector.OnEditModeExited += OnSelectorEditModeExited;
        }
        if (keyShopLockSelector != null)
        {
            keyShopLockSelector.OnEditModeEntered += BeginShopLockRebind;
            keyShopLockSelector.OnEditModeExited += OnSelectorEditModeExited;
        }
        if (keyQuickSellSelector != null)
        {
            keyQuickSellSelector.OnEditModeEntered += BeginQuickSellRebind;
            keyQuickSellSelector.OnEditModeExited += OnSelectorEditModeExited;
        }
        if (keyOpenDeckSelector != null)
        {
            keyOpenDeckSelector.OnEditModeEntered += BeginOpenDeckRebind;
            keyOpenDeckSelector.OnEditModeExited += OnSelectorEditModeExited;
        }
        if (keyOpenMapSelector != null)
        {
            keyOpenMapSelector.OnEditModeEntered += BeginOpenMapRebind;
            keyOpenMapSelector.OnEditModeExited += OnSelectorEditModeExited;
        }
        if (keyOpenSettingsSelector != null)
        {
            keyOpenSettingsSelector.OnEditModeEntered += BeginOpenSettingsRebind;
            keyOpenSettingsSelector.OnEditModeExited += OnSelectorEditModeExited;
        }
    }

    private void UnbindSelectors()
    {
        if (keyEndTurnSelector != null)
        {
            keyEndTurnSelector.OnEditModeEntered -= BeginEndTurnRebind;
            keyEndTurnSelector.OnEditModeExited -= OnSelectorEditModeExited;
        }
        if (keyShowThreatSelector != null)
        {
            keyShowThreatSelector.OnEditModeEntered -= BeginShowThreatRebind;
            keyShowThreatSelector.OnEditModeExited -= OnSelectorEditModeExited;
        }
        if (keyCancelSelector != null)
        {
            keyCancelSelector.OnEditModeEntered -= BeginCancelRebind;
            keyCancelSelector.OnEditModeExited -= OnSelectorEditModeExited;
        }
        if (keyShopRerollSelector != null)
        {
            keyShopRerollSelector.OnEditModeEntered -= BeginShopRerollRebind;
            keyShopRerollSelector.OnEditModeExited -= OnSelectorEditModeExited;
        }
        if (keyShopLockSelector != null)
        {
            keyShopLockSelector.OnEditModeEntered -= BeginShopLockRebind;
            keyShopLockSelector.OnEditModeExited -= OnSelectorEditModeExited;
        }
        if (keyQuickSellSelector != null)
        {
            keyQuickSellSelector.OnEditModeEntered -= BeginQuickSellRebind;
            keyQuickSellSelector.OnEditModeExited -= OnSelectorEditModeExited;
        }
        if (keyOpenDeckSelector != null)
        {
            keyOpenDeckSelector.OnEditModeEntered -= BeginOpenDeckRebind;
            keyOpenDeckSelector.OnEditModeExited -= OnSelectorEditModeExited;
        }
        if (keyOpenMapSelector != null)
        {
            keyOpenMapSelector.OnEditModeEntered -= BeginOpenMapRebind;
            keyOpenMapSelector.OnEditModeExited -= OnSelectorEditModeExited;
        }
        if (keyOpenSettingsSelector != null)
        {
            keyOpenSettingsSelector.OnEditModeEntered -= BeginOpenSettingsRebind;
            keyOpenSettingsSelector.OnEditModeExited -= OnSelectorEditModeExited;
        }
    }

    private void BeginEndTurnRebind()
    {
        BeginRebind(BindingTarget.EndTurn, keyEndTurnSelector);
    }

    private void BeginShowThreatRebind()
    {
        BeginRebind(BindingTarget.ShowThreat, keyShowThreatSelector);
    }

    private void BeginCancelRebind()
    {
        BeginRebind(BindingTarget.Cancel, keyCancelSelector);
    }

    private void BeginShopRerollRebind()
    {
        BeginRebind(BindingTarget.ShopReroll, keyShopRerollSelector);
    }

    private void BeginShopLockRebind()
    {
        BeginRebind(BindingTarget.ShopLock, keyShopLockSelector);
    }

    private void BeginQuickSellRebind()
    {
        BeginRebind(BindingTarget.QuickSell, keyQuickSellSelector);
    }

    private void BeginOpenDeckRebind()
    {
        BeginRebind(BindingTarget.OpenDeck, keyOpenDeckSelector);
    }

    private void BeginOpenMapRebind()
    {
        BeginRebind(BindingTarget.OpenMap, keyOpenMapSelector);
    }

    private void BeginOpenSettingsRebind()
    {
        BeginRebind(BindingTarget.OpenSettings, keyOpenSettingsSelector);
    }

    private void OnSelectorEditModeExited()
    {
        activeBinding = BindingTarget.None;
        activeSelector = null;
    }

    private void Update()
    {
        if (ModalManager.IsKeyboardBlocked)
        {
            return;
        }

        if (activeBinding == BindingTarget.None)
        {
            return;
        }

        KeyCode capturedKey = CapturePressedKey();
        if (capturedKey == KeyCode.None)
        {
            return;
        }

        AssignBinding(capturedKey);
    }

    private void BeginRebind(BindingTarget bindingTarget, SettingsKeyBindingSelector selector)
    {
        activeBinding = bindingTarget;
        activeSelector = selector;
        // Ignore any key presses that occurred to trigger the edit mode (prevents
        // the confirm/return press from immediately being captured as the new binding).
        rebindIgnoreUntil = Time.time + 0.12f;
    }

    private void AssignBinding(KeyCode keyCode)
    {
        if (staged == null || activeSelector == null)
        {
            return;
        }

        string keyName = keyCode.ToString();

        switch (activeBinding)
        {
            case BindingTarget.EndTurn:
                staged.keyEndTurn = keyName;
                break;
            case BindingTarget.ShowThreat:
                staged.keyShowThreat = keyName;
                break;
            case BindingTarget.Cancel:
                staged.keyCancel = keyName;
                break;
            case BindingTarget.ShopReroll:
                staged.keyShopReroll = keyName;
                break;
            case BindingTarget.ShopLock:
                staged.keyShopLock = keyName;
                break;
            case BindingTarget.QuickSell:
                staged.keyQuickSell = keyName;
                break;
            case BindingTarget.OpenDeck:
                staged.keyOpenDeck = keyName;
                break;
            case BindingTarget.OpenMap:
                staged.keyOpenMap = keyName;
                break;
            case BindingTarget.OpenSettings:
                staged.keyOpenSettings = keyName;
                break;
        }

        // Use a local reference because ExitEditMode may invoke events that
        // clear `activeSelector` (avoid NullReferenceException).
        var selector = activeSelector;
        selector.SetKeyText(FormatKey(keyName));
        selector.ExitEditMode();
        // Return visual focus to the label so the label marker is shown after
        // a successful rebind.
        selector.FocusLabel();
        activeBinding = BindingTarget.None;
        activeSelector = null;
        UpdateSelectionVisuals();
    }

    private KeyCode CapturePressedKey()
    {
        if (ModalManager.IsKeyboardBlocked)
        {
            return KeyCode.None;
        }

        // Skip capturing keys for a short window after entering rebind mode so
        // the key that activated edit mode isn't immediately captured.
        if (Time.time < rebindIgnoreUntil)
        {
            return KeyCode.None;
        }

        if (!Input.anyKeyDown)
        {
            return KeyCode.None;
        }

        foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (keyCode == KeyCode.None)
            {
                continue;
            }

            if (Input.GetKeyDown(keyCode) && !IsMouseOrJoystickKey(keyCode))
            {
                return keyCode;
            }
        }

        return KeyCode.None;
    }

    private bool IsMouseOrJoystickKey(KeyCode keyCode)
    {
        return keyCode.ToString().StartsWith("Mouse") || keyCode.ToString().StartsWith("Joystick");
    }

    private string GetActionLabel(BindingTarget binding)
    {
        switch (binding)
        {
            case BindingTarget.EndTurn: return "확인";
            case BindingTarget.ShowThreat: return "위협 표시";
            case BindingTarget.Cancel: return "취소";
            case BindingTarget.ShopReroll: return "상점 재뽑기";
            case BindingTarget.ShopLock: return "상점 고정";
            case BindingTarget.QuickSell: return "빠른 판매";
            case BindingTarget.OpenDeck: return "덱 열기";
            case BindingTarget.OpenMap: return "지도 열기";
            case BindingTarget.OpenSettings: return "설정 열기";
            default: return "";
        }
    }

    private string FormatKey(string keyName)
    {
        if (string.IsNullOrEmpty(keyName))
        {
            return "None";
        }

        if (keyName == "LeftAlt")
        {
            bool isMac = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
            return isMac ? "Option" : "Alt";
        }
        if (keyName == "LeftControl") return "Ctrl";
        if (keyName == "LeftShift") return "Shift";
        if (keyName == "Alpha0") return "0";
        if (keyName == "Alpha1") return "1";
        if (keyName == "Alpha2") return "2";
        if (keyName == "Alpha3") return "3";
        if (keyName == "Alpha4") return "4";
        if (keyName == "Alpha5") return "5";
        if (keyName == "Alpha6") return "6";
        if (keyName == "Alpha7") return "7";
        if (keyName == "Alpha8") return "8";
        if (keyName == "Alpha9") return "9";
        if (keyName == "Space") return "Space";

        return keyName;
    }

    private void UpdateSelectionVisuals()
    {
        SettingsSelectionTextUtility.SetMarkedButtonText(restoreDefaultsButton, IsSelected(restoreDefaultsButton));
    }

    private bool IsSelected(Button button)
    {
        return EventSystem.current != null && button != null && EventSystem.current.currentSelectedGameObject == button.gameObject;
    }
}
