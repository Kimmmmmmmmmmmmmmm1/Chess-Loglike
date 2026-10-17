using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using DG.Tweening;

/// <summary>
/// 폰(Pawn) 승급(프로모션)을 관리하는 매니저
/// </summary>
public class PiecePromotionManager : MonoBehaviour
{
    public static PiecePromotionManager Instance { get; private set; }

    [Header("UI")]
    public GameObject promotionPanel;              // 승급 선택 패널
    public Button horseButton;                     // 마 선택 버튼
    public Button elephantButton;                  // 상 선택 버튼
    public Button chariotButton;                   // 차 선택 버튼
    public Button cannonButton;                    // 포 선택 버튼
    public TextMeshProUGUI titleText;             // "기물 승급" 타이틀
    public TextMeshProUGUI descriptionText;       // 설명 텍스트
    public SealData promotionSealData;            // 승급의 인장(선택 연결)

    [Header("Animation")]
    public float animDuration = 0.3f;
    public Ease openEase = Ease.OutBack;
    public Ease closeEase = Ease.InQuad;
    public float promotionCollapsedWidth = 72f;
    public float promotionExpandedWidth = 420f;

    private RectTransform promotionPanelRect;

    [Header("Promotion Effect")]
    public RectTransform uiCanvasRect; // assign the root UI canvas RectTransform
    public Color promotionEffectColor = new Color(0f, 1f, 0f, 0.85f);
    public float effectHeight = 128f;
    public float effectTargetWidth = 48f;
    public float effectExpandDuration = 0.36f;
    public float effectFadeDuration = 0.4f;
    public Ease effectEase = Ease.OutQuad;

    private float GetEnemyEffectHeight(PieceController piece)
    {
        if (piece != null && PieceManager.Instance != null &&
            PieceManager.Instance.gridManager != null)
        {
            float cellHeight = PieceManager.Instance.gridManager.cellSize.y;
            return Mathf.Max(cellHeight * 5f, piece.transform as RectTransform != null
                ? (piece.transform as RectTransform).rect.height * 5f
                : cellHeight * 5f);
        }

        return effectHeight;
    }

    [Header("Enemy Promotion Timing & Delays")]
    [Tooltip("1단계: 이동(AnimatePlacement) 완료 대기 시간")]
    [SerializeField] private float enemyMoveWaitTime = 0.65f;
    [Tooltip("1단계->2단계: 이동 완료 후 이펙트 발생 전 대기 딜레이 (기물이 끝 줄에 도착했음을 인지하는 시간)")]
    [SerializeField] private float enemyMoveToEffectDelay = 0.45f;
    [Tooltip("2단계: 승급 이펙트(붉은 기둥 솟구침 및 기물 에너지 충전 떨림) 지속 시간")]
    [SerializeField] private float enemyEffectDuration = 0.75f;
    [Tooltip("2단계->3단계: 이펙트 절정 후 변신 직전 긴장감 딜레이")]
    [SerializeField] private float enemyEffectToTransformDelay = 0.3f;
    [Tooltip("3단계->종료: 변신(퀸 스폰 및 등장 연출) 후 플레이어가 확인할 수 있도록 대기하는 딜레이")]
    [SerializeField] private float enemyPostTransformDelay = 0.75f;

    private PieceController pendingSoldier;        // 승급 대기 중인 졸
    private bool isOpen = false;
    private bool isEnemyPromoting = false;
    private bool isPlayerPromotingEffectRunning = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 프로모션 패널이 열려있거나 프로모션 연출이 진행 중인지 확인
    /// </summary>
    public bool IsPromotionPanelOpen() => IsPromoting;
    public bool IsPromoting => IsEnemyPromoting || IsPlayerPromoting;
    public bool IsEnemyPromoting => isEnemyPromoting;
    public bool IsPlayerPromoting => isOpen || pendingSoldier != null || isPlayerPromotingEffectRunning;

    private void Start()
    {
        EnsurePromotionSealData();

        if (promotionPanel != null)
        {
            promotionPanelRect = promotionPanel.GetComponent<RectTransform>();
            SetPromotionPanelWidth(promotionExpandedWidth);
            promotionPanel.SetActive(false);
        }

        // 버튼 리스너 등록 (Knight, Bishop, Rook, Queen)
        if (horseButton != null)
            horseButton.onClick.AddListener(() => OnPromotionSelected(PieceType.Knight));
        
        if (elephantButton != null)
            elephantButton.onClick.AddListener(() => OnPromotionSelected(PieceType.Bishop));
        
        if (chariotButton != null)
            chariotButton.onClick.AddListener(() => OnPromotionSelected(PieceType.Rook));
        
        if (cannonButton != null)
            cannonButton.onClick.AddListener(() => OnPromotionSelected(PieceType.Queen));

        UpdateButtonSprites();
    }

