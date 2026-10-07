using TMPro;
using UnityEngine.UI;

public static class SettingsSelectionTextUtility
{
    public static string ApplyMarker(string text, bool selected)
    {
        string baseText = StripMarker(text);

        if (!selected)
        {
            return baseText;
        }

        if (string.IsNullOrEmpty(baseText))
        {
            return "> <";
        }

        return $"> {baseText} <";
    }

    public static void SetMarkedText(TMP_Text label, bool selected)
    {
        if (label == null)
        {
            return;
        }

        label.text = ApplyMarker(label.text, selected);
    }

    public static void SetMarkedDropdownCaption(TMP_Dropdown dropdown, bool selected)
    {
        if (dropdown == null || dropdown.captionText == null)
        {
            return;
        }

        SetMarkedText(dropdown.captionText, selected);
    }

    public static void SetMarkedButtonText(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        if (button.GetComponent<SettingsButtonSelector>() != null)
        {
            return; // Managed internally by SettingsButtonSelector
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        SetMarkedText(label, selected);
    }

    public static void SetMarkedToggleText(Toggle toggle, bool selected)
    {
        if (toggle == null)
        {
            return;
        }

        if (toggle.GetComponent<SettingsToggleSelector>() != null)
        {
            return; // Managed internally by SettingsToggleSelector
        }

        TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
        SetMarkedText(label, selected);
    }

    private static string StripMarker(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string trimmed = text.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '>' && trimmed[trimmed.Length - 1] == '<')
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
        }

        return trimmed;
    }
}