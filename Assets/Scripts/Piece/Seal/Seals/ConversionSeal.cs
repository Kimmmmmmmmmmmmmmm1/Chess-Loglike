using UnityEngine;

/// <summary>
/// 오셀로의 성역 유물로 전향된 기물에 부착되는 임시 마커 인장.
/// 전투 승리 후 장착된 기물은 소멸하여 원래 덱 밸런스를 보존합니다.
/// </summary>
public class ConversionSeal : SealBase
{
    public const string SealName = "전향의 인장";

    private static SealData cachedSealData;
    private static Sprite fallbackIconSprite;

    public static bool IsConversionSealData(SealData data)
    {
        return data != null && data.sealName == SealName;
    }

    public static SealData GetOrCreateSealData()
    {
        if (cachedSealData != null)
        {
            if (cachedSealData.icon == null)
            {
                cachedSealData.icon = GetOrCreateFallbackIcon();
            }
            return cachedSealData;
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SealData", new[] { "Assets/Resources/Seal" });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            SealData candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<SealData>(path);
            if (IsConversionSealData(candidate))
            {
                cachedSealData = candidate;
                break;
            }
        }
#endif

        if (cachedSealData == null)
        {
            SealData[] seals = Resources.LoadAll<SealData>("Seal");
            foreach (SealData candidate in seals)
            {
                if (IsConversionSealData(candidate))
                {
                    cachedSealData = candidate;
                    break;
                }
            }
        }

        if (cachedSealData == null)
        {
            cachedSealData = ScriptableObject.CreateInstance<SealData>();
            cachedSealData.name = "Conversion";
            cachedSealData.sealName = SealName;
            cachedSealData.rarity = 0;
            cachedSealData.description = "오셀로의 성역에 의해 전향된 기물에게 붙는 인장이다.\n전투 종료 시 해당 기물은 소멸한다.";
            cachedSealData.isSoldInShop = false;
            cachedSealData.icon = GetOrCreateFallbackIcon();
        }
        else if (cachedSealData.icon == null)
        {
            cachedSealData.icon = GetOrCreateFallbackIcon();
        }

        return cachedSealData;
    }

    public static Sprite GetOrCreateFallbackIcon()
    {
        if (fallbackIconSprite != null)
        {
            return fallbackIconSprite;
        }

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "ConversionSealFallbackIcon";
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = 13.5f;
        float borderRadius = 11.5f;

        Color borderColor = new Color(0.43f, 0.27f, 0.72f, 1f);
        Color lightColor = new Color(1f, 0.95f, 0.78f, 1f);
        Color darkColor = new Color(0.49f, 0.34f, 0.78f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist > outerRadius)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else if (dist >= borderRadius)
                {
                    tex.SetPixel(x, y, borderColor);
                }
                else
                {
                    tex.SetPixel(x, y, x >= center.x ? lightColor : darkColor);
                }
            }
        }

        tex.Apply();
        fallbackIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return fallbackIconSprite;
    }
}

