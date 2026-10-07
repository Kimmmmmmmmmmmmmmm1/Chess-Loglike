using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

/// <summary>
/// 보물상자 매니저: Shop처럼 장기판을 밀고 위에서 아래로 나타나는 보물상자 패널을 관리한다.
/// 보물상자를 클릭하면 RewardManager를 통해 현재 전투 보상의 2.5배 보상을 제공한다.
/// </summary>
public class TreasureManager : MonoBehaviour
{
    public static TreasureManager Instance { get; private set; }

    [Header("UI")]
    public GameObject treasurePanel;        // 보물상자 패널 (위에서 내려옴)
    public Button treasureChestButton;      // 보물상자 버튼
    public Image treasureChestImage;        // 보물상자 버튼 이미지
    public Sprite closedChestSprite;        // 클릭 전 보물상자 이미지
    public Sprite openedChestSprite;        // 클릭 후 보물상자 이미지
    private RectTransform treasurePanelRect;

    [Header("Animation")]
    public float animDuration = 0.5f;
    public Ease openEase = Ease.OutBack;
    public Ease closeEase = Ease.InQuad;
    public float panelOpenY = 0f;           // 패널이 열렸을 때의 Y 위치

    [Header("Reward Multiplier")]
    public float rewardCountMultiplier = 2.5f;  // 보상 개수 배율
    public float rewardRarityMultiplier = 2.5f; // 보상 희귀도 배율

    [Header("Pixel Particles")]
    public RectTransform particleRoot;
    public int pixelParticleCount = 28;
    public Vector2 pixelParticleSizeRange = new Vector2(6f, 12f);
    public float pixelParticleDistance = 120f;
    public float pixelParticleDuration = 0.55f;
    public Color[] pixelParticleColors =
    {
        new Color(1f, 0.86f, 0.24f, 1f),
        new Color(1f, 0.95f, 0.55f, 1f),
        new Color(1f, 1f, 1f, 1f),
        new Color(1f, 0.98f, 0.78f, 1f)
    };

    private bool isOpen = false;
    private bool chestOpened = false; // 이미 상자를 열었는지 여부

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        CacheReferences();
        HideTreasureLabel();
        SetChestSprite(false);

        if (treasurePanel != null) treasurePanel.SetActive(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 재로드 시 참조 복구
        CacheReferences();

        if (treasurePanel == null)
        {
            treasurePanel = GameObject.Find("TreasurePanel");
            if (treasurePanel != null)
            {
                treasurePanelRect = treasurePanel.GetComponent<RectTransform>();
                treasurePanel.SetActive(false);
            }
        }

        if (treasureChestButton == null)
        {
            GameObject btnObj = GameObject.Find("TreasureChestButton");
            if (btnObj != null)
            {
                treasureChestButton = btnObj.GetComponent<Button>();
                treasureChestButton.onClick.RemoveListener(OnTreasureChestClicked);
                treasureChestButton.onClick.AddListener(OnTreasureChestClicked);
            }
        }

        HideTreasureLabel();
        SetChestSprite(false);
    }

    /// <summary>
    /// 보물상자 패널을 위에서 아래로 슬라이드하며 열기
    /// </summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        chestOpened = false;
        SetChestSprite(false);
        HideTreasureLabel();

        if (treasurePanel != null)
        {
            treasurePanel.SetActive(true);

            // 패널 애니메이션: 위쪽 화면 밖에서 아래로 슬라이드
            if (treasurePanelRect != null)
            {
                // 초기 위치를 위쪽 밖으로 설정 (높이만큼 이동)
                treasurePanelRect.anchoredPosition = new Vector2(
                    treasurePanelRect.anchoredPosition.x,
                    treasurePanelRect.rect.height
                );
                treasurePanelRect.DOAnchorPosY(panelOpenY, animDuration).SetEase(openEase);
            }

            // 보물상자 버튼 활성화 및 연출
            if (treasureChestButton != null)
            {
                treasureChestButton.interactable = true;
                // 상자 등장 연출: 약간의 바운스
                treasureChestButton.transform.localScale = Vector3.zero;
                treasureChestButton.transform.DOScale(Vector3.one, 0.4f)
                    .SetEase(Ease.OutBack)
                    .SetDelay(animDuration * 0.5f);
            }

            HideTreasureLabel();
        }

