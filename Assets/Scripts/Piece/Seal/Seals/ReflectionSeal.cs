using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 반사의 인장 (Seal of Reflection):
/// 장착한 기물이 보드 외곽(벽)을 향해 이동할 때, 밖으로 나가는 대신
/// 입사각과 반사각에 맞춰 1회 튕겨서 이동 및 공격할 수 있습니다.
/// (비숍, 아치비숍, 퀸, 나이트 전용)
/// </summary>
public class ReflectionSeal : SealBase
{
    // 보드 위에서 계산된 반사 이동 경로의 경유 지점(벽에 부딪힌 지점)
    private readonly Dictionary<Vector2Int, Vector2Int> reflectionWaypoints = new Dictionary<Vector2Int, Vector2Int>();

    public override void ModifyMoves(ref List<Vector2Int> moves, Vector2Int currentPos, bool isEnemy, System.Func<Vector2Int, bool> validator = null, System.Func<Vector2Int, bool> isOccupied = null)
    {
        reflectionWaypoints.Clear();

        PieceType type = owner != null ? owner.Type : PieceType.Bishop;
        GridManager gridManager = PieceManager.Instance != null ? PieceManager.Instance.gridManager : Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null) return;

        Vector2Int min = gridManager.gridMinBounds;
        int minX = min.x;
        int maxX = min.x + gridManager.boardWidth - 1;
        int minY = min.y;
        int maxY = min.y + gridManager.boardHeight - 1;

        System.Func<Vector2Int, bool> checkCanMove = target =>
        {
            if (validator != null) return validator(target);
            if (owner != null) return owner.CanMoveTo(target);
            return true;
        };

        System.Func<Vector2Int, bool> checkOccupied = target =>
        {
            if (isOccupied != null) return isOccupied(target);
            if (owner != null) return owner.IsOccupied(target);
            return false;
        };

        System.Func<Vector2Int, bool> checkDestroyed = target =>
        {
            GridCell cell = gridManager.GetGridCell(target);
            return cell != null && cell.isDestroyed;
        };

        // 1. 슬라이딩 기물 (비숍, 아치비숍, 퀸) 반사 계산
        List<Vector2Int> slidingDirs = GetSlidingDirectionsForType(type);
        if (slidingDirs != null && slidingDirs.Count > 0)
        {
            foreach (var dir in slidingDirs)
            {
                Vector2Int curr = currentPos + dir;
                bool hitWall = false;
                Vector2Int bouncePos = currentPos;
                Vector2Int reflectedDir = Vector2Int.zero;

                // 1구간: 벽을 향해 전진
                while (true)
                {
                    // 보드 바깥으로 나가려고 함 -> 벽 충돌 발생!
                    if (curr.x < minX || curr.x > maxX || curr.y < minY || curr.y > maxY)
                    {
                        bouncePos = curr - dir; // 벽 바로 안쪽의 타일
                        reflectedDir = ReflectDirection(dir, curr.x < minX || curr.x > maxX, curr.y < minY || curr.y > maxY);
                        if (reflectedDir != Vector2Int.zero) hitWall = true;
                        break;
                    }

                    // 이동 경로 중간에 파괴된 셀이 있으면 그 방향은 반사 불가 (단절)
                    if (checkDestroyed(curr))
                    {
                        break;
                    }

                    // 이동 경로 중간에 다른 기물이 가로막고 있으면 반사 불가
                    if (checkOccupied(curr))
                    {
                        break;
                    }

                    curr += dir;
                }

                // 2구간: 벽에 튕겨서 반사각으로 전진
                if (hitWall && reflectedDir != Vector2Int.zero)
                {
                    Vector2Int refCurr = bouncePos + reflectedDir;

                    while (refCurr.x >= minX && refCurr.x <= maxX && refCurr.y >= minY && refCurr.y <= maxY)
                    {
                        if (checkDestroyed(refCurr))
                        {
                            break;
                        }

                        if (checkCanMove(refCurr))
                        {
                            if (!moves.Contains(refCurr))
                            {
                                moves.Add(refCurr);
                            }

                            if (!reflectionWaypoints.ContainsKey(refCurr))
                            {
                                reflectionWaypoints[refCurr] = bouncePos;
                            }
                        }

                        // 착지 지점에 다른 기물이 있다면 (적군 포획 포함) 그 지점까지만 진행하고 중단
                        if (checkOccupied(refCurr))
                        {
                            break;
                        }

                        refCurr += reflectedDir;
                    }
                }
            }
        }

