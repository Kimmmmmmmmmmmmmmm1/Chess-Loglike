using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 유물 효과 발동 시 마인크래프트 '불사의 토템' 스타일로
/// 유물 아이콘의 복제본을 화면 하단에서 중앙으로 팝업시키는 연출 컴포넌트입니다.
/// 씬에 배치하지 않아도 ArtifactPopupEffect.Play(...)로 즉시 호출할 수 있습니다.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class ArtifactPopupEffect : MonoBehaviour
{
    public static ArtifactPopupEffect Instance { get; private set; }

    [Header("Components")]
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image iconImage;

    [Header("Clone Settings")]
    [Tooltip("체크 시 매 발동마다 아이콘 복제본(Clone)을 생성하여 연출 후 파괴합니다.")]
    [SerializeField] private bool createCloneOnPlay = true;
    [Tooltip("복제본 아이콘의 기본 UI 크기 (슬롯 아이콘이 작을 때 중앙 연출용 해상도 크기)")]
    [SerializeField] private Vector2 cloneBaseSize = new Vector2(128f, 128f);

    [Header("Position & Scale")]
    [SerializeField] private Vector2 startOffset = new Vector2(0f, -600f); // 화면 하단 시작 위치
    [SerializeField] private Vector2 targetPos = Vector2.zero;             // 화면 중앙 목표 위치
    [SerializeField] private float startScale = 0.3f;
    [SerializeField] private float targetScale = 1.2f;
    [SerializeField] private Vector3 startEulerAngles = new Vector3(0f, 180f, -25f);
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 0.75f;        // 최대 불투명도 (기본 0.75)

    [Header("Timing & Easing")]
    [SerializeField] private float popDuration = 0.45f;     // 튀어오르는 시간
    [SerializeField] private float holdDuration = 0.25f;    // 중앙에서 멈춰있는 시간
    [SerializeField] private float fadeDuration = 0.35f;    // 사라지는 시간
    [SerializeField] private float overshoot = 1.5f;        // 탄성 강도 (OutBack)
    [SerializeField] private bool ignoreTimeScale = true;   // Time.timeScale = 0 이어도 작동 여부

    private Sequence _totemSequence;

    private void Reset()
    {
        iconRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        iconImage = GetComponentInChildren<Image>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (iconRect == null) iconRect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (iconImage == null) iconImage = GetComponentInChildren<Image>();
    }

    private void OnDestroy()
    {
        _totemSequence?.Kill();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region Static Helper API

    public static ArtifactPopupEffect EnsureInstance()
    {
        if (Instance != null) return Instance;

        Canvas rootCanvas = FindTargetCanvas();
        GameObject hostObj = new GameObject(nameof(ArtifactPopupEffect), typeof(RectTransform), typeof(CanvasGroup));
        if (rootCanvas != null)
        {
            hostObj.transform.SetParent(rootCanvas.transform, false);
            hostObj.transform.SetAsLastSibling();
        }

        RectTransform rt = hostObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(128f, 128f);

        CanvasGroup cg = hostObj.GetComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        Image img = hostObj.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = false; // 복제본 생성 모드에서는 원본 호스트 이미지를 숨김

        Instance = hostObj.AddComponent<ArtifactPopupEffect>();
        return Instance;
    }

    /// <summary>
    /// 유물 ID로 보유 중인 유물을 찾아 불사의 토템 팝업 연출을 재생합니다.
    /// </summary>
    public static void Play(string artifactId, Action onComplete = null)
    {
        if (string.IsNullOrEmpty(artifactId) || ArtifactManager.Instance == null)
        {
            onComplete?.Invoke();
            return;
        }

        ArtifactData artifact = ArtifactManager.Instance.GetOwnedArtifact(artifactId);
        if (artifact == null)
        {
            onComplete?.Invoke();
            return;
        }

        Play(artifact, onComplete);
    }

    /// <summary>
    /// 유물 데이터(ArtifactData)의 아이콘 복제본으로 불사의 토템 팝업 연출을 재생합니다.
    /// </summary>
    public static void Play(ArtifactData artifact, Action onComplete = null)
    {
        if (artifact == null || artifact.icon == null)
        {
            onComplete?.Invoke();
            return;
        }

        Image slotIcon = FindSlotIconImage(artifact);
        EnsureInstance().PlayTotemEffect(artifact.icon, slotIcon, onComplete);
    }

    /// <summary>
    /// 지정한 스프라이트로 불사의 토템 팝업 연출을 재생합니다.
    /// </summary>
    public static void Play(Sprite iconSprite, Action onComplete = null)
    {
        if (iconSprite == null)
        {
            onComplete?.Invoke();
            return;
        }

        EnsureInstance().PlayTotemEffect(iconSprite, null, onComplete);
    }

    /// <summary>
    /// 버프 스택(보호, 회피, 역습 등) 소모 시 해당 기물의 위치를 중심으로
    /// 유물 토템 스타일의 소형 팝업 연출을 재생합니다.
    /// </summary>
    public static void PlayBuffPop(PieceBuffType buffType, Vector3 worldPosition, Action onComplete = null)
    {
        Sprite buffSprite = PieceSpriteData.Instance != null 
            ? PieceSpriteData.Instance.GetBuffSprite(buffType) 
            : null;

        if (buffSprite == null)
        {
            buffSprite = Resources.Load<Sprite>($"BuffIcon/Buff_{buffType}");
        }

        if (buffSprite == null)
        {
            onComplete?.Invoke();
            return;
        }

        EnsureInstance().PlayBuffPopInternal(buffSprite, worldPosition, onComplete);
    }

    /// <summary>
    /// 지정한 위치(기물 등)를 중심으로 유물 토템 스타일의 소형 팝업 연출을 재생합니다.
    /// </summary>
    public static void PlayAtPosition(Sprite iconSprite, Vector3 worldPosition, Action onComplete = null)
    {
        if (iconSprite == null)
        {
            onComplete?.Invoke();
            return;
        }

        EnsureInstance().PlayBuffPopInternal(iconSprite, worldPosition, onComplete);
    }

    #endregion

    #region Instance API

    /// <summary>
    /// 현재 할당된 컴포넌트(또는 복제본)로 토템 팝업 연출을 실행합니다.
    /// </summary>
    public void PlayTotemEffect()
    {
        Sprite sprite = iconImage != null ? iconImage.sprite : null;
        PlayTotemEffect(sprite, iconImage, null);
    }

    public void PlayTotemEffect(ArtifactData artifact, Action onComplete = null)
    {
        if (artifact == null)
        {
            onComplete?.Invoke();
            return;
        }

        Image slotIcon = FindSlotIconImage(artifact);
        PlayTotemEffect(artifact.icon, slotIcon, onComplete);
    }

    public void PlayTotemEffect(Sprite sprite, Image sourceIconImage = null, Action onComplete = null)
    {
        if (createCloneOnPlay)
        {
            PlayWithClone(sprite, sourceIconImage, onComplete);
            return;
        }

        PlayOnSelf(sprite, onComplete);
    }

    #endregion

    private void PlayWithClone(Sprite sprite, Image sourceIconImage, Action onComplete)
    {
        Canvas targetCanvas = GetComponentInParent<Canvas>() ?? FindTargetCanvas();
        if (targetCanvas == null)
        {
            onComplete?.Invoke();
            return;
        }

        GameObject cloneObj;
        Image cloneImage;

        // 원본 슬롯의 아이콘 Image가 있으면 복제(Instantiate)하여 머티리얼/색상/비율 설정을 유지
        if (sourceIconImage != null && sourceIconImage.gameObject != this.gameObject)
        {
            cloneObj = Instantiate(sourceIconImage.gameObject, targetCanvas.transform, false);
            cloneObj.name = $"ArtifactTotemPopup_{sourceIconImage.name}_Clone";
            cloneImage = cloneObj.GetComponent<Image>();
        }
        else
        {
            cloneObj = new GameObject("ArtifactTotemPopup_Clone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cloneObj.transform.SetParent(targetCanvas.transform, false);
            cloneImage = cloneObj.GetComponent<Image>();
        }

        cloneObj.SetActive(true);
        cloneObj.transform.SetAsLastSibling();

        RectTransform cloneRect = cloneObj.GetComponent<RectTransform>();
        cloneRect.anchorMin = new Vector2(0.5f, 0.5f);
        cloneRect.anchorMax = new Vector2(0.5f, 0.5f);
        cloneRect.pivot = new Vector2(0.5f, 0.5f);
        cloneRect.sizeDelta = cloneBaseSize;

        if (cloneImage != null)
        {
            if (sprite != null)
            {
                cloneImage.sprite = sprite;
            }
            cloneImage.preserveAspect = true;
            cloneImage.raycastTarget = false;
            cloneImage.color = Color.white;
            cloneImage.enabled = true;
        }

        CanvasGroup cloneCanvasGroup = cloneObj.GetComponent<CanvasGroup>();
        if (cloneCanvasGroup == null)
        {
            cloneCanvasGroup = cloneObj.AddComponent<CanvasGroup>();
        }
        cloneCanvasGroup.interactable = false;
        cloneCanvasGroup.blocksRaycasts = false;

        Sequence seq = BuildTotemSequence(cloneRect, cloneCanvasGroup);
        seq.OnComplete(() =>
        {
            if (cloneObj != null)
            {
                Destroy(cloneObj);
            }
            onComplete?.Invoke();
        });
    }

    private void PlayBuffPopInternal(Sprite sprite, Vector3 worldPosition, Action onComplete)
    {
        Canvas targetCanvas = GetComponentInParent<Canvas>() ?? FindTargetCanvas();
        if (targetCanvas == null)
        {
            onComplete?.Invoke();
            return;
        }

        Transform parentTransform = (EffectManager.Instance != null && EffectManager.Instance.debrisParent != null)
            ? EffectManager.Instance.debrisParent
            : targetCanvas.transform;

        GameObject cloneObj = new GameObject("BuffTotemPopup_Clone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cloneObj.transform.SetParent(parentTransform, false);
        cloneObj.transform.position = new Vector3(worldPosition.x, worldPosition.y, parentTransform.position.z);
        cloneObj.transform.SetAsLastSibling();

        RectTransform cloneRect = cloneObj.GetComponent<RectTransform>();
        cloneRect.anchorMin = new Vector2(0.5f, 0.5f);
        cloneRect.anchorMax = new Vector2(0.5f, 0.5f);
        cloneRect.pivot = new Vector2(0.5f, 0.5f);

        // 기물 크기 대비 적당하고 조그마한 UI 크기 (48x48)
        Vector2 buffPopBaseSize = new Vector2(48f, 48f);
        cloneRect.sizeDelta = buffPopBaseSize;

        Image cloneImage = cloneObj.GetComponent<Image>();
        cloneImage.sprite = sprite;
        cloneImage.preserveAspect = true;
        cloneImage.raycastTarget = false;
        cloneImage.color = Color.white;
        cloneImage.enabled = true;

        CanvasGroup cloneCanvasGroup = cloneObj.AddComponent<CanvasGroup>();
        cloneCanvasGroup.interactable = false;
        cloneCanvasGroup.blocksRaycasts = false;
        cloneCanvasGroup.alpha = 0.95f;

        Vector2 baseAnchoredPos = cloneRect.anchoredPosition;
        Vector2 startPos = baseAnchoredPos + new Vector2(0f, -20f);
        Vector2 peakPos = baseAnchoredPos + new Vector2(0f, 22f);
        Vector2 fadePos = peakPos + new Vector2(0f, 24f);

        cloneRect.anchoredPosition = startPos;
        cloneRect.localScale = Vector3.one * 0.35f;
        cloneRect.localEulerAngles = new Vector3(0f, 180f, -20f);

        float buffPopDuration = 0.35f;
        float buffHoldDuration = 0.18f;
        float buffFadeDuration = 0.28f;

        Sequence seq = DOTween.Sequence();

        // 1. Pop-up: 아래에서 기물 상단으로 튀어오름 + 확대 + 3D 반바퀴 회전 복구
        seq.Append(
            cloneRect.DOAnchorPos(peakPos, buffPopDuration)
                .SetEase(Ease.OutBack, 1.4f)
        );
        seq.Join(
            cloneRect.DOScale(1.15f, buffPopDuration)
                .SetEase(Ease.OutBack, 1.4f)
        );
        seq.Join(
            cloneRect.DOLocalRotate(Vector3.zero, buffPopDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic)
        );

        // 2. Hold: 정점에서 살짝 커지며 강조
        seq.Append(
            cloneRect.DOScale(1.22f, buffHoldDuration)
                .SetEase(Ease.OutQuad)
        );

        // 3. Fade-out: 위로 살짝 더 떠오르며 투명해짐
        seq.Append(
            cloneCanvasGroup.DOFade(0f, buffFadeDuration)
                .SetEase(Ease.InQuad)
        );
        seq.Join(
            cloneRect.DOAnchorPosY(fadePos.y, buffFadeDuration)
                .SetEase(Ease.InQuad)
        );
        seq.Join(
            cloneRect.DOScale(0.85f, buffFadeDuration)
                .SetEase(Ease.InQuad)
        );

        seq.SetUpdate(ignoreTimeScale);
        seq.SetLink(cloneObj, LinkBehaviour.KillOnDestroy);

        seq.OnComplete(() =>
        {
            if (cloneObj != null)
            {
                Destroy(cloneObj);
            }
            onComplete?.Invoke();
        });
    }

    private void PlayOnSelf(Sprite sprite, Action onComplete)
    {
        _totemSequence?.Kill();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (iconImage != null)
        {
            if (sprite != null)
            {
                iconImage.sprite = sprite;
            }
            iconImage.enabled = true;
            iconImage.raycastTarget = false;
        }

        _totemSequence = BuildTotemSequence(iconRect, canvasGroup);
        _totemSequence.OnComplete(() =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    private Sequence BuildTotemSequence(RectTransform targetRect, CanvasGroup targetCanvasGroup)
    {
        // 초기 상태 세팅 (화면 아래, 축소 상태, 약간 기울어짐)
        targetRect.anchoredPosition = startOffset;
        targetRect.localScale = Vector3.one * startScale;
        targetRect.localEulerAngles = startEulerAngles;
        targetCanvasGroup.alpha = maxAlpha;

        Sequence seq = DOTween.Sequence();

        // 1. Pop-up: 하단에서 중앙으로 튀어오름 + 스케일 확대 + 3D 플립 회전
        seq.Append(
            targetRect.DOAnchorPos(targetPos, popDuration)
                .SetEase(Ease.OutBack, overshoot)
        );
        seq.Join(
            targetRect.DOScale(targetScale, popDuration)
                .SetEase(Ease.OutBack, overshoot)
        );
        // 마크 특유의 입체감을 주는 Y축 반바퀴 회전 + Z축 각도 복구
        seq.Join(
            targetRect.DOLocalRotate(Vector3.zero, popDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic)
        );

        // 2. Hold: 정점에서 잠깐 머무르며 살짝만 더 커짐 (강조 효과)
        seq.Append(
            targetRect.DOScale(targetScale * 1.08f, holdDuration)
                .SetEase(Ease.OutQuad)
        );

        // 3. Fade-out: 위로 살짝 떠오르며 투명해짐
        seq.Append(
            targetCanvasGroup.DOFade(0f, fadeDuration)
                .SetEase(Ease.InQuad)
        );
        seq.Join(
            targetRect.DOAnchorPosY(targetPos.y + 80f, fadeDuration)
                .SetEase(Ease.InQuad)
        );
        seq.Join(
            targetRect.DOScale(targetScale * 0.85f, fadeDuration)
                .SetEase(Ease.InQuad)
        );

        // 일시정지 대응(TimeScale 무시 여부 선택)
        seq.SetUpdate(ignoreTimeScale);
        seq.SetLink(targetRect.gameObject, LinkBehaviour.KillOnDestroy);

        return seq;
    }

    private static Image FindSlotIconImage(ArtifactData artifact)
    {
        if (ArtifactManager.Instance == null || artifact == null) return null;

        ArtifactSlot slot = ArtifactManager.Instance.GetSlotOf(artifact);
        if (slot == null) return null;

        return slot.IconImage != null ? slot.IconImage : slot.GetComponentInChildren<Image>();
    }

    private static Canvas FindTargetCanvas()
    {
        if (EffectManager.Instance != null && EffectManager.Instance.debrisParent != null)
        {
            Canvas effectCanvas = EffectManager.Instance.debrisParent.GetComponentInParent<Canvas>();
            if (effectCanvas != null) return effectCanvas.rootCanvas;
        }

        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        Scene activeScene = SceneManager.GetActiveScene();

        foreach (Canvas c in canvases)
        {
            if (c != null && c.gameObject.activeInHierarchy && c.gameObject.scene == activeScene && c.isRootCanvas)
            {
                return c;
            }
        }

        foreach (Canvas c in canvases)
        {
            if (c != null && c.gameObject.activeInHierarchy && c.isRootCanvas)
            {
                return c;
            }
        }

        return FindFirstObjectByType<Canvas>();
    }
}
