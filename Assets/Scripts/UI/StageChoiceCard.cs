using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// 스테이지 행선지 및 범용 선택지 카드 프리팹을 제어하는 컴포넌트입니다.
/// 인스펙터에서 외형 및 텍스트/버튼/아웃라인 컴포넌트들을 자유롭게 커스텀할 수 있습니다.
/// </summary>
public class StageChoiceCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Background & Frame")]
    public Image bgImage;
    public Outline outline;
    public Image headerBg;

    [Header("Text Content")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;

    [Header("Reward Banner")]
    public GameObject rewardBanner;
    public TextMeshProUGUI rewardText;

    [Header("Button")]
    public Button selectButton;
    public Image selectButtonBg;
    public TextMeshProUGUI selectButtonText;

    private StageChoiceOption option;
    private Action onSelectedCallback;
    private bool isAppearing = true;
    private bool isSelected = false;

    private void Awake()
    {
        // 인스펙터 미할당 시 자식 오브젝트로부터 자동 바인딩
        if (bgImage == null) bgImage = GetComponent<Image>();
        if (outline == null) outline = GetComponent<Outline>();

        if (headerBg == null)
        {
            Transform header = transform.Find("Header");
            if (header != null) headerBg = header.GetComponent<Image>();
        }

        if (titleText == null)
        {
            Transform titleObj = transform.Find("Header/TitleText") ?? transform.Find("TitleText");
            if (titleObj != null) titleText = titleObj.GetComponent<TextMeshProUGUI>();
        }

        if (descText == null)
        {
            Transform descObj = transform.Find("DescText");
            if (descObj != null) descText = descObj.GetComponent<TextMeshProUGUI>();
        }

        if (rewardBanner == null)
        {
            Transform bannerObj = transform.Find("RewardBanner");
            if (bannerObj != null) rewardBanner = bannerObj.gameObject;
        }

        if (rewardText == null && rewardBanner != null)
        {
            Transform rTextObj = rewardBanner.transform.Find("RewardText");
            if (rTextObj != null) rewardText = rTextObj.GetComponent<TextMeshProUGUI>();
        }

        if (selectButton == null)
        {
            Transform btnObj = transform.Find("SelectBtn");
            if (btnObj != null)
            {
                selectButton = btnObj.GetComponent<Button>();
                selectButtonBg = btnObj.GetComponent<Image>();
                Transform tObj = btnObj.Find("Text");
                if (tObj != null) selectButtonText = tObj.GetComponent<TextMeshProUGUI>();
            }
            if (selectButton == null) selectButton = GetComponent<Button>();
        }
    }

    /// <summary>
    /// 전달받은 선택지 정보로 카드의 외형 및 텍스트를 구성하고 등장 애니메이션을 재생합니다.
    /// </summary>
    public void Setup(StageChoiceOption opt, float width, float height, bool isCompact, Action onSelected)
    {
        option = opt;
        onSelectedCallback = onSelected;
        isSelected = false;
        isAppearing = true;

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.zero;
        }

        Color themeColor = option != null ? option.themeColor : Color.white;

        if (bgImage != null)
        {
            bgImage.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
        }

        if (outline != null)
        {
            outline.effectColor = themeColor;
            outline.effectDistance = new Vector2(2f, 2f);
        }

        if (headerBg != null)
        {
            Color hColor = themeColor;
            hColor.a = 0.35f;
            headerBg.color = hColor;
        }

        if (titleText != null)
        {
            titleText.text = option?.title ?? "";
            titleText.color = themeColor;
            titleText.fontSize = isCompact ? 13.5f : 15f;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 8.5f;
            titleText.fontSizeMax = isCompact ? 13.5f : 15f;
            titleText.textWrappingMode = TextWrappingModes.Normal;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
        }

        bool hasReward = option != null && !string.IsNullOrEmpty(option.rewardText);

        if (descText != null)
        {
            descText.text = option?.description ?? "";
            descText.fontSize = isCompact ? 10.5f : 11.5f;
            descText.enableAutoSizing = true;
            descText.fontSizeMin = 7.5f;
            descText.fontSizeMax = isCompact ? 10.5f : 11.5f;
            descText.textWrappingMode = TextWrappingModes.Normal;
            descText.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform descRect = descText.rectTransform;
            if (descRect != null)
            {
                descRect.anchoredPosition = new Vector2(0f, hasReward ? 15f : 0f);
                descRect.sizeDelta = new Vector2(-20f, hasReward ? 75f : 110f);
            }
        }

        if (rewardBanner != null)
        {
            rewardBanner.SetActive(hasReward);
        }

        if (rewardText != null && hasReward)
        {
            rewardText.text = option.rewardText;
            rewardText.fontSize = isCompact ? 8.5f : 9.5f;
            rewardText.enableAutoSizing = true;
            rewardText.fontSizeMin = 7f;
            rewardText.fontSizeMax = isCompact ? 8.5f : 9.5f;
            rewardText.textWrappingMode = TextWrappingModes.Normal;
            rewardText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (selectButtonBg != null)
        {
            Color btnBgColor = themeColor * 0.8f;
            btnBgColor.a = 1f;
            selectButtonBg.color = btnBgColor;
        }

        if (selectButtonText != null)
        {
            selectButtonText.text = string.IsNullOrEmpty(option?.buttonText) ? "선택" : option.buttonText;
            selectButtonText.enableAutoSizing = true;
            selectButtonText.fontSizeMin = 8f;
            selectButtonText.fontSizeMax = 12f;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnCardClicked);
        }

        // 등장 애니메이션
        transform.DOKill();
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetUpdate(true)
            .OnComplete(() =>
            {
                isAppearing = false;
                transform.localScale = Vector3.one;
            });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected || isAppearing) return;

        transform.DOKill();
        transform.DOScale(1.05f, 0.15f).SetUpdate(true);
        if (outline != null) outline.effectDistance = new Vector2(3f, 3f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected || isAppearing) return;

        transform.DOKill();
        transform.DOScale(1.0f, 0.15f).SetUpdate(true);
        if (outline != null) outline.effectDistance = new Vector2(2f, 2f);
    }

    private void OnCardClicked()
    {
        if (isSelected) return;
        isSelected = true;

        transform.DOKill();
        transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 10, 1).SetUpdate(true)
            .OnComplete(() =>
            {
                onSelectedCallback?.Invoke();
            });
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}

