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
    private ArtifactData data;
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
                nameText.text = artifact.IsActive
                    ? (artifact.StackCount > 1 ? $"({artifact.StackCount})" : string.Empty)
                    : artifact.Level.ToString();
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

        iconImage.color = isDimmed ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
    }

    public void ShowTooltip()
    {
        if (data != null && TooltipManager.Instance != null)
        {
            string title = data.GetTooltipTitle();
            string description = data.GetTooltipDescription();

            TooltipManager.Instance.ShowTooltip(title, description, transform.position, data.flavorText, TooltipManager.TooltipPriorityDefault, gameObject);
        }
    }

    public void HideTooltip()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }
    }

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
