using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelfDestructSeal : SealBase
{
    [Header("Explosion Timing")]
    [SerializeField] private float explosionDelayAfterOwnerDeath = 0.5f;

    public override void OnOwnerDestroyed(PieceController killer, Vector2Int ownerPosition)
    {
        if (owner == null || owner.Type != PieceType.Soldier)
        {
            return;
        }

        if (PieceManager.Instance == null)
        {
            return;
        }

        List<PieceController> targets = new List<PieceController>();

        PieceController leftPiece = PieceManager.Instance.GetPieceAt(ownerPosition + Vector2Int.left);
        if (leftPiece != null)
        {
            targets.Add(leftPiece);
        }

        PieceController rightPiece = PieceManager.Instance.GetPieceAt(ownerPosition + Vector2Int.right);
        if (rightPiece != null && !targets.Contains(rightPiece))
        {
            targets.Add(rightPiece);
        }

        if (killer != null && killer.gridPosition.HasValue && !targets.Contains(killer))
        {
            targets.Add(killer);
        }

        if (targets.Count == 0)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                PieceController target = targets[i];
                if (target != null)
                {
                    PieceManager.Instance.RemovePiece(target);
                }
            }

            return;
        }

        PieceManager.Instance.StartCoroutine(ExplodeAfterDelay(targets));
    }

    private IEnumerator ExplodeAfterDelay(List<PieceController> targets)
    {
        if (explosionDelayAfterOwnerDeath > 0f)
        {
            yield return new WaitForSecondsRealtime(explosionDelayAfterOwnerDeath);
        }

        for (int i = 0; i < targets.Count; i++)
        {
            PieceController target = targets[i];
            if (target != null)
            {
                PieceManager.Instance.RemovePiece(target);
            }
        }
    }
}
