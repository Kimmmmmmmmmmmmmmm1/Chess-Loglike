using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class PhysicsGridSynchronizer : MonoBehaviour
{
    [Header("콜라이더 설정")]
    [Tooltip("기물 UI(RectTransform) 크기 대비 BoxCollider 가로/세로 크기 비율")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float colliderSizeRatio = 0.75f;
    [Tooltip("생성되는 BoxCollider의 Z축 두께")]
    [SerializeField] private float colliderDepth = 1f;

    [Header("물리 이동 / 감속 / 탄성 설정")]
    [Tooltip("기물이 이동 중 감속되는 공기/바닥 저항 (클수록 빨리 멈춤)")]
    [SerializeField] private float linearDamping = 1.2f;
    [Tooltip("기물 회전 저항")]
    [SerializeField] private float angularDamping = 1f;
    [Tooltip("기물 간 기본 탄성 계수 (0 = 안 튕김, 1 = 완전 탄성)")]
    [Range(0f, 1f)]
    [SerializeField] private float pieceBounciness = 0.65f;
    [Tooltip("기물 물리 재질의 운동 마찰 계수")]
    [Range(0f, 1f)]
    [SerializeField] private float dynamicFriction = 0.35f;
    [Tooltip("기물 물리 재질의 정지 마찰 계수")]
    [Range(0f, 1f)]
    [SerializeField] private float staticFriction = 0.35f;

    [Header("충돌 반발력 / 질량감 설정")]
    [Tooltip("기물 충돌 시 질량 비율에 따른 추가 반발 충격량 배율 (클수록 질량 차이에 따른 튕김이 강해짐)")]
    [SerializeField] private float collisionImpulseMultiplier = 0.45f;
    [Tooltip("충돌 판정이 발생하는 최소 상대 속도 제곱값")]
    [SerializeField] private float minCollisionRelativeVelocitySqr = 0.25f;

    [Header("기물 점수별 질량 설정")]
    [Tooltip("1점 기물 질량 (폰)")]
    [SerializeField] private float massScore1 = 1.0f;
    [Tooltip("3점 기물 질량 (나이트, 비숍, 그라스호퍼)")]
    [SerializeField] private float massScore3 = 2.5f;
    [Tooltip("4점 기물 질량 (킹)")]
    [SerializeField] private float massScore4 = 4.2f;
    [Tooltip("5점 기물 질량 (룩, 센타우로스, 캐논)")]
    [SerializeField] private float massScore5 = 4.8f;
    [Tooltip("6점 기물 질량 (아크비숍)")]
    [SerializeField] private float massScore6 = 5.8f;
    [Tooltip("7점 기물 질량 (챈슬러)")]
    [SerializeField] private float massScore7 = 6.8f;
    [Tooltip("9점 기물 질량 (퀸)")]
    [SerializeField] private float massScore9 = 8.5f;
    [Tooltip("기타 점수 기물의 점수당 기본 질량 배율")]
    [SerializeField] private float fallbackMassPerScore = 0.95f;

    [Header("정지 판정 및 타일 스냅 설정")]
    [Tooltip("기물이 멈췄다고 판단하는 속도 임계값")]
    [SerializeField] private float stopVelocity = 0.05f;
    [Tooltip("물리 시뮬레이션 최대 대기 시간 (초)")]
    [SerializeField] private float timeout = 4f;
    [Tooltip("정지 후 가장 가까운 타일 중심으로 스냅되는 연출 시간 (초)")]
    [SerializeField] private float snapDuration = 0.2f;

    private Coroutine monitorRoutine;
    private PhysicsMaterial pieceMaterial;
    private bool synchronizing;
    public bool IsSynchronizing => synchronizing;

    public float GetPieceMass(PieceType type)
    {
        int score = PieceData.GetPieceScore(type);
        return score switch
        {
            1 => massScore1,
            3 => massScore3,
            4 => massScore4,
            5 => massScore5,
            6 => massScore6,
            7 => massScore7,
            9 => massScore9,
            _ => Mathf.Max(1f, score * fallbackMassPerScore)
        };
    }

    public static float GetMass(PieceType type)
    {
        if (PieceFlickLauncher.Instance != null)
        {
            PhysicsGridSynchronizer sync = PieceFlickLauncher.Instance.GetComponent<PhysicsGridSynchronizer>();
            if (sync != null)
            {
                return sync.GetPieceMass(type);
            }
        }

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
        pieceMaterial = CreatePieceMaterial();
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
                    Mathf.Max(0.01f, rect.rect.width * colliderSizeRatio),
                    Mathf.Max(0.01f, rect.rect.height * colliderSizeRatio),
                    colliderDepth);
            }
        }

        body.mass = GetPieceMass(piece.PieceType);
        body.useGravity = false;
        body.linearDamping = linearDamping;
        body.angularDamping = angularDamping;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        body.isKinematic = false;
        body.position = piece.transform.position;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        PiecePhysicsCollisionResponse response = piece.GetComponent<PiecePhysicsCollisionResponse>();
        if (response == null)
        {
            response = piece.gameObject.AddComponent<PiecePhysicsCollisionResponse>();
        }
        response.Configure(collisionImpulseMultiplier, minCollisionRelativeVelocitySqr);
    }

    private PhysicsMaterial CreatePieceMaterial()
    {
        return new PhysicsMaterial("AlkkagiPieceMaterial")
        {
            dynamicFriction = dynamicFriction,
            staticFriction = staticFriction,
            bounciness = pieceBounciness,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine = PhysicsMaterialCombine.Maximum
        };
    }

    private sealed class PiecePhysicsCollisionResponse : MonoBehaviour
    {
        private float impulseMultiplier = 0.45f;
        private float minRelativeVelocitySqr = 0.25f;

        public void Configure(float multiplier, float minVelSqr)
        {
            impulseMultiplier = multiplier;
            minRelativeVelocitySqr = minVelSqr;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Rigidbody body = GetComponent<Rigidbody>();
            Rigidbody other = collision.rigidbody;
            if (body == null || other == null || body.GetEntityId() > other.GetEntityId()) return;
            if (collision.contactCount == 0 || collision.relativeVelocity.sqrMagnitude < minRelativeVelocitySqr) return;

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

            float impulse = closingSpeed * reducedMass * impulseMultiplier;
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

        if (piecesToDestroy.Count > 0)
        {
            ArtifactPopupEffect.Play(AlkkagiArtifact.Id);
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
