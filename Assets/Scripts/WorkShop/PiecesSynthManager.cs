using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
//using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 기물 합성 매니저: 기물 2개를 드래그/드롭으로 올려놓고 합성하는 기능을 제공합니다.
/// </summary>
public class PiecesSynthManager : MonoBehaviour
{
    public static PiecesSynthManager Instance { get; private set; }

    [Header("UI")]
    public GameObject synthesisPanel;       // 기물 합성 패널
    public Transform synthesisSlot1;        // 첫 번째 합성 슬롯
    public Transform synthesisSlot2;        // 두 번째 합성 슬롯
    public Button synthesisButton;          // 합성 버튼
    public Button clearButton;              // 초기화 버튼
    public TextMeshProUGUI titleText;       // "기물 합성" 타이틀
    public TextMeshProUGUI descriptionText; // 설명 텍스트
    public TextMeshProUGUI slot1Text;       // 슬롯1 정보 텍스트
    public TextMeshProUGUI slot2Text;       // 슬롯2 정보 텍스트

    [Header("Preview")]
    public TextMeshProUGUI previewText;     // 합성 결과 미리보기 텍스트
    public Image previewPieceImage;         // 합성 결과 미리보기 이미지
    public Transform previewSealsContainer; // 합성 결과 인장 아이콘 컨테이너
    public GameObject previewSealIconPrefab; // 인장 아이콘 프리팹(선택)
    public Vector2 previewSealIconSize = new Vector2(24f, 24f);
    public bool autoResizePreviewPieceImage = true;
    public float previewPieceImageSizeMultiplier = 1f;

    [Header("Slot Return Animation")]
    public bool useFlyAnimationToInventory = true;
    public float flyToInventoryDuration = 0.35f;
    public float flyToInventoryJumpPower = 5f;

    private PieceController piece1;         // 슬롯1의 기물
    private PieceController piece2;         // 슬롯2의 기물
    private bool hasPreviewResult;
    private bool isPreviewRandom;
    private PieceType previewResultType;
    private string previewSignature = string.Empty;
    private RectTransform synthesisFlyLayer;
    private readonly List<SealData> previewResultSeals = new List<SealData>();
    private readonly List<GameObject> previewSealIconObjects = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (synthesisPanel != null)
        {
            synthesisPanel.SetActive(false);
        }

        if (synthesisButton != null)
        {
            synthesisButton.onClick.AddListener(OnSynthesisClick);
        }

        if (clearButton != null)
        {
            clearButton.onClick.AddListener(OnClearClick);
        }

