using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnLogUIManager : MonoBehaviour
{
    private static TurnLogUIManager _instance;
    public static TurnLogUIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TurnLogUIManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("References")]
    public RectTransform contentParent;
    public GameObject logEntryPrefab;
    public ScrollRect scrollRect;

    [Header("Appearance")]
    public Color playerColor = new Color(0.45f, 0.84f, 0.45f, 1f);
    public Color enemyColor = new Color(0.89f, 0.58f, 0.58f, 1f);
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
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Start()
    {
        EnsureReferences();

        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }
    }

    private void EnsureReferences()
    {
        if (scrollRect == null)
        {
            scrollRect = FindFirstObjectByType<ScrollRect>();
        }

        if (contentParent == null)
        {
            if (scrollRect != null && scrollRect.content != null)
            {
                contentParent = scrollRect.content;
            }
            else
            {
                GameObject contentObj = GameObject.Find("Content");
                if (contentObj != null)
                {
                    contentParent = contentObj.GetComponent<RectTransform>();
                }
            }
        }
    }

    public void AddLog(string text, bool isEnemy)
    {
        EnsureReferences();

        if (contentParent == null)
        {
            Debug.LogWarning("[TurnLogUIManager] contentParent가 없어 로그를 추가할 수 없습니다: " + text);
            return;
        }

        GameObject entryObj = null;
        TextMeshProUGUI textComponent = null;

        if (logEntryPrefab != null)
        {
            entryObj = Instantiate(logEntryPrefab, contentParent);
        }
        else
        {
            entryObj = new GameObject("TurnLogEntry", typeof(RectTransform));
            entryObj.transform.SetParent(contentParent, false);
            textComponent = entryObj.AddComponent<TextMeshProUGUI>();
            textComponent.raycastTarget = false;
            textComponent.textWrappingMode = TextWrappingModes.Normal;
            textComponent.fontSize = 11;
            textComponent.alignment = TextAlignmentOptions.Left;
        }

        // RectTransform 위치, 스케일, 로테이션 초기화 (프리팹의 비정상 Z좌표(-681px) 등으로 인한 카메라 클리핑 방지)
        RectTransform rt = entryObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            if (rt.sizeDelta.y < 14f)
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x > 0 ? rt.sizeDelta.x : 230f, 16f);
            }
        }

        // LayoutElement 보장 (VerticalLayoutGroup에서 0높이로 축소되는 현상 방지)
        LayoutElement layoutElement = entryObj.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = entryObj.AddComponent<LayoutElement>();
        }
        layoutElement.minHeight = 14f;
        layoutElement.preferredHeight = 16f;

        // TurnLogEntry 컨트롤러 설정
        TurnLogEntry entryController = entryObj.GetComponent<TurnLogEntry>();
        if (entryController == null)
        {
            entryController = entryObj.AddComponent<TurnLogEntry>();
        }

        if (textComponent == null)
        {
            textComponent = entryObj.GetComponentInChildren<TextMeshProUGUI>();
        }

        Color targetColor = isEnemy ? enemyColor : playerColor;
        if (entryController != null)
        {
            entryController.SetText(text, targetColor);
            entryController.SetTimings(autoHideDelay, fadeOutDuration);
            entryController.Show(); // 알파 1.0 및 표시 상태 보장
        }
        else if (textComponent != null)
        {
            textComponent.text = text;
            textComponent.color = targetColor;
        }

        entries.Add(entryObj);
        if (entryController != null)
        {
            entryControllers.Add(entryController);
        }

        while (entries.Count > maxEntries)
        {
            var old = entries[0];
            entries.RemoveAt(0);
            if (entryControllers.Count > 0)
            {
                entryControllers.RemoveAt(0);
            }
            if (old != null) Destroy(old);
        }

        if (scrollRect == null && contentParent != null)
        {
            scrollRect = contentParent.GetComponentInParent<ScrollRect>();
        }

        if (contentParent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
        }

        if (scrollRect != null)
        {
            if (scrollRect.viewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);
            }
            Canvas.ForceUpdateCanvases();
            suppressScrollEvent = true;
            scrollRect.verticalNormalizedPosition = 0f;
            StartCoroutine(ClearSuppressScrollNextFrame());
        }
    }

    /// <summary>
    /// 현재까지 기록된 턴 로그 엔트리 개수를 반환합니다.
    /// </summary>
    public int GetLogCount()
    {
        return entries.Count;
    }

    /// <summary>
    /// 지정한 인덱스부터 현재까지의 모든 로그에 취소선을 적용합니다 (시간 역행 시 사용).
    /// </summary>
    public void ApplyStrikethroughFromIndex(int startIndex)
    {
        if (startIndex < 0) startIndex = 0;

        for (int i = startIndex; i < entries.Count; i++)
        {
            if (i < entryControllers.Count && entryControllers[i] != null)
            {
                entryControllers[i].ApplyStrikethrough();
            }
            else if (entries[i] != null)
            {
                var tmp = entries[i].GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null && !string.IsNullOrEmpty(tmp.text) && !tmp.text.StartsWith("<s>"))
                {
                    tmp.text = $"<s>{tmp.text}</s>";
                    tmp.color = new Color(0.6f, 0.6f, 0.65f, 0.7f);
                }
            }
        }
    }

    private IEnumerator ClearSuppressScrollNextFrame()
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
