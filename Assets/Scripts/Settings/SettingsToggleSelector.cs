using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class SettingsToggleSelector : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private TextMeshProUGUI valueText; // Displays text like "On" or "Off" if assigned. Otherwise uses toggle's own text.
    [SerializeField] private string onText = "켜짐";
    [SerializeField] private string offText = "꺼짐";
    [SerializeField] private bool isInteractive = true;

    private Toggle toggle;
    private string labelBaseText = string.Empty;
    private bool isSelected;
    private bool isEditing;
    private int editFrame;

    public event Action<bool> OnValueChanged;

    public Toggle ToggleControl => toggle;
    public Selectable SelectableControl => toggle;
    public bool Value => toggle != null && toggle.isOn;
    public bool IsEditing => isEditing;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();

        if (labelText != null && string.IsNullOrEmpty(labelBaseText))
        {
            labelBaseText = labelText.text;
        }

        if (toggle != null)
        {
            toggle.onValueChanged.AddListener(HandleToggleChanged);
        }

        RefreshVisuals();
    }

    private void OnEnable()
    {
        RefreshVisuals();
    }

    private void OnDisable()
    {
        isSelected = false;
        isEditing = false;
        RefreshVisuals();
    }

    private void OnDestroy()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(HandleToggleChanged);
        }
    }

    private void Update()
    {
        if (!isSelected || !isEditing || toggle == null || !isInteractive)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            SetToggleValue(false);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            SetToggleValue(true);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            if (Time.frameCount == editFrame) return; // Prevent same-frame exit
            SetToggleValue(!toggle.isOn);
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitEditMode();
        }
    }

    public void SetLabel(string label)
    {
        labelBaseText = label ?? string.Empty;
        RefreshVisuals();
    }

    public void SetStateTexts(string on, string off)
    {
        if (!string.IsNullOrEmpty(on))
        {
            onText = on;
        }
        if (!string.IsNullOrEmpty(off))
        {
            offText = off;
        }
        RefreshVisuals();
    }

    public void SetValueWithoutNotify(bool value)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.SetIsOnWithoutNotify(value);
        RefreshVisuals();
    }

    public void SetInteractable(bool interactable)
    {
        isInteractive = interactable;
        if (!isInteractive)
        {
            isEditing = false;
        }

        if (toggle != null)
        {
            toggle.interactable = interactable;
        }

        RefreshVisuals();
    }

    public void EnterEditMode()
    {
        isEditing = true;
        editFrame = Time.frameCount;
        RefreshVisuals();
    }

    public void ExitEditMode()
    {
        isEditing = false;
        RefreshVisuals();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        isEditing = false;
        RefreshVisuals();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        isEditing = false;
        RefreshVisuals();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (toggle == null || !isInteractive)
        {
            return;
        }

        if (!isEditing)
        {
            isEditing = true;
            editFrame = Time.frameCount;
            RefreshVisuals();
            return;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        SettingPanelView.Instance?.NotifyMouseInteraction();

        if (toggle == null || !isInteractive)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        if (IsValueTextPointerHit(eventData))
        {
            SetToggleValue(!Value);
            ExitEditMode();
            return;
        }

        OnSubmit(null);
    }

    private void HandleToggleChanged(bool value)
    {
        RefreshVisuals();
        OnValueChanged?.Invoke(value);
    }

    private void SetToggleValue(bool value)
    {
        if (toggle == null || !isInteractive || toggle.isOn == value) return;
        toggle.isOn = value; // Trigger HandleToggleChanged
    }

    private bool IsValueTextPointerHit(PointerEventData eventData)
    {
        if (valueText == null || eventData == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            valueText.rectTransform,
            eventData.position,
            eventData.pressEventCamera);
    }

    private void RefreshVisuals()
    {
        bool hideSelectionMarker = SettingPanelView.Instance != null && SettingPanelView.Instance.IsMouseInteractionMode;
        Color textColor = isInteractive ? Color.white : new Color(0.7f, 0.7f, 0.7f); // 밝은 회색

        if (labelText != null)
        {
            labelText.text = (isSelected && !isEditing && !hideSelectionMarker) ? $">{labelBaseText}<" : labelBaseText;
            labelText.color = textColor;
        }

        string stateStr = (toggle != null && toggle.isOn) ? onText : offText;

        if (valueText != null)
        {
            bool showMarker = isEditing || (isSelected && !isEditing && hideSelectionMarker);
            valueText.text = showMarker ? $">{stateStr}<" : stateStr;
            valueText.color = textColor;
        }
        else if (toggle != null)
        {
            // Fallback: Use toggle's built-in text if no valueText is assigned
            TMP_Text builtInText = toggle.GetComponentInChildren<TMP_Text>(true);
            if (builtInText != null)
            {
                bool showMarker = isEditing || (isSelected && !isEditing && hideSelectionMarker);
                builtInText.text = showMarker ? $">{stateStr}<" : stateStr;
                builtInText.color = textColor;
            }
        }

        if (toggle != null)
        {
            toggle.interactable = isInteractive;
        }
    }
}
