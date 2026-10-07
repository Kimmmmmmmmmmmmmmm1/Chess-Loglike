using System;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class SettingsSliderSelector : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private float step = 1f;
    [SerializeField] private bool showAsInteger = true;
    [SerializeField] private bool isInteractive = true;

    private Slider slider;
    private string labelBaseText = string.Empty;
    private bool isSelected;
    private bool isEditing;
    private int editFrame;
    private float valueBeforeMute = -1f;

    public event Action<float> OnValueChanged;

    public Slider SliderControl => slider;
    public Selectable SelectableControl => slider;
    public float Value => slider != null ? slider.value : 0f;
    public bool IsMuted => slider != null && Mathf.Approximately(slider.value, slider.minValue);

    public bool IsEditing => isEditing;

    private void Awake()
    {
        slider = GetComponent<Slider>();

        if (labelText != null && string.IsNullOrEmpty(labelBaseText))
            labelBaseText = labelText.text.Trim();

        if (slider != null)
        {
            slider.onValueChanged.AddListener(HandleSliderChanged);
        }

        // 라벨에 버튼이 있으면 음소거 토글 기능을 onClick에 연결합니다.
        if (labelText != null)
        {
            Button labelButton = labelText.GetComponent<Button>();
            if (labelButton != null)
            {
                labelButton.onClick.AddListener(ToggleMute);
            }
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
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(HandleSliderChanged);
        }
    }

    private void Update()
    {
        if (!isSelected || !isEditing || slider == null || !isInteractive)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            Adjust(-1f);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            Adjust(1f);
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

    public void SetValueWithoutNotify(float value)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(value);
        RefreshVisuals();
    }

    public void SetInteractable(bool interactable)
    {
        isInteractive = interactable;
        if (!isInteractive)
        {
            isEditing = false;
        }

        if (slider != null)
        {
            slider.interactable = interactable;
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
        if (slider == null || !isInteractive)
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

        if (slider == null || !isInteractive)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        // 값 텍스트 클릭 시 값 증가
        if (IsValueTextPointerHit(eventData))
        {
            Adjust(1f);
            ExitEditMode();
            return;
        }
        
        // 슬라이더 바 또는 핸들 클릭 시 편집 모드 진입/종료
        OnSubmit(null);
    }
    
    private void ToggleMute()
    {
        if (slider == null || !isInteractive)
        {
            return;
        }

        if (Mathf.Approximately(slider.value, slider.minValue))
        {
            // 음소거 해제: 이전 값으로 복원
            slider.value = (valueBeforeMute >= 0 && valueBeforeMute > slider.minValue) ? valueBeforeMute : (slider.maxValue * 0.5f);
            valueBeforeMute = -1f;
        }
        else
        {
            // 음소거: 현재 값을 저장하고 최소값으로 설정
            valueBeforeMute = slider.value;
            slider.value = slider.minValue;
        }
    }

    private void HandleSliderChanged(float value)
    {
        RefreshVisuals();
        OnValueChanged?.Invoke(value);
    }

    private void Adjust(float direction)
    {
        if (slider == null || !isInteractive)
        {
            return;
        }

        float delta = step * direction;
        float nextValue = Mathf.Clamp(slider.value + delta, slider.minValue, slider.maxValue);
        slider.value = nextValue; // Update standard slider value, which fires HandleSliderChanged
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
        Color textColor = isInteractive ? Color.white : new Color(0.7f, 0.7f, 0.7f);

        if (labelText != null)
        {   
            // 선택 상태일 때 마커 추가
            string text = (isSelected && !isEditing && !hideSelectionMarker) ? $">{labelBaseText}<" : labelBaseText;
            
            // 음소거 시 취소선 추가
            if (IsMuted)
            {
                labelText.fontStyle = FontStyles.Strikethrough;
            }
            else
            {
                labelText.fontStyle = FontStyles.Normal;
            }
            labelText.text = text;
            labelText.color = textColor;
        }

        UpdateValueText(textColor, hideSelectionMarker);

        if (slider != null)
        {
            slider.interactable = isInteractive;
        }
    }

    private void UpdateValueText(Color textColor, bool hideSelectionMarker)
    {
        if (valueText == null || slider == null)
        {
            return;
        }

        string text;
        float displayValue = slider.value;
        text = showAsInteger ? Mathf.RoundToInt(displayValue).ToString() : displayValue.ToString("0.##");

        bool showMarker = isEditing || (isSelected && !isEditing && hideSelectionMarker);
        valueText.text = showMarker ? $">{text}<" : text;
        
        // 음소거 시 취소선 추가
        if (IsMuted)
        {
            valueText.fontStyle = FontStyles.Strikethrough;
        }
        else
        {
            valueText.fontStyle = FontStyles.Normal;
        }
        valueText.color = textColor;
    }
}
