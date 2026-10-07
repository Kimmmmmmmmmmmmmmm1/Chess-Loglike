using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using DG.Tweening;

/// <summary>
/// 졸 승급을 관리하는 매니저
/// A006: 전장의 훈장 유물과 연동
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
    public Color promotionEffectColor = Color.white;
    public float effectHeight = 128f;
    public float effectTargetWidth = 48f;
    public float effectExpandDuration = 0.36f;
    public float effectFadeDuration = 0.4f;
    public Ease effectEase = Ease.OutQuad;
    private PieceController pendingSoldier;        // 승급 대기 중인 졸
    private bool isOpen = false;

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
    /// 프로모션 패널이 열려있는지 확인
    /// </summary>
    public bool IsPromotionPanelOpen() => isOpen;

    private void Start()
    {
        EnsurePromotionSealData();

        if (promotionPanel != null)
        {
            promotionPanelRect = promotionPanel.GetComponent<RectTransform>();
            SetPromotionPanelWidth(promotionCollapsedWidth);
            promotionPanel.SetActive(false);
        }

        // 버튼 리스너 등록
        if (horseButton != null)
            horseButton.onClick.AddListener(() => OnPromotionSelected(PieceType.Horse));
        
        if (elephantButton != null)
            elephantButton.onClick.AddListener(() => OnPromotionSelected(PieceType.Elephant));
        
        if (chariotButton != null)
            chariotButton.onClick.AddListener(() => OnPromotionSelected(PieceType.Chariot));
        
        if (cannonButton != null)
            cannonButton.onClick.AddListener(() => OnPromotionSelected(PieceType.Cannon));
        
    }

    /// <summary>
    /// 졸이 적 진영 끝 줄에 도달했을 때 호출
    /// </summary>
    public void ShowPromotionPanel(PieceController soldier)
    {
        if (soldier == null || soldier.Type != PieceType.Soldier || soldier.IsEnemy)
        {
            return;
        }

        if (!ArtifactEffectHandlers.HasMedalPromotionRemaining())
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
            SetPromotionPanelWidth(promotionCollapsedWidth);
            CreatePromotionPanelWidthTween(promotionExpandedWidth).SetEase(openEase);
        }
        else
        {
            promotionPanel.SetActive(true);
        }

        if (titleText != null)
            titleText.text = "기물 승급";

        if (descriptionText != null)
            descriptionText.text = "승급할 기물을 선택하세요.";

    }

    private void OnPromotionSelected(PieceType promotedType)
    {
        if (pendingSoldier == null)
        {
            return;
        }

        // 유물 효과 적용
        bool success = ArtifactEffectHandlers.TryMedalPromotion(pendingSoldier, promotedType);

        if (!success)
        {
            return;
        }

        // Play promotion effect at the soldier's position, then perform the actual promotion when effect reaches target width
        StartPromotionEffect(pendingSoldier, promotedType);
    }

    private void StartPromotionEffect(PieceController soldier, PieceType promotedType)
    {
        if (soldier == null)
        {
            return;
        }

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
            effectImage.color = promotionEffectColor;
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

            effectRect.sizeDelta = new Vector2(0f, effectHeight);
        }

        // Sequence: expand width -> when reached, swap piece -> fade out
        Sequence seq = DOTween.Sequence();
        if (effectRect != null)
        {
            seq.Append(DOTween.To(() => effectRect.sizeDelta.x, x => { var s = effectRect.sizeDelta; s.x = x; effectRect.sizeDelta = s; }, effectTargetWidth, effectExpandDuration).SetEase(effectEase));
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
                if (TurnManager.Instance != null) TurnManager.Instance.AdvanceTurn();
            });
        }
        else
        {
            // Fallback: immediate promotion
            PerformPromotion(soldier, promotedType);
            ClosePanel();
            if (TurnManager.Instance != null) TurnManager.Instance.AdvanceTurn();
        }
    }

    private void PerformPromotion(PieceController soldier, PieceType promotedType)
    {
        if (soldier == null || !soldier.gridPosition.HasValue)
            return;

        EnsurePromotionSealData();

        Vector2Int pos = soldier.gridPosition.Value;
        Vector3 spawnPos = soldier.transform.position;

        // 기존 졸 제거 (파괴 루트 사용 금지: 호리병 트리거 방지)
        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.UnregisterPiece(soldier);
        }
        soldier.gridPosition = null;
        if (Application.isPlaying) Destroy(soldier.gameObject);
        else DestroyImmediate(soldier.gameObject);

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
                PieceController newPiece = newPieceObj.GetComponent<PieceController>();
                if (newPiece != null)
                {
                    // 새 기물 초기화
                    newPiece.Initialize(promotedType, false);
                    newPiece.MoveToGrid(pos);

                    bool attached = newPiece.AttachPromotionSeal(promotionSealData);
                    if (!attached)
                    {
                    }

                    newPiece.MarkPromotedByMedalThisStage();
                    
                    if (PieceManager.Instance != null)
                    {
                        PieceManager.Instance.UpdateThreatenedStatus();
                    }
                }
            }
        }
    }

    private void EnsurePromotionSealData()
    {
        if (promotionSealData != null)
        {
            return;
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SealData", new[] { "Assets/Data/Seal" });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            SealData candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<SealData>(path);
            if (candidate != null &&
                (candidate.sealName == "승급의 인장" || candidate.sealName == "승급자의 인장"))
            {
                promotionSealData = candidate;
                break;
            }
        }
#else
        SealData[] seals = Resources.LoadAll<SealData>("Seal");
        foreach (SealData candidate in seals)
        {
            if (candidate != null &&
                (candidate.sealName == "승급의 인장" || candidate.sealName == "승급자의 인장"))
            {
                promotionSealData = candidate;
                break;
            }
        }
#endif

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
            CreatePromotionPanelWidthTween(promotionCollapsedWidth)
                .SetEase(closeEase)
                .OnComplete(() =>
                {
                    promotionPanel.SetActive(false);
                    pendingSoldier = null;
                    isOpen = false;
                });
        }
        else
        {
            isOpen = false;
            promotionPanel.SetActive(false);
            pendingSoldier = null;
        }
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
