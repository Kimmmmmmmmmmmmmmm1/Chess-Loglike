using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class MoveMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private PieceManager manager;
    private Vector2Int gridPosition;
    private bool isHovered;
    private Image image;
    private Color baseColor;

    public Vector2Int GridPosition => gridPosition;
    public bool IsHovered => isHovered;

    public void Initialize(PieceManager pieceManager, Vector2Int position, bool isEnemy)
    {
        manager = pieceManager;
        gridPosition = position;
        image = GetComponent<Image>();

        transform.localScale = Vector3.one;

        // 적 마커면 붉은색, 내 마커면 연두색
        baseColor = isEnemy ? Color.red : new Color(0.3f, 1f, 0.3f);
        
        // 초기 상태: 반투명
        UpdateColor(0.5f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        UpdateColor(1f); // 마우스 올리면 불투명
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        UpdateColor(0.5f); // 마우스 떼면 반투명
    }

    private void UpdateColor(float alpha)
    {
        if (image != null)
        {
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }
}
