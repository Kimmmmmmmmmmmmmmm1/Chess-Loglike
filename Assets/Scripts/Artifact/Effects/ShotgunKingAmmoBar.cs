using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShotgunKingAmmoBar : MonoBehaviour
{
    private GameObject ammoRoot;
    private Slider ammoSlider;
    private TMP_Text ammoLabel;

    public void SetAmmoVisible(bool visible, int ammo, int maxAmmo)
    {
        if (!visible)
        {
            if (ammoRoot != null) ammoRoot.SetActive(false);
            return;
        }

        EnsureUI();
        ammoRoot.SetActive(true);
        ammoSlider.maxValue = Mathf.Max(1, maxAmmo);
        ammoSlider.SetValueWithoutNotify(Mathf.Clamp(ammo, 0, maxAmmo));
        ammoLabel.text = $"탄 {ammo}/{maxAmmo}";
    }

    private void EnsureUI()
    {
        if (ammoRoot != null) return;

        TMP_Text sourceText = GetComponentInChildren<TMP_Text>(true);
        ammoRoot = new GameObject("ShotgunAmmo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ammoRoot.transform.SetParent(transform, false);
        RectTransform rootRect = ammoRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, -25f);
        rootRect.sizeDelta = new Vector2(40f, 20f);

        Image panel = ammoRoot.GetComponent<Image>();
        panel.color = new Color(0.08f, 0.07f, 0.06f, 0.88f);
        panel.raycastTarget = false;

        GameObject labelObject = new GameObject("AmmoCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(ammoRoot.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(1f, 0f);
        labelRect.offsetMax = new Vector2(-1f, -1f);
        ammoLabel = labelObject.GetComponent<TextMeshProUGUI>();
        ammoLabel.font = sourceText != null ? sourceText.font : TMP_Settings.defaultFontAsset;
        ammoLabel.fontSize = 8f;
        ammoLabel.alignment = TextAlignmentOptions.Center;
        ammoLabel.color = new Color(1f, 0.91f, 0.68f, 1f);
        ammoLabel.raycastTarget = false;

        GameObject sliderObject = new GameObject("AmmoSlider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        sliderObject.transform.SetParent(ammoRoot.transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 3f);
        sliderRect.sizeDelta = new Vector2(34f, 5f);

        Image sliderBackground = sliderObject.GetComponent<Image>();
        sliderBackground.color = new Color(0.22f, 0.19f, 0.16f, 1f);
        sliderBackground.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.one;
        fillRect.offsetMax = -Vector2.one;
        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.96f, 0.62f, 0.18f, 1f);
        fillImage.raycastTarget = false;

        ammoSlider = sliderObject.GetComponent<Slider>();
        ammoSlider.transition = Selectable.Transition.None;
        ammoSlider.navigation = new Navigation { mode = Navigation.Mode.None };
        ammoSlider.targetGraphic = sliderBackground;
        ammoSlider.fillRect = fillRect;
        ammoSlider.direction = Slider.Direction.LeftToRight;
        ammoSlider.wholeNumbers = true;
        ammoSlider.minValue = 0f;
        ammoSlider.maxValue = TyrantsBarrageArtifactEffect.MaxAmmo;
        ammoSlider.interactable = false;
    }
}
