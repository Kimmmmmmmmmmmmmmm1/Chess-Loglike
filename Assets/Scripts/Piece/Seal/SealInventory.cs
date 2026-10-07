using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 소유한 모든 인장을 관리하는 인벤토리입니다.
/// </summary>
public class SealInventory : MonoBehaviour
{
    public static SealInventory Instance { get; private set; }

    [Header("Owned Seals")]
    [SerializeField] private List<SealData> ownedSeals = new List<SealData>();

    public IReadOnlyList<SealData> OwnedSeals => ownedSeals;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 인벤토리에 새 인장을 추가합니다.
    /// </summary>
    public void AddSeal(SealData seal)
    {
        if (seal == null) return;
        int emptyIndex = ownedSeals.IndexOf(null);
        if (emptyIndex >= 0)
        {
            ownedSeals[emptyIndex] = seal;
        }
        else
        {
            ownedSeals.Add(seal);
        }
        CollectionManager.EnsureInstance().RecordSeal(seal);
    }

    /// <summary>
    /// 인벤토리에서 특정 인장의 현재 인덱스를 반환합니다.
    /// </summary>
    public int GetSealIndex(SealData seal)
    {
        if (seal == null) return -1;
        return ownedSeals.IndexOf(seal);
    }

    /// <summary>
    /// 인벤토리의 지정된 위치에 인장을 삽입합니다.
    /// </summary>
    public void InsertSeal(SealData seal, int index)
    {
        if (seal == null) return;

        int clampedIndex = Mathf.Max(0, index);
        EnsureSlot(clampedIndex);
        ownedSeals[clampedIndex] = seal;
        CollectionManager.EnsureInstance().RecordSeal(seal);
    }

    public bool MoveSeal(SealData seal, int targetIndex)
    {
        int currentIndex = GetSealIndex(seal);
        return MoveSeal(currentIndex, targetIndex);
    }

    public bool MoveSeal(int currentIndex, int targetIndex)
    {
        if (currentIndex < 0 || currentIndex >= ownedSeals.Count) return false;
        SealData seal = ownedSeals[currentIndex];
        if (seal == null) return false;

        int clampedIndex = Mathf.Max(0, targetIndex);
        EnsureSlot(clampedIndex);

        ownedSeals[currentIndex] = null;
        ownedSeals[clampedIndex] = seal;
        return true;
    }

    public bool SwapSeals(int firstIndex, int secondIndex)
    {
        if (firstIndex < 0 || firstIndex >= ownedSeals.Count) return false;
        if (secondIndex < 0) return false;
        EnsureSlot(secondIndex);
        if (firstIndex == secondIndex) return true;

        SealData temp = ownedSeals[firstIndex];
        ownedSeals[firstIndex] = ownedSeals[secondIndex];
        ownedSeals[secondIndex] = temp;
        return true;
    }

    public bool ReplaceSeal(int index, SealData seal)
    {
        if (seal == null) return false;
        if (index < 0) return false;

        EnsureSlot(index);
        ownedSeals[index] = seal;
        CollectionManager.EnsureInstance().RecordSeal(seal);
        return true;
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= ownedSeals.Count) return;
        ownedSeals[index] = null;
    }

    /// <summary>
    /// 인벤토리에서 특정 인장을 제거합니다.
    /// </summary>
    public bool RemoveSeal(SealData seal)
    {
        if (seal == null) return false;
        int index = ownedSeals.IndexOf(seal);
        if (index == -1) return false;

        ownedSeals[index] = null;
        return true;
    }

    /// <summary>
    /// 인벤토리를 비웁니다.
    /// </summary>
    public void ClearSeals()
    {
        ownedSeals.Clear();
    }

    private void EnsureSlot(int index)
    {
        while (ownedSeals.Count <= index)
        {
            ownedSeals.Add(null);
        }
    }
}
