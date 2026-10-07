using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject eventPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Image eventImage;
    [SerializeField] private Transform choiceButtonContainer;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("Data")]
    [SerializeField] private TextAsset eventDatabaseFile;
    [SerializeField] private string defaultDatabaseResourcePath = "Events/event_database";
    [SerializeField] private bool useRandomEvent = true;
    [SerializeField] private string fixedEventId;

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.5f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InQuad;
    [SerializeField] private float eventOpenY = 0f;

    [Header("Reward Source")]
    [SerializeField] private ShopPieceData pieceData;
    [SerializeField] private List<ArtifactData> artifactCatalog = new List<ArtifactData>();
    [SerializeField] private List<SealData> availableSeals = new List<SealData>();

    private EventDatabaseData loadedDatabase;
    private EventData currentEvent;
    private Dictionary<string, EventNodeData> currentNodeLookup;
    private RectTransform eventPanelRect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (eventPanel != null)
        {
            eventPanel.SetActive(false);
        }
    }

    private void Start()
    {
        if (eventPanel != null)
        {
            eventPanelRect = eventPanel.GetComponent<RectTransform>();
        }

        // 자동으로 폴더에서 모든 인장/유물 데이터 로드
        if (availableSeals == null || availableSeals.Count == 0)
        {
            LoadAllSealsFromFolder();
        }
        if (artifactCatalog == null || artifactCatalog.Count == 0)
        {
            LoadAllArtifactsFromFolder();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged += OnFlowStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFlowStateChanged -= OnFlowStateChanged;
        }
    }

    private void OnFlowStateChanged(GameFlowState flowState)
    {
        if (flowState == GameFlowState.Event)
        {
            OpenEvent();
        }
        else if (eventPanel != null && eventPanel.activeSelf)
        {
            CloseEventUI();
        }
    }

    public void OpenEvent(string eventId = null)
    {
        if (!TryLoadDatabase())
        {
            ExitToMap();
            return;
        }

        currentEvent = SelectEvent(eventId);
        if (currentEvent == null)
        {
            ExitToMap();
            return;
        }

        currentNodeLookup = currentEvent.nodes?
            .Where(node => !string.IsNullOrEmpty(node.nodeId))
            .GroupBy(node => node.nodeId)
            .ToDictionary(group => group.Key, group => group.First());

        if (currentNodeLookup == null || currentNodeLookup.Count == 0)
        {
            ExitToMap();
            return;
        }

        if (eventPanel != null)
        {
            eventPanel.SetActive(true);
            //eventPanel.transform.SetAsLastSibling();

            if (eventPanelRect != null)
            {
                eventPanelRect.anchoredPosition = new Vector2(eventPanelRect.anchoredPosition.x, eventPanelRect.rect.height);
                eventPanelRect.DOAnchorPosY(eventOpenY, animDuration).SetEase(openEase);
            }
        }

        string startNodeId = string.IsNullOrEmpty(currentEvent.startNodeId)
            ? currentNodeLookup.Keys.First()
            : currentEvent.startNodeId;

        ShowNode(startNodeId);
    }

    private bool TryLoadDatabase()
    {
        if (loadedDatabase != null)
        {
            return true;
        }

        TextAsset dataFile = eventDatabaseFile;
        if (dataFile == null)
        {
            dataFile = Resources.Load<TextAsset>(defaultDatabaseResourcePath);
        }

        if (dataFile == null || string.IsNullOrEmpty(dataFile.text))
        {
            return false;
        }

        loadedDatabase = JsonUtility.FromJson<EventDatabaseData>(dataFile.text);
        return loadedDatabase != null && loadedDatabase.events != null && loadedDatabase.events.Count > 0;
    }

    private EventData SelectEvent(string eventId)
    {
        if (loadedDatabase == null || loadedDatabase.events == null || loadedDatabase.events.Count == 0)
        {
            return null;
        }

        string requestedId = eventId;

        if (string.IsNullOrEmpty(requestedId) && !string.IsNullOrEmpty(fixedEventId) && !useRandomEvent)
        {
            requestedId = fixedEventId;
        }

        if (!string.IsNullOrEmpty(requestedId))
        {
            return loadedDatabase.events.FirstOrDefault(e => e.eventId == requestedId);
        }

        return loadedDatabase.events[UnityEngine.Random.Range(0, loadedDatabase.events.Count)];
    }

    private void ShowNode(string nodeId)
    {
        if (currentNodeLookup == null || !currentNodeLookup.TryGetValue(nodeId, out EventNodeData node))
        {
            ExitToMap();
            return;
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrEmpty(currentEvent?.title) ? "이벤트" : currentEvent.title;
        }

        if (dialogueText != null)
        {
            dialogueText.text = node.dialogue;
        }

        UpdateImage(node);
        RebuildChoiceButtons(node.choices);
    }

    private void UpdateImage(EventNodeData node)
    {
        if (eventImage == null)
        {
            return;
        }

        string imagePath = !string.IsNullOrEmpty(node.imagePath) ? node.imagePath : currentEvent.defaultImagePath;

        if (string.IsNullOrEmpty(imagePath))
        {
            eventImage.sprite = null;
            eventImage.enabled = false;
            return;
        }

        Sprite sprite = Resources.Load<Sprite>(imagePath);
        eventImage.sprite = sprite;
        eventImage.enabled = sprite != null;
    }

    private void RebuildChoiceButtons(List<EventChoiceData> choices)
    {
        if (choiceButtonContainer == null)
        {
            return;
        }

        foreach (Transform child in choiceButtonContainer)
        {
            Destroy(child.gameObject);
        }

        List<EventChoiceData> validChoices = choices ?? new List<EventChoiceData>();
        if (validChoices.Count == 0)
        {
            CreateChoiceButton("떠난다", () => ExitToMap(), true);
            return;
        }

        foreach (EventChoiceData choice in validChoices)
        {
            EventChoiceData cachedChoice = choice;
            string buttonText = string.IsNullOrEmpty(cachedChoice.text) ? "계속" : cachedChoice.text;
            bool isEnabled = CanExecuteEffect(cachedChoice.effect);
            CreateChoiceButton(buttonText, () => OnChoiceSelected(cachedChoice), isEnabled);
        }
    }

    private void CreateChoiceButton(string label, Action callback, bool isEnabled = true)
    {
        if (choiceButtonPrefab == null || choiceButtonContainer == null)
        {
            return;
        }

        Button button = Instantiate(choiceButtonPrefab, choiceButtonContainer);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => callback?.Invoke());
        button.interactable = isEnabled;

        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buttonText != null)
        {
            buttonText.text = label;
        }
    }

    private void OnChoiceSelected(EventChoiceData choice)
    {
        ApplyChoiceEffect(choice.effect);

        if (choice.endEvent || string.IsNullOrEmpty(choice.nextNodeId))
        {
            ExitToMap();
            return;
        }

        ShowNode(choice.nextNodeId);
    }

    private void ApplyChoiceEffect(EventChoiceEffectData effect)
    {
        if (effect == null)
        {
            return;
        }

        if (!Enum.TryParse(effect.effectType, true, out EventEffectType effectType))
        {
            effectType = EventEffectType.None;
        }

        switch (effectType)
        {
            case EventEffectType.None:
                break;

            case EventEffectType.GainGold:
                if (GameManager.Instance != null && effect.goldAmount > 0)
                {
                    AnimateGoldFly(effect.goldAmount);
                }
                break;

            case EventEffectType.LoseGold:
                if (GameManager.Instance != null && effect.goldAmount > 0)
                {
                    GameManager.Instance.UseCoin(effect.goldAmount);
                }
                break;

            case EventEffectType.GainSpecificArtifact:
                GrantSpecificArtifact(effect.artifactId, true);
                break;

            case EventEffectType.GainRandomArtifact:
                GrantRandomArtifact(true);
                break;

            case EventEffectType.GainRandomSpecificArtifact:
                GrantRandomSpecificArtifact(effect.artifactIds, true);
                break;

            case EventEffectType.GainSpecificPiece:
                if (TryParsePieceType(effect.pieceType, out PieceType pieceType))
                {
                    GrantPiece(pieceType);
                }
                break;

            case EventEffectType.GainRandomPiece:
                GrantRandomPiece();
                break;

            case EventEffectType.GainRandomSpecificPiece:
                GrantRandomSpecificPiece(effect.pieceTypes);
                break;

            case EventEffectType.GainSeal:
                // 새로운 인장 획득 효과 처리
                SealData sealToGain = ResolveSeal(null, effect.sealName, effect.seal);
                if (sealToGain != null) GrantSeal(sealToGain);
                break;

            case EventEffectType.RemoveSpecificArtifact:
                RemoveSpecificArtifact(effect.removeArtifactId);
                break;

            case EventEffectType.RemoveRandomArtifact:
                RemoveRandomArtifact();
                break;

            case EventEffectType.RemoveSpecificPiece:
                if (TryParsePieceType(effect.removePieceType, out PieceType removePieceType))
                {
                    RemoveSpecificPiece(removePieceType);
                }
                break;

            case EventEffectType.RemoveRandomPiece:
                RemoveRandomPiece();
                break;
        }
    }

    private void GrantSpecificArtifact(string artifactId, bool animate = false)
    {
        ArtifactData artifact = ResolveArtifactById(artifactId);
        if (artifact != null && ArtifactManager.Instance != null)
        {
            if (animate)
            {
                AnimateArtifactFly(artifact);
            }
            else
            {
                ArtifactManager.Instance.AddArtifact(artifact);
            }
        }
    }

    private void GrantRandomArtifact(bool animate = false)
    {
        if (ArtifactManager.Instance == null)
        {
            return;
        }

        ArtifactData artifact = ArtifactManager.Instance.GetRandomArtifact();
        if (artifact != null)
        {
            if (animate)
            {
                AnimateArtifactFly(artifact);
            }
            else
            {
                ArtifactManager.Instance.AddArtifact(artifact);
            }
        }
    }

    private void GrantRandomSpecificArtifact(List<string> artifactIds, bool animate = false)
    {
        if (artifactIds == null || artifactIds.Count == 0)
        {
            return;
        }

        // 리스트에서 랜덤으로 하나 선택
        string randomId = artifactIds[UnityEngine.Random.Range(0, artifactIds.Count)];
        GrantSpecificArtifact(randomId, animate);
    }

    private ArtifactData ResolveArtifactById(string artifactId)
    {
        if (string.IsNullOrEmpty(artifactId))
        {
            return null;
        }

        ArtifactData artifact = artifactCatalog.FirstOrDefault(data => data != null && data.id == artifactId);
        if (artifact != null)
        {
            return artifact;
        }

        ArtifactData[] loadedArtifacts = Resources.LoadAll<ArtifactData>("");
        return loadedArtifacts.FirstOrDefault(data => data != null && data.id == artifactId);
    }

    private void GrantRandomPiece()
    {
        PieceType pieceType;

        if (pieceData != null)
        {
            var pieceInfo = pieceData.GetRandomPiece();
            pieceType = pieceInfo != null ? pieceInfo.pieceType : PieceType.Soldier;
        }
        else
        {
            Array values = Enum.GetValues(typeof(PieceType));
            pieceType = (PieceType)values.GetValue(UnityEngine.Random.Range(0, values.Length));
        }

        GrantPiece(pieceType);
    }

    private void GrantRandomSpecificPiece(List<string> pieceTypes)
    {
        if (pieceTypes == null || pieceTypes.Count == 0)
        {
            return;
        }

        // 리스트에서 랜덤으로 하나 선택
        string randomPieceTypeStr = pieceTypes[UnityEngine.Random.Range(0, pieceTypes.Count)];
        
        if (TryParsePieceType(randomPieceTypeStr, out PieceType pieceType))
        {
            GrantPiece(pieceType);
        }
    }

    private void GrantPiece(PieceType pieceType)
    {
        InventorySlot slot = FindEmptyInventorySlot();

        if (slot != null && PieceSpawner.Instance != null)
        {
            slot.IsReserved = true;
            // 인장 없이 기물만 생성
            PieceSpawner.Instance.SpawnPieceAndFlyToInventory(pieceType, transform.position, slot, null, (piece) =>
            {
                slot.IsReserved = false;
            });
            return;
        }

        // Fallback
        PieceInventory.Instance?.AddPiece(pieceType);
    }

    private void GrantSeal(SealData sealData)
    {
        if (sealData == null) return;

        // TODO: 인장 획득 연출 추가 (예: 아이콘이 인벤토리로 날아가는 효과)
        SealInventory.Instance?.AddSeal(sealData);
    }

    public SealData ResolveSeal(PieceType? pieceType, string sealName, int seal)
    {
        // seal: 0 = no seal, 1 = random chance, 2 = always give seal
        
        if (seal == 0)
        {
            return null; // 인장 안줌
        }

        List<SealData> source = availableSeals;
        if ((source == null || source.Count == 0) && ShopManager.Instance != null)
        {
            source = ShopManager.Instance.allSeals;
        }

        if (source == null || source.Count == 0)
        {
            return null;
        }

        // 특정 인장 이름이 지정된 경우
        if (!string.IsNullOrEmpty(sealName))
        {
            SealData specificSeal = source.FirstOrDefault(s => s != null &&
                string.Equals(s.sealName, sealName, StringComparison.OrdinalIgnoreCase));

            if (specificSeal != null)
            {
                return specificSeal;
            }
        }

        // seal == 1: 50% 확률로 인장 지급
        if (seal == 1)
        {
            if (UnityEngine.Random.Range(0, 2) == 0)
            {
                return null; // 50% 확률로 인장 안줌
            }
        }
        // seal == 2: 무조건 인장 지급

        // 호환되는 인장 중 랜덤 선택 (상점 판매 가능한 인장만)
        List<SealData> candidates = source
            .Where(s => s != null &&
                        s.isSoldInShop && // 이벤트에서도 상점용 인장만 지급
                        (pieceType == null || s.compatiblePieces == null || s.compatiblePieces.Count == 0 || s.compatiblePieces.Contains(pieceType.Value)))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private bool CanExecuteEffect(EventChoiceEffectData effect)
    {
        if (effect == null)
        {
            return true;
        }

        EventEffectType effectType = ParseEffectType(effect.effectType);

        switch (effectType)
        {
            case EventEffectType.LoseGold:
                if (GameManager.Instance == null || effect.goldAmount <= 0)
                {
                    return false;
                }
                // Check if player has enough gold
                return GameManager.Instance.Coin >= effect.goldAmount;

            case EventEffectType.RemoveSpecificArtifact:
                if (string.IsNullOrEmpty(effect.removeArtifactId) || ArtifactManager.Instance == null)
                {
                    return false;
                }

                // Check if the artifact is owned
                ArtifactData owned = ArtifactManager.Instance.OwnedArtifacts
                    .FirstOrDefault(artifact => artifact != null && artifact.id == effect.removeArtifactId);

                return owned != null;

            case EventEffectType.RemoveRandomArtifact:
                if (ArtifactManager.Instance == null || ArtifactManager.Instance.OwnedArtifacts.Count == 0)
                {
                    return false;
                }
                return true;

            case EventEffectType.GainRandomSpecificArtifact:
                if (effect.artifactIds == null || effect.artifactIds.Count == 0)
                {
                    return false;
                }
                return true;

            case EventEffectType.GainRandomSpecificPiece:
                if (effect.pieceTypes == null || effect.pieceTypes.Count == 0)
                {
                    return false;
                }
                return true;

            case EventEffectType.RemoveSpecificPiece:
            case EventEffectType.RemoveRandomPiece:
                if (PieceInventory.Instance == null || PieceInventory.Instance.OwnedPieces.Count == 0)
                {
                    return false;
                }
                return true;

            default:
                return true;
        }
    }

    private EventEffectType ParseEffectType(string effectTypeString)
    {
        if (string.IsNullOrEmpty(effectTypeString))
        {
            return EventEffectType.None;
        }

        if (System.Enum.TryParse<EventEffectType>(effectTypeString, out var result))
        {
            return result;
        }

        return EventEffectType.None;
    }

    private void RemoveSpecificArtifact(string artifactId)
    {
        if (ArtifactManager.Instance == null || string.IsNullOrEmpty(artifactId))
        {
            return;
        }

        ArtifactData owned = ArtifactManager.Instance.OwnedArtifacts
            .FirstOrDefault(artifact => artifact != null && artifact.id == artifactId);

        if (owned != null)
        {
            AnimateArtifactLose(owned);
        }
    }

    private void RemoveRandomArtifact()
    {
        if (ArtifactManager.Instance == null || ArtifactManager.Instance.OwnedArtifacts.Count == 0)
        {
            return;
        }

        ArtifactData randomOwned = ArtifactManager.Instance.OwnedArtifacts
            [UnityEngine.Random.Range(0, ArtifactManager.Instance.OwnedArtifacts.Count)];

        if (randomOwned != null)
        {
            AnimateArtifactLose(randomOwned);
        }
    }

    private void RemoveSpecificPiece(PieceType pieceType)
    {
        if (PieceInventory.Instance == null)
        {
            return;
        }

        // 인벤토리 슬롯에서 해당 타입의 기물 찾아서 제거
        InventorySlot[] slots = FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);
        foreach (var slot in slots)
        {
            PieceController piece = slot.GetComponentInChildren<PieceController>();
            if (piece != null && piece.Type == pieceType)
            {
                AnimatePieceLose(piece);
                return;
            }
        }
    }

    private void RemoveRandomPiece()
    {
        if (PieceInventory.Instance == null || PieceInventory.Instance.OwnedPieces.Count == 0)
        {
            return;
        }

        // 인벤토리 슬롯에 있는 기물들 목록 가져오기
        List<PieceController> inventoryPieces = new List<PieceController>();
        InventorySlot[] slots = FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);
        foreach (var slot in slots)
        {
            PieceController piece = slot.GetComponentInChildren<PieceController>();
            if (piece != null)
            {
                inventoryPieces.Add(piece);
            }
        }

        if (inventoryPieces.Count == 0)
        {
            return;
        }

        // 랜덤으로 하나 선택하여 제거
        PieceController randomPiece = inventoryPieces[UnityEngine.Random.Range(0, inventoryPieces.Count)];
        AnimatePieceLose(randomPiece);
    }

    private bool TryParsePieceType(string source, out PieceType pieceType)
    {
        if (string.IsNullOrEmpty(source))
        {
            pieceType = PieceType.Soldier;
            return false;
        }

        return Enum.TryParse(source, true, out pieceType);
    }

    private InventorySlot FindEmptyInventorySlot()
    {
        InventorySlot[] slots = FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);
        InventorySlot[] sortedSlots = slots.OrderBy(slot => slot.transform.GetSiblingIndex()).ToArray();
        return sortedSlots.FirstOrDefault(slot => slot.GetComponentInChildren<PieceController>() == null && !slot.IsReserved);
    }

    private void AnimateGoldFly(int amount)
    {
        if (GameManager.Instance == null || GameManager.Instance.coinText == null)
        {
            GameManager.Instance.AddCoin(amount);
            return;
        }

        GameObject flyObj = new GameObject("FlyingCoin");
        Canvas rootCanvas = eventPanel != null ? eventPanel.GetComponentInParent<Canvas>() : null;
        if (rootCanvas != null)
        {
            flyObj.transform.SetParent(rootCanvas.transform, false);
        }
        else
        {
            flyObj.transform.SetParent(eventPanel.transform.root, false);
        }
        
        flyObj.transform.position = eventPanel.transform.position;
        flyObj.transform.localScale = Vector3.one;

        TextMeshProUGUI flyText = flyObj.AddComponent<TextMeshProUGUI>();
        flyText.raycastTarget = false;
        flyText.text = $"+{amount}";
        flyText.fontSize = 40;
        flyText.color = Color.yellow;
        flyText.alignment = TextAlignmentOptions.Center;
        flyText.textWrappingMode = TextWrappingModes.NoWrap;
        
        // RectTransform 설정
        RectTransform rectTrans = flyObj.GetComponent<RectTransform>();
        rectTrans.sizeDelta = new Vector2(200, 100);
        
        if (GameManager.Instance.coinText != null)
        {
            flyText.font = GameManager.Instance.coinText.font;
        }
        if (flyText.font == null)
        {
            flyText.font = TMP_Settings.defaultFontAsset;
        }
        
        Sequence seq = DOTween.Sequence();
        seq.Append(flyObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(GameManager.Instance.coinText.transform.position, 0.8f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0.5f, 0.8f).SetDelay(0.2f));
        
        seq.OnComplete(() =>
        {
            if (GameManager.Instance != null) GameManager.Instance.AddCoin(amount);
            Destroy(flyObj);
        });
    }

    private void AnimateArtifactFly(ArtifactData artifact)
    {
        if (ArtifactManager.Instance == null || artifact == null) return;

        // 목표 슬롯을 먼저 찾기
        Transform targetSlot = FindArtifactSlotInInventory();
        if (targetSlot == null)
        {
            // 슬롯을 못 찾으면 그냥 추가
            ArtifactManager.Instance.AddArtifact(artifact);
            return;
        }

        GameObject flyObj = new GameObject("FlyingArtifact");
        Canvas rootCanvas = eventPanel != null ? eventPanel.GetComponentInParent<Canvas>() : null;
        if (rootCanvas != null)
        {
            flyObj.transform.SetParent(rootCanvas.transform, true);
        }
        else
        {
            flyObj.transform.SetParent(eventPanel.transform.root, true);
        }
        
        // 이벤트 패널의 중앙에서 시작
        if (eventPanel != null)
        {
            flyObj.transform.position = eventPanel.transform.position;
        }
        flyObj.transform.localScale = Vector3.one;

        Image flyImage = flyObj.AddComponent<Image>();
        flyImage.sprite = artifact.icon;
        flyImage.raycastTarget = false;
        
        RectTransform rectTransform = flyObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(64, 64);

        Sequence seq = DOTween.Sequence();
        seq.Append(flyObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(targetSlot.position, 0.8f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0.3f, 0.8f).SetDelay(0.2f));
        
        // 완료 시 데이터 추가 및 시각화 업데이트
        seq.OnComplete(() =>
        {
            if (ArtifactManager.Instance != null)
            {
                ArtifactManager.Instance.AddArtifact(artifact);
            }
            Destroy(flyObj);
        });
    }

    private Transform FindArtifactSlotInInventory()
    {
        if (ArtifactManager.Instance == null) return null;

        // 다음에 추가될 유물의 인덱스 사용
        int targetIndex = ArtifactManager.Instance.OwnedArtifacts.Count;
        if (ArtifactManager.Instance.artifactSlots != null && 
            targetIndex < ArtifactManager.Instance.artifactSlots.Count)
        {
            if (ArtifactManager.Instance.artifactSlots[targetIndex] != null)
            {
                return ArtifactManager.Instance.artifactSlots[targetIndex].transform;
            }
        }
        
        return null;
    }

    private void AnimateArtifactLose(ArtifactData artifact)
    {
        if (ArtifactManager.Instance == null || artifact == null) return;

        // OwnedArtifacts에서 해당 유물의 인덱스 찾기
        int artifactIndex = -1;
        for (int i = 0; i < ArtifactManager.Instance.OwnedArtifacts.Count; i++)
        {
            if (ArtifactManager.Instance.OwnedArtifacts[i] == artifact)
            {
                artifactIndex = i;
                break;
            }
        }
        
        Vector3 startPos = Vector3.zero;
        bool foundSlot = false;
        
        if (artifactIndex >= 0 && 
            ArtifactManager.Instance.artifactSlots != null &&
            artifactIndex < ArtifactManager.Instance.artifactSlots.Count &&
            ArtifactManager.Instance.artifactSlots[artifactIndex] != null)
        {
            startPos = ArtifactManager.Instance.artifactSlots[artifactIndex].transform.position;
            foundSlot = true;
        }

        if (!foundSlot)
        {
            // 슬롯을 못 찾으면 그냥 제거
            ArtifactManager.Instance.RemoveArtifact(artifact);
            return;
        }

        GameObject flyObj = new GameObject("LosingArtifact");
        Canvas rootCanvas = eventPanel != null ? eventPanel.GetComponentInParent<Canvas>() : null;
        if (rootCanvas != null)
        {
            flyObj.transform.SetParent(rootCanvas.transform, true);
        }
        else
        {
            flyObj.transform.SetParent(eventPanel.transform.root, true);
        }
        
        flyObj.transform.position = startPos;
        flyObj.transform.localScale = Vector3.one;

        Image flyImage = flyObj.AddComponent<Image>();
        flyImage.sprite = artifact.icon;
        flyImage.raycastTarget = false;
        
        RectTransform rectTransform = flyObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(64, 64);

        // 이벤트 패널 방향으로 날아가기 (월드 좌표 사용)
        Vector3 targetPos = eventPanel != null ? eventPanel.transform.position : Vector3.zero;

        // 애니메이션 시작 시 즉시 유물 제거
        ArtifactManager.Instance.RemoveArtifact(artifact);

        Sequence seq = DOTween.Sequence();
        seq.Append(flyObj.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(flyObj.transform.DOMove(targetPos, 0.8f).SetEase(Ease.InBack));
        seq.Join(flyObj.transform.DOScale(0f, 0.8f).SetDelay(0.2f));
        
        seq.OnComplete(() =>
        {
            Destroy(flyObj);
        });
    }

    private void AnimatePieceLose(PieceController piece)
    {
        if (piece == null || PieceInventory.Instance == null) return;

        PieceType pieceType = piece.Type;
        
        // 기물을 최상위로 이동 (다른 UI 요소에 가려지지 않도록)
        piece.transform.SetAsLastSibling();

        // 이벤트 패널 방향으로 날아가기
        Vector3 targetPos = eventPanel != null ? eventPanel.transform.position : Vector3.zero;

        Sequence seq = DOTween.Sequence();
        seq.Append(piece.transform.DOScale(piece.transform.localScale * 1.2f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(piece.transform.DOMove(targetPos, 0.8f).SetEase(Ease.InBack));
        seq.Join(piece.transform.DOScale(Vector3.zero, 0.8f).SetDelay(0.2f));
        
        seq.OnComplete(() =>
        {
            PieceInventory.Instance.RemovePiece(pieceType);
            if (piece != null)
            {
                Destroy(piece.gameObject);
            }
        });
    }

    private void ExitToMap()
    {
        CloseEventUI();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeFlowState(GameFlowState.Map);
        }
    }

    private void CloseEventUI()
    {
        if (choiceButtonContainer != null)
        {
            foreach (Transform child in choiceButtonContainer)
            {
                Destroy(child.gameObject);
            }
        }

        if (eventPanel != null)
        {
            // 패널 애니메이션: 아래쪽으로 슬라이드 아웃 (세로)
            if (eventPanelRect != null)
            {
                eventPanelRect.DOAnchorPosY(eventPanelRect.rect.height, animDuration).SetEase(closeEase).OnComplete(() =>
                {
                    eventPanel.SetActive(false);
                });
            }
            else
            {
                eventPanel.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Resources/Seal 폴더에서 모든 SealData 자동 로드
    /// </summary>
    private void LoadAllSealsFromFolder()
    {
        availableSeals = new List<SealData>();
        
        #if UNITY_EDITOR
        // 에디터 환경: AssetDatabase 사용
        string folderPath = "Assets/Data/Seal";
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SealData", new[] { folderPath });
        
        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            SealData seal = UnityEditor.AssetDatabase.LoadAssetAtPath<SealData>(assetPath);
            if (seal != null)
            {
                availableSeals.Add(seal);
            }
        }
        #else
        // 런타임 환경: Resources 폴더 사용
        SealData[] loadedSeals = Resources.LoadAll<SealData>("Seal");
        availableSeals.AddRange(loadedSeals);
        #endif

    }

    /// <summary>
    /// Resources/Artifact 폴더에서 모든 ArtifactData 자동 로드
    /// </summary>
    private void LoadAllArtifactsFromFolder()
    {
        artifactCatalog = new List<ArtifactData>();
        
        #if UNITY_EDITOR
        // 에디터 환경: AssetDatabase 사용
        string folderPath = "Assets/Data/Artifact";
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ArtifactData", new[] { folderPath });
        
        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ArtifactData artifact = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtifactData>(assetPath);
            if (artifact != null)
            {
                artifactCatalog.Add(artifact);
            }
        }
        #else
        // 런타임 환경: Resources 폴더 사용
        ArtifactData[] loadedArtifacts = Resources.LoadAll<ArtifactData>("Artifact");
        artifactCatalog.AddRange(loadedArtifacts);
        #endif

    }
}
