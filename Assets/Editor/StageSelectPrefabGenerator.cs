#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public static class StageSelectPrefabGenerator
{
    private const string PrefabDir = "Assets/Prefabs/UI";
    private const string ResourcesDir = "Assets/Resources/UI";

    private const string ChoiceCardPrefabPath = "Assets/Prefabs/UI/StageChoiceCard.prefab";
    private const string ChoiceCardResourcesPath = "Assets/Resources/UI/StageChoiceCard.prefab";

    private const string SelectPanelPrefabPath = "Assets/Prefabs/UI/StageSelectPanel.prefab";
    private const string SelectPanelResourcesPath = "Assets/Resources/UI/StageSelectPanel.prefab";

    [InitializeOnLoadMethod]
    private static void CheckAndGenerateOnInit()
    {
        EditorApplication.delayCall += () =>
        {
            bool needChoiceCard = !File.Exists(ChoiceCardPrefabPath) || !File.Exists(ChoiceCardResourcesPath);
            bool needSelectPanel = !File.Exists(SelectPanelPrefabPath) || !File.Exists(SelectPanelResourcesPath);

            if (needChoiceCard || needSelectPanel)
            {
                GenerateAllPrefabs();
            }
        };
    }

    [MenuItem("Tools/UI/Generate All Stage Select Prefabs")]
    public static void GenerateAllPrefabs()
    {
        EnsureDirectories();
        GenerateChoiceCardPrefab();
        GenerateSelectPanelPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StageSelectPrefabGenerator] Successfully generated all Stage Select prefabs!");
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }

        if (!AssetDatabase.IsValidFolder(ResourcesDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        }
    }

    [MenuItem("Tools/UI/Generate Stage Choice Card Prefab")]
    public static void GenerateChoiceCardPrefab()
    {
        EnsureDirectories();

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF.asset");
        if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Pretendard-Medium SDF.asset");

        GameObject cardObj = new GameObject("StageChoiceCard", typeof(RectTransform));
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(195f, 210f);
        cardRect.localScale = Vector3.one;

        Image bgImage = cardObj.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(2f, 2f);

        Button btn = cardObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = cb;

        // Header
        GameObject headerObj = new GameObject("Header", typeof(RectTransform));
        headerObj.transform.SetParent(cardObj.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, 0f);
        headerRect.sizeDelta = new Vector2(0f, 38f);
        headerRect.localScale = Vector3.one;

        Image headerBg = headerObj.AddComponent<Image>();
        headerBg.color = new Color(1f, 1f, 1f, 0.35f);

        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform));
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;
        titleRect.localScale = Vector3.one;

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (font != null) titleText.font = font;
        titleText.text = "OPTION TITLE";
        titleText.fontSize = 15f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // DescText
        GameObject descObj = new GameObject("DescText", typeof(RectTransform));
        descObj.transform.SetParent(cardObj.transform, false);
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0.5f);
        descRect.anchorMax = new Vector2(1f, 0.5f);
        descRect.pivot = new Vector2(0.5f, 0.5f);
        descRect.anchoredPosition = new Vector2(0f, 15f);
        descRect.sizeDelta = new Vector2(-20f, 75f);
        descRect.localScale = Vector3.one;

        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        if (font != null) descText.font = font;
        descText.text = "선택지에 대한 상세 설명이 표시됩니다.";
        descText.fontSize = 11.5f;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.85f, 0.85f, 0.88f, 1f);
        descText.textWrappingMode = TextWrappingModes.Normal;

        // RewardBanner
        GameObject rewardBannerObj = new GameObject("RewardBanner", typeof(RectTransform));
        rewardBannerObj.transform.SetParent(cardObj.transform, false);
        RectTransform rewardBannerRect = rewardBannerObj.GetComponent<RectTransform>();
        rewardBannerRect.anchorMin = new Vector2(0f, 0f);
        rewardBannerRect.anchorMax = new Vector2(1f, 0f);
        rewardBannerRect.pivot = new Vector2(0.5f, 0f);
        rewardBannerRect.anchoredPosition = new Vector2(0f, 40f);
        rewardBannerRect.sizeDelta = new Vector2(-12f, 32f);
        rewardBannerRect.localScale = Vector3.one;

        Image rewardBg = rewardBannerObj.AddComponent<Image>();
        rewardBg.color = new Color(0f, 0f, 0f, 0.5f);

        GameObject rewardTextObj = new GameObject("RewardText", typeof(RectTransform));
        rewardTextObj.transform.SetParent(rewardBannerObj.transform, false);
        RectTransform rewardTextRect = rewardTextObj.GetComponent<RectTransform>();
        rewardTextRect.anchorMin = Vector2.zero;
        rewardTextRect.anchorMax = Vector2.one;
        rewardTextRect.anchoredPosition = Vector2.zero;
        rewardTextRect.sizeDelta = Vector2.zero;
        rewardTextRect.localScale = Vector3.one;

        TextMeshProUGUI rewardText = rewardTextObj.AddComponent<TextMeshProUGUI>();
        if (font != null) rewardText.font = font;
        rewardText.text = "🎁 보상 안내";
        rewardText.fontSize = 9.5f;
        rewardText.alignment = TextAlignmentOptions.Center;
        rewardText.textWrappingMode = TextWrappingModes.Normal;

        // SelectBtn
        GameObject selectBtnObj = new GameObject("SelectBtn", typeof(RectTransform));
        selectBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform selectBtnRect = selectBtnObj.GetComponent<RectTransform>();
        selectBtnRect.anchorMin = new Vector2(0f, 0f);
        selectBtnRect.anchorMax = new Vector2(1f, 0f);
        selectBtnRect.pivot = new Vector2(0.5f, 0f);
        selectBtnRect.anchoredPosition = new Vector2(0f, 8f);
        selectBtnRect.sizeDelta = new Vector2(-24f, 26f);
        selectBtnRect.localScale = Vector3.one;

        Image selectBtnBg = selectBtnObj.AddComponent<Image>();
        selectBtnBg.color = new Color(0.3f, 0.6f, 0.9f, 1f);

        GameObject selectBtnTextObj = new GameObject("Text", typeof(RectTransform));
        selectBtnTextObj.transform.SetParent(selectBtnObj.transform, false);
        RectTransform selectBtnTextRect = selectBtnTextObj.GetComponent<RectTransform>();
        selectBtnTextRect.anchorMin = Vector2.zero;
        selectBtnTextRect.anchorMax = Vector2.one;
        selectBtnTextRect.anchoredPosition = Vector2.zero;
        selectBtnTextRect.sizeDelta = Vector2.zero;
        selectBtnTextRect.localScale = Vector3.one;

        TextMeshProUGUI selectBtnText = selectBtnTextObj.AddComponent<TextMeshProUGUI>();
        if (font != null) selectBtnText.font = font;
        selectBtnText.text = "선택";
        selectBtnText.fontSize = 12f;
        selectBtnText.fontStyle = FontStyles.Bold;
        selectBtnText.alignment = TextAlignmentOptions.Center;
        selectBtnText.color = Color.white;

        // Component binding
        StageChoiceCard choiceCard = cardObj.AddComponent<StageChoiceCard>();
        choiceCard.bgImage = bgImage;
        choiceCard.outline = outline;
        choiceCard.headerBg = headerBg;
        choiceCard.titleText = titleText;
        choiceCard.descText = descText;
        choiceCard.rewardBanner = rewardBannerObj;
        choiceCard.rewardText = rewardText;
        choiceCard.selectButton = btn;
        choiceCard.selectButtonBg = selectBtnBg;
        choiceCard.selectButtonText = selectBtnText;

        PrefabUtility.SaveAsPrefabAsset(cardObj, ChoiceCardPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(cardObj, ChoiceCardResourcesPath);
        Object.DestroyImmediate(cardObj);

        Debug.Log($"[StageSelectPrefabGenerator] Successfully saved StageChoiceCard at {ChoiceCardResourcesPath}");
    }

    [MenuItem("Tools/UI/Generate Stage Select Panel Prefab")]
    public static void GenerateSelectPanelPrefab()
    {
        EnsureDirectories();

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF.asset");
        if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Pretendard-Medium SDF.asset");

        GameObject rootPanel = new GameObject("StageSelectPanel", typeof(RectTransform));
        RectTransform panelRect = rootPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Image blocker = rootPanel.AddComponent<Image>();
        blocker.color = Color.clear;

        CanvasGroup cg = rootPanel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        // Header Title
        GameObject titleHeaderObj = new GameObject("HeaderTitle", typeof(RectTransform));
        titleHeaderObj.transform.SetParent(rootPanel.transform, false);
        RectTransform headerTitleRect = titleHeaderObj.GetComponent<RectTransform>();
        headerTitleRect.anchorMin = new Vector2(0.5f, 1f);
        headerTitleRect.anchorMax = new Vector2(0.5f, 1f);
        headerTitleRect.pivot = new Vector2(0.5f, 1f);
        headerTitleRect.anchoredPosition = new Vector2(0f, -22f);
        headerTitleRect.sizeDelta = new Vector2(400f, 30f);
        headerTitleRect.localScale = Vector3.one;

        TextMeshProUGUI headerTitleText = titleHeaderObj.AddComponent<TextMeshProUGUI>();
        if (font != null) headerTitleText.font = font;
        headerTitleText.text = "NEXT STAGE";
        headerTitleText.fontSize = 18f;
        headerTitleText.fontStyle = FontStyles.Bold;
        headerTitleText.alignment = TextAlignmentOptions.Center;
        headerTitleText.color = new Color(1f, 0.85f, 0.3f, 1f);

        // Header Subtitle
        GameObject subtitleObj = new GameObject("HeaderSubtitle", typeof(RectTransform));
        subtitleObj.transform.SetParent(rootPanel.transform, false);
        RectTransform subtitleRect = subtitleObj.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 1f);
        subtitleRect.anchorMax = new Vector2(0.5f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -50f);
        subtitleRect.sizeDelta = new Vector2(400f, 20f);
        subtitleRect.localScale = Vector3.one;

        TextMeshProUGUI headerSubtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
        if (font != null) headerSubtitleText.font = font;
        headerSubtitleText.text = "다음 행선지를 선택하세요";
        headerSubtitleText.fontSize = 12f;
        headerSubtitleText.alignment = TextAlignmentOptions.Center;
        headerSubtitleText.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);

        // Card Container
        GameObject containerObj = new GameObject("CardContainer", typeof(RectTransform));
        containerObj.transform.SetParent(rootPanel.transform, false);
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0f, -10f);
        containerRect.sizeDelta = new Vector2(560f, 240f);
        containerRect.localScale = Vector3.one;

        // Component binding
        StageSelectPanel selectPanel = rootPanel.AddComponent<StageSelectPanel>();
        selectPanel.canvasGroup = cg;
        selectPanel.headerTitleText = headerTitleText;
        selectPanel.headerSubtitleText = headerSubtitleText;
        selectPanel.cardContainer = containerRect;

        rootPanel.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(rootPanel, SelectPanelPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(rootPanel, SelectPanelResourcesPath);
        Object.DestroyImmediate(rootPanel);

        Debug.Log($"[StageSelectPrefabGenerator] Successfully saved StageSelectPanel at {SelectPanelResourcesPath}");
    }
}
#endif

