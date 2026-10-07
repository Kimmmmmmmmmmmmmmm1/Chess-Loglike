using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 게임 상태에 따라 인장 인벤토리 UI를 제어하고, 소유한 인장 목록을 표시합니다.
/// </summary>
public class SealInventoryPanel : MonoBehaviour, ISealDropTarget
{
    [Header("UI References")]
    [SerializeField] private GameObject inventoryPanel;      // 인벤토리 전체 패널
    [SerializeField] private GameObject draggableSealPrefab; // 드래그 가능한 인장 UI 프리팹

    // 자식 오브젝트에서 SealInventorySlot들을 자동으로 찾아옵니다.
    private List<SealInventorySlot> sealSlots = new List<SealInventorySlot>();

    private void Start()
    {
        if (inventoryPanel == null)
        {
            inventoryPanel = gameObject;
        }

        GetComponentsInChildren<SealInventorySlot>(true, sealSlots);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            OnGameStateChanged(GameStateManager.Instance.CurrentState);
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Prepare)
        {
            RefreshSealItems();
        }
    }

    /// <summary>
    /// SealInventory의 데이터를 기반으로 UI 아이템들을 새로고침합니다.
    /// </summary>
    public void RefreshSealItems()
    {
        if (SealInventory.Instance == null || draggableSealPrefab == null || sealSlots.Count == 0) return;

        var ownedSeals = SealInventory.Instance.OwnedSeals;
        int sealCount = ownedSeals.Count;

        // 슬롯에 인장 할당
        for (int i = 0; i < sealSlots.Count; i++)
        {
            if (i < sealCount)
            {
                if (ownedSeals[i] != null)
                {
                    sealSlots[i].SetSeal(ownedSeals[i], draggableSealPrefab, i);
                }
                else
                {
                    sealSlots[i].ClearSeal(i);
                }
            }
            else
            {
                sealSlots[i].ClearSeal(i);
            }
        }
    }

    // IDropHandler 인터페이스의 OnDrop 메서드
    public void OnDrop(PointerEventData eventData)
    {
        // DraggableSeal.OnEndDrag에서 드롭 대상 판정과 처리를 한 번만 수행합니다.
    }

    // ISealDropTarget 인터페이스의 HandleSealDrop 메서드
    public void HandleSealDrop(DraggableSeal draggableSeal)
    {
        if (draggableSeal.SourcePiece != null)
        {
            SealInventory.Instance?.AddSeal(draggableSeal.SealData);
        }

        RefreshSealItems();
        draggableSeal.MarkDropHandledSuccessfully();
    }
}
