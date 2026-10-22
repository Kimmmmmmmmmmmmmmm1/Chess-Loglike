#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[InitializeOnLoad]
public static class TurnIndicatorSceneBuilder
{
    private const string PrefabDir = "Assets/Prefabs/UI";
    private const string ResourcesDir = "Assets/Resources/UI";
    private const string PrefabPath = "Assets/Prefabs/UI/TurnIndicatorUI.prefab";
    private const string ResourcesPrefabPath = "Assets/Resources/UI/TurnIndicatorUI.prefab";

    [InitializeOnLoadMethod]
    private static void OnInit()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return;
            }
            EnsurePrefabAndScene();
        };
    }

    [MenuItem("Tools/UI/Build Turn Indicator In Scene")]
    public static void EnsurePrefabAndScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            Debug.LogWarning("[TurnIndicatorSceneBuilder] Play mode 중에는 씬을 수정/저장할 수 없습니다.");
            return;
        }

        EnsureDirectories();
        GameObject prefab = GenerateOrUpdatePrefab();
        EnsureInActiveScene(prefab);
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(ResourcesDir)) AssetDatabase.CreateFolder("Assets/Resources", "UI");
    }

    public static GameObject GenerateOrUpdatePrefab()
    {
        GameObject root = CreateHierarchy();
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

        // Resources 폴더에도 복사 저장하여 런타임 Resources.Load 지원
        File.Copy(PrefabPath, ResourcesPrefabPath, true);
        AssetDatabase.ImportAsset(ResourcesPrefabPath, ImportAssetOptions.ForceUpdate);

        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefabAsset;
    }

    public static void EnsureInActiveScene(GameObject prefab)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        // Canvas 오브젝트 탐색
        Canvas canvas = FindTargetCanvas(activeScene);
        if (canvas == null)
        {
            Debug.LogWarning("[TurnIndicatorSceneBuilder] Target Canvas not found in scene: " + activeScene.name);
            return;
        }

        Transform existing = canvas.transform.Find("TurnIndicatorUI");
        if (existing != null)
        {
            // 이미 씬에 존재하면 유효성 검증 후 반환
            TurnIndicatorUI existingComp = existing.GetComponent<TurnIndicatorUI>();
            if (existingComp != null)
            {
                ValidateComponentBindings(existingComp);
                return;
            }
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject sceneInstance;
        if (prefab != null)
        {
            sceneInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        }
        else
        {
            sceneInstance = CreateHierarchy();
            sceneInstance.transform.SetParent(canvas.transform, false);
        }

        sceneInstance.name = "TurnIndicatorUI";
        sceneInstance.transform.SetAsLastSibling();

        RectTransform rt = sceneInstance.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -6f);
            rt.sizeDelta = new Vector2(210f, 48f);
            rt.localScale = Vector3.one;
        }

        TurnIndicatorUI uiComp = sceneInstance.GetComponent<TurnIndicatorUI>();
        if (uiComp != null)
        {
            ValidateComponentBindings(uiComp);
        }

        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[TurnIndicatorSceneBuilder] TurnIndicatorUI가 성공적으로 '{activeScene.name}' 씬의 Canvas에 배치 및 저장되었습니다.");
        }
    }

    private static Canvas FindTargetCanvas(Scene scene)
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.gameObject.scene != scene) continue;
            if (c.gameObject.name == "Canvas") return c;
        }

        foreach (Canvas c in canvases)
        {
            if (c.gameObject.scene != scene) continue;
            string n = c.gameObject.name;
            if (n.Contains("Setting") || n.Contains("Modal") || n.Contains("Tooltip")) continue;
            return c;
        }

        return null;
    }

    private static GameObject CreateHierarchy()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Pretendard-Medium SDF.asset");
        if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF.asset");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        Sprite boxSprite = TurnIndicatorUI.GetOrCreateBoxSprite();

        // 루트
        GameObject root = new GameObject("TurnIndicatorUI", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -6f);
        rootRect.sizeDelta = new Vector2(210f, 26f);
        rootRect.localScale = Vector3.one;

        // 1. 남은 스테이지 뱃지
        GameObject badgeObj = new GameObject("RemainingStageBadge", typeof(RectTransform));
        badgeObj.SetActive(false);
        badgeObj.transform.SetParent(root.transform, false);
        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 1f);
        badgeRect.anchorMax = new Vector2(0.5f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, 0f);
        badgeRect.sizeDelta = new Vector2(145f, 16f);
        badgeRect.localScale = Vector3.one;

        Image badgeBorder = badgeObj.AddComponent<Image>();
        badgeBorder.sprite = boxSprite;
        badgeBorder.type = Image.Type.Simple;
        badgeBorder.color = new Color(0.40f, 0.40f, 0.40f, 0.9f);
        badgeBorder.raycastTarget = false;

        GameObject badgeFace = new GameObject("Face", typeof(RectTransform));
        badgeFace.transform.SetParent(badgeObj.transform, false);
        RectTransform faceRect = badgeFace.GetComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.offsetMin = new Vector2(1.5f, 1.5f);
        faceRect.offsetMax = new Vector2(-1.5f, -1.5f);
        faceRect.localScale = Vector3.one;

        Image badgeFaceBg = badgeFace.AddComponent<Image>();
        badgeFaceBg.sprite = boxSprite;
        badgeFaceBg.type = Image.Type.Simple;
        badgeFaceBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        badgeFaceBg.raycastTarget = false;

        GameObject textObj = new GameObject("RemainingStagesBadgeText", typeof(RectTransform));
        textObj.transform.SetParent(badgeFace.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.localScale = Vector3.one;

        TextMeshProUGUI badgeTmp = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) badgeTmp.font = font;
        badgeTmp.text = "남은 턴: 15";
        badgeTmp.fontSize = 11f;
        badgeTmp.fontStyle = FontStyles.Bold;
        badgeTmp.alignment = TextAlignmentOptions.Center;
        badgeTmp.color = new Color(0.95f, 0.90f, 0.78f, 1f);
        badgeTmp.textWrappingMode = TextWrappingModes.NoWrap;
        badgeTmp.overflowMode = TextOverflowModes.Overflow;
        badgeTmp.raycastTarget = false;

        // 2. 턴 박스 컨테이너
        GameObject containerObj = new GameObject("BoxesContainer", typeof(RectTransform));
        containerObj.transform.SetParent(root.transform, false);
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 1f);
        containerRect.anchorMax = new Vector2(0.5f, 1f);
        containerRect.pivot = new Vector2(0.5f, 1f);
        containerRect.anchoredPosition = new Vector2(0f, 0f);
        containerRect.sizeDelta = new Vector2(88f, 26f);
        containerRect.localScale = Vector3.one;

        HorizontalLayoutGroup hlg = containerObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Player Box (흑: 내 기물)
        TurnIndicatorBox pBox = CreateClockBox(
            containerObj.transform,
            "PlayerBox",
            "흑",
            "내 턴",
            boxSprite,
            font,
            new Color(0.88f, 0.88f, 0.88f, 1f),
            new Color(0.25f, 0.25f, 0.25f, 0.8f),
            new Color(0.08f, 0.08f, 0.08f, 1f),
            new Color(0.12f, 0.12f, 0.12f, 0.8f),
            new Color(1f, 1f, 1f, 1f),
            new Color(0.45f, 0.45f, 0.45f, 1f)
        );

        // Opponent Box (백: 상대 기물)
        TurnIndicatorBox oBox = CreateClockBox(
            containerObj.transform,
            "OpponentBox",
            "백",
            "대기중",
            boxSprite,
            font,
            new Color(0.28f, 0.18f, 0.10f, 1f),
            new Color(0.20f, 0.14f, 0.10f, 0.8f),
            new Color(0.94f, 0.85f, 0.71f, 1f),
            new Color(0.48f, 0.43f, 0.36f, 0.8f),
            new Color(0.10f, 0.06f, 0.03f, 1f),
            new Color(0.28f, 0.22f, 0.18f, 1f)
        );

        TurnIndicatorUI uiComp = root.AddComponent<TurnIndicatorUI>();
        SerializedObject so = new SerializedObject(uiComp);
        so.FindProperty("rootContainer").objectReferenceValue = rootRect;
        SerializedProperty turnsProp = so.FindProperty("remainingTurnsText") ?? so.FindProperty("remainingStagesText");
        if (turnsProp != null) turnsProp.objectReferenceValue = badgeTmp;
        so.FindProperty("playerBox").objectReferenceValue = pBox;
        so.FindProperty("opponentBox").objectReferenceValue = oBox;
        so.FindProperty("hideOutsideBattle").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        TurnIndicatorBox[] boxes = uiComp.GetComponentsInChildren<TurnIndicatorBox>(true);
        foreach (var b in boxes)
        {
            if (b != null) b.ApplySingleLabelLayout();
        }

        return root;
    }

    private static TurnIndicatorBox CreateClockBox(
        Transform parent,
        string name,
        string label,
        string status,
        Sprite boxSprite,
        TMP_FontAsset font,
        Color activeBorder,
        Color inactiveBorder,
        Color activeBg,
        Color inactiveBg,
        Color activeText,
        Color inactiveText)
    {
        GameObject boxObj = new GameObject(name, typeof(RectTransform));
        boxObj.transform.SetParent(parent, false);
        RectTransform boxRect = boxObj.GetComponent<RectTransform>();
        boxRect.sizeDelta = new Vector2(40f, 26f);
        boxRect.localScale = Vector3.one;

        LayoutElement le = boxObj.AddComponent<LayoutElement>();
        le.preferredWidth = 40f;
        le.preferredHeight = 26f;
        le.minWidth = 40f;
        le.minHeight = 26f;
        le.flexibleWidth = 0;
        le.flexibleHeight = 0;

        // Shadow
        GameObject shadowObj = new GameObject("Shadow", typeof(RectTransform));
        shadowObj.transform.SetParent(boxObj.transform, false);
        RectTransform shadowRect = shadowObj.GetComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.anchoredPosition = new Vector2(1f, -1f);
        shadowRect.sizeDelta = Vector2.zero;
        shadowRect.localScale = Vector3.one;

        Image shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = boxSprite;
        shadowImg.type = Image.Type.Simple;
        shadowImg.color = new Color(0f, 0f, 0f, 0.5f);
        shadowImg.raycastTarget = false;

        // Border
        Image borderImg = boxObj.AddComponent<Image>();
        borderImg.sprite = boxSprite;
        borderImg.type = Image.Type.Simple;
        borderImg.color = activeBorder;
        borderImg.raycastTarget = false;

        // Face
        GameObject faceObj = new GameObject("Face", typeof(RectTransform));
        faceObj.transform.SetParent(boxObj.transform, false);
        RectTransform faceRect = faceObj.GetComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.offsetMin = new Vector2(1.5f, 1.5f);
        faceRect.offsetMax = new Vector2(-1.5f, -1.5f);
        faceRect.localScale = Vector3.one;

        Image bgImg = faceObj.AddComponent<Image>();
        bgImg.sprite = boxSprite;
        bgImg.type = Image.Type.Simple;
        bgImg.color = activeBg;
        bgImg.raycastTarget = false;

        // Content
        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(faceObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentRect.localScale = Vector3.one;

        // Label ("흑" or "백" 단독 중앙 배치)
        GameObject labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(contentObj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelRect.localScale = Vector3.one;

        TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        if (font != null) labelTmp.font = font;
        labelTmp.text = label;
        labelTmp.fontSize = 13.5f;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = activeText;
        labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
        labelTmp.raycastTarget = false;

        // Divider & Status는 비활성화하여 단독 레이아웃 유지
        GameObject divObj = new GameObject("Divider", typeof(RectTransform));
        divObj.transform.SetParent(contentObj.transform, false);
        divObj.SetActive(false);

        GameObject statusObj = new GameObject("Status", typeof(RectTransform));
        statusObj.transform.SetParent(contentObj.transform, false);
        statusObj.SetActive(false);
        TextMeshProUGUI statusTmp = statusObj.AddComponent<TextMeshProUGUI>();
        statusTmp.alignment = TextAlignmentOptions.Center;
        statusTmp.color = activeText;
        statusTmp.textWrappingMode = TextWrappingModes.NoWrap;
        statusTmp.overflowMode = TextOverflowModes.Overflow;
        statusTmp.raycastTarget = false;

        TurnIndicatorBox boxComp = boxObj.AddComponent<TurnIndicatorBox>();
        SerializedObject boxSo = new SerializedObject(boxComp);
        boxSo.FindProperty("boxRect").objectReferenceValue = boxRect;
        boxSo.FindProperty("borderImage").objectReferenceValue = borderImg;
        boxSo.FindProperty("bgImage").objectReferenceValue = bgImg;
        boxSo.FindProperty("shadowImage").objectReferenceValue = shadowImg;
        boxSo.FindProperty("labelText").objectReferenceValue = labelTmp;
        boxSo.FindProperty("statusText").objectReferenceValue = statusTmp;
        boxSo.FindProperty("activeBorderColor").colorValue = activeBorder;
        boxSo.FindProperty("inactiveBorderColor").colorValue = inactiveBorder;
        boxSo.FindProperty("activeBgColor").colorValue = activeBg;
        boxSo.FindProperty("inactiveBgColor").colorValue = inactiveBg;
        boxSo.FindProperty("activeTextColor").colorValue = activeText;
        boxSo.FindProperty("inactiveTextColor").colorValue = inactiveText;
        boxSo.ApplyModifiedPropertiesWithoutUndo();

        return boxComp;
    }

    private static void ValidateComponentBindings(TurnIndicatorUI uiComp)
    {
        SerializedObject so = new SerializedObject(uiComp);
        if (so.FindProperty("rootContainer").objectReferenceValue == null)
        {
            so.FindProperty("rootContainer").objectReferenceValue = uiComp.GetComponent<RectTransform>();
        }

        SerializedProperty turnsProp = so.FindProperty("remainingTurnsText") ?? so.FindProperty("remainingStagesText");
        if (turnsProp != null && turnsProp.objectReferenceValue == null)
        {
            Transform t = uiComp.transform.Find("RemainingTurnsBadge");
            if (t == null) t = uiComp.transform.Find("RemainingStageBadge");
            if (t != null)
            {
                turnsProp.objectReferenceValue = t.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        if (so.FindProperty("playerBox").objectReferenceValue == null)
        {
            Transform b = uiComp.transform.Find("BoxesContainer/PlayerBox");
            if (b != null) so.FindProperty("playerBox").objectReferenceValue = b.GetComponent<TurnIndicatorBox>();
        }

        if (so.FindProperty("opponentBox").objectReferenceValue == null)
        {
            Transform b = uiComp.transform.Find("BoxesContainer/OpponentBox");
            if (b != null) so.FindProperty("opponentBox").objectReferenceValue = b.GetComponent<TurnIndicatorBox>();
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        TurnIndicatorBox[] boxes = uiComp.GetComponentsInChildren<TurnIndicatorBox>(true);
        foreach (var b in boxes)
        {
            if (b != null) b.ApplySingleLabelLayout();
        }
    }
}
#endif

