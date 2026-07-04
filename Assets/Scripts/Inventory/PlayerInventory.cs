using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    public const int SlotCount = 6;

    [SerializeField] private ItemDatabase database;
    [SerializeField] private int[] startingItems;

    // her slottaki itemId, -1 = bos slot
    private readonly NetworkList<int> slots = new NetworkList<int>();
    private readonly NetworkVariable<int> selectedSlot =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public ItemDatabase Database => database;
    public NetworkList<int> Slots => slots;
    public NetworkVariable<int> SelectedSlot => selectedSlot;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                int startId = i < startingItems.Length ? startingItems[i] : -1;
                slots.Add(startId);
            }
        }
    }

    public int GetSelectedItemId()
    {
        return slots[selectedSlot.Value];
    }

    // owner kendi elindeki slotu secer
    public void SelectSlot(int index)
    {
        if (IsOwner && index >= 0 && index < SlotCount)
        {
            selectedSlot.Value = index;
        }
    }

    // ilk bos slota ekler, envanter doluysa false doner
    public bool TryAddItem(int itemId)
    {
        if (!IsServer)
        {
            return false;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == -1)
            {
                slots[i] = itemId;
                return true;
            }
        }
        return false;
    }

    public void RemoveSelectedItem()
    {
        if (IsServer)
        {
            slots[selectedSlot.Value] = -1;
        }
    }
}
