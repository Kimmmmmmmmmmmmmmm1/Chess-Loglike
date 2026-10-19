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

        UnityEngine.UI.Image bg = GetComponent<UnityEngine.UI.Image>();
        if (bg != null)
        {
            bg.color = new Color(1f, 1f, 1f, 0.95f);
        }
        UnityEngine.UI.Outline outline = GetComponent<UnityEngine.UI.Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<UnityEngine.UI.Outline>();
        }
        outline.effectColor = new Color(0.22f, 0.24f, 0.28f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
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
                titleText.color = new Color(0.10f, 0.11f, 0.14f, 1f);
            }
        }

        if (descriptionText != null)
        {
            bool hasDescription = !string.IsNullOrEmpty(description);
            descriptionText.gameObject.SetActive(hasDescription);
            if (hasDescription)
            {
                descriptionText.text = description;
                descriptionText.color = new Color(0.18f, 0.20f, 0.24f, 1f);
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
                flavorText.color = new Color(0.46f, 0.49f, 0.56f, 1f);
            }
        }
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
