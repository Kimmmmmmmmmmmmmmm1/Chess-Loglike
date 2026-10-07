using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MouseParallaxImages : MonoBehaviour
{
    [Header("움직일 이미지 3개")]
    [SerializeField] private Image[] images = new Image[3];

    [Header("움직임 거리")]
    [SerializeField] private float firstImageMoveAmount = 5f;
    [SerializeField] private float amountStep = 1f;

    [Header("부드러움")]
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private bool useUnscaledTime = true;

    private RectTransform[] rectTransforms;
    private Vector2[] startPositions;

    private void Reset()
    {
        images = new Image[3];
    }

    private void Awake()
    {
        CacheImages();
    }

    private void OnEnable()
    {
        CacheImages();
    }

    private void Update()
    {
        if (rectTransforms == null || startPositions == null)
        {
            CacheImages();
        }

        if (!TryGetPointerPosition(out Vector2 pointerPosition))
        {
            return;
        }

        float halfWidth = Screen.width * 0.5f;
        float halfHeight = Screen.height * 0.5f;
        if (halfWidth <= 0f || halfHeight <= 0f)
        {
            return;
        }

        Vector2 normalizedPointerOffset = new Vector2(
            Mathf.Clamp((pointerPosition.x - halfWidth) / halfWidth, -1f, 1f),
            Mathf.Clamp((pointerPosition.y - halfHeight) / halfHeight, -1f, 1f)
        );

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float lerpAmount = 1f - Mathf.Exp(-smoothSpeed * deltaTime);

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform targetRect = rectTransforms[i];
            if (targetRect == null)
            {
                continue;
            }

            float moveAmount = Mathf.Max(0f, firstImageMoveAmount - amountStep * i);
            Vector2 targetPosition = startPositions[i] - normalizedPointerOffset * moveAmount;
            targetRect.anchoredPosition = Vector2.Lerp(targetRect.anchoredPosition, targetPosition, lerpAmount);
        }
    }

    private void CacheImages()
    {
        if (images == null)
        {
            images = new Image[3];
        }

        rectTransforms = new RectTransform[images.Length];
        startPositions = new Vector2[images.Length];

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null)
            {
                continue;
            }

            rectTransforms[i] = images[i].rectTransform;
            startPositions[i] = rectTransforms[i].anchoredPosition;
        }
    }

    private bool TryGetPointerPosition(out Vector2 pointerPosition)
    {
        if (Mouse.current != null)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Pointer.current != null)
        {
            pointerPosition = Pointer.current.position.ReadValue();
            return true;
        }

        try
        {
            pointerPosition = Input.mousePosition;
            return true;
        }
        catch
        {
            pointerPosition = Vector2.zero;
            return false;
        }
    }

    private void OnValidate()
    {
        if (images == null || images.Length != 3)
        {
            System.Array.Resize(ref images, 3);
        }

        firstImageMoveAmount = Mathf.Max(0f, firstImageMoveAmount);
        amountStep = Mathf.Max(0f, amountStep);
        smoothSpeed = Mathf.Max(0f, smoothSpeed);
    }
}
