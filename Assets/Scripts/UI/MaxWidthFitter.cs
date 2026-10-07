using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TextMeshProUGUI), typeof(LayoutElement))]
public class MaxWidthFitter : MonoBehaviour
{
    [Header("최대 너비 설정")]
    public float maxWidth = 400f;

    private TextMeshProUGUI tmpText;
    private LayoutElement layoutElement;
    private RectTransform rectTransform; // 추가됨

    void Awake()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
        layoutElement = GetComponent<LayoutElement>();
        rectTransform = GetComponent<RectTransform>(); // 추가됨
    }

    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        UpdateLayout();
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    private void OnTextChanged(Object obj)
    {
        if (obj == tmpText)
        {
            UpdateLayout();
        }
    }

    private void UpdateLayout()
    {
        if (tmpText == null || layoutElement == null) return;

        // 텍스트의 본래 너비가 설정한 최대 너비보다 큰지 확인
        if (tmpText.preferredWidth > maxWidth)
        {
            layoutElement.preferredWidth = maxWidth; 
        }
        else
        {
            layoutElement.preferredWidth = -1; 
        }

        // 🎯 추가된 핵심 부분: UI 레이아웃을 즉시 강제로 새로고침합니다.
        if (rectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (tmpText != null && layoutElement != null)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) UpdateLayout();
            };
        }
    }
#endif
}