using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 보물(Treasure) 노드에서 보물 상자를 열고 보상을 수령하는 UI 및 매니저입니다.
/// </summary>
public class TreasureManager : MonoBehaviour
{
    public static TreasureManager Instance { get; private set; }

    private GameObject rootPanel;
    private RectTransform panelRect;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI descText;
    private Button openChestButton;
    private TextMeshProUGUI openChestButtonText;
    private Button completeButton;
    private TextMeshProUGUI completeButtonText;
    private GameObject chestIconObj;

    private bool isOpened = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Open()
    {
        EnsureUI();

        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
            rootPanel.transform.SetAsLastSibling();

            CanvasGroup cg = rootPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.DOFade(1f, 0.25f).SetUpdate(true);
            }
        }

        isOpened = false;

        if (titleText != null) titleText.text = "🎁 신비한 보물 상자";
        if (descText != null) descText.text = "상자를 열어 숨겨진 특별 보상을 획득하세요!";
        if (openChestButton != null)
        {
            openChestButton.gameObject.SetActive(true);
            openChestButton.interactable = true;
        }
        if (completeButton != null) completeButton.gameObject.SetActive(false);

        if (chestIconObj != null)
        {
            chestIconObj.transform.localScale = Vector3.one;
            chestIconObj.transform.DOKill();
            chestIconObj.transform.DOPunchScale(Vector3.one * 0.1f, 1.2f, 2, 0.5f).SetLoops(-1, LoopType.Restart).SetUpdate(true);
        }
    }

    public void Close()
    {
        if (chestIconObj != null) chestIconObj.transform.DOKill();

        if (rootPanel != null && rootPanel.activeSelf)
        {
            CanvasGroup cg = rootPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
                {
                    rootPanel.SetActive(false);
                });
            }
            else
            {
                rootPanel.SetActive(false);
            }
        }
    }

    private void OnOpenChestClicked()
    {
        if (isOpened) return;
        isOpened = true;

        if (openChestButton != null) openChestButton.interactable = false;

        if (chestIconObj != null)
        {
            chestIconObj.transform.DOKill();
            chestIconObj.transform.localScale = Vector3.one;
            chestIconObj.transform.DOShakePosition(0.4f, 8f, 20, 90, false, true).SetUpdate(true)
                .OnComplete(() =>
                {
                    chestIconObj.transform.DOScale(1.25f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true)
                        .OnComplete(GrantReward);
                });
        }
        else
        {
            GrantReward();
        }
    }

    private void GrantReward()
    {
        float roll = UnityEngine.Random.value;
        string resultMessage = "";

        if (roll < 0.30f)
        {
            // 30% 고티어 유물 (전설 35%, 영웅 45%, 희귀 20%)
            ArtifactData artifact = null;
            if (ArtifactManager.Instance != null)
            {
                artifact = ArtifactManager.Instance.GetHighTierArtifactForTreasure();
                if (artifact != null)
                {
                    ArtifactManager.Instance.AddArtifact(artifact);
                    string rarityTitle = artifact.rarity == ArtifactRarity.Legendary 
                        ? "<color=#FFD700>【전설 유물 획득!】</color>" 
                        : (artifact.rarity == ArtifactRarity.Epic ? "<color=#DA70D6>【영웅 유물 획득!】</color>" : "<color=#00FFFF>【희귀 유물 획득!】</color>");
                    resultMessage = $"{rarityTitle}\n<b>{artifact.artifactName}</b>\n<size=10>{artifact.description}</size>";
                }
            }

            if (artifact == null)
            {
                int fallbackGold = 300;
                GameManager.Instance?.AddCoin(fallbackGold);
                resultMessage = $"<color=#FFD700>【대량 골드 획득!】</color>\n<b>+{fallbackGold} Gold</b>";
            }
        }
        else if (roll < 0.60f)
        {
            // 30% 고티어 인장 (전설 35%, 영웅 45%, 희귀 20%)
            SealData pickedSeal = GetHighTierSealForTreasure();
            if (pickedSeal != null)
            {
                if (SealInventory.Instance != null)
                {
                    SealInventory.Instance.AddSeal(pickedSeal);
                    FindFirstObjectByType<SealInventoryPanel>(FindObjectsInactive.Include)?.RefreshSealItems();
                }

                string rarityTitle = pickedSeal.rarity == SealRarity.Legendary
                    ? "<color=#FFD700>【전설 인장 획득!】</color>"
                    : (pickedSeal.rarity == SealRarity.Epic ? "<color=#DA70D6>【영웅 인장 획득!】</color>" : "<color=#00FFFF>【희귀 인장 획득!】</color>");
                resultMessage = $"{rarityTitle}\n<b>{pickedSeal.sealName}</b>\n<size=10>{pickedSeal.description}</size>";
            }
            else
            {
                int fallbackGold = 240;
                GameManager.Instance?.AddCoin(fallbackGold);
                resultMessage = $"<color=#FFD700>【골드 획득!】</color>\n<b>+{fallbackGold} Gold</b>";
            }
        }
        else if (roll < 0.85f)
        {
            // 25% 고티어 기물 (아마존(전설) 30%, 퀸 20%, 룩 20%, 챈슬러/아치비숍/켄타우로스 각 10%)
            PieceType highTierPiece = GetHighTierPieceForTreasure();
            GrantPieceToPlayer(highTierPiece);

            if (highTierPiece == PieceType.Amazon)
            {
                resultMessage = "<color=#FFD700>【전설 기물 획득!】</color>\n<b>아마존 (Amazon)</b>\n<size=10>대기석으로 이동했습니다. (퀸 + 나이트 행마)</size>";
            }
            else
            {
                string pieceName = PieceController.GetPieceNameForType(highTierPiece);
                resultMessage = $"<color=#00FFFF>【고급 기물 획득!】</color>\n<b>{pieceName}</b>\n<size=10>대기석으로 이동했습니다.</size>";
            }
        }
        else
        {
            // 15% 대량의 황금 잭팟 (300~600G)
            int gold = UnityEngine.Random.Range(30, 61) * 10;
            GameManager.Instance?.AddCoin(gold);
            resultMessage = $"<color=#FFD700>【대량의 황금 잭팟!】</color>\n<b>+{gold} Gold</b>";
        }

        if (descText != null)
        {
            descText.text = resultMessage;
        }

        if (openChestButton != null) openChestButton.gameObject.SetActive(false);
        if (completeButton != null) completeButton.gameObject.SetActive(true);
    }

    private SealData GetHighTierSealForTreasure()
    {
        if (ShotgunKingSystem.IsActive) return ShotgunKingSystem.GetRandomWeaponPart();

        List<SealData> allSeals = null;
        if (ShopManager.Instance != null && ShopManager.Instance.allSeals != null && ShopManager.Instance.allSeals.Count > 0)
        {
            allSeals = ShopManager.Instance.allSeals;
        }
        else
        {
            SealData[] loaded = Resources.LoadAll<SealData>("Seal");
            if (loaded != null && loaded.Length > 0)
            {
                allSeals = new List<SealData>(loaded);
            }
        }

        if (allSeals == null || allSeals.Count == 0) return null;

        // 전설 35%, 영웅 45%, 희귀 20%
        float r = UnityEngine.Random.Range(0f, 100f);
        SealRarity targetRarity;
        if (r < 35f) targetRarity = SealRarity.Legendary;
        else if (r < 80f) targetRarity = SealRarity.Epic;
        else targetRarity = SealRarity.Rare;

        var pool = allSeals.Where(s => s != null && s.rarity == targetRarity).ToList();
        if (pool.Count > 0)
        {
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        // 해당 등급이 없으면 높은 순으로 폴백
        var fallbackOrder = new[] { SealRarity.Legendary, SealRarity.Epic, SealRarity.Rare, SealRarity.Common };
        foreach (var fbRarity in fallbackOrder)
        {
            var fallbackPool = allSeals.Where(s => s != null && s.rarity == fbRarity).ToList();
            if (fallbackPool.Count > 0)
            {
                return fallbackPool[UnityEngine.Random.Range(0, fallbackPool.Count)];
            }
        }

        return allSeals[UnityEngine.Random.Range(0, allSeals.Count)];
    }

    private PieceType GetHighTierPieceForTreasure()
    {
        // 전설(Amazon) 30, Queen 20, Rook 20, 특수 에픽 기물(Chancellor, Archbishop, Centaur) 각 10
        var candidates = new (PieceType piece, int weight)[]
        {
            (PieceType.Amazon, 30),
            (PieceType.Queen, 20),
            (PieceType.Rook, 20),
            (PieceType.Chancellor, 10),
            (PieceType.Archbishop, 10),
            (PieceType.Centaur, 10)
        };

        int totalWeight = 0;
        foreach (var c in candidates) totalWeight += c.weight;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int current = 0;
        foreach (var c in candidates)
        {
            current += c.weight;
            if (roll < current)
            {
                return c.piece;
            }
        }

        return PieceType.Amazon;
    }

    private void GrantPieceToPlayer(PieceType pieceType)
    {
        InventorySlot emptySlot = FindEmptyInventorySlot();
        if (emptySlot != null && PieceSpawner.Instance != null)
        {
            emptySlot.IsReserved = true;
            Vector3 startPos = chestIconObj != null ? chestIconObj.transform.position : transform.position;
            PieceSpawner.Instance.SpawnPieceAndFlyToInventory(pieceType, startPos, emptySlot, null, (p) =>
            {
                emptySlot.IsReserved = false;
            });
        }
        else if (PieceInventory.Instance != null)
        {
            PieceInventory.Instance.AddPiece(pieceType);
        }

        CollectionManager.EnsureInstance()?.RecordPiece(pieceType);
    }

    private InventorySlot FindEmptyInventorySlot()
    {
        var slots = FindObjectsByType<InventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sortedSlots = slots.OrderBy(s => s.transform.GetSiblingIndex()).ToArray();
        return sortedSlots.FirstOrDefault(slot => slot.GetComponentInChildren<PieceController>() == null && !slot.IsReserved);
    }

    private void OnCompleteClicked()
    {
        Close();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnNonBattleStageCompleted();
        }
    }

    private void EnsureUI()
    {
        if (rootPanel != null) return;

        Canvas rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas == null)
        {
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj != null)
            {
                rootCanvas = canvasObj.GetComponent<Canvas>();
            }
        }

        if (rootCanvas == null) return;

        TMP_FontAsset font = null;
        if (GameManager.Instance != null && GameManager.Instance.coinText != null)
        {
            font = GameManager.Instance.coinText.font;
        }
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
        }

        rootPanel = new GameObject("TreasurePanel");
        rootPanel.transform.SetParent(rootCanvas.transform, false);

        panelRect = rootPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = Vector2.zero;

        Image blocker = rootPanel.AddComponent<Image>();
        blocker.color = Color.clear;

        CanvasGroup cg = rootPanel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        // 중앙 모달 박스 (320 x 210)
        GameObject modalBox = new GameObject("ModalBox");
        modalBox.transform.SetParent(rootPanel.transform, false);
        RectTransform modalRect = modalBox.AddComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.pivot = new Vector2(0.5f, 0.5f);
        modalRect.sizeDelta = new Vector2(330f, 220f);
        modalRect.anchoredPosition = Vector2.zero;

        Image modalBg = modalBox.AddComponent<Image>();
        modalBg.color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        Outline outline = modalBox.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.85f, 0.55f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        // 타이틀
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(modalBox.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);
        titleRect.sizeDelta = new Vector2(0f, 28f);

        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.font = font;
        titleText.text = "🎁 신비한 보물 상자";
        titleText.fontSize = 16f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.5f, 1f, 0.7f, 1f);

        // 상자 아이콘 텍스트 (이모지)
        chestIconObj = new GameObject("ChestIcon");
        chestIconObj.transform.SetParent(modalBox.transform, false);
        RectTransform iconRect = chestIconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 38f);
        iconRect.sizeDelta = new Vector2(60f, 60f);

        TextMeshProUGUI iconText = chestIconObj.AddComponent<TextMeshProUGUI>();
        iconText.font = font;
        iconText.text = "🎁";
        iconText.fontSize = 42f;
        iconText.alignment = TextAlignmentOptions.Center;

        // 설명 및 결과 텍스트
        GameObject descObj = new GameObject("Desc");
        descObj.transform.SetParent(modalBox.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0.5f, 0.5f);
        descRect.anchoredPosition = new Vector2(0f, -18f);
        descRect.sizeDelta = new Vector2(-30f, -110f);

        descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.font = font;
        descText.text = "상자를 열어 숨겨진 특별 보상을 획득하세요!";
        descText.fontSize = 11.5f;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = Color.white;
        descText.textWrappingMode = TextWrappingModes.Normal;

        // "상자 열기" 버튼
        GameObject openBtnObj = new GameObject("OpenChestButton");
        openBtnObj.transform.SetParent(modalBox.transform, false);
        RectTransform openBtnRect = openBtnObj.AddComponent<RectTransform>();
        openBtnRect.anchorMin = new Vector2(0.5f, 0f);
        openBtnRect.anchorMax = new Vector2(0.5f, 0f);
        openBtnRect.pivot = new Vector2(0.5f, 0f);
        openBtnRect.anchoredPosition = new Vector2(0f, 14f);
        openBtnRect.sizeDelta = new Vector2(120f, 30f);

        Image openBtnBg = openBtnObj.AddComponent<Image>();
        openBtnBg.color = new Color(0.2f, 0.75f, 0.45f, 1f);

        openChestButton = openBtnObj.AddComponent<Button>();
        openChestButton.onClick.AddListener(OnOpenChestClicked);

        GameObject openTextObj = new GameObject("Text");
        openTextObj.transform.SetParent(openBtnObj.transform, false);
        RectTransform openTextRect = openTextObj.AddComponent<RectTransform>();
        openTextRect.anchorMin = Vector2.zero;
        openTextRect.anchorMax = Vector2.one;
        openTextRect.anchoredPosition = Vector2.zero;
        openTextRect.sizeDelta = Vector2.zero;

        openChestButtonText = openTextObj.AddComponent<TextMeshProUGUI>();
        openChestButtonText.font = font;
        openChestButtonText.text = "상자 열기";
        openChestButtonText.fontSize = 12f;
        openChestButtonText.fontStyle = FontStyles.Bold;
        openChestButtonText.alignment = TextAlignmentOptions.Center;
        openChestButtonText.color = Color.white;

        // "확인 / 다음으로" 버튼
        GameObject compBtnObj = new GameObject("CompleteButton");
        compBtnObj.transform.SetParent(modalBox.transform, false);
        RectTransform compBtnRect = compBtnObj.AddComponent<RectTransform>();
        compBtnRect.anchorMin = new Vector2(0.5f, 0f);
        compBtnRect.anchorMax = new Vector2(0.5f, 0f);
        compBtnRect.pivot = new Vector2(0.5f, 0f);
        compBtnRect.anchoredPosition = new Vector2(0f, 14f);
        compBtnRect.sizeDelta = new Vector2(120f, 30f);

        Image compBtnBg = compBtnObj.AddComponent<Image>();
        compBtnBg.color = new Color(0.25f, 0.55f, 0.95f, 1f);

        completeButton = compBtnObj.AddComponent<Button>();
        completeButton.onClick.AddListener(OnCompleteClicked);

        GameObject compTextObj = new GameObject("Text");
        compTextObj.transform.SetParent(compBtnObj.transform, false);
        RectTransform compTextRect = compTextObj.AddComponent<RectTransform>();
        compTextRect.anchorMin = Vector2.zero;
        compTextRect.anchorMax = Vector2.one;
        compTextRect.anchoredPosition = Vector2.zero;
        compTextRect.sizeDelta = Vector2.zero;

        completeButtonText = compTextObj.AddComponent<TextMeshProUGUI>();
        completeButtonText.font = font;
        completeButtonText.text = "다음으로";
        completeButtonText.fontSize = 12f;
        completeButtonText.fontStyle = FontStyles.Bold;
        completeButtonText.alignment = TextAlignmentOptions.Center;
        completeButtonText.color = Color.white;

        compBtnObj.SetActive(false);
        rootPanel.SetActive(false);
    }
}
