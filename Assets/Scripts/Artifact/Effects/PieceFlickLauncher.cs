using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PieceFlickLauncher : MonoBehaviour
{
    public static PieceFlickLauncher Instance { get; private set; }
    [SerializeField] private float power = 120f;
    [SerializeField] private float maxDragDistance = 220f;
    [SerializeField] private LineRenderer aimLine;
    [SerializeField] private PhysicsGridSynchronizer synchronizer;

    private PieceController selectedPiece;
    private Vector3 dragStart;
    private bool dragging;
    private bool launched;
    private Camera mainCamera;
    private RectTransform canvasRect;
    private RectTransform uiAimLine;
    private Image uiAimImage;

    public bool IsBusy => IsFlickMode || IsFlying;
    public bool IsFlying => launched || (synchronizer != null && synchronizer.IsSynchronizing);

    public static bool HasAlkkagiArtifact =>
        ArtifactManager.Instance != null &&
        ArtifactManager.Instance.HasArtifact(AlkkagiArtifact.Id);

    public static bool IsPhysicsActive =>
        Instance != null && Instance.IsFlying;

    public static bool IsFlickMode =>
        Instance != null &&
        Instance.enabled &&
        !Instance.launched &&
        HasAlkkagiArtifact &&
        Instance.IsPlayerTurn;

    private bool HasPassiveArtifact => HasAlkkagiArtifact;
    private bool IsPlayerTurn => TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn;

    public static PieceFlickLauncher EnsureInstance()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject(nameof(PieceFlickLauncher));
        Instance = go.AddComponent<PieceFlickLauncher>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        mainCamera = Camera.main;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        if (synchronizer == null) synchronizer = GetComponent<PhysicsGridSynchronizer>();
        if (synchronizer == null) synchronizer = gameObject.AddComponent<PhysicsGridSynchronizer>();
        if (aimLine != null)
        {
            aimLine.useWorldSpace = true;
            aimLine.material = new Material(Shader.Find("Sprites/Default"));
            aimLine.startColor = new Color(1f, 0.85f, 0.15f, 1f);
            aimLine.endColor = new Color(1f, 0.35f, 0.05f, 1f);
            aimLine.widthMultiplier = 12f;
            aimLine.sortingOrder = 32767;
        }
        if (canvasRect != null)
        {
            GameObject lineObject = new GameObject("AlkkagiAimLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lineObject.transform.SetParent(canvasRect, false);
            uiAimLine = lineObject.GetComponent<RectTransform>();
            uiAimImage = lineObject.GetComponent<Image>();
            uiAimImage.color = new Color(1f, 0.55f, 0.05f, 1f);
            uiAimImage.raycastTarget = false;
            uiAimLine.anchorMin = new Vector2(0.5f, 0.5f);
            uiAimLine.anchorMax = new Vector2(0.5f, 0.5f);
            uiAimLine.pivot = new Vector2(0f, 0.5f);
            uiAimLine.SetAsLastSibling();
            uiAimLine.gameObject.SetActive(false);
        }
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced += HandleTurnAdvanced;
        }
    }

    private void Start()
    {
        enabled = true;
    }

    private void HandleTurnAdvanced(TurnOwner owner)
    {
        if (owner == TurnOwner.Player && HasPassiveArtifact)
        {
            launched = false;
            selectedPiece = null;
            dragging = false;
        }
        else
        {
            Cancel();
        }
    }

    private void Update()
    {
        if (!HasPassiveArtifact || !IsPlayerTurn ||
            GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameStateManager.GameState.GamePlay)
        {
            Cancel();
            return;
        }
        if (launched) return;
        if (Input.GetMouseButtonDown(0))
        {
            selectedPiece = FindAllyUnderPointer();
            if (selectedPiece != null)
            {
                dragStart = Input.mousePosition;
                dragging = true;
            }
        }

        if (dragging)
        {
            Vector3 current = Input.mousePosition;
            Vector3 delta = Vector3.ClampMagnitude(dragStart - current, maxDragDistance);
            DrawAimLine(delta);
            if (Input.GetMouseButtonUp(0))
            {
                dragging = false;
                if (delta.sqrMagnitude > 25f) Launch(selectedPiece, delta);
                else Cancel();
            }
        }

        if (Input.GetMouseButtonDown(1)) Cancel();
    }

    private PieceController FindAllyUnderPointer()
    {
        if (EventSystem.current != null)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current) { position = Input.mousePosition }, results);
            for (int i = 0; i < results.Count; i++)
            {
                PieceController piece = results[i].gameObject.GetComponentInParent<PieceController>();
                if (piece != null && !piece.IsEnemy && piece.CurrentLocation == PieceLocation.Board)
                    return piece;
            }
        }

        PieceController[] pieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        for (int i = 0; i < pieces.Length; i++)
        {
            PieceController piece = pieces[i];
            RectTransform rect = piece.transform as RectTransform;
            if (rect != null &&
                !piece.IsEnemy &&
                piece.CurrentLocation == PieceLocation.Board &&
                RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition,
                    rect.GetComponentInParent<Canvas>()?.worldCamera))
            {
                return piece;
            }
        }
        return null;
    }

    private void DrawAimLine(Vector3 delta)
    {
        if (selectedPiece == null) return;
        Camera canvasCamera = canvasRect != null ? canvasRect.GetComponentInParent<Canvas>()?.worldCamera : null;
        Vector2 pieceScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, selectedPiece.transform.position);
        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, pieceScreen, canvasCamera, out Vector2 startLocal) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, Input.mousePosition, canvasCamera, out Vector2 pointerLocal))
        {
            Vector2 localDelta = Vector2.ClampMagnitude(startLocal - pointerLocal, maxDragDistance);
            Vector2 endLocal = startLocal + localDelta;
            DrawUiAimLine(startLocal, endLocal);
            if (aimLine != null)
            {
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, canvasRect.TransformPoint(startLocal));
                aimLine.SetPosition(1, canvasRect.TransformPoint(endLocal));
            }
        }
        else
        {
            if (aimLine != null)
            {
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, selectedPiece.transform.position);
                aimLine.SetPosition(1, selectedPiece.transform.position + delta);
            }
        }
    }

    private void DrawUiAimLine(Vector2 start, Vector2 end)
    {
        if (uiAimLine == null || uiAimImage == null) return;
        Vector2 delta = end - start;
        float length = delta.magnitude;
        uiAimLine.anchoredPosition = start;
        uiAimLine.sizeDelta = new Vector2(length, 16f);
        uiAimLine.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        uiAimLine.SetAsLastSibling();
        uiAimLine.gameObject.SetActive(length > 0.01f);
    }

    private void Launch(PieceController piece, Vector3 screenDelta)
    {
        Rigidbody body = piece.GetComponent<Rigidbody>();
        if (body == null) body = piece.gameObject.AddComponent<Rigidbody>();
        if (piece.GetComponent<Collider>() == null) piece.gameObject.AddComponent<BoxCollider>();

        body.mass = PhysicsGridSynchronizer.GetMass(piece.PieceType);
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        Vector3 direction = new Vector3(screenDelta.x, screenDelta.y, 0f).normalized;
        body.AddForce(direction * power, ForceMode.Impulse);
        launched = true;
        if (aimLine != null) aimLine.positionCount = 0;
        if (uiAimLine != null) uiAimLine.gameObject.SetActive(false);
        PieceManager.Instance?.ClearSelection();
        PieceManager.Instance?.HideMoveMarkers();
        synchronizer.BeginMonitoring();
    }

    private void Cancel()
    {
        selectedPiece = null;
        dragging = false;
        if (aimLine != null) aimLine.positionCount = 0;
        if (uiAimLine != null) uiAimLine.gameObject.SetActive(false);
        PieceManager.Instance?.HideMoveMarkers();
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= HandleTurnAdvanced;
        }
        if (Instance == this) Instance = null;
    }
}
