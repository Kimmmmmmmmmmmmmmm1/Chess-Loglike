using UnityEngine;

public class RewardService : PersistentManagerBase
{
    public static RewardService Instance { get; private set; }

    [Header("Data")]
    public ShopPieceData pieceData;

    private RewardManager boundUI;
    private bool hasPendingShow = false;
    private int pendingCount;
    private int pendingRarity;
    private bool pendingIsTreasure;

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        base.Awake();
        Instance = this;
    }

    public void RegisterUI(RewardManager ui)
    {
        boundUI = ui;
        // if there is a pending request from before the UI was available, show it now
        if (hasPendingShow && boundUI != null)
        {
            boundUI.ShowRewards(pendingCount, pendingRarity, pendingIsTreasure);
            hasPendingShow = false;
        }
    }

    public void UnregisterUI(RewardManager ui)
    {
        if (boundUI == ui) boundUI = null;
    }

    public void ShowRewards(int count, int rarity, bool isTreasure = false)
    {
        if (boundUI != null)
        {
            boundUI.ShowRewards(count, rarity, isTreasure);
            return;
        }

        // cache if UI is not yet available
        pendingCount = count;
        pendingRarity = rarity;
        pendingIsTreasure = isTreasure;
        hasPendingShow = true;
    }

    public override void ResetForNewRun()
    {
        // Clear any pending UI requests between runs
        hasPendingShow = false;
        pendingCount = 0;
        pendingRarity = 0;
        pendingIsTreasure = false;
    }
}
