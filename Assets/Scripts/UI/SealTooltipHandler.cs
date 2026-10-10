using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SealTooltipHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private SealData sealData;

    // PieceType 한글 표기
    private static readonly Dictionary<PieceType, string> PieceKoreanNames = new Dictionary<PieceType, string>
    {
        { PieceType.King,   "킹" },
        { PieceType.Rook,   "룩" },
        { PieceType.Knight, "나이트" },
        { PieceType.Bishop, "비숍" },
        { PieceType.Queen,  "퀸" },
        { PieceType.Pawn,   "폰" }
    };

    public void Initialize(SealData data)
    {
        sealData = data;
    }

    private string BuildDescription()
    {
        if (sealData == null) return string.Empty;

        string desc = sealData.description;

        if (sealData.compatiblePieces != null && sealData.compatiblePieces.Count > 0)
        {
            List<string> names = new List<string>();
            foreach (PieceType pt in sealData.compatiblePieces)
            {
                names.Add(PieceKoreanNames.TryGetValue(pt, out string n) ? n : pt.ToString());
            }
            desc += "\n\n장착 가능: " + string.Join(", ", names);
        }

        return desc;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (sealData != null && TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowTooltip(
                sealData.sealName,
                BuildDescription(),
                transform.position,
                sealData.flavorText,
                TooltipManager.TooltipPrioritySeal,
                gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }
    }

    private void OnDisable()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }
    }
}

