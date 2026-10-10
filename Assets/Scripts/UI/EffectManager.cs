using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [Header("Settings")]
    public GameObject debrisPrefab;
    public Transform debrisParent; // Canvas 혹은 BoardPanel
    public int debrisCount = 6;    // 글리치 텍스트 파편 개수
    [SerializeField] private float spreadRadiusMultiplier = 2.5f;
    private Image flashImage;

    private static readonly string[] GlitchTokens = new string[]
    {
        "0x00", "ERR!", "SEGFAULT", "KILL -9", "0101", "#%$&", "[X]", "NULL", "0xFF", "OVERFLOW"
    };

    private static readonly char[] GlitchChars = "!@#$%^&*0123456789ABCDEF<>[]/_=".ToCharArray();

    private void Awake()
    {
        Instance = this;
    }

    public void PlayExplosion(Vector2 worldPos, Color pieceColor, bool isEnemy, float scale = 1f)
    {
        string cliCommand = isEnemy
            ? "[DDoS ATTACK SUCCESS]"
            : "[PROCESS TERMINATED // SIGKILL]";
        Color cliColor = isEnemy
            ? new Color(0f, 1f, 0f, 1f)       // #00FF00
            : new Color(1f, 0.2f, 0.33f, 1f); // #FF3355

        SpawnCliCommandPopup(worldPos, cliCommand, cliColor, scale);

        int count = Mathf.RoundToInt(debrisCount * scale);
        for (int i = 0; i < count; i++)
        {
            SpawnGlitchFragment(worldPos, cliColor, isEnemy, scale, 1f, 1f, 0f);
        }
    }

    public void PlayCollapseExplosion(Vector2 worldPos, Color pieceColor, bool isEnemy, float scale = 1f)
    {
        string cliCommand = "[LOGIC_BOMB // BUFFER_OVERFLOW]";
        Color cliColor = new Color(0f, 1f, 0f, 1f);

        SpawnCliCommandPopup(worldPos, cliCommand, cliColor, scale * 1.15f);

        int count = Mathf.RoundToInt(debrisCount * scale * 1.2f);
        for (int i = 0; i < count; i++)
        {
            float t = (i + 1f) / count;
            float spreadMultiplier = Mathf.Lerp(1.4f, 2.2f, t);
            float speedMultiplier = Mathf.Lerp(1.0f, 1.6f, t);
            float delay = i * 0.02f;

            SpawnGlitchFragment(worldPos, cliColor, isEnemy, scale, spreadMultiplier, speedMultiplier, delay);
        }
    }

    public void PlayCliInjectionEffect(Vector2 worldPos, string commandText = "[CODE INJECTION APPLIED]")
    {
        SpawnCliCommandPopup(worldPos, commandText, new Color(0f, 1f, 0f, 1f), 1f);
    }

    public void PlaySlowMotion()
    {
        float restoreSpeed = 1f;
        if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
        {
            restoreSpeed = Mathf.Max(0.01f, SettingsManager.Instance.Settings.gameSpeed);
        }

        DOTween.timeScale = 0.1f;
        DOTween.To(() => DOTween.timeScale, x => DOTween.timeScale = x, restoreSpeed, 1f)
            .SetUpdate(true)
            .SetEase(Ease.OutQuad);
    }

    public void PlayScreenFlash(float duration = 0.15f, float maxAlpha = 0.35f)
    {
        if (flashImage == null)
        {
            CreateFlashImage();
        }

        if (flashImage != null)
        {
            flashImage.color = new Color(0f, 1f, 0f, Mathf.Min(maxAlpha, 0.35f));
            flashImage.gameObject.SetActive(true);
            flashImage.DOFade(0f, duration).SetEase(Ease.OutQuad).OnComplete(() => flashImage.gameObject.SetActive(false));
        }
    }

    private void CreateFlashImage()
    {
        Canvas canvas = debrisParent != null ? debrisParent.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject go = new GameObject("TerminalFlashOverlay");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        flashImage = go.AddComponent<Image>();
        flashImage.color = new Color(0f, 1f, 0f, 0.25f);
        flashImage.raycastTarget = false;

        RectTransform rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        go.SetActive(false);
    }

    public void PlayCameraShake(float duration = 0.2f, float strength = 0.3f, int vibrato = 20)
    {
        if (debrisParent != null && debrisParent.GetComponent<RectTransform>() != null)
        {
            RectTransform target = debrisParent.GetComponent<RectTransform>();
            target.DOComplete();
            target.DOShakeAnchorPos(duration, strength * 50f, vibrato, 90, false, true);
        }
        else if (Camera.main != null)
        {
            Camera.main.transform.DOComplete();
            Camera.main.transform.DOShakePosition(duration, strength, vibrato, 90, false, true);
        }
    }

    public void PlayLandingEffect(Vector2 worldPos)
    {
        SpawnCliCommandPopup(worldPos, "[PACKET_ACK // 200_OK]", new Color(0f, 1f, 0f, 0.85f), 0.75f);
        for (int i = 0; i < 4; i++)
        {
            SpawnPacketRipple(worldPos);
        }
    }

    private Transform ResolveEffectParent()
    {
        if (debrisParent != null) return debrisParent;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }

    private void SpawnCliCommandPopup(Vector2 worldPos, string finalText, Color textColor, float scale)
    {
        Transform parent = ResolveEffectParent();
        if (parent == null) return;

        GameObject popupObj = new GameObject("CLI_GlitchPopup", typeof(RectTransform), typeof(CanvasGroup));
        popupObj.transform.SetParent(parent, false);
        popupObj.transform.SetAsLastSibling();

        RectTransform rt = popupObj.GetComponent<RectTransform>();
        rt.position = worldPos;
        rt.sizeDelta = new Vector2(260f, 26f);
        rt.localScale = Vector3.one * Mathf.Clamp(scale, 0.7f, 1.3f);

        CanvasGroup cg = popupObj.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        Image bg = popupObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("CLIText", typeof(RectTransform));
        textObj.transform.SetParent(popupObj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(4f, 0f);
        textRt.offsetMax = new Vector2(-4f, 0f);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
        tmp.fontSize = 11f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.text = ScrambleText(finalText, 0.6f);

        StartCoroutine(GlitchResolveRoutine(tmp, finalText, 0.35f));

        Vector2 startAnchored = rt.anchoredPosition;
        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOShakeAnchorPos(0.2f, new Vector2(8f, 4f), 30, 90, false, true));
        seq.Join(rt.DOAnchorPosY(startAnchored.y + 32f * scale, 0.75f).SetEase(Ease.OutCubic));
        seq.Insert(0.45f, cg.DOFade(0f, 0.3f));
        seq.OnComplete(() => Destroy(popupObj));
    }

    private IEnumerator GlitchResolveRoutine(TextMeshProUGUI tmp, string finalText, float duration)
    {
        float elapsed = 0f;
        const float step = 0.04f;
        while (elapsed < duration && tmp != null)
        {
            float corruption = 1f - Mathf.Clamp01(elapsed / duration);
            tmp.text = ScrambleText(finalText, corruption * 0.75f);
            yield return new WaitForSecondsRealtime(step);
            elapsed += step;
        }

        if (tmp != null)
        {
            tmp.text = finalText;
        }
    }

    private static string ScrambleText(string input, float corruptionRatio)
    {
        if (string.IsNullOrEmpty(input) || corruptionRatio <= 0.01f) return input;
        char[] buffer = input.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == '[' || buffer[i] == ']' || buffer[i] == ' ') continue;
            if (Random.value < corruptionRatio)
            {
                buffer[i] = GlitchChars[Random.Range(0, GlitchChars.Length)];
            }
        }
        return new string(buffer);
    }

    private void SpawnPacketRipple(Vector2 pos)
    {
        Transform parent = ResolveEffectParent();
        if (parent == null) return;

        GameObject dust = new GameObject("TerminalPacketBit", typeof(RectTransform));
        dust.transform.SetParent(parent, false);
        RectTransform rt = dust.GetComponent<RectTransform>();
        rt.position = pos;
        rt.sizeDelta = new Vector2(40f, 16f);

        TextMeshProUGUI tmp = dust.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
        tmp.text = Random.value < 0.5f ? "01" : ">>";
        tmp.fontSize = 10f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0f, 1f, 0f, 0.85f);
        tmp.raycastTarget = false;

        Vector2 dir = Random.insideUnitCircle * Random.Range(24f, 48f);
        float duration = Random.Range(0.25f, 0.45f);

        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOAnchorPos(rt.anchoredPosition + dir, duration).SetEase(Ease.OutQuad));
        seq.Join(tmp.DOFade(0f, duration));
        seq.OnComplete(() => Destroy(dust));
    }

    private void SpawnGlitchFragment(Vector2 startWorldPos, Color color, bool isEnemy, float scale, float spreadMultiplier, float speedMultiplier, float startDelay)
    {
        Transform parent = ResolveEffectParent();
        if (parent == null) return;

        GameObject debris = new GameObject("GlitchFragment", typeof(RectTransform));
        debris.transform.SetParent(parent, false);
        RectTransform rt = debris.GetComponent<RectTransform>();
        rt.position = startWorldPos;
        rt.sizeDelta = new Vector2(64f, 18f);

        TextMeshProUGUI tmp = debris.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
        tmp.text = GlitchTokens[Random.Range(0, GlitchTokens.Length)];
        tmp.fontSize = Random.Range(9f, 13f) * Mathf.Clamp(scale, 0.8f, 1.3f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Random.value < 0.7f
            ? new Color(0f, 1f, 0f, 1f)
            : (isEnemy ? new Color(1f, 0.25f, 0.35f, 1f) : new Color(0.5f, 1f, 0.5f, 1f));
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        float cellX = (PieceManager.Instance != null && PieceManager.Instance.gridManager != null)
            ? PieceManager.Instance.gridManager.cellSize.x
            : 42f;
        float spreadRadius = cellX * spreadRadiusMultiplier * spreadMultiplier;
        Vector2 randomDir = Random.insideUnitCircle * spreadRadius * scale;
        Vector2 startAnchoredPos = rt.anchoredPosition;
        Vector2 targetAnchoredPos = startAnchoredPos + randomDir;

        float duration = Random.Range(0.35f, 0.6f) / Mathf.Max(0.1f, speedMultiplier);

        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOAnchorPos(targetAnchoredPos, duration).SetEase(Ease.OutExpo));
        seq.Join(rt.DOShakeAnchorPos(duration * 0.6f, 6f, 25, 90, false, true));
        seq.AppendInterval(0.1f);
        seq.Append(tmp.DOFade(0f, 0.25f));

        if (startDelay > 0f)
        {
            seq.SetDelay(startDelay);
        }

        seq.OnComplete(() => Destroy(debris));
    }
}