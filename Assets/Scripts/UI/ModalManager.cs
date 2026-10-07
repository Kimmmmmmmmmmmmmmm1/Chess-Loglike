using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ModalManager : MonoBehaviour
{
    public static ModalManager Instance { get; private set; }
    private static int keyboardBlockCount;
    private const int ModalSortingOrder = 31000;

    [Tooltip("Drag the modal prefab (with ModalController attached) here in the inspector")]
    [SerializeField] private GameObject modalPrefab;

    [Tooltip("Optional: parent transform (Canvas) to instantiate the modal under). If empty, the first Canvas will be used.")]
    [SerializeField] private Transform uiParent;

    private static GameObject runtimeModalCanvas;

    public static bool IsKeyboardBlocked => keyboardBlockCount > 0;

    public static void PushKeyboardBlock()
    {
        keyboardBlockCount++;
    }

    public static void PopKeyboardBlock()
    {
        keyboardBlockCount = Mathf.Max(0, keyboardBlockCount - 1);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private Transform GetParent()
    {
        return GetOrCreateModalCanvas().transform;
    }

    private static GameObject GetOrCreateModalCanvas()
    {
        if (runtimeModalCanvas != null)
        {
            return runtimeModalCanvas;
        }

        runtimeModalCanvas = GameObject.Find("ModalCanvas");
        if (runtimeModalCanvas != null)
        {
            return runtimeModalCanvas;
        }

        runtimeModalCanvas = new GameObject("ModalCanvas");
        DontDestroyOnLoad(runtimeModalCanvas);

        Canvas canvas = runtimeModalCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ModalSortingOrder;

        CanvasScaler scaler = runtimeModalCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        runtimeModalCanvas.AddComponent<GraphicRaycaster>();

        return runtimeModalCanvas;
    }

    private static void BringModalToFront(GameObject modalObject)
    {
        if (modalObject == null)
        {
            return;
        }

        modalObject.transform.SetAsLastSibling();

        Canvas modalCanvas = modalObject.GetComponent<Canvas>();
        if (modalCanvas != null)
        {
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder = Mathf.Max(modalCanvas.sortingOrder, ModalSortingOrder);
        }
    }

    public Task<bool> ShowModalAsync(string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (modalPrefab == null)
        {
            tcs.SetException(new Exception("ModalManager.modalPrefab is not assigned in the inspector."));
            return tcs.Task;
        }

        Transform parent = GetParent();
        GameObject go = parent != null ? Instantiate(modalPrefab, parent, false) : Instantiate(modalPrefab);
        BringModalToFront(go);

        var controller = go.GetComponent<ModalController>();
        if (controller == null)
        {
            Destroy(go);
            tcs.SetException(new Exception("Modal prefab does not contain ModalController component."));
            return tcs.Task;
        }

        PushKeyboardBlock();

        void HandleResult(bool result)
        {
            controller.OnResult -= HandleResult;
            tcs.TrySetResult(result);
            PopKeyboardBlock();
            if (go != null) Destroy(go);
        }

        controller.OnResult += HandleResult;
        controller.Init(message);

        return tcs.Task;
    }

    public System.Collections.IEnumerator ShowModal(string message, Action<bool> callback)
    {
        var task = ShowModalAsync(message);
        while (!task.IsCompleted) yield return null;
        if (task.IsCompletedSuccessfully) callback?.Invoke(task.Result);
        else callback?.Invoke(false);
    }
}
