using UnityEngine;
using TMPro;

/// <summary>
/// 스테이지 선택 모달 팝업 패널 프리팹을 제어하는 컴포넌트입니다.
/// </summary>
public class StageSelectPanel : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI headerTitleText;
    public TextMeshProUGUI headerSubtitleText;
    public RectTransform cardContainer;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (headerTitleText == null)
        {
            Transform t = transform.Find("HeaderTitle");
            if (t != null) headerTitleText = t.GetComponent<TextMeshProUGUI>();
        }

        if (headerSubtitleText == null)
        {
            Transform st = transform.Find("HeaderSubtitle");
            if (st != null) headerSubtitleText = st.GetComponent<TextMeshProUGUI>();
        }

        if (cardContainer == null)
        {
            Transform cc = transform.Find("CardContainer");
            if (cc != null) cardContainer = cc.GetComponent<RectTransform>();
        }
    }
}