        // 2. 나이트 행마 반사 계산 (나이트, 아치비숍)
        if (type == PieceType.Knight || type == PieceType.Archbishop)
        {
            Vector2Int[] knightOffsets =
            {
                new(2, 1), new(2, -1), new(-2, 1), new(-2, -1),
                new(1, 2), new(1, -2), new(-1, 2), new(-1, -2)
            };

            foreach (var offset in knightOffsets)
            {
                Vector2Int target = currentPos + offset;
                // 보드 바깥으로 나가는 나이트 도약만 반사 대상
                if (target.x >= minX && target.x <= maxX && target.y >= minY && target.y <= maxY)
                {
                    continue;
                }

                bool outX = target.x < minX || target.x > maxX;
                bool outY = target.y < minY || target.y > maxY;

                // 벽(외곽 경계)에서 입사/반사 부호 반전
                Vector2Int reflectedOffset = offset;
                if (outX) reflectedOffset.x = -reflectedOffset.x;
                if (outY) reflectedOffset.y = -reflectedOffset.y;

                Vector2Int reflectedTarget = currentPos + reflectedOffset;

                // 반사된 최종 착지 칸이 보드 내부이고, 이동 가능하면 추가
                if (reflectedTarget.x >= minX && reflectedTarget.x <= maxX && reflectedTarget.y >= minY && reflectedTarget.y <= maxY)
                {
                    if (!checkDestroyed(reflectedTarget) && checkCanMove(reflectedTarget))
                    {
                        if (!moves.Contains(reflectedTarget))
                        {
                            moves.Add(reflectedTarget);
                        }

                        // 나이트가 튕겨나간 벽의 경계 타일을 경유지로 설정 (자연스러운 2단계 도약 연출)
                        Vector2Int bouncePos = currentPos;
                        if (outX)
                        {
                            bouncePos.x = target.x < minX ? minX : maxX;
                            bouncePos.y = currentPos.y + (offset.y / 2);
                        }
                        else if (outY)
                        {
                            bouncePos.y = target.y < minY ? minY : maxY;
                            bouncePos.x = currentPos.x + (offset.x / 2);
                        }

                        if (!reflectionWaypoints.ContainsKey(reflectedTarget))
                        {
                            reflectionWaypoints[reflectedTarget] = bouncePos;
                        }
                    }
                }
            }
        }
    }

    public override void OnBeforeMove(Vector2Int fromPos, Vector2Int toPos)
    {
        if (owner != null && reflectionWaypoints.TryGetValue(toPos, out Vector2Int waypoint))
        {
            owner.moveWaypoint = waypoint;
        }
    }

    public override List<Vector2Int> GetPreviewAdditionalMovementOffsets(PieceType pieceType, bool isEnemy)
    {
        List<Vector2Int> offsets = new List<Vector2Int>();
        int min = -3;
        int max = 3;

        // 슬라이딩 기물 미리보기
        List<Vector2Int> slidingDirs = GetSlidingDirectionsForType(pieceType);
        if (slidingDirs != null && slidingDirs.Count > 0)
        {
            foreach (var dir in slidingDirs)
            {
                Vector2Int curr = dir;
                Vector2Int bouncePos = Vector2Int.zero;
                Vector2Int reflectedDir = Vector2Int.zero;
                bool hitWall = false;

                while (true)
                {
                    if (curr.x < min || curr.x > max || curr.y < min || curr.y > max)
                    {
                        bouncePos = curr - dir;
                        reflectedDir = ReflectDirection(dir, curr.x < min || curr.x > max, curr.y < min || curr.y > max);
                        hitWall = true;
                        break;
                    }
                    curr += dir;
                }

                if (hitWall && reflectedDir != Vector2Int.zero)
                {
                    Vector2Int refCurr = bouncePos + reflectedDir;
                    while (refCurr.x >= min && refCurr.x <= max && refCurr.y >= min && refCurr.y <= max)
                    {
                        if (!offsets.Contains(refCurr) && refCurr != Vector2Int.zero)
                        {
                            offsets.Add(refCurr);
                        }
                        refCurr += reflectedDir;
                    }
                }
            }
        }

        // 나이트 도약 미리보기
        if (pieceType == PieceType.Knight || pieceType == PieceType.Archbishop)
        {
            Vector2Int[] knightOffsets =
            {
                new(2, 1), new(2, -1), new(-2, 1), new(-2, -1),
                new(1, 2), new(1, -2), new(-1, 2), new(-1, -2)
            };

            foreach (var offset in knightOffsets)
            {
                bool outX = offset.x < min || offset.x > max;
                bool outY = offset.y < min || offset.y > max;

                if (outX || outY)
                {
                    Vector2Int refOffset = offset;
                    if (outX) refOffset.x = -refOffset.x;
                    if (outY) refOffset.y = -refOffset.y;

                    if (!offsets.Contains(refOffset) && refOffset != Vector2Int.zero)
                    {
                        offsets.Add(refOffset);
                    }
                }
            }
        }

        return offsets;
    }

    private static Vector2Int ReflectDirection(Vector2Int dir, bool hitVerticalWall, bool hitHorizontalWall)
    {
        Vector2Int refDir = dir;
        if (hitVerticalWall) refDir.x = -refDir.x;
        if (hitHorizontalWall) refDir.y = -refDir.y;

        if (refDir == -dir)
        {
            return Vector2Int.zero;
        }

        return refDir;
    }

    public static List<Vector2Int> GetSlidingDirectionsForType(PieceType type)
    {
        List<Vector2Int> dirs = new List<Vector2Int>();
        switch (type)
        {
            case PieceType.Bishop:
            case PieceType.Archbishop:
                dirs.Add(new Vector2Int(1, 1));
                dirs.Add(new Vector2Int(1, -1));
                dirs.Add(new Vector2Int(-1, 1));
                dirs.Add(new Vector2Int(-1, -1));
                break;

            case PieceType.Queen:
            case PieceType.Amazon:
                dirs.Add(new Vector2Int(1, 1));
                dirs.Add(new Vector2Int(1, -1));
                dirs.Add(new Vector2Int(-1, 1));
                dirs.Add(new Vector2Int(-1, -1));
                dirs.Add(Vector2Int.up);
                dirs.Add(Vector2Int.down);
                dirs.Add(Vector2Int.left);
                dirs.Add(Vector2Int.right);
                break;
        }
        return dirs;
    }
}
