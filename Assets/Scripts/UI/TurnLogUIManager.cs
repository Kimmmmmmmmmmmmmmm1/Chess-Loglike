using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnLogUIManager : MonoBehaviour
{
    public static TurnLogUIManager Instance { get; private set; }

    [Header("References")]
    public RectTransform contentParent;
    public GameObject logEntryPrefab;
    public ScrollRect scrollRect;

    [Header("Appearance")]
    public Color playerColor = new Color(0.1f, 0.6f, 0.1f);
    public Color enemyColor = new Color(0.6f, 0.1f, 0.1f);
    public int maxEntries = 200;

    [Header("Fade Settings")]
    public float autoHideDelay = 5f;
    public float fadeOutDuration = 0.5f;

    private readonly List<GameObject> entries = new List<GameObject>();
    private readonly List<TurnLogEntry> entryControllers = new List<TurnLogEntry>();
    private bool isLogExpanded = false;
    private bool suppressScrollEvent = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (contentParent == null && scrollRect != null)
        {
            contentParent = scrollRect.content;
        }

        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }
    }

    public void AddLog(string text, bool isEnemy)
    {
        if (contentParent == null)
        {
            return;
        }

        GameObject entryObj = null;
        TextMeshProUGUI textComponent = null;
        if (logEntryPrefab != null)
        {
            entryObj = Instantiate(logEntryPrefab, contentParent);
            textComponent = entryObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            entryObj = new GameObject("TurnLogEntry", typeof(RectTransform));
            entryObj.transform.SetParent(contentParent, false);
            textComponent = entryObj.AddComponent<TextMeshProUGUI>();
            textComponent.raycastTarget = false;
            textComponent.textWrappingMode = TextWrappingModes.Normal;
            textComponent.fontSize = 20;
            textComponent.alignment = TextAlignmentOptions.Left;
        }

        TurnLogEntry entryController = entryObj.GetComponent<TurnLogEntry>();
        if (entryController == null)
        {
            entryController = entryObj.AddComponent<TurnLogEntry>();
        }

        textComponent = textComponent ?? entryObj.GetComponent<TextMeshProUGUI>();
        if (textComponent != null)
        {
            entryController.SetText(text, isEnemy ? enemyColor : playerColor);
        }
        else
        {
            var childTextComponent = entryObj.GetComponentInChildren<TextMeshProUGUI>();
            if (childTextComponent != null)
            {
                childTextComponent.text = text;
                childTextComponent.color = isEnemy ? enemyColor : playerColor;
            }
        }

        entryController.SetTimings(autoHideDelay, fadeOutDuration);

        entries.Add(entryObj);
        entryControllers.Add(entryController);

        while (entries.Count > maxEntries)
        {
            var old = entries[0];
            entries.RemoveAt(0);
            entryControllers.RemoveAt(0);
            if (old != null) Destroy(old);
        }

        if (scrollRect == null)
        {
            scrollRect = contentParent.GetComponentInParent<ScrollRect>();
        }

        if (scrollRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport != null
                ? scrollRect.viewport
                : scrollRect.GetComponent<RectTransform>());
            Canvas.ForceUpdateCanvases();
            suppressScrollEvent = true;
            scrollRect.verticalNormalizedPosition = 0f;
            StartCoroutine(ClearSuppressScrollNextFrame());
        }
    }

    private System.Collections.IEnumerator ClearSuppressScrollNextFrame()
    {
        yield return null;
        suppressScrollEvent = false;
    }

    private void OnScrollValueChanged(Vector2 scrollPosition)
    {
        if (suppressScrollEvent)
        {
            return;
        }

        if (!isLogExpanded)
        {
            RestoreVisibleEntries();
        }
    }

    public void ExpandLog()
    {
        isLogExpanded = true;
        RestoreVisibleEntries();
    }

    public void CollapseLog()
    {
        isLogExpanded = false;
        HideVisibleEntries();
    }

    private void RestoreVisibleEntries()
    {
        foreach (var controller in entryControllers)
        {
            if (controller != null && controller.GetState() != TurnLogEntry.State.Visible)
            {
                controller.Restore();
            }
        }
    }

    private void HideVisibleEntries()
    {
        foreach (var controller in entryControllers)
        {
            if (controller != null && controller.GetState() == TurnLogEntry.State.Visible)
            {
                controller.StartFadeOut();
            }
        }
    }

    public void ClearAllLogs()
    {
        foreach (var entry in entries)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }
        entries.Clear();
        entryControllers.Clear();
    }
}
