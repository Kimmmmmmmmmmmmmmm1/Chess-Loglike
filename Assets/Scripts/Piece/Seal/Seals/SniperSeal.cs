using UnityEngine;

/// <summary>
/// 저격의 인장 (Sniper Seal)
/// 효과: 장거리 기물 전용. 포획 가능 거리에 적 기물이 있으면 이동하지 않고 제자리에서 포획(저격)합니다.
/// </summary>
public class SniperSeal : SealBase
{
    public const string SealName = "저격의 인장";

    public override bool CanSnipeWithoutMoving => true;
}

