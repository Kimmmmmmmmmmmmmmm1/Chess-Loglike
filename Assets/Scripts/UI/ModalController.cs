using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModalController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    public event Action<bool> OnResult;
    private bool resolved;

    public void Init(string message)
    {
        if (messageText != null) messageText.text = message;
        if (confirmButton != null) confirmButton.onClick.AddListener(() => Resolve(true));
        if (cancelButton != null) cancelButton.onClick.AddListener(() => Resolve(false));
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Resolve(true);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Resolve(false);
        }
    }

    private void Resolve(bool value)
    {
        resolved = true;
        OnResult?.Invoke(value);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
        if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();

        if (!resolved)
        {
            ModalManager.PopKeyboardBlock();
        }
    }
}
