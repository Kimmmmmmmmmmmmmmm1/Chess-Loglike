using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;
using DG.Tweening;

public class PresentationManager : MonoBehaviour
{
    public static PresentationManager Instance { get; private set; }

    [Header("General")]
    [SerializeField] private GameObject presentationEffectPrefab;

    [Header("Boss Entrance Tuning")]
    [SerializeField] private float bossGridShiftDuration = 0.4f;
    [SerializeField] private float bossPopDuration = 0.45f;
    [SerializeField] private Vector2 bossSpriteSize = new Vector2(180f, 180f);
    [SerializeField] private float bossSpriteOffsetX = 192f;
    [SerializeField] private float bossSpriteOffsetY = 40f;
    [SerializeField] private float bossSpriteScaleAfterMove = 0.8f;
    [SerializeField] private float bossEntranceMoveDuration = 0.45f;

    public bool IsPresenting { get; private set; } = false;
    public event Action OnPresentationComplete;
    private GameObject persistentBossSpriteObj = null;

    public bool HasPersistentBossSprite => persistentBossSpriteObj != null;

    public void RemovePersistentBossSprite()
    {
        if (persistentBossSpriteObj != null)
        {
            Destroy(persistentBossSpriteObj);
            persistentBossSpriteObj = null;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private Canvas GetPresentationCanvas()
    {
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        Scene active = SceneManager.GetActiveScene();
        foreach (var c in canvases)
        {
            if (!c.gameObject.activeInHierarchy) continue;
            if (c.gameObject.scene == active && c.isRootCanvas)
            {
                return c;
            }
        }

        foreach (var c in canvases)
        {
            if (c.gameObject.activeInHierarchy && c.isRootCanvas) return c;
        }

        return null;
    }

    public void PlayBossClearPresentation()
    {
        if (presentationEffectPrefab == null)
        {
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ReloadMap();
            }
            return;
        }

        Canvas canvas = GetPresentationCanvas();
        Transform parent = canvas != null ? canvas.transform : null;
        GameObject presentationInstance = parent != null ? Instantiate(presentationEffectPrefab, parent, false) : Instantiate(presentationEffectPrefab);
        presentationInstance.transform.SetAsLastSibling();
        RectTransform rtInstance = presentationInstance.GetComponent<RectTransform>();
        if (rtInstance != null)
        {
            rtInstance.anchoredPosition = Vector2.zero;
            rtInstance.localScale = Vector3.one;
        }

        PresentationEffect effect = presentationInstance.GetComponent<PresentationEffect>();

        if (effect == null)
        {
            Destroy(presentationInstance);
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ReloadMap();
            }
            return;
        }

        string bossName = "";
        Sprite bossSprite = null;

        if (BossManager.Instance != null)
        {
            bossName = BossManager.Instance.CurrentBossName;
            bossSprite = BossManager.Instance.CurrentBossSprite;
        }

        IsPresenting = true;
        effect.PlayPresentation(bossName, bossSprite, () =>
        {
            IsPresenting = false;
            OnPresentationComplete?.Invoke();
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ReloadMap();
            }
        });
    }

    public void PlayCustomPresentation(string text, Sprite sprite, Action onComplete = null, string subtext = "")
    {
        if (presentationEffectPrefab == null)
        {
            onComplete?.Invoke();
            return;
        }

        Canvas canvas = GetPresentationCanvas();
        Transform parent = canvas != null ? canvas.transform : null;
        GameObject presentationInstance = parent != null ? Instantiate(presentationEffectPrefab, parent, false) : Instantiate(presentationEffectPrefab);
        presentationInstance.transform.SetAsLastSibling();
        RectTransform rtInstance = presentationInstance.GetComponent<RectTransform>();
        if (rtInstance != null)
        {
            rtInstance.anchoredPosition = Vector2.zero;
            rtInstance.localScale = Vector3.one;
        }

        PresentationEffect effect = presentationInstance.GetComponent<PresentationEffect>();

        if (effect == null)
        {
            Destroy(presentationInstance);
            onComplete?.Invoke();
            return;
        }

        IsPresenting = true;
        effect.PlayPresentation(text, sprite, () =>
        {
            IsPresenting = false;
            OnPresentationComplete?.Invoke();
            onComplete?.Invoke();
        }, subtext);
    }

    public void PlayBossEntrancePresentation(string bossName, string bossSubname, Sprite bossSprite, Action onComplete = null)
    {
        if (presentationEffectPrefab == null)
        {
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(BossEntranceRoutine(bossName, bossSubname, bossSprite, onComplete));
    }

    private IEnumerator BossEntranceRoutine(string bossName, string bossSubname, Sprite bossSprite, Action onComplete)
    {
        IsPresenting = true;

        GridManager grid = (PieceManager.Instance != null) ? PieceManager.Instance.gridManager : null;
        if (grid != null)
        {
            grid.ShiftBoardVertical(true, bossGridShiftDuration);
        }

        yield return new WaitForSeconds(bossGridShiftDuration);

        Canvas canvas = GetPresentationCanvas();
        Transform parent = canvas != null ? canvas.transform : null;

        GameObject bossImgObj = new GameObject("BossEntranceSprite");
        bossImgObj.transform.SetParent(parent, false);
        RectTransform rt = bossImgObj.AddComponent<RectTransform>();
        Image img = bossImgObj.AddComponent<Image>();

        Sprite fallback = presentationEffectPrefab.GetComponentInChildren<Image>()?.sprite;
        img.sprite = bossSprite != null ? bossSprite : fallback;
        img.preserveAspect = true;

        Vector2 anchoredPos = Vector2.zero;
        if (grid != null && grid.boardContainer != null)
        {
            anchoredPos = grid.boardContainer.anchoredPosition + new Vector2(0f, grid.boardContainer.sizeDelta.y * 0.5f + bossSpriteOffsetY);
        }
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = bossSpriteSize;
        rt.localScale = Vector3.zero;

        rt.DOScale(Vector3.one, bossPopDuration).SetEase(Ease.OutBack);
        img.DOFade(1f, bossPopDuration).From(0f).SetEase(Ease.InQuad);

        yield return new WaitForSeconds(bossPopDuration + 0.05f);

        Action handler = null;
        handler = () =>
        {
            OnPresentationComplete -= handler;

            if (grid != null)
            {
                grid.ShiftBoardVertical(false, bossGridShiftDuration);
            }

            persistentBossSpriteObj = bossImgObj;

            Vector2 targetAnchored = Vector2.zero;
            if (grid != null && grid.boardContainer != null)
            {
                RectTransform board = grid.boardContainer;
                float offsetX = board.sizeDelta.x * 0.5f + bossSpriteOffsetX;
                float mapCenterY = 0f;
                targetAnchored = board.anchoredPosition + new Vector2(offsetX, mapCenterY);
            }

            RectTransform pRt = persistentBossSpriteObj.GetComponent<RectTransform>();
            if (pRt != null)
            {
                pRt.SetAsLastSibling();
                pRt.DOAnchorPos(targetAnchored, bossEntranceMoveDuration).SetEase(Ease.InOutQuad);
                pRt.DOScale(Vector3.one * bossSpriteScaleAfterMove, bossEntranceMoveDuration).SetEase(Ease.OutQuad);
            }

            IsPresenting = false;
            onComplete?.Invoke();
        };

        OnPresentationComplete += handler;

        PlayCustomPresentation(bossName, bossSprite, null, bossSubname);
    }
}