    private void UpdateButtonSprites()
    {
        PieceSpriteData data = PieceSpriteData.Instance;
        if (data == null) return;

        SetButtonSprite(horseButton, data.GetSprite(PieceType.Knight, false));
        SetButtonSprite(elephantButton, data.GetSprite(PieceType.Bishop, false));
        SetButtonSprite(chariotButton, data.GetSprite(PieceType.Rook, false));
        SetButtonSprite(cannonButton, data.GetSprite(PieceType.Queen, false));
    }

    private void SetButtonSprite(Button button, Sprite sprite)
    {
        if (button == null || sprite == null) return;
        Image[] images = button.GetComponentsInChildren<Image>(true);
        bool foundChild = false;
        foreach (var img in images)
        {
            if (img.gameObject != button.gameObject)
            {
                img.sprite = sprite;
                foundChild = true;
            }
        }
        if (!foundChild)
        {
            Image btnImg = button.GetComponent<Image>();
            if (btnImg != null) btnImg.sprite = sprite;
        }
    }

    /// <summary>
    /// 폰(Pawn)이 적 진영 끝 줄에 도달했을 때 호출
    /// </summary>
    public void ShowPromotionPanel(PieceController soldier)
    {
        if (soldier == null || soldier.Type != PieceType.Pawn || soldier.IsEnemy)
        {
            return;
        }

        if (GameManager.Instance == null || GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
        {
            return;
        }

        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            return;
        }

        pendingSoldier = soldier;
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.ClearSelection();
        }
        OpenPanel();
    }

    private void OpenPanel()
    {
        if (isOpen || promotionPanel == null)
            return;

        isOpen = true;
        if (promotionPanelRect != null)
        {
            promotionPanel.SetActive(true);
            promotionPanelRect.DOKill();
            SetPromotionPanelWidth(promotionExpandedWidth);
        }
        else
        {
            promotionPanel.SetActive(true);
        }

        if (titleText != null)
            titleText.text = "기물 승급";

        if (descriptionText != null)
            descriptionText.text = "승급할 기물을 선택하세요.";

        UpdateButtonSprites();
    }

    private void OnPromotionSelected(PieceType promotedType)
    {
        if (pendingSoldier == null)
        {
            return;
        }

        if (promotedType == PieceType.King || promotedType == PieceType.Pawn)
        {
            return;
        }

        // Play promotion effect at the soldier's position, then perform the actual promotion when effect reaches target width
        StartPromotionEffect(pendingSoldier, promotedType);
    }

    /// <summary>
    /// 적군 폰(Pawn)이 아군 진영 끝 줄에 도달했을 때 호출 (기본 퀸으로 자동 승급)
    /// [이동] -> [딜레이] -> [이펙트] -> [딜레이] -> [변신] -> [딜레이] 단계별 연출 진행
    /// </summary>
    public void PromoteEnemyPawn(PieceController enemyPawn, PieceType promotedType = PieceType.Queen)
    {
        if (enemyPawn == null || enemyPawn.Type != PieceType.Pawn || !enemyPawn.IsEnemy)
        {
            return;
        }

        if (GameManager.Instance == null || GameManager.Instance.CurrentFlowState != GameFlowState.Battle)
        {
            return;
        }

        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            return;
        }

        isEnemyPromoting = true;
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.ClearSelection();
        }
        StartCoroutine(AnimateEnemyPromotionRoutine(enemyPawn, promotedType));
    }

    private IEnumerator AnimateEnemyPromotionRoutine(PieceController enemyPawn, PieceType promotedType)
    {
        if (enemyPawn == null)
        {
            isEnemyPromoting = false;
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.CheckPieceCounts();
            }
            yield break;
        }

        // ========================================================
        // 1단계: 이동 완료 대기 (Move Completion)
        // ========================================================
        yield return new WaitForSeconds(enemyMoveWaitTime);

        if (enemyPawn == null)
        {
            isEnemyPromoting = false;
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.CheckPieceCounts();
            }
            yield break;
        }

        // [딜레이 1] 적 폰이 목표 끝 줄에 온전히 안착한 모습을 플레이어가 인지하는 딜레이
        yield return new WaitForSeconds(enemyMoveToEffectDelay);

        if (enemyPawn == null)
        {
            isEnemyPromoting = false;
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.CheckPieceCounts();
            }
            yield break;
        }

        // ========================================================
        // 2단계: 승급 이펙트 (Aura Effect & Warning Energy Pulse)
        // ========================================================
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SFXType.OpenPanel);
        }


        if (uiCanvasRect == null)
        {
            Canvas c = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (c != null) uiCanvasRect = c.GetComponent<RectTransform>();
        }

        GameObject effectGo = null;
        Image effectImage = null;
        RectTransform effectRect = null;

        if (uiCanvasRect != null)
        {
            effectGo = new GameObject("EnemyPromotionEffect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            effectGo.transform.SetParent(uiCanvasRect, false);
            effectImage = effectGo.GetComponent<Image>();
            if (effectImage.sprite == null)
            {
                effectImage.sprite = GetOrCreateRuntimeSprite();
            }
            effectImage.color = new Color(1f, 1f, 1f, 0.85f);
            effectRect = effectImage.rectTransform;
            effectRect.pivot = new Vector2(0.5f, 0f);

            Vector3 worldBottom = enemyPawn.transform.position;
            Renderer r = enemyPawn.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                worldBottom = new Vector3(r.bounds.center.x, r.bounds.min.y, r.bounds.center.z);
            }

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCanvasRect.GetComponentInParent<Canvas>()?.worldCamera, worldBottom);
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(uiCanvasRect, screenPoint, uiCanvasRect.GetComponentInParent<Canvas>()?.worldCamera, out localPoint);
            localPoint.y -= 16f;
            effectRect.anchoredPosition = localPoint;
            float promotionEffectHeight = GetEnemyEffectHeight(enemyPawn);
            effectRect.sizeDelta = new Vector2(0f, promotionEffectHeight);

            effectRect.DOSizeDelta(new Vector2(effectTargetWidth, promotionEffectHeight), enemyEffectDuration * 0.7f)
                .SetEase(effectEase);
        }

        // 폰 기물이 에너지를 응축하며 떨리고 커지는 연출
        enemyPawn.transform.DOShakePosition(enemyEffectDuration, strength: new Vector3(6f, 0f, 0f), vibrato: 20, randomness: 0, snapping: false, fadeOut: false);
        enemyPawn.transform.DOScale(Vector3.one * 1.2f, enemyEffectDuration * 0.5f).SetLoops(2, LoopType.Yoyo);

        yield return new WaitForSeconds(enemyEffectDuration);

        // [딜레이 2] 이펙트가 최고조에 달한 후 변신 직전 잠시 멈춤 (긴장감 부여)
        yield return new WaitForSeconds(enemyEffectToTransformDelay);

        if (enemyPawn == null)
        {
            if (effectGo != null) Destroy(effectGo);
            isEnemyPromoting = false;
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.CheckPieceCounts();
            }
            yield break;
        }

        // ========================================================
        // 3단계: 기물 변신 (Transformation into Queen)
        // ========================================================
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SFXType.Confirm);
        }
        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.PlayCameraShake(0.25f, 0.45f);
        }

        // 기물 교체 (폰 제거 및 퀸 생성)
        PieceController newPiece = PerformPromotion(enemyPawn, promotedType);

        // 새 기물 등장 펀치 스케일 연출
        if (newPiece != null)
        {
            newPiece.transform.DOPunchScale(new Vector3(0.35f, 0.35f, 0f), 0.45f, 8, 0.6f);
        }

        // 흰색 기둥 페이드 아웃 후 정리
        if (effectImage != null)
        {
            effectImage.DOFade(0f, 0.35f).OnComplete(() =>
            {
                if (effectGo != null) Destroy(effectGo);
            });
        }

        // [딜레이 3] 변신한 퀸의 위용을 플레이어가 명확히 파악할 수 있도록 대기
        yield return new WaitForSeconds(enemyPostTransformDelay);

        // ========================================================
        // 4단계: 적 승급 종료 및 턴 진행
        // ========================================================
        isEnemyPromoting = false;
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.CheckPieceCounts();
        }
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.AdvanceTurn();
        }
    }

    private void StartPromotionEffect(PieceController soldier, PieceType promotedType)
    {
        if (soldier == null)
        {
            isEnemyPromoting = false;
            isPlayerPromotingEffectRunning = false;
            return;
        }

        isPlayerPromotingEffectRunning = true;

        // Create UI image under the provided UI canvas
        if (uiCanvasRect == null)
        {
            // Try to find a canvas in scene
            Canvas c = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (c != null) uiCanvasRect = c.GetComponent<RectTransform>();
        }

        RectTransform effectRect = null;
        Image effectImage = null;

        if (uiCanvasRect != null)
        {
            GameObject go = new GameObject("PromotionEffect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(uiCanvasRect, false);
            effectImage = go.GetComponent<Image>();
            // assign a default runtime sprite so Image renders (avoid relying on builtin resource path)
            if (effectImage.sprite == null)
            {
                effectImage.sprite = GetOrCreateRuntimeSprite();
            }
            // 적군 프로모션은 흰색 빛기둥으로 연출
            effectImage.color = soldier.IsEnemy ? new Color(1f, 1f, 1f, 0.85f) : promotionEffectColor;
            effectRect = effectImage.rectTransform;
            effectRect.pivot = new Vector2(0.5f, 0f); // bottom align

            // Compute bottom world position of the soldier
            Vector3 worldBottom = soldier.transform.position;
            Renderer r = soldier.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                worldBottom = new Vector3(r.bounds.center.x, r.bounds.min.y, r.bounds.center.z);
            }

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCanvasRect.GetComponentInParent<Canvas>()?.worldCamera, worldBottom);
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(uiCanvasRect, screenPoint, uiCanvasRect.GetComponentInParent<Canvas>()?.worldCamera, out localPoint);
            // shift effect slightly down so its bottom aligns 16px below soldier bottom
            localPoint.y -= 16f;
            effectRect.anchoredPosition = localPoint;

            float promotionEffectHeight = soldier.IsEnemy
                ? GetEnemyEffectHeight(soldier)
                : effectHeight;
            effectRect.sizeDelta = new Vector2(0f, promotionEffectHeight);
        }

        // Sequence: expand width -> when reached, swap piece -> fade out
        Sequence seq = DOTween.Sequence();
        if (effectRect != null)
        {
            seq.Append(DOTween.To(() => effectRect.sizeDelta.x, x => { var s = effectRect.sizeDelta; s.x = x; effectRect.sizeDelta = s; }, effectTargetWidth, effectExpandDuration).SetEase(effectEase));
            if (soldier.IsEnemy)
            {
                seq.Join(effectImage.DOFade(0.85f, effectExpandDuration).SetEase(Ease.OutQuad));
            }
            seq.AppendCallback(() =>
            {
                // Swap the piece now
                PerformPromotion(soldier, promotedType);
            });
            if (effectImage != null)
            {
                seq.Append(effectImage.DOFade(0f, effectFadeDuration));
            }
            seq.OnComplete(() =>
            {
                if (effectImage != null) Destroy(effectImage.gameObject);
                // Close panel and advance turn after effect finished
                ClosePanel();
                isEnemyPromoting = false;
                isPlayerPromotingEffectRunning = false;
                if (PieceManager.Instance != null)
                {
                    PieceManager.Instance.CheckPieceCounts();
                }
                if (TurnManager.Instance != null) TurnManager.Instance.AdvanceTurn();
            });
        }
        else
        {
            // Fallback: immediate promotion
            PerformPromotion(soldier, promotedType);
            ClosePanel();
            isEnemyPromoting = false;
            isPlayerPromotingEffectRunning = false;
            if (PieceManager.Instance != null)
            {
                PieceManager.Instance.CheckPieceCounts();
            }
            if (TurnManager.Instance != null) TurnManager.Instance.AdvanceTurn();
        }
    }

    private PieceController PerformPromotion(PieceController soldier, PieceType promotedType)
    {
        if (soldier == null || !soldier.gridPosition.HasValue)
            return null;

        EnsurePromotionSealData();

        Vector2Int pos = soldier.gridPosition.Value;
        Vector3 spawnPos = soldier.transform.position;
        bool isEnemy = soldier.IsEnemy;

        // 기존 기물이 장착하고 있던 인장들 백업 (예: 카멜레온의 인장 등)
        List<SealData> existingSeals = new List<SealData>();
        if (soldier.EquippedSeals != null)
        {
            foreach (var seal in soldier.EquippedSeals)
            {
                if (seal != null && seal.Data != null)
                {
                    existingSeals.Add(seal.Data);
                }
            }
        }

        // 기존 기물 제거 (파괴 루트 사용 금지: 호리병 트리거 방지)
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.UnregisterPiece(soldier);
        }
        soldier.gridPosition = null;
        if (Application.isPlaying) Destroy(soldier.gameObject);
        else DestroyImmediate(soldier.gameObject);

        PieceController newPiece = null;

        // 새 기물 스폰
        if (PieceSpawner.Instance != null && PieceSpawner.Instance.piecePrefab != null)
        {
            GameObject newPieceObj = Instantiate(
                PieceSpawner.Instance.piecePrefab,
                spawnPos,
                Quaternion.identity,
                PieceSpawner.Instance.piecesParent
            );

            if (newPieceObj != null)
            {
                newPieceObj.name = $"PiecePrefab(Clone)@Promotion[{promotedType}_{pos}]";
                newPiece = newPieceObj.GetComponent<PieceController>();
                if (newPiece != null)
                {
                    // 새 기물 초기화 (적군 여부 올바르게 전달)
                    newPiece.Initialize(promotedType, isEnemy);
                    newPiece.MoveToGrid(pos);

                    // 기존 기물이 보유하고 있던 인장 계승 (카멜레온 인장 등)
                    foreach (var sData in existingSeals)
                    {
                        if (sData != null)
                        {
                            newPiece.EquipSeal(sData);
                        }
                    }

                    // 승급한 기물은 아군과 적군 모두 승급의 인장을 부착한다.
                    bool attachedPromotionSeal = newPiece.AttachPromotionSeal(promotionSealData);
                    if (attachedPromotionSeal)
                    {
                        newPiece.MarkPromotedByMedalThisStage();
                    }

                    if (!isEnemy)
                    {
                        CollectionManager.EnsureInstance()?.RecordPiece(promotedType);

                        string promoSymbol = PieceManager.GetPieceAlgebraicSymbol(promotedType);
                        string promoKorName = PieceManager.GetPieceTypeKoreanName(promotedType);
                        bool useSimpleTurnLog = SettingsManager.Instance != null
                            && SettingsManager.Instance.Settings != null
                            && SettingsManager.Instance.Settings.simpleTurnLog;
                        string promoLog = useSimpleTurnLog ? $"={promoSymbol}" : $"[승급] ={promoSymbol} ({promoKorName})";
                        TurnLogUIManager.Instance?.AddLog(promoLog, isEnemy);
                    }
                    else
                    {
                        CollectionManager.EnsureInstance()?.RecordPieceSeen(promotedType);
                        if (newPiece.GetComponent<EnemyPieceController>() == null)
                        {
                            newPiece.gameObject.AddComponent<EnemyPieceController>();
                        }
                    }
                    
                    if (PieceManager.Instance != null)
                    {
                        PieceManager.Instance.UpdateThreatenedStatus();
                    }
                }
            }
        }

        return newPiece;
    }

    private void EnsurePromotionSealData()
    {
        if (promotionSealData != null)
        {
            return;
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SealData", new[] { "Assets/Resources/Seal" });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            SealData candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<SealData>(path);
            if (candidate != null &&
                (candidate.sealName == "승급의 인장" ||
                 candidate.sealName == "승급자의 인장"))
            {
                promotionSealData = candidate;
                break;
            }
        }
#endif
        if (promotionSealData == null)
        {
            SealData[] seals = Resources.LoadAll<SealData>("Seal");
            foreach (SealData candidate in seals)
            {
                if (candidate != null &&
                    (candidate.sealName == "승급의 인장" ||
                     candidate.sealName == "승급자의 인장"))
                {
                    promotionSealData = candidate;
                    break;
                }
            }
        }

        if (promotionSealData == null)
        {
        }
    }

    private void ClosePanel()
    {
        if (!isOpen || promotionPanel == null)
            return;

        if (promotionPanelRect != null)
        {
            promotionPanelRect.DOKill();
            SetPromotionPanelWidth(promotionExpandedWidth);
        }

        isOpen = false;
        promotionPanel.SetActive(false);
        pendingSoldier = null;
    }

    private void SetPromotionPanelWidth(float width)
    {
        if (promotionPanelRect == null) return;
        Vector2 size = promotionPanelRect.sizeDelta;
        size.x = width;
        promotionPanelRect.sizeDelta = size;
    }

    private Tween CreatePromotionPanelWidthTween(float targetWidth)
    {
        if (promotionPanelRect == null) return DOVirtual.DelayedCall(0f, () => { });

        return DOTween.To(
            () => promotionPanelRect.sizeDelta.x,
            value => SetPromotionPanelWidth(value),
            targetWidth,
            animDuration
        );
    }

    private static Sprite runtimePromotionSprite;
    private Sprite GetOrCreateRuntimeSprite()
    {
        if (runtimePromotionSprite != null) return runtimePromotionSprite;
        Texture2D tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        runtimePromotionSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        return runtimePromotionSprite;
    }
}
