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
        return BuildDescription(sealData);
    }

    public static string BuildDescription(SealData data)
    {
        if (data == null) return string.Empty;

        string desc = data.description;

        if (data.compatiblePieces != null && data.compatiblePieces.Count > 0)
        {
            List<string> names = new List<string>();
            foreach (PieceType pt in data.compatiblePieces)
            {
                names.Add(PieceKoreanNames.TryGetValue(pt, out string n) ? n : pt.ToString());
            }
            desc += "\n\n장착 가능: " + string.Join(", ", names);
        }

        return desc;
    }

    private bool HasParentPieceCardHandler()
    {
        return GetComponentInParent<PieceController>() != null
            || GetComponentInParent<ShopPieceSlot>() != null
            || GetComponentInParent<RewardButton>() != null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (HasParentPieceCardHandler())
        {
            return;
        }

        if (sealData != null && TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowSealTooltip(
                sealData,
                transform.position,
                gameObject,
                TooltipManager.TooltipPrioritySeal);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (HasParentPieceCardHandler())
        {
            return;
        }

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }
    }

    private void OnDisable()
    {
        if (HasParentPieceCardHandler())
        {
            return;
        }

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(gameObject);
        }
    }
}

