using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 체스 시계 스타일의 턴 인디케이터 단일 상자(흑/백) 컴포넌트입니다.
/// CanvasGroup이나 스케일 애니메이션 없이 색상과 활성화 상태를 직접 제어하여 크기를 일정하게 유지합니다.
/// </summary>
public class TurnIndicatorBox : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform boxRect;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image bgImage;
    [SerializeField] private Image shadowImage;
    [SerializeField] private TextMeshProUGUI labelText;   // "흑" or "백"
    [SerializeField] private TextMeshProUGUI statusText;  // "내 턴" or "상대 턴"

    [Header("Color Theme")]
    [SerializeField] private Color activeBorderColor = Color.white;
    [SerializeField] private Color inactiveBorderColor = Color.gray;
    [SerializeField] private Color activeBgColor = Color.black;
    [SerializeField] private Color inactiveBgColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color activeTextColor = Color.white;
    [SerializeField] private Color inactiveTextColor = Color.gray;

    private bool isCurrentlyActive = false;

    private void Awake()
    {
        EnsureSprites();
        if (boxRect != null) boxRect.localScale = Vector3.one;
    }

    public void EnsureSprites()
    {
        Sprite sprite = TurnIndicatorUI.GetOrCreateBoxSprite();

        if (borderImage != null)
        {
            borderImage.sprite = sprite;
            borderImage.type = Image.Type.Simple;
        }

        if (bgImage != null)
        {
            bgImage.sprite = sprite;
            bgImage.type = Image.Type.Simple;
        }

        if (shadowImage != null)
        {
            shadowImage.sprite = sprite;
            shadowImage.type = Image.Type.Simple;
        }
    }

    public void Initialize(
        RectTransform rect,
        Image border,
        Image bg,
        Image shadow,
        TextMeshProUGUI label,
        TextMeshProUGUI status,
        Color actBorder,
        Color inactBorder,
        Color actBg,
        Color inactBg,
        Color actTxt,
        Color inactTxt)
    {
        boxRect = rect;
        borderImage = border;
        bgImage = bg;
        shadowImage = shadow;
        labelText = label;
        statusText = status;
        activeBorderColor = actBorder;
        inactiveBorderColor = inactBorder;
        activeBgColor = actBg;
        inactiveBgColor = inactBg;
        activeTextColor = actTxt;
        inactiveTextColor = inactTxt;

        if (boxRect != null) boxRect.localScale = Vector3.one;
        EnsureSprites();
        SetState(false, statusText != null ? statusText.text : "대기중");
    }

    public void SetState(bool active, string statusString = null)
    {
        isCurrentlyActive = active;

        if (statusText != null && !string.IsNullOrEmpty(statusString))
        {
            statusText.text = statusString;
        }

        // 스케일은 항상 1로 고정하여 크기 변형 원천 차단
        if (boxRect != null)
        {
            boxRect.localScale = Vector3.one;
        }

        Color targetBorder = active ? activeBorderColor : inactiveBorderColor;
        Color targetBg = active ? activeBgColor : inactiveBgColor;
        Color targetText = active ? activeTextColor : inactiveTextColor;

        if (borderImage != null) borderImage.color = targetBorder;
        if (bgImage != null) bgImage.color = targetBg;
        if (labelText != null) labelText.color = targetText;
        if (statusText != null) statusText.color = targetText;
        if (shadowImage != null) shadowImage.gameObject.SetActive(active);
    }
}
