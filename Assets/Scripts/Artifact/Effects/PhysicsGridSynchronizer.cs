using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class PhysicsGridSynchronizer : MonoBehaviour
{
    [SerializeField] private float stopVelocity = 0.05f;
    [SerializeField] private float timeout = 4f;
    [SerializeField] private float snapDuration = 0.2f;
    [SerializeField] private float linearDamping = 1.4f;
    [SerializeField] private float pieceBounciness = 0.65f;
    private Coroutine monitorRoutine;
    private PhysicsMaterial pieceMaterial;
    private bool synchronizing;
    public bool IsSynchronizing => synchronizing;

    public static float GetMass(PieceType type)
    {
        int score = PieceData.GetPieceScore(type);
        return score switch
        {
            1 => 1f,       // Pawn
            3 => 2.5f,     // Knight, Bishop, Grasshopper
            4 => 4.2f,     // King
            5 => 4.8f,     // Rook, Centaur, Cannon
            6 => 5.8f,     // Archbishop
            7 => 6.8f,     // Chancellor
            9 => 8.5f,     // Queen
            _ => Mathf.Max(1f, score * 0.95f)
        };
    }

    public void BeginMonitoring()
    {
        if (synchronizing) return;
        if (monitorRoutine != null) StopCoroutine(monitorRoutine);
        synchronizing = true;
        PrepareBodies();
        monitorRoutine = StartCoroutine(MonitorRoutine());
    }

    private void PrepareBodies()
    {
        if (PieceManager.Instance == null) return;
        pieceMaterial ??= CreatePieceMaterial();
        foreach (PieceController piece in PieceManager.Instance.Pieces)
        {
            if (piece == null || piece.CurrentLocation != PieceLocation.Board) continue;
            ConfigurePhysics(piece, pieceMaterial);
        }
        Physics.SyncTransforms();
    }

    private void ConfigurePhysics(PieceController piece, PhysicsMaterial material)
    {
        Rigidbody body = piece.GetComponent<Rigidbody>();
        if (body == null) body = piece.gameObject.AddComponent<Rigidbody>();

        Collider collider = piece.GetComponent<Collider>();
        if (collider == null) collider = piece.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.sharedMaterial = material;
        if (collider is BoxCollider box)
        {
            RectTransform rect = piece.GetComponent<RectTransform>();
            if (rect != null)
            {
                box.size = new Vector3(
                    Mathf.Max(0.01f, rect.rect.width * 0.85f),
                    Mathf.Max(0.01f, rect.rect.height * 0.85f),
                    1f);
            }
        }

        body.mass = GetMass(piece.PieceType);
        body.useGravity = false;
        body.linearDamping = linearDamping;
        body.angularDamping = 1f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        body.isKinematic = false;
        body.position = piece.transform.position;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        if (piece.GetComponent<PiecePhysicsCollisionResponse>() == null)
        {
            piece.gameObject.AddComponent<PiecePhysicsCollisionResponse>();
        }
    }

    private PhysicsMaterial CreatePieceMaterial()
    {
        return new PhysicsMaterial("AlkkagiPieceMaterial")
        {
            dynamicFriction = 0.35f,
            staticFriction = 0.35f,
            bounciness = pieceBounciness,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine = PhysicsMaterialCombine.Maximum
        };
    }

    private sealed class PiecePhysicsCollisionResponse : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            Rigidbody body = GetComponent<Rigidbody>();
            Rigidbody other = collision.rigidbody;
            if (body == null || other == null || body.GetEntityId() > other.GetEntityId()) return;
            if (collision.contactCount == 0 || collision.relativeVelocity.sqrMagnitude < 0.25f) return;

            SoundManager.Instance?.PlaySFX(SFXType.Move);

            // 두 기물의 질량 비율이 확실히 느껴지도록 탄성 운동량 보존 기반의 보조 충격량 적용
            // reducedMass = (m1 * m2) / (m1 + m2) -> 무거운 기물은 덜 밀리고 가벼운 기물은 크게 튕겨나감
            float totalMass = Mathf.Max(0.01f, body.mass + other.mass);
            float reducedMass = (body.mass * other.mass) / totalMass;
            Vector3 direction = (other.worldCenterOfMass - body.worldCenterOfMass).normalized;
            float closingSpeed = Vector3.Dot(collision.relativeVelocity, direction);
            if (closingSpeed <= 0f)
            {
                closingSpeed = collision.relativeVelocity.magnitude;
            }

            float impulse = closingSpeed * reducedMass * 0.45f;
            Vector3 extraImpulse = direction * impulse;
            body.AddForce(-extraImpulse, ForceMode.Impulse);
            other.AddForce(extraImpulse, ForceMode.Impulse);
        }
    }

    private IEnumerator MonitorRoutine()
    {
        float elapsed = 0f;
        yield return new WaitForFixedUpdate();
        while (elapsed < timeout && !AllPiecesStopped())
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        yield return StartCoroutine(SnapRoutine());
        monitorRoutine = null;
        synchronizing = false;
    }

    private bool AllPiecesStopped()
    {
        if (PieceManager.Instance == null) return true;
        foreach (PieceController piece in PieceManager.Instance.Pieces)
        {
            if (piece == null || piece.CurrentLocation != PieceLocation.Board) continue;
            Rigidbody body = piece.GetComponent<Rigidbody>();
            if (body != null && body.linearVelocity.magnitude >= stopVelocity) return false;
        }
        return true;
    }

    private IEnumerator SnapRoutine()
    {
        GridManager grid = PieceManager.Instance != null ? PieceManager.Instance.gridManager : null;
        if (grid == null) yield break;

        var occupants = new Dictionary<Vector2Int, PieceController>();
        var pending = new List<Tween>();
        var pieces = new List<PieceController>(PieceManager.Instance.Pieces);
        var uniquePieces = new HashSet<PieceController>();
        var piecesToDestroy = new List<PieceController>();
        foreach (PieceController piece in pieces)
        {
            if (piece == null ||
                !uniquePieces.Add(piece) ||
                piece.CurrentLocation != PieceLocation.Board) continue;
            Rigidbody body = piece.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
                body.constraints = RigidbodyConstraints.FreezeRotation;
            }

            DOTween.Kill(piece.transform, false);
            Vector2 local = GetLocalPosition(piece);
            Vector2Int? nearest = grid.GetNearestCell(local);
            if (!nearest.HasValue)
            {
                piecesToDestroy.Add(piece);
                continue;
            }
            Vector2Int target = FindFreeCell(nearest.Value, occupants, grid);
            occupants[target] = piece;
            piece.SetPhysicsGridPosition(target);
            pending.Add(SnapPiece(piece, grid.GridToUiPosition(target), snapDuration));
        }

        for (int i = 0; i < piecesToDestroy.Count; i++)
        {
            PieceManager.Instance.RemovePieceFromPhysics(piecesToDestroy[i]);
        }

        for (int i = 0; i < pending.Count; i++)
        {
            Tween t = pending[i];
            if (t != null && t.IsActive() && t.IsPlaying())
            {
                yield return t.WaitForCompletion();
            }
        }
        foreach (PieceController piece in uniquePieces)
        {
            if (piece == null || piece.CurrentLocation != PieceLocation.Board) continue;
            Vector2Int? position = piece.GridPosition;
            if (!position.HasValue) continue;
            RectTransform rect = piece.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = grid.GridToUiPosition(position.Value);
            }
        }
        PieceManager.Instance.EnsurePositionCacheReady();
        PieceManager.Instance.UpdateThreatenedStatus();
        yield return PieceManager.Instance.WaitForThreatenedStatusReady();
        RemovePhysicsComponents();
        TurnManager.Instance?.EndTurn();
    }

    private void RemovePhysicsComponents()
    {
        if (PieceManager.Instance == null) return;
        foreach (PieceController piece in PieceManager.Instance.Pieces)
        {
            if (piece == null) continue;
            Rigidbody body = piece.GetComponent<Rigidbody>();
            if (body != null) Destroy(body);
            Collider collider = piece.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
    }

    private static Vector2 GetLocalPosition(PieceController piece)
    {
        RectTransform rect = piece.GetComponent<RectTransform>();
        if (rect == null)
        {
            return piece.transform.localPosition;
        }

        RectTransform parent = rect.parent as RectTransform;
        if (parent == null)
        {
            return rect.anchoredPosition;
        }

        Vector3 parentLocalPosition = parent.InverseTransformPoint(piece.transform.position);
        return new Vector2(parentLocalPosition.x, parentLocalPosition.y);
    }

    private static Tween SnapPiece(PieceController piece, Vector2 target, float duration)
    {
        RectTransform rect = piece.GetComponent<RectTransform>();
        if (rect != null) return rect.DOAnchorPos(target, duration).SetEase(Ease.OutQuad);
        return piece.transform.DOLocalMove(target, duration).SetEase(Ease.OutQuad);
    }

    private static Vector2Int FindFreeCell(Vector2Int start, Dictionary<Vector2Int, PieceController> occupants, GridManager grid)
    {
        if (!occupants.ContainsKey(start)) return start;
        var queue = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int> { start };
        queue.Enqueue(start);
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = current + directions[i];
                if (!grid.IsInBounds(next) || !visited.Add(next)) continue;
                if (!occupants.ContainsKey(next)) return next;
                queue.Enqueue(next);
            }
        }
        return start;
    }
}
