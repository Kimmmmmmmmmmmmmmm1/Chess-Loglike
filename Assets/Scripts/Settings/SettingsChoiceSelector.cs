using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SettingsChoiceSelector : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private bool wrapAround = true;
    [SerializeField] private bool isInteractive = true;

    [Header("Options")]
    [SerializeField] private bool useInspectorOptions = true;
    [SerializeField] private List<string> inspectorOptions = new List<string>();

    private readonly List<string> options = new List<string>();
    private Button button;
    private string labelBaseText = string.Empty;
    private int selectedIndex;
    private bool isSelected;
    private bool isEditing;
    private int editFrame;

    public event Action<int> OnValueChanged;

    public Button SelectableButton => button;
    public int SelectedIndex => selectedIndex;
    public string SelectedOption => options.Count > 0 ? options[selectedIndex] : string.Empty;
    public bool IsEditing => isEditing;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (labelText != null && string.IsNullOrEmpty(labelBaseText))
        {
            labelBaseText = labelText.text;
        }

        if (labelText != null)
        {
            Button labelButton = labelText.GetComponent<Button>();
            if (labelButton != null)
            {
                labelButton.interactable = false;
                var nav = labelButton.navigation;
                nav.mode = Navigation.Mode.None;
                labelButton.navigation = nav;
            }
        }
        if (useInspectorOptions)
        {
            ApplyInspectorOptions();
        }

        RefreshVisuals();
    }

    private void OnValidate()
    {
        if (useInspectorOptions)
        {
            ApplyInspectorOptions();
        }
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

    private void Update()
    {
        if (!isSelected || !isEditing || !isInteractive)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            Step(-1);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            Step(1);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (Time.frameCount == editFrame) return; // Prevent same-frame exit
            ExitEditMode();
        }
    }

    public void SetLabel(string label)
    {
        labelBaseText = label ?? string.Empty;
        RefreshVisuals();
    }

    public void SetOptions(IList<string> newOptions)
    {
        useInspectorOptions = false; // Code overrides inspector options
        options.Clear();

        if (newOptions != null)
        {
            for (int i = 0; i < newOptions.Count; i++)
            {
                options.Add(newOptions[i] ?? string.Empty);
            }
        }

        if (options.Count == 0)
        {
            options.Add(string.Empty);
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
        RefreshVisuals();
    }

    public void SetValue(int index, bool notify = false)
    {
        if (options.Count == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(index, 0, options.Count - 1);
        if (clampedIndex == selectedIndex)
        {
            RefreshVisuals();
            return;
        }

        selectedIndex = clampedIndex;
        RefreshVisuals();

        if (notify)
        {
            OnValueChanged?.Invoke(selectedIndex);
        }
    }

    public void SetOptionsFromInspector(bool enabled)
    {
        useInspectorOptions = enabled;
        if (useInspectorOptions)
        {
            ApplyInspectorOptions();
        }
    }

    public void SetValueWithoutNotify(int index)
    {
        SetValue(index, false);
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
        isEditing = false;  // Reset to label selection state
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
        if (options.Count == 0 || !isInteractive)
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

        ExitEditMode();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        SettingPanelView.Instance?.NotifyMouseInteraction();

        if (!isInteractive)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        if (IsValueTextPointerHit(eventData))
        {
            Step(1);
            ExitEditMode();
            return;
        }

        OnSubmit(null);
    }

    private void ApplyInspectorOptions()
    {
        options.Clear();

        for (int i = 0; i < inspectorOptions.Count; i++)
        {
            options.Add(inspectorOptions[i] ?? string.Empty);
        }

        if (options.Count == 0)
        {
            options.Add(string.Empty);
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
        if (Application.isPlaying)
        {
            RefreshVisuals();
        }
    }

    private void SyncInspectorOptions()
    {
        inspectorOptions.Clear();
        inspectorOptions.AddRange(options);
    }

    private void Step(int delta)
    {
        if (options.Count <= 1 || !isInteractive)
        {
            return;
        }

        int nextIndex = selectedIndex + delta;
        if (wrapAround)
        {
            nextIndex = (nextIndex % options.Count + options.Count) % options.Count;
        }
        else
        {
            nextIndex = Mathf.Clamp(nextIndex, 0, options.Count - 1);
        }

        if (nextIndex == selectedIndex)
        {
            return;
        }

        selectedIndex = nextIndex;
        RefreshVisuals();
        OnValueChanged?.Invoke(selectedIndex);
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

        if (valueText != null)
        {
            string value = SelectedOption;
            bool showMarker = isEditing || (isSelected && !isEditing && hideSelectionMarker);
            valueText.text = showMarker ? $">{value}<" : value;
            valueText.color = textColor;
        }

        if (button != null)
        {
            button.interactable = isInteractive;
        }
    }
}