        ClearPreviewSealIcons();
    }

    /// <summary>
    /// 기물 합성 패널을 엽니다.
    /// </summary>
    public void OpenSynthesisPanel()
    {
        if (synthesisPanel != null)
        {
            synthesisPanel.SetActive(true);
        }

        // 초기화
        piece1 = null;
        piece2 = null;
        ResetPreviewState();
        UpdateSlotUI();

        if (descriptionText != null)
        {
            descriptionText.text = "인벤토리에서 기물을 드래그해서\n" +
                "두 개의 슬롯에 올려놓으세요.\n" +
                "그 후 합성 버튼을 누르면\n" +
                "새로운 기물을 획득할 수 있습니다!";
        }

    }

    /// <summary>
    /// 기물 합성 패널을 닫습니다.
    /// </summary>
    public void CloseSynthesisPanel()
    {
        if (synthesisPanel != null)
        {
            synthesisPanel.SetActive(false);
        }

    }

    /// <summary>
    /// 슬롯1에 기물을 설정합니다 (드래그 드롭 완료 시 호출)
    /// </summary>
    public void SetSlot1Piece(PieceController piece)
    {
        if (piece1 != null && piece1 != piece)
        {
            // 기존 기물 원래 위치로 돌려보내기
            //piece1.MoveToInventory(piece1.OriginalParent);
        }

        piece1 = piece;
        if (piece1 != null)
        {
            // 부모를 슬롯으로 변경하고 RectTransform 기준으로 중앙 정렬
            piece1.transform.SetParent(synthesisSlot1, false);
            piece1.SetOriginalParent(synthesisSlot1);
            RectTransform rt = piece1.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                piece1.transform.localPosition = Vector3.zero;
            }
        }

        UpdateSlotUI();
    }

    /// <summary>
    /// 슬롯2에 기물을 설정합니다 (드래그 드롭 완료 시 호출)
    /// </summary>
    public void SetSlot2Piece(PieceController piece)
    {
        if (piece2 != null && piece2 != piece)
        {
            // 기존 기물 원래 위치로 돌려보내기
            //piece2.MoveToInventory(piece2.OriginalParent);
        }

        piece2 = piece;
        if (piece2 != null)
        {
            // 부모를 슬롯으로 변경하고 RectTransform 기준으로 중앙 정렬
            piece2.transform.SetParent(synthesisSlot2, false);
            piece2.SetOriginalParent(synthesisSlot2);
            RectTransform rt = piece2.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                piece2.transform.localPosition = Vector3.zero;
            }
        }

        UpdateSlotUI();
    }

    /// <summary>
    /// 슬롯 UI 업데이트
    /// </summary>
    private void UpdateSlotUI()
    {
        if (slot1Text != null)
        {
            slot1Text.text = piece1 != null ? piece1.Type.ToString() : "비어있음";
        }

        if (slot2Text != null)
        {
            slot2Text.text = piece2 != null ? piece2.Type.ToString() : "비어있음";
        }

        bool hasBothPieces = (piece1 != null && piece2 != null);
        bool canSynthesize = hasBothPieces && ValidateSynthesis(false);

        // 두 개의 기물이 있고 검증을 통과할 때만 합성 버튼 활성화
        if (synthesisButton != null)
        {
            synthesisButton.interactable = canSynthesize;
        }

        RefreshPreview(canSynthesize);
    }

    /// <summary>
    /// 초기화 버튼 클릭
    /// </summary>
    private void OnClearClick()
    {
        if (piece1 != null)
        {
            if (TryMovePieceToInventory(piece1, piece1.OriginalParent, useFlyAnimationToInventory))
            {
                piece1 = null;
            }
        }

        if (piece2 != null)
        {
            if (TryMovePieceToInventory(piece2, piece2.OriginalParent, useFlyAnimationToInventory))
            {
                piece2 = null;
            }
        }

        ResetPreviewState();

        UpdateSlotUI();
    }

    /// <summary>
    /// 합성 버튼 클릭
    /// </summary>
    private void OnSynthesisClick()
    {
        if (piece1 == null || piece2 == null)
        {
            return;
        }

        // 합성 가능 여부 검증
        if (!ValidateSynthesis())
        {
            return;
        }

        if (!hasPreviewResult)
        {
            return;
        }

        if (previewPieceImage != null)
        {
            previewPieceImage.enabled = false;
        }

        ClearPreviewSealIcons();

        StartCoroutine(PerformSynthesis(previewResultType, new List<SealData>(previewResultSeals)));
    }

    /// <summary>
    /// 합성 가능 여부 검증
    /// </summary>
    private bool ValidateSynthesis(bool logWarnings = true)
    {
        // piece1의 기존 인장 정보
        List<string> piece1SealNames = new List<string>();
        foreach (var seal in piece1.EquippedSeals)
        {
            if (seal != null && seal.Data != null)
            {
                piece1SealNames.Add(seal.Data.sealName);
            }
        }

        // piece2에서 옮길 인장 정보 검증
        foreach (var sealBase in piece2.EquippedSeals)
        {
            if (sealBase == null || sealBase.Data == null) continue;

            SealData sealData = sealBase.Data;

            // 1. 인장이 piece1 기물에 호환되는지 확인
            if (sealData.compatiblePieces != null && sealData.compatiblePieces.Count > 0)
            {
                if (!sealData.compatiblePieces.Contains(piece1.Type))
                {
                    if (logWarnings)
                    {
                    }
                    return false;
                }
            }

            // 2. 같은 인장이 piece1에 이미 있는지 확인
            if (piece1SealNames.Contains(sealData.sealName))
            {
                if (logWarnings)
                {
                }
                return false;
            }
        }

        return true;
    }

    private void RefreshPreview(bool canSynthesize)
    {
        string newSignature = BuildPreviewSignature();
        if (newSignature == previewSignature)
        {
            UpdatePreviewUI();
            return;
        }

        previewSignature = newSignature;
        if (!canSynthesize || (piece1.EquippedSeals.Count == 0 && piece2.EquippedSeals.Count == 0))
        {
            isPreviewRandom = true;
            previewResultType = GetRandomPieceType();
        }
        else
        {
            isPreviewRandom = false;
            previewResultType = piece1.Type;
        }

        hasPreviewResult = true;
        UpdatePreviewUI();
    }

    private string BuildPreviewSignature()
    {
        if (piece1 == null || piece2 == null)
        {
            return "EMPTY";
        }

        string p1Seals = string.Join("|", piece1.EquippedSeals
            .Where(s => s != null && s.Data != null)
            .Select(s => s.Data.sealName)
            .OrderBy(n => n));

        string p2Seals = string.Join("|", piece2.EquippedSeals
            .Where(s => s != null && s.Data != null)
            .Select(s => s.Data.sealName)
            .OrderBy(n => n));

        return $"{piece1.GetInstanceID()}:{piece1.Type}:{p1Seals}>{piece2.GetInstanceID()}:{piece2.Type}:{p2Seals}";
    }

    private void UpdatePreviewUI()
    {
        if (previewText != null)
        {
            if (!hasPreviewResult)
            {
                previewText.text = "미리보기: -";
            }
            else if (isPreviewRandom)
            {
                previewText.text = "미리보기: 랜덤 기물";
            }
            else
            {
                previewText.text = $"미리보기: {previewResultType}";
            }
        }

        if (previewPieceImage != null)
        {
            if (!hasPreviewResult || isPreviewRandom || PieceManager.Instance == null)
            {
                previewPieceImage.enabled = false;
            }
            else
            {
                Sprite sprite = PieceManager.Instance.GetSpriteFor(previewResultType);
                if (sprite != null)
                {
                    previewPieceImage.sprite = sprite;
                    previewPieceImage.preserveAspect = true;
                    if (autoResizePreviewPieceImage)
                    {
                        ResizePreviewPieceImageToSprite();
                    }
                    previewPieceImage.enabled = true;
                }
                else
                {
                    previewPieceImage.enabled = false;
                }
            }
        }
    }

    private void ResizePreviewPieceImageToSprite()
    {
        if (previewPieceImage == null || previewPieceImage.sprite == null)
        {
            return;
        }

        previewPieceImage.SetNativeSize();

        RectTransform rt = previewPieceImage.rectTransform;
        if (rt != null && Mathf.Abs(previewPieceImageSizeMultiplier - 1f) > 0.0001f)
        {
            rt.sizeDelta *= previewPieceImageSizeMultiplier;
        }
    }

    private void RefreshPreviewSealIcons()
    {
        ClearPreviewSealIcons();

        if (previewSealsContainer == null)
        {
            return;
        }

        if (!hasPreviewResult || previewResultSeals.Count == 0)
        {
            return;
        }

        foreach (var sealData in previewResultSeals)
        {
            if (sealData == null || sealData.icon == null)
            {
                continue;
            }

            GameObject iconObj;
            if (previewSealIconPrefab != null)
            {
                iconObj = Instantiate(previewSealIconPrefab, previewSealsContainer);
            }
            else
            {
                iconObj = new GameObject($"PreviewSeal_{sealData.sealName}", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(previewSealsContainer, false);
            }

            RectTransform rt = iconObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = previewSealIconSize;
            }

            Image iconImage = iconObj.GetComponent<Image>();
            if (iconImage == null)
            {
                iconImage = iconObj.AddComponent<Image>();
            }
            iconImage.sprite = sealData.icon;
            iconImage.raycastTarget = true;

            SealTooltipHandler tooltipHandler = iconObj.GetComponent<SealTooltipHandler>();
            if (tooltipHandler == null)
            {
                tooltipHandler = iconObj.AddComponent<SealTooltipHandler>();
            }
            tooltipHandler.Initialize(sealData);

            previewSealIconObjects.Add(iconObj);
        }
    }

    private void ClearPreviewSealIcons()
    {
        for (int i = 0; i < previewSealIconObjects.Count; i++)
        {
            if (previewSealIconObjects[i] != null)
            {
                Destroy(previewSealIconObjects[i]);
            }
        }
        previewSealIconObjects.Clear();
    }

    private PieceType GetRandomPieceType()
    {
        PieceType[] allPieceTypes = System.Enum.GetValues(typeof(PieceType)) as PieceType[];
        return allPieceTypes[Random.Range(0, allPieceTypes.Length)];
    }

    private Vector3 GetPreviewStartPosition()
    {
        if (previewPieceImage != null && previewPieceImage.enabled)
        {
            return previewPieceImage.transform.position;
        }

        if (piece1 != null)
        {
            return piece1.transform.position;
        }

        return Vector3.zero;
    }

    private Transform GetSynthesisFlyLayer()
    {
        if (synthesisFlyLayer != null)
        {
            return synthesisFlyLayer;
        }

        if (synthesisPanel == null)
        {
            return null;
        }

        GameObject layerObj = new GameObject("SynthesisFlyLayer", typeof(RectTransform));
        layerObj.transform.SetParent(synthesisPanel.transform, false);
        layerObj.transform.SetAsLastSibling();

        synthesisFlyLayer = layerObj.GetComponent<RectTransform>();
        synthesisFlyLayer.anchorMin = Vector2.zero;
        synthesisFlyLayer.anchorMax = Vector2.one;
        synthesisFlyLayer.offsetMin = Vector2.zero;
        synthesisFlyLayer.offsetMax = Vector2.zero;
        synthesisFlyLayer.pivot = new Vector2(0.5f, 0.5f);

        return synthesisFlyLayer;
    }

    private System.Collections.IEnumerator PerformSynthesis(PieceType resultPieceType, List<SealData> resultSeals)
    {
        // 3. 첫 번째 빈 인벤토리 슬롯 찾기
        InventorySlot targetSlot = FindEmptyInventorySlot();
        if (targetSlot == null)
        {
            yield break;
        }

        // 4. 시작 위치 저장 (미리보기 위치 우선)
        Vector3 startPos = GetPreviewStartPosition();

        // 5. piece1, piece2 파괴 및 인벤토리에서 제거
        if (PieceInventory.Instance != null)
        {
            PieceInventory.Instance.RemovePiece(piece1.Type);
            PieceInventory.Instance.RemovePiece(piece2.Type);
        }
        Object.Destroy(piece1.gameObject);
        Object.Destroy(piece2.gameObject);

        // 사용된 기물의 인장을 모두 인벤토리로 반환합니다.
        if (SealInventory.Instance != null)
        {
            foreach (var sealBase in piece1.EquippedSeals)
            {
                if (sealBase != null && sealBase.Data != null)
                    SealInventory.Instance.AddSeal(sealBase.Data);
            }
            foreach (var sealBase in piece2.EquippedSeals)
            {
                if (sealBase != null && sealBase.Data != null)
                    SealInventory.Instance.AddSeal(sealBase.Data);
            }
        }


        yield return new WaitForSeconds(0.1f);

        // 6. 새로운 기물을 생성해서 애니메이션과 함께 인벤토리에 추가
        bool completed = false;
        PieceController resultPiece = null;

        PieceSpawner.Instance.SpawnPieceAndFlyToInventory(
            resultPieceType,
            startPos,
            targetSlot,
            null, // 인장을 계승하지 않으므로 null 전달
            (piece) =>
            {
                resultPiece = piece;
                completed = true;
            },
            GetSynthesisFlyLayer());

        // 애니메이션 완료 대기
        while (!completed)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // 7. 생성된 기물에 인장 추가
        if (resultPiece != null)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecordSynthesis();
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 슬롯 초기화
        piece1 = null;
        piece2 = null;
        ResetPreviewState();
        UpdateSlotUI();

        yield return new WaitForSeconds(0.5f);

        // 작업 완료 후 워크샵 종료
        if (WorkShopManager.Instance != null)
        {
            WorkShopManager.Instance.OnWorkShopComplete();
        }
    }

    public PieceController GetPieceInSlot(int index)
    {
        if (index == 0) return piece1;
        if (index == 1) return piece2;
        return null;
    }

    public void AssignPieceToSynthesisSlot(int slotIndex, PieceController piece)
    {
        if (slotIndex == 0)
        {
            piece1 = piece;
        }
        else if (slotIndex == 1)
        {
            piece2 = piece;
        }

        UpdateSlotUI();
    }

    public bool SwapSynthesisSlotPieces(int sourceSlotIndex, int targetSlotIndex, PieceController draggedPiece, PieceController displacedPiece, bool animateFly = false)
    {
        if (draggedPiece == null || displacedPiece == null)
        {
            return false;
        }

        Transform sourceSlot = GetSynthesisSlotTransform(sourceSlotIndex);
        Transform targetSlot = GetSynthesisSlotTransform(targetSlotIndex);
        if (sourceSlot == null || targetSlot == null)
        {
            return false;
        }

        if (!animateFly)
        {
            SetSlotPieceByIndex(sourceSlotIndex, displacedPiece);
            SetSlotPieceByIndex(targetSlotIndex, draggedPiece);
            return true;
        }

        AnimateSynthesisSwap(sourceSlotIndex, targetSlotIndex, draggedPiece, displacedPiece);
        return true;
    }

    public bool TryMovePieceToInventory(PieceController piece, Transform preferredSlot, bool animateFly = false)
    {
        if (piece == null)
        {
            return false;
        }

        if (!TryGetTargetInventorySlot(preferredSlot, out InventorySlot targetSlot))
        {
            return false;
        }

        if (animateFly && useFlyAnimationToInventory)
        {
            AnimatePieceToInventory(piece, targetSlot);
        }
        else
        {
            piece.MoveToInventory(targetSlot.transform);
        }

        return true;
    }

    public bool TryRestorePieceToSynthesisSlot(PieceController piece, int slotIndex, bool animateFly = false)
    {
        if (piece == null)
        {
            return false;
        }

        Transform targetSlot = GetSynthesisSlotTransform(slotIndex);
        if (targetSlot == null)
        {
            return false;
        }

        if (animateFly)
        {
            AnimatePieceToSynthesisSlot(piece, targetSlot);
        }
        else
        {
            SetPieceToSynthesisSlot(piece, targetSlot);
        }

        return true;
    }

    private bool TryGetTargetInventorySlot(Transform preferredSlot, out InventorySlot targetSlot)
    {
        targetSlot = null;

        InventorySlot preferredInventorySlot = preferredSlot != null ? preferredSlot.GetComponent<InventorySlot>() : null;
        if (preferredInventorySlot != null && !preferredInventorySlot.IsReserved)
        {
            PieceController occupied = preferredInventorySlot.GetComponentInChildren<PieceController>();
            if (occupied == null)
            {
                targetSlot = preferredInventorySlot;
                return true;
            }
        }

        InventorySlot emptySlot = FindEmptyInventorySlot();
        if (emptySlot == null)
        {
            return false;
        }

        targetSlot = emptySlot;
        return true;
    }

    private void AnimatePieceToInventory(PieceController piece, InventorySlot targetSlot)
    {
        if (piece == null || targetSlot == null)
        {
            return;
        }

        PieceManager.MovePieceToInventoryWithJump(piece, targetSlot, flyToInventoryJumpPower, flyToInventoryDuration, null, true, Ease.OutQuad, 0f, GetSynthesisFlyLayer());
    }

    private void AnimatePieceToSynthesisSlot(PieceController piece, Transform targetSlot)
    {
        if (piece == null || targetSlot == null)
        {
            return;
        }

        PieceManager.PlayJumpAnimation(piece, targetSlot, flyToInventoryJumpPower, flyToInventoryDuration, () =>
        {
            if (piece == null)
            {
                return;
            }
            SetPieceToSynthesisSlot(piece, targetSlot);
        });
    }

    private void AnimateSynthesisSwap(int sourceSlotIndex, int targetSlotIndex, PieceController draggedPiece, PieceController displacedPiece)
    {
        Transform sourceSlot = GetSynthesisSlotTransform(sourceSlotIndex);
        Transform targetSlot = GetSynthesisSlotTransform(targetSlotIndex);
        if (sourceSlot == null || targetSlot == null)
        {
            return;
        }

        int completedCount = 0;
        void OnJumpCompleted()
        {
            completedCount++;
            if (completedCount < 2)
            {
                return;
            }

            if (draggedPiece != null)
            {
                SetSlotPieceByIndex(targetSlotIndex, draggedPiece);
            }

            if (displacedPiece != null)
            {
                SetSlotPieceByIndex(sourceSlotIndex, displacedPiece);
            }
        }

        PieceManager.PlayJumpAnimation(draggedPiece, targetSlot, flyToInventoryJumpPower, flyToInventoryDuration, OnJumpCompleted);
        PieceManager.PlayJumpAnimation(displacedPiece, sourceSlot, flyToInventoryJumpPower, flyToInventoryDuration, OnJumpCompleted);
    }

    private void SetSlotPieceByIndex(int slotIndex, PieceController piece)
    {
        if (slotIndex == 0)
        {
            SetSlot1Piece(piece);
        }
        else if (slotIndex == 1)
        {
            SetSlot2Piece(piece);
        }
    }

    private void SetPieceToSynthesisSlot(PieceController piece, Transform slotTransform)
    {
        if (piece == null || slotTransform == null)
        {
            return;
        }

        piece.transform.SetParent(slotTransform, false);
        piece.SetOriginalParent(slotTransform);
        RectTransform rt = piece.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
        }
        else
        {
            piece.transform.localPosition = Vector3.zero;
        }
    }

    private Transform GetSynthesisSlotTransform(int slotIndex)
    {
        return slotIndex == 0 ? synthesisSlot1 : slotIndex == 1 ? synthesisSlot2 : null;
    }

    private InventorySlot FindEmptyInventorySlot()
    {
        InventorySlot[] slots = FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);
        
        foreach (var slot in slots)
        {
            if (slot.GetComponentInChildren<PieceController>() == null && !slot.IsReserved)
            {
                return slot;
            }
        }

        return slots.Length > 0 ? slots[0] : null;
    }

    private void ResetPreviewState()
    {
        hasPreviewResult = false;
        isPreviewRandom = false;
        previewSignature = string.Empty;
        previewResultSeals.Clear();
        ClearPreviewSealIcons();
    }
}
