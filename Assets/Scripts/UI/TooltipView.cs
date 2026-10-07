using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TooltipView : MonoBehaviour
{
    [Header("Tooltip UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI flavorText;

    private CanvasGroup canvasGroup;
    private RectTransform cachedRectTransform;

    public RectTransform RectTransform
    {
        get
        {
            if (cachedRectTransform == null)
            {
                cachedRectTransform = GetComponent<RectTransform>();
            }

            return cachedRectTransform;
        }
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public void SetContent(string title, string description, string flavor)
    {
        if (titleText != null)
        {
            bool hasTitle = !string.IsNullOrEmpty(title);
            titleText.gameObject.SetActive(hasTitle);
            if (hasTitle)
            {
                titleText.text = title;
            }
        }

        if (descriptionText != null)
        {
            bool hasDescription = !string.IsNullOrEmpty(description);
            descriptionText.gameObject.SetActive(hasDescription);
            if (hasDescription)
            {
                descriptionText.text = description;
            }
        }

        if (flavorText != null)
        {
            bool hasFlavor = !string.IsNullOrEmpty(flavor);
            flavorText.gameObject.SetActive(hasFlavor);
            if (hasFlavor)
            {
                flavorText.text = flavor;
                flavorText.fontStyle = FontStyles.Italic;
                flavorText.color = Color.lightGray;
            }
        }
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
