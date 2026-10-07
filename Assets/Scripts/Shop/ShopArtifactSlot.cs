using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class ShopArtifactSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    public Image iconImage;
    public TextMeshProUGUI costText;
    public Button buyButton;

    private ArtifactData artifactData;
    private bool isSoldOut = false;

    private void Start()
    {
        if (buyButton == null) buyButton = GetComponent<Button>();
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnBuyClick);
        }
        UpdateUI();
    }

    public void Setup(ArtifactData data)
    {
        artifactData = data;
        isSoldOut = false;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (isSoldOut)
        {
            if (costText != null)
            {
                costText.text = "";
                // 코스트 텍스트 배경 이미지 숨김 (슬롯 자체 이미지가 아닌 경우)
                Image costBg = costText.GetComponentInParent<Image>();
                if (costBg != null && costBg.transform != transform) costBg.enabled = false;
            }
            if (buyButton != null) buyButton.interactable = false;
            
            if (iconImage != null)
            {
                iconImage.sprite = null;
                Color c = iconImage.color;
                c.a = 0.5f;
                iconImage.color = c;
                iconImage.enabled = false;
            }

            // 판매 완료되어도 슬롯 배경(Image)은 유지
            if (GetComponent<Image>() != null) GetComponent<Image>().enabled = true;
            return;
        }

        if (GetComponent<Image>() != null) GetComponent<Image>().enabled = true;

        if (iconImage != null && artifactData != null)
        {
            iconImage.sprite = artifactData.icon;
            iconImage.enabled = true;
            iconImage.color = Color.white;
        }

        if (costText != null && artifactData != null)
        {
            costText.text = $"{artifactData.price}";
            Image costBg = costText.GetComponentInParent<Image>();
            if (costBg != null && costBg.transform != transform) costBg.enabled = true;
        }

        if (buyButton != null) buyButton.interactable = true;
    }

    private void OnBuyClick()
    {
        if (isSoldOut || artifactData == null) return;

        if (GameManager.Instance == null || ArtifactManager.Instance == null) return;

        // 돈 확인
        if (GameManager.Instance.Coin < artifactData.price)
        {
            if (buyButton != null)
            {
                buyButton.transform.DOShakePosition(0.5f, new Vector3(5f, 0, 0), 20, 90, false, true);
            }
            return;
        }

        // 구매 처리
        if (GameManager.Instance.UseCoin(artifactData.price))
        {
            GameManager.Instance.RecordPurchase();
            FlyArtifactToInventory();
            SetSoldOut();
            
            // 구매 연출
            transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1);
        }
    }

    private void FlyArtifactToInventory()
    {
        if (iconImage == null || ArtifactManager.Instance == null) return;

        // 1. Create flying icon
        GameObject flyingIcon = new GameObject("FlyingArtifact");
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            flyingIcon.transform.SetParent(canvas.transform, true);
        }
        else
        {
            flyingIcon.transform.SetParent(transform.parent, true);
        }
        
        flyingIcon.transform.position = iconImage.transform.position;
        flyingIcon.transform.localScale = iconImage.transform.localScale;

        Image img = flyingIcon.AddComponent<Image>();
        img.sprite = iconImage.sprite;
        img.color = iconImage.color;
        img.raycastTarget = false;

        // 2. Find target position
        Vector3 targetPos = transform.position; 
        
        int targetIndex = ArtifactManager.Instance.OwnedArtifacts.Count;
        if (ArtifactManager.Instance.artifactSlots != null && targetIndex < ArtifactManager.Instance.artifactSlots.Count)
        {
            if (ArtifactManager.Instance.artifactSlots[targetIndex] != null)
            {
                targetPos = ArtifactManager.Instance.artifactSlots[targetIndex].transform.position;
            }
        }

        // 3. Animate
        Sequence seq = DOTween.Sequence();
        seq.Append(flyingIcon.transform.DOMove(targetPos, 0.7f).SetEase(Ease.InOutQuad));
        seq.Join(flyingIcon.transform.DOScale(0.3f, 0.7f));
        
        // 완료 시 데이터 추가 및 시각화 업데이트
        seq.OnComplete(() =>
        {
            if (ArtifactManager.Instance != null)
            {
                ArtifactManager.Instance.AddArtifact(artifactData);
            }
            Destroy(flyingIcon);
        });
    }

    private void SetSoldOut()
    {
        isSoldOut = true;
        UpdateUI();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSoldOut) return;

        if (artifactData != null && TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowTooltip(artifactData.GetTooltipTitle(), artifactData.GetTooltipDescription(), transform.position, artifactData.flavorText);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}
