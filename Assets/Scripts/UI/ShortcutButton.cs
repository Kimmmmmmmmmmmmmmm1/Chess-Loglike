using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[AddComponentMenu("UI/Shortcut Button")]
public class ShortcutButton : Button
{
    [Tooltip("Logical action for this shortcut. KeyManager will provide the KeyCode if available.")]
    [SerializeField]
    private ShortcutAction action = ShortcutAction.None;

    [SerializeField, Tooltip("Fallback keyboard key if KeyManager is not present or action is None.")]
    private KeyCode fallbackKey = KeyCode.None;

    protected override void Start()
    {
        base.Start();
    }

    private void LateUpdate()
    {
        if (ModalManager.IsKeyboardBlocked)
        {
            return;
        }

        KeyCode keyToCheck = KeyCode.None;

        if (action != ShortcutAction.None && KeyManager.Instance != null)
        {
            // Let KeyManager handle the action lookup and input checking
            if (!IsActive() || !interactable)
                return;

            if (KeyManager.Instance.IsPressed(action))
            {
                onClick?.Invoke();
            }
            return;
        }

        keyToCheck = fallbackKey;
        if (keyToCheck == KeyCode.None)
            return;

        if (!IsActive() || !interactable)
            return;

        if (KeyManager.IsKeyDown(keyToCheck))
        {
            onClick?.Invoke();
        }
    }
}
