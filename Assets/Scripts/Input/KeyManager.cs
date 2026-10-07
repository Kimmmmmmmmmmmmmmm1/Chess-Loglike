using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using ISKey = UnityEngine.InputSystem.Key;
#endif

[AddComponentMenu("Input/Key Manager")]
public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance { get; private set; }

    [System.Serializable]
    public struct KeyBinding
    {
        public ShortcutAction action;
        public KeyCode key;
    }

    [SerializeField]
    private List<KeyBinding> bindings = new()
    {
        new() { action = ShortcutAction.Confirm, key = KeyCode.Return },
        new() { action = ShortcutAction.Cancel, key = KeyCode.Escape },
        new(){ action = ShortcutAction.Modifier, key = KeyCode.LeftShift },
        new(){ action = ShortcutAction.Jump, key = KeyCode.Space },
        new(){ action = ShortcutAction.Alternative, key = KeyCode.LeftAlt },
        new(){ action = ShortcutAction.Tab, key = KeyCode.Tab },
        new(){ action = ShortcutAction.Undo, key = KeyCode.Z },
        new(){ action = ShortcutAction.Reroll, key = KeyCode.R },
        new(){ action = ShortcutAction.Freze, key = KeyCode.F },
        new(){ action = ShortcutAction.Sell, key = KeyCode.S },
        new(){ action = ShortcutAction.first, key = KeyCode.Alpha1 },
        new(){ action = ShortcutAction.second, key = KeyCode.Alpha2 },
        new(){ action = ShortcutAction.third, key = KeyCode.Alpha3 },
        new(){ action = ShortcutAction.fourth, key = KeyCode.Alpha4 },
        new(){ action = ShortcutAction.fifth, key = KeyCode.Alpha5 },
        new(){ action = ShortcutAction.sixth, key = KeyCode.Alpha6 },
        new(){ action = ShortcutAction.seventh, key = KeyCode.Alpha7 },
        new(){ action = ShortcutAction.eighth, key = KeyCode.Alpha8 },
        new(){ action = ShortcutAction.ninth, key = KeyCode.Alpha9 },
        new(){ action = ShortcutAction.zero, key = KeyCode.Alpha0 },
    };

    private Dictionary<ShortcutAction, KeyCode> map;
#if ENABLE_INPUT_SYSTEM
    private UnityEngine.InputSystem.InputActionMap inputActionMap;
    private Dictionary<ShortcutAction, UnityEngine.InputSystem.InputAction> inputActions;
    private HashSet<ShortcutAction> pressedThisFrame;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildMap();
        LoadFromPlayerPrefs();
        SetupInputActionsIfNeeded();
    }

#if ENABLE_INPUT_SYSTEM
    private void SetupInputActionsIfNeeded()
    {
        try
        {
            if (inputActionMap != null) return;
            inputActionMap = new UnityEngine.InputSystem.InputActionMap("Shortcuts");
            inputActions = new Dictionary<ShortcutAction, UnityEngine.InputSystem.InputAction>();
            pressedThisFrame = new HashSet<ShortcutAction>();

            foreach (var b in bindings)
            {
                if (b.action == ShortcutAction.None) continue;
                var path = KeyToControlPath(b.key);
                if (string.IsNullOrEmpty(path)) continue;
                var ia = inputActionMap.AddAction(b.action.ToString(), UnityEngine.InputSystem.InputActionType.Button, path);
                // capture for closure
                var act = b.action;
                ia.performed += ctx => { pressedThisFrame.Add(act); };
                ia.Enable();
                inputActions[act] = ia;
            }
        }
        catch (Exception)
        {
            // Ignore input system setup failures at runtime; fallback to legacy checks
            inputActionMap = null;
        }
    }

    private static string KeyToControlPath(KeyCode key)
    {
        // Try to parse to InputSystem Key and produce <Keyboard>/keyname path
        string name = key.ToString();
        if (name == "Return") name = "Enter";
        if (name.StartsWith("Alpha") && name.Length > 5) name = "Digit" + name.Substring(5);
        if (Enum.TryParse<ISKey>(name, true, out var parsed) && parsed != ISKey.None)
        {
            var path = parsed.ToString().ToLower();
            return $"<Keyboard>/{path}";
        }
        return null;
    }
#endif

    private void BuildMap()
    {
        map = new Dictionary<ShortcutAction, KeyCode>();
        foreach (var b in bindings)
        {
            if (b.action == ShortcutAction.None)
                continue;
            map[b.action] = b.key;
        }
    }

    public KeyCode GetKeyForAction(ShortcutAction action)
    {
        if (action == ShortcutAction.None)
            return KeyCode.None;
        if (map == null) BuildMap();
        return map.TryGetValue(action, out var k) ? k : KeyCode.None;
    }

    public bool IsPressed(ShortcutAction action)
    {
        if (action == ShortcutAction.None) return false;
        if (ModalManager.IsKeyboardBlocked) return false;
#if ENABLE_INPUT_SYSTEM
        if (inputActions != null && inputActions.TryGetValue(action, out var ia))
        {
            // check pressed flag set by performed callback
            if (pressedThisFrame != null && pressedThisFrame.Contains(action)) return true;
        }
#endif
        var key = GetKeyForAction(action);
        if (key == KeyCode.None) return false;
        return IsKeyDown(key);
    }

    public void SetKeyForAction(ShortcutAction action, KeyCode key, bool save = true)
    {
        if (action == ShortcutAction.None) return;
        if (map == null) BuildMap();
        map[action] = key;

        int idx = bindings.FindIndex(b => b.action == action);
        if (idx >= 0)
        {
            var nb = bindings[idx];
            nb.key = key;
            bindings[idx] = nb;
        }
        else
        {
            bindings.Add(new KeyBinding { action = action, key = key });
        }

        if (save) SaveToPlayerPrefs();
    }

    public void SaveToPlayerPrefs()
    {
        foreach (var kv in map)
        {
            PlayerPrefs.SetInt("KeyBinding_" + (int)kv.Key, (int)kv.Value);
        }
        PlayerPrefs.Save();
    }

    public void LoadFromPlayerPrefs()
    {
        bool changed = false;
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            string key = "KeyBinding_" + (int)b.action;
            if (PlayerPrefs.HasKey(key))
            {
                b.key = (KeyCode)PlayerPrefs.GetInt(key);
                bindings[i] = b;
                changed = true;
            }
        }
        if (changed) BuildMap();
    }

    [ContextMenu("Reset Bindings to Defaults")]
    public void ResetBindingsToDefaults()
    {
        foreach (var b in bindings)
        {
            PlayerPrefs.DeleteKey("KeyBinding_" + (int)b.action);
        }
        PlayerPrefs.Save();
        BuildMap();
    }

#if ENABLE_INPUT_SYSTEM
    private void LateUpdate()
    {
        // clear per-frame pressed flags so they only last one frame
        pressedThisFrame?.Clear();
    }
#endif

    // Cross-input-system key check
    public static bool IsKeyDown(KeyCode key)
    {
        if (ModalManager.IsKeyboardBlocked) return false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        string name = key.ToString();
        if (name == "Return") name = "Enter";
        if (name.StartsWith("Alpha") && name.Length > 5) name = "Digit" + name.Substring(5);
        // Try direct parse to InputSystem Key enum
        if (Enum.TryParse<ISKey>(name, true, out var parsed) && parsed != ISKey.None)
        {
            var control = Keyboard.current[parsed];
            return control != null && control.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(key);
#endif
    }
}
