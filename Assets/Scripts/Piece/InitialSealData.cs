using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewInitialSealData", menuName = "Janggi/Initial Seal Data")]
public class InitialSealData : ScriptableObject
{
    [Tooltip("게임 시작 시 인장 인벤토리에 지급할 인장 목록")]
    public List<SealData> initialSeals = new List<SealData>();
}
