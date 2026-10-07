using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using ISKey = UnityEngine.InputSystem.Key;
#endif

public class ShowThreatController : MonoBehaviour
{
    private KeyCode boundKey = KeyCode.LeftAlt;
#if ENABLE_INPUT_SYSTEM
    private ISKey parsedKey;
    private bool hasParsedKey = false;
#endif

    private bool wasShowing = false;

    private void Start()
    {
        SettingsManager.EnsureInstance();
        UpdateBinding();
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged += UpdateBinding;
        }
    }

    private void OnDestroy()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= UpdateBinding;
        }
        if (wasShowing)
        {
            PieceManager.Instance?.HideMoveMarkers();
            wasShowing = false;
        }
    }

    private void UpdateBinding()
    {
        var s = SettingsManager.Instance != null ? SettingsManager.Instance.Settings : SettingsData.Default();
        if (s == null) return;

        string keyName = s.keyShowThreat ?? string.Empty;
        if (keyName == "Option" || keyName == "Alt") keyName = "LeftAlt";

        if (!System.Enum.TryParse<KeyCode>(keyName, out boundKey))
        {
            boundKey = KeyCode.LeftAlt;
        }


#if ENABLE_INPUT_SYSTEM
        string name = boundKey.ToString();
        if (name == "Return") name = "Enter";
        if (name.StartsWith("Alpha") && name.Length > 5) name = "Digit" + name.Substring(5);
        if (System.Enum.TryParse<ISKey>(name, true, out var parsed) && parsed != ISKey.None)
        {
            parsedKey = parsed;
            hasParsedKey = true;
        }
        else
        {
            if (name == "LeftAlt")
            {
                if (System.Enum.TryParse<ISKey>("AltLeft", true, out var parsed2) && parsed2 != ISKey.None)
                {
                    parsedKey = parsed2;
                    hasParsedKey = true;
                }
                else hasParsedKey = false;
            }
            else
            {
                hasParsedKey = false;
            }
        }

    #if UNITY_EDITOR
    #endif
#endif
    }

    private void Update()
    {
        if (ModalManager.IsKeyboardBlocked) return;

        bool held = IsKeyHeld();
        if (held && !wasShowing)
        {
            PieceManager.Instance?.ShowEnemyMoveMarkers();
        }
        else if (!held && wasShowing)
        {
            PieceManager.Instance?.HideMoveMarkers();
        }

        wasShowing = held;
    }

    private bool IsKeyHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (hasParsedKey && Keyboard.current != null)
        {
            var control = Keyboard.current[parsedKey];
            if (control != null)
            {
                return control.isPressed;
            }
        }
#endif
        if (boundKey == KeyCode.None) return false;
        return Input.GetKey(boundKey);
    }
}
