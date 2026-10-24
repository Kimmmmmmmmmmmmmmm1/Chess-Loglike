using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public partial class RewardManager
{
    public void SetupHorizontalRewardLayout()
    {
        if (rewardPanel != null)
        {
            rewardPanelRect = rewardPanel.GetComponent<RectTransform>();
            if (rewardPanelRect != null)
            {
                rewardPanelRect.pivot = new Vector2(0.5f, 0.5f);
                rewardPanelFinalAnchoredPos = Vector2.zero;
            }

            Transform titleTextObj = rewardPanel.transform.Find("Image/GameObject/Text (TMP)");
            if (titleTextObj != null)
            {
                titleText = titleTextObj.GetComponent<TMPro.TextMeshProUGUI>();
            }
        }

        if (closeButton != null)
        {
            closeButtonText = closeButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        }

        if (rewardContainer != null)
        {
            var vertical = rewardContainer.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
            {
                DestroyImmediate(vertical);
            }

            var horizontal = rewardContainer.GetComponent<HorizontalLayoutGroup>();
            if (horizontal == null)
            {
                horizontal = rewardContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            horizontal.spacing = 15f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = false;
            horizontal.childControlHeight = false;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;

            RectTransform containerRect = rewardContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                containerRect.anchorMin = new Vector2(0f, 0f);
                containerRect.anchorMax = new Vector2(1f, 1f);
                containerRect.pivot = new Vector2(0.5f, 0.5f);
                containerRect.anchoredPosition = Vector2.zero;
                containerRect.sizeDelta = Vector2.zero;
            }

            rewardScrollRect = rewardContainer.GetComponentInParent<ScrollRect>();
            if (rewardScrollRect != null)
            {
                rewardScrollRect.horizontal = false;
                rewardScrollRect.vertical = false;
                RectTransform scrollRectTransform = rewardScrollRect.GetComponent<RectTransform>();
                if (scrollRectTransform != null)
                {
                    scrollRectTransform.sizeDelta = new Vector2(490f, 185f);
                    scrollRectTransform.anchoredPosition = new Vector2(0f, -4f);
                }

                if (rewardScrollRect.verticalScrollbar != null)
                {
                    rewardScrollRect.verticalScrollbar.gameObject.SetActive(false);
                }
            }
        }

        SetupRewardSummaryText();
    }

    private void SetupRewardSummaryText()
    {
        if (rewardSummaryText != null || rewardPanel == null) return;

        Transform found = rewardPanel.transform.Find("RewardSummaryText")
            ?? rewardPanel.transform.Find("Image/GameObject/RewardSummaryText");

        if (found != null)
        {
            rewardSummaryText = found.GetComponent<TMPro.TextMeshProUGUI>();
            return;
        }

        Transform titleTextObj = rewardPanel.transform.Find("Image/GameObject/Text (TMP)");
        Transform parentTarget = titleTextObj != null ? titleTextObj.parent : rewardPanel.transform;

        GameObject summaryObj = new GameObject("RewardSummaryText");
        summaryObj.transform.SetParent(parentTarget, false);
        rewardSummaryText = summaryObj.AddComponent<TMPro.TextMeshProUGUI>();

        if (titleTextObj != null)
        {
            TMPro.TextMeshProUGUI titleTmp = titleTextObj.GetComponent<TMPro.TextMeshProUGUI>();
            if (titleTmp != null)
            {
                rewardSummaryText.font = titleTmp.font;
            }
        }

        rewardSummaryText.alignment = TMPro.TextAlignmentOptions.Center;
        rewardSummaryText.fontSize = 11f;
        rewardSummaryText.textWrappingMode = TMPro.TextWrappingModes.Normal;

        RectTransform summaryRect = rewardSummaryText.GetComponent<RectTransform>();
        if (summaryRect != null)
        {
            summaryRect.anchorMin = new Vector2(0.5f, 0.5f);
            summaryRect.anchorMax = new Vector2(0.5f, 0.5f);
            summaryRect.pivot = new Vector2(0.5f, 0.5f);
            summaryRect.anchoredPosition = new Vector2(0f, 15f);
            summaryRect.sizeDelta = new Vector2(280f, 110f);
        }
    }

    private void ShowSettlementPhase(GameManager.BattleRewardResult result)
    {
        isSettlementPhase = true;

        if (rewardPanelRect != null)
        {
            rewardPanelRect.DOKill();
            rewardPanelRect.sizeDelta = new Vector2(320f, 240f);
            rewardPanelRect.anchoredPosition = Vector2.zero;
        }

        if (titleText != null)
        {
            titleText.text = "전투 정산";
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            if (titleRect != null)
            {
                titleRect.anchoredPosition = new Vector2(0f, 92f);
            }
        }

        if (rewardSummaryText != null)
        {
            rewardSummaryText.gameObject.SetActive(true);
            UpdateRewardSummaryUI(result);
            RectTransform summaryRect = rewardSummaryText.GetComponent<RectTransform>();
            if (summaryRect != null)
            {
                summaryRect.anchoredPosition = new Vector2(0f, 12f);
                summaryRect.sizeDelta = new Vector2(280f, 115f);
            }
        }

        if (rewardScrollRect != null) rewardScrollRect.gameObject.SetActive(false);
        if (rerollButton != null) rerollButton.gameObject.SetActive(false);

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(true);
            closeButton.interactable = true;
            if (closeButtonText != null)
            {
                closeButtonText.text = "보상 받기";
            }
            RectTransform btnRect = closeButton.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                btnRect.anchoredPosition = new Vector2(0f, -85f);
            }

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClaimSettlementRewardClicked);
        }
    }

    private void OnClaimSettlementRewardClicked()
    {
        if (closeButton != null)
        {
            closeButton.interactable = false;
        }

        Vector3 startPos = closeButton != null
            ? closeButton.transform.position
            : (rewardPanel != null ? rewardPanel.transform.position : transform.position);

        PlayCoinFlyAnimation(currentBattleReward.TotalReward, startPos, () =>
        {
            ShowChoicePhase(3, currentRarity);
        });
    }

    private void PlayCoinFlyAnimation(int amount, Vector3 startPos, System.Action onComplete)
    {
        if (amount <= 0 || GameManager.Instance == null || GameManager.Instance.coinText == null)
        {
            if (amount > 0 && GameManager.Instance != null)
            {
                GameManager.Instance.AddCoin(amount);
            }
            onComplete?.Invoke();
            return;
        }

        GameObject flyObj = new GameObject("FlyingCoin");
        Canvas rootCanvas = rewardPanel != null ? rewardPanel.GetComponentInParent<Canvas>() : null;
        if (rootCanvas != null)
        {
            flyObj.transform.SetParent(rootCanvas.transform, true);
        }
        else
        {
            flyObj.transform.SetParent(rewardPanel != null ? rewardPanel.transform.root : transform.root, true);
        }

        flyObj.transform.position = startPos;
        flyObj.transform.localScale = Vector3.one;

        TMPro.TextMeshProUGUI flyText = flyObj.AddComponent<TMPro.TextMeshProUGUI>();
        flyText.raycastTarget = false;
        flyText.text = $"+{amount}";
        flyText.fontSize = 34;
        flyText.color = Color.yellow;
        flyText.alignment = TMPro.TextAlignmentOptions.Center;
        flyText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        if (GameManager.Instance.coinText != null)
        {
            flyText.font = GameManager.Instance.coinText.font;
        }
        if (flyText.font == null)
        {
            flyText.font = TMPro.TMP_Settings.defaultFontAsset;
        }

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(flyObj.transform.DOScale(1.25f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(GameManager.Instance.coinText.transform.position, 0.75f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0.5f, 0.75f).SetDelay(0.15f));

        seq.OnComplete(() =>
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoin(amount);
            }
            Destroy(flyObj);
            onComplete?.Invoke();
        });
    }

    private void UpdateRewardSummaryUI(GameManager.BattleRewardResult result)
    {
        if (rewardSummaryText == null) return;

        string summary = 
            $"기본 보상 +{result.BaseReward}\n" +
            $"적 기물 포획 +{result.EnemyCaptureReward}\n" +
            $"내 기물 포획당함 +{result.PlayerLossReward}\n" +
            $"__________________\n" +
            $"<color=#FFD700><b>총 보상 +{result.TotalReward}G</b></color>";

        rewardSummaryText.text = summary;
    }

    private void AnimateCardEntrance(GameObject obj, RewardButton btn, int index, bool isTutorial)
    {
        if (isTutorial && index == 0)
        {
            obj.transform.localScale = Vector3.zero;
            obj.transform.DOScale(Vector3.one, 0.3f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (btn != null)
                    {
                        btn.SetTutorialHighlighted(true);
                    }
                });
        }
        else
        {
            obj.transform.localScale = Vector3.zero;
            obj.transform.DOScale(Vector3.one, 0.35f)
                .SetEase(Ease.OutBack)
                .SetDelay(index * 0.08f + 0.1f)
                .SetUpdate(true);
        }
    }

    private void SetRewardPanelHeight(float height)
    {
        if (rewardPanelRect == null) return;

        Vector2 size = rewardPanelRect.sizeDelta;
        size.y = height;
        rewardPanelRect.sizeDelta = size;
    }

    private Tween CreateRewardPanelHeightTween(float targetHeight)
    {
        if (rewardPanelRect == null)
        {
            return DOVirtual.DelayedCall(0f, () => { });
        }

        return DOTween.To(
            () => rewardPanelRect.sizeDelta.y,
            value => SetRewardPanelHeight(value),
            targetHeight,
            rewardAnimDuration
        );
    }
}

