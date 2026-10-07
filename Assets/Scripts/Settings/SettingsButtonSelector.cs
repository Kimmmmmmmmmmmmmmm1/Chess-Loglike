using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SettingsButtonSelector : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private TextMeshProUGUI buttonText; 
    [SerializeField] private bool isInteractive = true;

    private Button button;
    private string labelBaseText = string.Empty;
    private string buttonBaseText = string.Empty;
    private bool isSelected;
    private bool isEditing;
    private int editFrame;

    public Button ButtonControl => button;
    public Selectable SelectableControl => button;
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
        
        if (buttonText == null)
        {
            buttonText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (buttonText != null && string.IsNullOrEmpty(buttonBaseText))
        {
            buttonBaseText = buttonText.text.Trim();
            // 기본값에 > < 가 붙어있다면 제거해둡니다
            if (buttonBaseText.StartsWith(">") && buttonBaseText.EndsWith("<"))
            {
                buttonBaseText = buttonBaseText.Substring(1, buttonBaseText.Length - 2).Trim();
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

    private void Update()
    {
        if (!isSelected || !isEditing || button == null || !isInteractive)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            if (Time.frameCount == editFrame) return; // 같은 프레임 입력 방지
            button.onClick?.Invoke();
            ExitEditMode(); // 버튼은 실행 후 편집 모드를 바로 빠져나오는 것이 자연스럽습니다.
        }
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            // 버튼은 좌우 방향키로 조절할 값이 없으므로 방향키를 눌러도 편집 모드를 빠져나가거나 무시합니다.
            ExitEditMode();
        }
    }

    public void SetLabel(string label)
    {
        labelBaseText = label ?? string.Empty;
        RefreshVisuals();
    }

    public void SetInteractable(bool interactable)
    {
        isInteractive = interactable;
        if (!isInteractive)
        {
            isEditing = false;
        }

        if (button != null)
        {
            button.interactable = interactable;
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
        if (button == null || !isInteractive)
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

        if (button == null || !isInteractive)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        OnSubmit(null);
    }

    private void RefreshVisuals()
    {
        bool hideSelectionMarker = SettingPanelView.Instance != null && SettingPanelView.Instance.IsMouseInteractionMode;
        Color textColor = isInteractive ? Color.white : new Color(0.7f, 0.7f, 0.7f);

        if (labelText != null)
        {
            labelText.text = (isSelected && !isEditing && !hideSelectionMarker) ? $">{labelBaseText}<" : labelBaseText;
            labelText.color = textColor;
        }

        if (buttonText != null)
        {
            bool showMarker = isEditing || (isSelected && !isEditing && hideSelectionMarker);
            buttonText.text = showMarker ? $">{buttonBaseText}<" : buttonBaseText;
            buttonText.color = textColor;
        }

        if (button != null)
        {
            button.interactable = isInteractive;
        }
    }
}
