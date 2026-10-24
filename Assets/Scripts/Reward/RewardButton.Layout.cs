using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public partial class RewardButton
{
    public void EnsureCardLayout()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(145f, 180f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }

        // 1. Root Image (카드 배경)
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
            originalBgColor = backgroundImage.color;
        }

        // 2. Outline
        if (cardOutline == null) cardOutline = GetComponent<Outline>();
        if (cardOutline == null) cardOutline = gameObject.AddComponent<Outline>();
        cardOutline.effectDistance = new Vector2(2f, 2f);

        // 3. Header Banner
        Transform hObj = transform.Find("Header");
        if (hObj == null)
        {
            GameObject headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(transform, false);
            hObj = headerGo.transform;
        }
        RectTransform headerRect = hObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, 0f);
        headerRect.sizeDelta = new Vector2(0f, 26f);
        headerRect.localScale = Vector3.one;

        headerBg = hObj.GetComponent<Image>() ?? hObj.gameObject.AddComponent<Image>();

        Transform htObj = hObj.Find("HeaderText");
        if (htObj == null)
        {
            GameObject htGo = new GameObject("HeaderText", typeof(RectTransform));
            htGo.transform.SetParent(hObj, false);
            htObj = htGo.transform;
        }
        RectTransform htRect = htObj.GetComponent<RectTransform>();
        htRect.anchorMin = Vector2.zero;
        htRect.anchorMax = Vector2.one;
        htRect.anchoredPosition = Vector2.zero;
        htRect.sizeDelta = Vector2.zero;
        htRect.localScale = Vector3.one;

        headerText = htObj.GetComponent<TextMeshProUGUI>() ?? htObj.gameObject.AddComponent<TextMeshProUGUI>();
        headerText.fontSize = 10f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.enableAutoSizing = true;
        headerText.fontSizeMin = 7.5f;
        headerText.fontSizeMax = 10.5f;
        headerText.textWrappingMode = TextWrappingModes.NoWrap;
        headerText.overflowMode = TextOverflowModes.Ellipsis;
        if (infoText != null && headerText.font == null) headerText.font = infoText.font;

        // 4. Center Icon
        if (iconImage != null)
        {
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -28f);
            iconRect.sizeDelta = new Vector2(36f, 36f);
            iconRect.localScale = Vector3.one;
            iconImage.preserveAspect = true;
        }

        // 5. Title Text
        Transform tObj = transform.Find("TitleText");
        if (tObj == null)
        {
            if (infoText != null && infoText.gameObject.name != "TitleText")
            {
                tObj = infoText.transform;
                tObj.name = "TitleText";
            }
            else
            {
                GameObject tGo = new GameObject("TitleText", typeof(RectTransform));
                tGo.transform.SetParent(transform, false);
                tObj = tGo.transform;
            }
        }
        RectTransform titleRect = tObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -66f);
        titleRect.sizeDelta = new Vector2(-10f, 26f);
        titleRect.localScale = Vector3.one;

        titleText = tObj.GetComponent<TextMeshProUGUI>() ?? tObj.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 11.5f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 8f;
        titleText.fontSizeMax = 12f;
        titleText.textWrappingMode = TextWrappingModes.Normal;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        if (infoText != null && titleText.font == null) titleText.font = infoText.font;
        infoText = titleText;

        // 6. Description Text
        Transform dObj = transform.Find("DescText");
        if (dObj == null)
        {
            GameObject dGo = new GameObject("DescText", typeof(RectTransform));
            dGo.transform.SetParent(transform, false);
            dObj = dGo.transform;
        }
        RectTransform descRect = dObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0.5f, 1f);
        descRect.anchoredPosition = new Vector2(0f, -94f);
        descRect.sizeDelta = new Vector2(-12f, 48f);
        descRect.localScale = Vector3.one;

        descText = dObj.GetComponent<TextMeshProUGUI>() ?? dObj.gameObject.AddComponent<TextMeshProUGUI>();
        descText.fontSize = 9.5f;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.85f, 0.85f, 0.88f, 1f);
        descText.enableAutoSizing = true;
        descText.fontSizeMin = 6.5f;
        descText.fontSizeMax = 9.5f;
        descText.textWrappingMode = TextWrappingModes.Normal;
        descText.overflowMode = TextOverflowModes.Ellipsis;
        if (infoText != null && descText.font == null) descText.font = infoText.font;

        // 7. Select Button
        Transform sObj = transform.Find("SelectBtn");
        if (sObj == null)
        {
            GameObject sGo = new GameObject("SelectBtn", typeof(RectTransform));
            sGo.transform.SetParent(transform, false);
            sObj = sGo.transform;
        }
        RectTransform selectRect = sObj.GetComponent<RectTransform>();
        selectRect.anchorMin = new Vector2(0f, 0f);
        selectRect.anchorMax = new Vector2(1f, 0f);
        selectRect.pivot = new Vector2(0.5f, 0f);
        selectRect.anchoredPosition = new Vector2(0f, 7f);
        selectRect.sizeDelta = new Vector2(-20f, 24f);
        selectRect.localScale = Vector3.one;

        selectBtnBg = sObj.GetComponent<Image>() ?? sObj.gameObject.AddComponent<Image>();

        Transform stObj = sObj.Find("Text");
        if (stObj == null)
        {
            GameObject stGo = new GameObject("Text", typeof(RectTransform));
            stGo.transform.SetParent(sObj, false);
            stObj = stGo.transform;
        }
        RectTransform stRect = stObj.GetComponent<RectTransform>();
        stRect.anchorMin = Vector2.zero;
        stRect.anchorMax = Vector2.one;
        stRect.anchoredPosition = Vector2.zero;
        stRect.sizeDelta = Vector2.zero;
        stRect.localScale = Vector3.one;

        selectBtnText = stObj.GetComponent<TextMeshProUGUI>() ?? stObj.gameObject.AddComponent<TextMeshProUGUI>();
        selectBtnText.fontSize = 11f;
        selectBtnText.fontStyle = FontStyles.Bold;
        selectBtnText.alignment = TextAlignmentOptions.Center;
        selectBtnText.color = Color.white;
        selectBtnText.enableAutoSizing = true;
        selectBtnText.fontSizeMin = 8f;
        selectBtnText.fontSizeMax = 11f;
        selectBtnText.text = "선택";
        if (infoText != null && selectBtnText.font == null) selectBtnText.font = infoText.font;
    }

    private void AnimateCoinFly(int amount, System.Action onComplete = null)
    {
        if (GameManager.Instance == null || GameManager.Instance.coinText == null)
        {
            GameManager.Instance?.AddCoin(amount);
            onComplete?.Invoke();
            return;
        }

        GameManager.Instance.AddCoin(amount);

        GameObject flyObj = new GameObject("FlyingCoin");
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        flyObj.transform.SetParent(rootCanvas != null ? rootCanvas.transform : transform.root, true);
        
        flyObj.transform.position = transform.position;
        flyObj.transform.localScale = Vector3.one;

        TextMeshProUGUI flyText = flyObj.AddComponent<TextMeshProUGUI>();
        flyText.raycastTarget = false;
        flyText.text = $"+{amount}";
        flyText.fontSize = 40;
        flyText.color = Color.yellow;
        flyText.alignment = TextAlignmentOptions.Center;
        flyText.textWrappingMode = TextWrappingModes.NoWrap;
        if (infoText != null) flyText.font = infoText.font;
        else if (GameManager.Instance != null && GameManager.Instance.coinText != null)
        {
            flyText.font = GameManager.Instance.coinText.font;
        }
        if (flyText.font == null)
        {
            flyText.font = TMP_Settings.defaultFontAsset;
        }
        
        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        seq.Append(flyObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(GameManager.Instance.coinText.transform.position, 0.8f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0.5f, 0.8f).SetDelay(0.2f));
        
        seq.OnComplete(() =>
        {
            Destroy(flyObj);
            onComplete?.Invoke();
        });
    }

    private void AnimateArtifactFly(ArtifactData artifact, System.Action onComplete = null)
    {
        if (ArtifactManager.Instance == null || artifact == null)
        {
            onComplete?.Invoke();
            return;
        }

        GameObject flyObj = new GameObject("FlyingArtifact");
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        flyObj.transform.SetParent(rootCanvas != null ? rootCanvas.transform : transform.root, true);
        
        flyObj.transform.position = transform.position;
        flyObj.transform.localScale = Vector3.one;

        Image flyImage = flyObj.AddComponent<Image>();
        flyImage.sprite = artifact.icon;
        flyImage.raycastTarget = false;
        
        RectTransform rectTransform = flyObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(64, 64);

        Vector3 targetPos = transform.position;
        ArtifactSlot incomingSlot = ArtifactManager.Instance.GetIncomingSlot(artifact);
        if (incomingSlot != null)
        {
            targetPos = incomingSlot.transform.position;
        }

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        seq.Append(flyObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(targetPos, 0.8f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0.3f, 0.8f).SetDelay(0.2f));
        
        seq.OnComplete(() =>
        {
            if (ArtifactManager.Instance != null)
            {
                ArtifactManager.Instance.AddArtifact(artifact);
            }
            Destroy(flyObj);
            onComplete?.Invoke();
        });
    }

    private void AnimateSealFly(SealData seal, System.Action onComplete = null)
    {
        if (SealInventory.Instance != null && seal != null)
        {
            SealInventory.Instance.AddSeal(seal);
        }

        if (seal == null)
        {
            onComplete?.Invoke();
            return;
        }

        Vector3 targetPos = transform.position;
        var sealSlots = FindObjectsByType<SealInventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(s => s.transform.GetSiblingIndex())
            .ToArray();
        int sealIdx = SealInventory.Instance != null ? SealInventory.Instance.GetSealIndex(seal) : -1;
        if (sealIdx >= 0 && sealIdx < sealSlots.Length)
        {
            targetPos = sealSlots[sealIdx].transform.position;
        }
        else if (sealSlots.Length > 0)
        {
            targetPos = sealSlots[0].transform.position;
        }

        GameObject flyObj = new GameObject("FlyingSeal");
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        flyObj.transform.SetParent(rootCanvas != null ? rootCanvas.transform : transform.root, true);

        flyObj.transform.position = transform.position;
        flyObj.transform.localScale = Vector3.one;

        Image flyImg = flyObj.AddComponent<Image>();
        flyImg.sprite = seal.icon != null ? seal.icon : (iconImage != null ? iconImage.sprite : null);
        flyImg.preserveAspect = true;
        flyImg.raycastTarget = false;

        RectTransform rt = flyObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(48, 48);

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        seq.Append(flyObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(targetPos, 0.6f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0.4f, 0.6f).SetDelay(0.2f));
        seq.OnComplete(() =>
        {
            FindFirstObjectByType<SealInventoryPanel>(FindObjectsInactive.Include)?.RefreshSealItems();
            Destroy(flyObj);
            onComplete?.Invoke();
        });
    }
}

