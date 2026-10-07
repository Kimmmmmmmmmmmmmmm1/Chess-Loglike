using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class OtherSettingsView : MonoBehaviour
{
    public event Action OnResetAllRequested;

    [Header("Info")]
    [SerializeField] private TextMeshProUGUI versionText;
    [SerializeField] private TextMeshProUGUI savePathText;

    [Header("Actions")]
    [SerializeField] private Button resetAllSettingsButton;

    private SettingsData staged;
    private GameObject lastSelectedObject;

    private void OnEnable()
    {
        UpdateSelectionVisuals();
    }

    private void Start()
    {
        SettingsManager.EnsureInstance();
        RefreshInfo();
        BindUI();
    }

    private void Update()
    {
        SyncSelectionVisuals();
    }

    private void OnDestroy()
    {
        UnbindUI();
    }

    public void SetTarget(SettingsData target)
    {
        staged = target;
        RefreshInfo();
        BindUI();
        UpdateSelectionVisuals();
    }

    private void BindUI()
    {
        UnbindUI();
        if (resetAllSettingsButton != null)
        {
            resetAllSettingsButton.onClick.AddListener(OnResetAllSettingsClicked);
        }
    }

    private void UnbindUI()
    {
        if (resetAllSettingsButton != null)
        {
            resetAllSettingsButton.onClick.RemoveListener(OnResetAllSettingsClicked);
        }
    }

    private void RefreshInfo()
    {
        if (versionText != null)
        {
            versionText.text = $"Version: {Application.version}";
        }

        if (savePathText != null)
        {
            savePathText.text = $"Save Path: {Application.persistentDataPath}";
        }
    }

    private void OnResetAllSettingsClicked()
    {
        OnResetAllRequested?.Invoke();
    }

    private void SyncSelectionVisuals()
    {
        GameObject currentSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (currentSelected == lastSelectedObject)
        {
            return;
        }

        lastSelectedObject = currentSelected;
        UpdateSelectionVisuals();
    }

    private void EnsureSelection()
    {
        if (EventSystem.current == null || resetAllSettingsButton == null)
        {
            return;
        }

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected == resetAllSettingsButton.gameObject)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(resetAllSettingsButton.gameObject);
        lastSelectedObject = resetAllSettingsButton.gameObject;
    }

    private void UpdateSelectionVisuals()
    {
        SettingsSelectionTextUtility.SetMarkedButtonText(resetAllSettingsButton, IsSelected(resetAllSettingsButton));
    }

    private bool IsSelected(Button button)
    {
        return EventSystem.current != null && button != null && EventSystem.current.currentSelectedGameObject == button.gameObject;
    }
}
