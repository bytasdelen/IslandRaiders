using Unity.Netcode;
using UnityEngine;

// oyuncunun ilerlemesini spawn'da yukler, despawn'da (cikis veya host kapanisi) kaydeder.
// pozisyon bilerek kaydedilmiyor: gemiler her oturum rastgele spawn oldugu icin
// kayitli konum artik var olmayan bir guvertenin ustune denk gelebilirdi
public class PlayerSaveState : NetworkBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerInventory inventory;

    private string playerName;

    // OnNetworkSpawn yerine Start: ayni objedeki diger behaviour'larin spawn kurulumu
    // (envanterin baslangic slotlari, canin max'a cekilmesi) bitmeden yukleme yapilmasin
    private void Start()
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        playerName = SaveSystem.GetName(OwnerClientId);
        SaveSystem.PlayerEntry entry = SaveSystem.Find(playerName);
        if (entry == null)
        {
            return;
        }

        inventory.Coins.Value = entry.coins;
        health.CurrentHealth.Value = Mathf.Clamp(entry.health, 1, health.MaxHealth);
        for (int i = 0; i < PlayerInventory.SlotCount && i < entry.slots.Length; i++)
        {
            inventory.Slots[i] = entry.slots[i];
            inventory.Ammo[i] = entry.ammo[i];
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer || string.IsNullOrEmpty(playerName))
        {
            return;
        }

        SaveSystem.PlayerEntry entry = new SaveSystem.PlayerEntry
        {
            playerName = playerName,
            coins = inventory.Coins.Value,
            health = health.CurrentHealth.Value,
            slots = new int[PlayerInventory.SlotCount],
            ammo = new int[PlayerInventory.SlotCount],
        };
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            entry.slots[i] = inventory.Slots[i];
            entry.ammo[i] = inventory.Ammo[i];
        }
        SaveSystem.Store(entry);
    }
}