        // 장기판 이동 연출 제거: 패널이 그리드 위로 겹치도록 변경
    }

    /// <summary>
    /// 보물상자 패널을 위로 슬라이드하며 닫기
    /// </summary>
    public void Close(Action onComplete = null)
    {
        if (!isOpen)
        {
            onComplete?.Invoke();
            return;
        }

        isOpen = false;

        if (treasurePanelRect != null)
        {
            // 패널을 위쪽(높이만큼)으로 이동하여 숨김
            treasurePanelRect.DOAnchorPosY(treasurePanelRect.rect.height, animDuration)
                .SetEase(closeEase)
                .OnComplete(() =>
                {
                    if (treasurePanel != null) treasurePanel.SetActive(false);
                    onComplete?.Invoke();
                });
        }
        else if (treasurePanel != null)
        {
            treasurePanel.SetActive(false);
            onComplete?.Invoke();
        }
        else
        {
            onComplete?.Invoke();
        }

        // 장기판 원위치 복귀 연출 제거: 패널이 겹치므로 별도 복귀 동작 불필요
    }

    /// <summary>
    /// 보물상자 클릭 시: 2.5배 보상을 RewardManager를 통해 제공
    /// </summary>
    private void OnTreasureChestClicked()
    {
        if (chestOpened) return; // 중복 클릭 방지
        chestOpened = true;

        // 상자 열림 연출
        if (treasureChestButton != null)
        {
            treasureChestButton.interactable = false;
            SetChestSprite(true);

            // 상자가 흔들리며 열리는 연출
            Sequence openSeq = DOTween.Sequence();
            openSeq.Append(treasureChestButton.transform.DOShakeRotation(0.5f, 30f, 10, 90f));
            openSeq.Append(treasureChestButton.transform.DOScale(Vector3.one * 1.3f, 0.3f).SetEase(Ease.OutBack));
            openSeq.AppendCallback(PlayPixelParticles);
            openSeq.Append(treasureChestButton.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
            openSeq.OnComplete(() =>
            {
                GiveTreasureRewards();
            });
        }
        else
        {
            PlayPixelParticles();
            GiveTreasureRewards();
        }
    }

    private void CacheReferences()
    {
        if (treasurePanel != null)
        {
            treasurePanelRect = treasurePanel.GetComponent<RectTransform>();
        }

        if (treasureChestButton != null)
        {
            treasureChestButton.onClick.RemoveListener(OnTreasureChestClicked);
            treasureChestButton.onClick.AddListener(OnTreasureChestClicked);

            if (treasureChestImage == null)
            {
                treasureChestImage = treasureChestButton.targetGraphic as Image;
            }

            if (treasureChestImage == null)
            {
                treasureChestImage = treasureChestButton.GetComponent<Image>();
            }
        }

        if (particleRoot == null && treasurePanel != null)
        {
            particleRoot = treasurePanel.transform as RectTransform;
        }
    }

    private void SetChestSprite(bool opened)
    {
        if (treasureChestImage == null) return;

        Sprite targetSprite = opened ? openedChestSprite : closedChestSprite;
        if (targetSprite != null)
        {
            treasureChestImage.sprite = targetSprite;
        }

        treasureChestImage.preserveAspect = true;
    }

    private void HideTreasureLabel()
    {
        Transform label = null;

        if (treasureChestButton != null)
        {
            label = FindChildByName(treasureChestButton.transform, "TreasureLabel");
        }

        if (label == null && treasurePanel != null)
        {
            label = FindChildByName(treasurePanel.transform, "TreasureLabel");
        }

        if (label == null)
        {
            GameObject labelObj = GameObject.Find("TreasureLabel");
            if (labelObj != null) label = labelObj.transform;
        }

        if (label != null)
        {
            label.gameObject.SetActive(false);
        }
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName) return child;

            Transform nestedChild = FindChildByName(child, childName);
            if (nestedChild != null) return nestedChild;
        }

        return null;
    }

    private void PlayPixelParticles()
    {
        if (treasureChestButton == null) return;

        RectTransform root = GetRuntimeParticleRoot();
        if (root == null) return;

        RectTransform buttonRect = treasureChestButton.transform as RectTransform;
        Vector3 spawnWorldPosition = buttonRect != null
            ? buttonRect.TransformPoint(buttonRect.rect.center)
            : treasureChestButton.transform.position;
        Vector2 spawnPosition = GetLocalPositionIn(root, buttonRect);

        for (int i = 0; i < pixelParticleCount; i++)
        {
            GameObject pixel = CreatePixelParticle(root);
            pixel.transform.SetAsLastSibling();

            RectTransform pixelRect = pixel.GetComponent<RectTransform>();
            Image pixelImage = pixel.GetComponent<Image>();
            if (pixelRect == null || pixelImage == null)
            {
                Destroy(pixel);
                continue;
            }

            float size = UnityEngine.Random.Range(pixelParticleSizeRange.x, pixelParticleSizeRange.y);
            pixelRect.anchorMin = new Vector2(0.5f, 0.5f);
            pixelRect.anchorMax = new Vector2(0.5f, 0.5f);
            pixelRect.pivot = new Vector2(0.5f, 0.5f);
            pixelRect.sizeDelta = new Vector2(size, size);
            pixelRect.position = spawnWorldPosition;
            pixelRect.anchoredPosition = spawnPosition;
            pixelRect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 45f));

            pixelImage.raycastTarget = false;
            pixelImage.color = GetRandomPixelColor();

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = UnityEngine.Random.Range(pixelParticleDistance * 0.35f, pixelParticleDistance);
            Vector2 burstOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            burstOffset.y += UnityEngine.Random.Range(10f, pixelParticleDistance * 0.35f);

            float duration = UnityEngine.Random.Range(pixelParticleDuration * 0.8f, pixelParticleDuration * 1.2f);
            Sequence seq = DOTween.Sequence();
            seq.Join(pixelRect.DOJumpAnchorPos(spawnPosition + burstOffset, UnityEngine.Random.Range(45f, 90f), 1, duration).SetEase(Ease.Linear));
            seq.Join(pixelRect.DORotate(new Vector3(0f, 0f, UnityEngine.Random.Range(-220f, 220f)), duration, RotateMode.FastBeyond360));
            seq.Join(pixelRect.DOScale(UnityEngine.Random.Range(0.35f, 0.8f), duration).SetEase(Ease.InQuad));
            seq.Insert(duration * 0.45f, pixelImage.DOFade(0f, duration * 0.55f));
            seq.OnComplete(() => Destroy(pixel));
        }
    }

    private GameObject CreatePixelParticle(RectTransform root)
    {
        GameObject prefab = EffectManager.Instance != null ? EffectManager.Instance.debrisPrefab : null;
        GameObject pixel = prefab != null
            ? Instantiate(prefab, root)
            : new GameObject("TreasurePixelParticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (prefab == null)
        {
            pixel.transform.SetParent(root, false);
        }

        if (pixel.GetComponent<RectTransform>() == null)
        {
            pixel.AddComponent<RectTransform>();
        }

        if (pixel.GetComponent<Image>() == null)
        {
            pixel.AddComponent<Image>();
        }

        return pixel;
    }

    private RectTransform GetRuntimeParticleRoot()
    {
        RectTransform debrisParent = EffectManager.Instance != null
            ? EffectManager.Instance.debrisParent as RectTransform
            : null;
        if (IsRuntimeSceneTransform(debrisParent))
        {
            return debrisParent;
        }

        if (IsRuntimeSceneTransform(particleRoot))
        {
            return particleRoot;
        }

        RectTransform buttonParent = treasureChestButton.transform.parent as RectTransform;
        if (IsRuntimeSceneTransform(buttonParent))
        {
            return buttonParent;
        }

        Canvas canvas = treasureChestButton.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }

        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        return IsRuntimeSceneTransform(canvasRect) ? canvasRect : null;
    }

    private bool IsRuntimeSceneTransform(Transform target)
    {
        return target != null && target.gameObject.scene.IsValid() && target.gameObject.scene.isLoaded;
    }

    private Vector2 GetLocalPositionIn(RectTransform root, RectTransform target)
    {
        if (target == null) return Vector2.zero;

        Vector3 worldPosition = target.TransformPoint(target.rect.center);
        Vector3 localPosition = root.InverseTransformPoint(worldPosition);
        return new Vector2(localPosition.x, localPosition.y);
    }

    private Color GetRandomPixelColor()
    {
        if (pixelParticleColors == null || pixelParticleColors.Length == 0)
        {
            return Color.white;
        }

        return pixelParticleColors[UnityEngine.Random.Range(0, pixelParticleColors.Length)];
    }

    /// <summary>
    /// 현재 전투 보상의 2.5배 보상 계산 후 RewardManager로 전달
    /// </summary>
    private void GiveTreasureRewards()
    {
        int difficulty = 1;
        if (GameManager.Instance != null)
        {
            difficulty = GameManager.Instance.ClearedStage + 1;
        }

        // 기본 보상 계산 (RewardManager의 공식과 동일)
        int baseCount = Mathf.Clamp(difficulty / 3, 1, 5);
        int baseRarity = difficulty / 2;

        // 2.5배 적용
        int treasureCount = Mathf.Max(1, Mathf.RoundToInt(baseCount * rewardCountMultiplier));
        int treasureRarity = Mathf.Max(1, Mathf.RoundToInt(baseRarity * rewardRarityMultiplier));

        // 최소 보장: 보물상자는 최소 3개 보상, 최소 희귀도 2
        treasureCount = Mathf.Max(3, treasureCount);
        treasureRarity = Mathf.Max(2, treasureRarity);

        // 보물상자 패널 위에 보상 패널 표시 (유물 확률 3배)
        if (RewardService.Instance != null)
        {
            RewardService.Instance.ShowRewards(treasureCount, treasureRarity, true);
        }
        else
        {
            // 보상 없이 보물상자 패널 닫고 맵으로 복귀
            Close(() =>
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ChangeFlowState(GameFlowState.Map);
                }
            });
        }
    }
}
