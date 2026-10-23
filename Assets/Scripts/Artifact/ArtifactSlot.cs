using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DG.Tweening;

public class ArtifactSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField]
    private Image iconImage;
    public Image IconImage => iconImage;
    [SerializeField]
    private TextMeshProUGUI nameText;
    [SerializeField]
    private LayoutGroup CountContainer;
    [SerializeField]
    private ArtifactCount CountPrefab;
    private Image cooldownOverlayImage;
    private TextMeshProUGUI cooldownText;
    private ArtifactData data;
    public ArtifactData Artifact => data;
    private Vector2 originalSize;
    private bool isInitialized = false;
    private readonly List<ArtifactCount> countInstances = new List<ArtifactCount>();
    

    private void Awake()
    {
        if (iconImage != null && !isInitialized)
        {
            originalSize = iconImage.rectTransform.sizeDelta;
            isInitialized = true;
        }
    }

    public void Initialize(ArtifactData artifact)
    {
        data = artifact;
        if ((iconImage != null)&&(nameText != null))
        {
            if (!isInitialized)
            {
                originalSize = iconImage.rectTransform.sizeDelta;
                isInitialized = true;
            }

            if (artifact != null)
            {
                iconImage.gameObject.SetActive(true);
                iconImage.sprite = artifact.icon;
                iconImage.preserveAspect = true;
                iconImage.rectTransform.sizeDelta = new Vector2(24, 24);
                iconImage.color = Color.white;
                if (artifact.IsActive)
                {
                    string stackText = artifact.StackCount.ToString();
                    string levelText = artifact.CanEnhanceWhenActive ? $"Lv.{artifact.Level}" : string.Empty;
                    nameText.text = string.IsNullOrEmpty(levelText) ? stackText : $"{stackText} {levelText}".Trim();
                }
                else
                {
                    nameText.text = artifact.Level.ToString();
                }
            }
            else
            {
                iconImage.sprite = null;
                iconImage.rectTransform.sizeDelta = originalSize;
                iconImage.color = Color.white;
                iconImage.gameObject.SetActive(false);
                nameText.text = string.Empty;
            }

            if (CountContainer != null)
            {
                bool showGauge = data != null && data.isGaugeRequired && CountPrefab != null;
                CountContainer.gameObject.SetActive(showGauge);

                if (showGauge)
                {
                    int current = ArtifactEffectHandlers.GetArtifactGaugeCurrentCount(data.id);
                    int max = ArtifactEffectHandlers.GetArtifactGaugeMaxCount(data.id);
                    RefreshCountUI(CountContainer, countInstances, current, max);
                }
                else
                {
                    ClearCountInstances(countInstances);
                }
            }
        }
    }

    private void RefreshCountUI(LayoutGroup container, List<ArtifactCount> instances, int current, int max)
    {
        int safeMax = Mathf.Max(0, max);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);

        if (safeMax <= 0)
        {
            ClearCountInstances(instances);
            return;
        }

        int requiredPrefabCount = safeMax <= 3 ? safeMax : Mathf.CeilToInt(safeMax / 2f);
        EnsureCountInstances(container, instances, requiredPrefabCount);

        if (safeMax <= 3)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] == null)
                {
                    continue;
                }

                instances[i].SetFillCount(i < safeCurrent ? 2 : 0);
            }
            return;
        }

        int remaining = safeCurrent;
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] == null)
            {
                continue;
            }

            int fill = Mathf.Clamp(remaining, 0, 2);
            instances[i].SetFillCount(fill);
            remaining -= 2;
        }
    }

    private void EnsureCountInstances(LayoutGroup container, List<ArtifactCount> instances, int count)
    {
        while (instances.Count < count)
        {
            ArtifactCount created = Instantiate(CountPrefab, container.transform);
            instances.Add(created);
        }

        while (instances.Count > count)
        {
            int lastIndex = instances.Count - 1;
            ArtifactCount target = instances[lastIndex];
            instances.RemoveAt(lastIndex);

            if (target != null)
            {
                Destroy(target.gameObject);
            }
        }
    }

    private void ClearCountInstances(List<ArtifactCount> instances)
    {
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null)
            {
                Destroy(instances[i].gameObject);
            }
        }

        instances.Clear();
    }

    private void OnDestroy()
    {
        ClearCountInstances(countInstances);
    }

    public void SetDimmed(bool isDimmed)
    {
        if (iconImage == null || data == null)
        {
            return;
        }

        if (cooldownOverlayImage != null) cooldownOverlayImage.gameObject.SetActive(false);
        if (cooldownText != null) cooldownText.gameObject.SetActive(false);
        iconImage.color = isDimmed ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
    }

    public void SetCooldownDim(float remainingRatio, int turnsRemaining)
    {
        if (iconImage == null || data == null) return;

        float fill = Mathf.Clamp01(remainingRatio);
        iconImage.color = Color.white;
        EnsureCooldownOverlay();
        cooldownOverlayImage.sprite = iconImage.sprite;
        cooldownOverlayImage.preserveAspect = iconImage.preserveAspect;
        cooldownOverlayImage.fillAmount = fill;
        cooldownOverlayImage.gameObject.SetActive(fill > 0f);
        cooldownText.text = turnsRemaining.ToString();
        cooldownText.gameObject.SetActive(turnsRemaining > 0);
    }

    private void EnsureCooldownOverlay()
    {
        if (cooldownOverlayImage != null) return;

        GameObject overlay = new GameObject("CooldownDimOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(iconImage.transform, false);

        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        cooldownOverlayImage = overlay.GetComponent<Image>();
        cooldownOverlayImage.type = Image.Type.Filled;
        cooldownOverlayImage.fillMethod = Image.FillMethod.Vertical;
        cooldownOverlayImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        cooldownOverlayImage.color = new Color(0f, 0f, 0f, 0.72f);
        cooldownOverlayImage.raycastTarget = false;
        cooldownOverlayImage.gameObject.SetActive(false);

        GameObject textObject = new GameObject("CooldownTurnsText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(iconImage.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        cooldownText = textObject.GetComponent<TextMeshProUGUI>();
        if (nameText != null) cooldownText.font = nameText.font;
        cooldownText.fontSize = 14f;
        cooldownText.fontStyle = FontStyles.Bold;
        cooldownText.alignment = TextAlignmentOptions.Center;
        cooldownText.color = Color.white;
        cooldownText.raycastTarget = false;
        Shadow textShadow = textObject.AddComponent<Shadow>();
        textShadow.effectColor = Color.black;
        textShadow.effectDistance = new Vector2(1f, -1f);
        cooldownText.gameObject.SetActive(false);
    }

    public void ShowTooltip()
    {
        if (data != null && TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowArtifactTooltip(data, transform.position, gameObject, TooltipManager.TooltipPriorityDefault);
        }
    }

    public void HideTooltip()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }
    }

    private void OnDisable() => HideTooltip();

    public void OnPointerEnter(PointerEventData eventData) => ShowTooltip();

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    /// <summary>
    /// 유물 클릭 시 강화 패널에 선택 / 액티브 유물이면 사용
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (data == null) return;

        // 강화 패널이 열려있으면 유물 선택
        if (ArtifactInhanceManager.Instance != null && 
            ArtifactInhanceManager.Instance.inhancePanel != null &&
            ArtifactInhanceManager.Instance.inhancePanel.activeInHierarchy)
        {
            ArtifactInhanceManager.Instance.SetSelectedArtifact(data);
            return;
        }

        // 액티브 유물 사용 (좌클릭만)
        if (data.IsActive && eventData.button == PointerEventData.InputButton.Left)
        {
            ActiveArtifactController.EnsureInstance().TryActivate(data, this);
        }
    }

    /// <summary>
    /// 지금 사용할 수 없을 때 흔들림 피드백
    /// </summary>
    public void PlayDeniedFeedback()
    {
        transform.DOComplete();
        transform.DOShakePosition(0.3f, new Vector3(5f, 0f, 0f), 20, 90, false, true);
    }

    /// <summary>
    /// 사용 성공 피드백
    /// </summary>
    public void PlayUseFeedback()
    {
        transform.DOComplete();
        transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 10, 1);
    }

    /// <summary>
    /// 대상 선택 모드 중 슬롯 강조
    /// </summary>
    public void SetTargetingVisual(bool isTargeting)
    {
        if (iconImage == null) return;

        Transform t = iconImage.transform;
        t.DOKill();
        t.localScale = Vector3.one;

        if (isTargeting)
        {
            t.DOScale(1.25f, 0.35f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
    }
    
    public void RefreshUI()
    {
        if (data != null)
        {
            Initialize(data);
        }
        else
        {
            Initialize(null);
        }
    }
}
