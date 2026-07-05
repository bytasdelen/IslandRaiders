using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    public const int SlotCount = 6;

    [SerializeField] private ItemDatabase database;
    [SerializeField] private int[] startingItems;

    // her slottaki itemId, -1 = bos slot
    private readonly NetworkList<int> slots = new NetworkList<int>();
    // slots ile ayni index'i kullanir; silah degilse anlamsiz (0 kalir)
    private readonly NetworkList<int> ammo = new NetworkList<int>();
    private readonly NetworkVariable<int> selectedSlot =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public readonly NetworkVariable<int> Coins = new NetworkVariable<int>();

    public ItemDatabase Database => database;
    public NetworkList<int> Slots => slots;
    public NetworkList<int> Ammo => ammo;
    public NetworkVariable<int> SelectedSlot => selectedSlot;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                int startId = i < startingItems.Length ? startingItems[i] : -1;
                slots.Add(startId);
                ammo.Add(startId == -1 ? 0 : GetMaxAmmo(startId));
            }
        }
    }

    public int GetSelectedItemId()
    {
        return slots[selectedSlot.Value];
    }

    public int GetSelectedAmmo()
    {
        return ammo[selectedSlot.Value];
    }

    public void ConsumeSelectedAmmo()
    {
        if (IsServer && ammo[selectedSlot.Value] > 0)
        {
            ammo[selectedSlot.Value]--;
        }
    }

    // owner kendi elindeki slotu secer
    public void SelectSlot(int index)
    {
        if (IsOwner && index >= 0 && index < SlotCount)
        {
            selectedSlot.Value = index;
        }
    }

    // ilk bos slota ekler, envanter doluysa false doner.
    // ammoOverride verilmezse (-1) silahin max mermisiyle baslar - yerden alinan,
    // kalan mermisi belli olan bir silah icin gercek deger WorldItem'dan gelir
    public bool TryAddItem(int itemId, int ammoOverride = -1)
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
                ammo[i] = ammoOverride >= 0 ? ammoOverride : GetMaxAmmo(itemId);
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
            ammo[selectedSlot.Value] = 0;
        }
    }

    public void AddCoins(int amount)
    {
        if (IsServer)
        {
            Coins.Value += amount;
        }
    }

    // silah degilse (HeldPrefab'inda Weapon component'i yoksa) 0 doner
    private int GetMaxAmmo(int itemId)
    {
        ItemDefinition def = database.Get(itemId);
        if (def == null || def.HeldPrefab == null)
        {
            return 0;
        }

        Weapon weapon = def.HeldPrefab.GetComponent<Weapon>();
        return weapon != null ? weapon.MaxAmmo : 0;
    }
}
