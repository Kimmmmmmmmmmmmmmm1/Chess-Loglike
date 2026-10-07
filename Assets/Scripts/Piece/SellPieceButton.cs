using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

public class SellPieceButton : MonoBehaviour, IPointerExitHandler, IPointerEnterHandler
{
    private PieceController pieceController;
    private Button button;
    private RectTransform rectTransform;
    private Vector2 startAnchoredPos;
    private bool isClosing = false;
    private Coroutine closeCoroutine;
    [SerializeField] private ShopPieceData shopPieceData;
    [SerializeField] private TextMeshProUGUI priceText;

    void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();

        if (priceText == null)
        {
            priceText = GetComponentInChildren<TextMeshProUGUI>();
        }
        
        if (button != null)
        {
            button.onClick.AddListener(OnSellClicked);
        }
    }

    public void Initialize(PieceController piece)
    {
        this.pieceController = piece;

        if (priceText != null)
        {
            int price = GetPiecePrice(piece.Type);
            int sellPrice = price / 2;
            priceText.text = $"+{sellPrice}";
        }
        Show();
    }

    private void Show()
    {
        // 1. 기물보다 뒤에 그려지도록 형제 순서를 가장 처음으로 변경 (기물 이미지가 형제일 경우 뒤로 감)
        transform.SetAsFirstSibling();

        // 2. 오른쪽으로 서서히 나오는 애니메이션
        if (rectTransform != null)
        {
            startAnchoredPos = rectTransform.anchoredPosition;
            rectTransform.localScale = Vector3.zero;

            // 애니메이션: 오른쪽(x=60)으로 이동하며 크기가 커짐
            rectTransform.DOKill();
            Sequence seq = DOTween.Sequence();
            seq.Append(rectTransform.DOAnchorPos(startAnchoredPos + new Vector2(30f, 0f), 0.3f).SetEase(Ease.OutBack));
            seq.Join(rectTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CancelClose();
    }

    public void CancelClose()
    {
        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
            closeCoroutine = null;
        }
    }

    public void Close(float delay = 0f)
    {
        if (isClosing) return;
        
        if (delay > 0f)
        {
            if (closeCoroutine != null) StopCoroutine(closeCoroutine);
            closeCoroutine = StartCoroutine(CloseRoutine(delay));
        }
        else
        {
            PerformClose();
        }
    }

    private IEnumerator CloseRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        PerformClose();
    }

    private void PerformClose()
    {
        if (isClosing) return;
        isClosing = true;

        if (rectTransform != null)
        {
            rectTransform.DOKill();
            Sequence seq = DOTween.Sequence();
            // 반대로 쏙 들어가는 애니메이션 (원래 위치로 복귀)
            seq.Append(rectTransform.DOAnchorPos(startAnchoredPos, 0.3f).SetEase(Ease.InBack));
            seq.Join(rectTransform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
            
            seq.OnComplete(() =>
            {
                Destroy(gameObject);
            });
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSellClicked()
    {
        if (pieceController != null)
        {
            SellPiece(pieceController);
        }
    }

    // 마우스가 버튼 영역을 벗어날 때 호출됩니다.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (pieceController != null)
        {
            // 마우스가 이동한 대상이 기물(형제)이거나 기물의 자식이라면 버튼을 끄지 않습니다.
            GameObject enteredObj = eventData.pointerEnter;
            if (enteredObj != null && (enteredObj == pieceController.gameObject || enteredObj.transform.IsChildOf(pieceController.transform)))
            {
                return;
            }
            
            Close();
        }
    }

    private void SellPiece(PieceController piece)
    {
        if (piece == null || piece.IsEnemy) return;

        int price = GetPiecePrice(piece.Type);
        int sellPrice = price / 2;

        AnimateCoinFly(sellPrice);

        if (PieceManager.Instance != null)
        {
            PieceManager.Instance.UnregisterPiece(piece);
            if (PieceManager.Instance.IsSelected(piece))
            {
                PieceManager.Instance.ClearSelection();
            }
        }

        Destroy(piece.gameObject);
        Close();
    }

    private void AnimateCoinFly(int amount)
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.coinText == null)
        {
            GameManager.Instance.AddCoin(amount);
            return;
        }

        GameObject flyObj = new GameObject("FlyingCoin");
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            flyObj.transform.SetParent(rootCanvas.transform, true);
        }
        else
        {
            flyObj.transform.SetParent(transform.root, true);
        }
        
        flyObj.transform.position = transform.position;
        flyObj.transform.localScale = Vector3.one;

        TextMeshProUGUI flyText = flyObj.AddComponent<TextMeshProUGUI>();
        flyText.raycastTarget = false;
        flyText.text = $"+{amount}";
        flyText.fontSize = 40;
        flyText.color = Color.yellow;
        flyText.alignment = TextAlignmentOptions.Center;
        flyText.textWrappingMode = TextWrappingModes.NoWrap;
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

    private int GetPiecePrice(PieceType type)
    {
        ShopPieceData data = shopPieceData;
        if (data == null && RewardService.Instance != null)
        {
            data = RewardService.Instance.pieceData;
        }

        if (data != null && data.pieceList != null)
        {
            var info = data.pieceList.Find(p => p.pieceType == type);
            if (info != null) return info.Price;
        }

        switch (type)
        {
            case PieceType.Soldier: return 10;
            case PieceType.Cannon: return 20;
            case PieceType.Horse: return 25;
            case PieceType.Elephant: return 30;
            case PieceType.Chariot: return 40;
            case PieceType.King: return 100;
            default: return 10;
        }
    }
}
