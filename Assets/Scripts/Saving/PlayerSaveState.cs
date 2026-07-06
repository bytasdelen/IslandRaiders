using Unity.Netcode;
using UnityEngine;

// oyuncunun ilerlemesini spawn'da onbellekten yukler, despawn'da (cikis) onbellege geri yazar.
// diske asil yazma dunya kaydinda (GameManager) olur; boylece cikmis oyuncular da snapshot'a girer.
// konum da kaydedilir: ayni isimle donen oyuncu kaydedildigi yerde dogar. (Gemi güvertesinde
// kaydedildiyse ve o gemi artik orada degilse suya düsebilir - kabul edilen sinir durum.)
public class PlayerSaveState : NetworkBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerInventory inventory;

    private CharacterController characterController;
    private string playerName;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        playerName = SaveSystem.GetName(OwnerClientId);
        SaveSystem.PlayerEntry entry = SaveSystem.GetPlayer(playerName);
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

        if (entry.hasPosition)
        {
            // konumu sahibi client uygular (CharacterController kapatilip acilmali, yoksa teleport'u ezer)
            ClientRpcParams targetOwnerOnly = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            RestorePositionClientRpc(entry.position, entry.yaw, targetOwnerOnly);
        }
    }

    [ClientRpc]
    private void RestorePositionClientRpc(Vector3 position, float yaw, ClientRpcParams clientRpcParams = default)
    {
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && !string.IsNullOrEmpty(playerName))
        {
            SaveSystem.SetPlayer(BuildEntry());
        }
    }

    // dunya kaydi sirasinda canli oyuncularin son durumunu okumak icin de kullanilir
    public SaveSystem.PlayerEntry BuildEntry()
    {
        SaveSystem.PlayerEntry entry = new SaveSystem.PlayerEntry
        {
            playerName = playerName,
            coins = inventory.Coins.Value,
            health = health.CurrentHealth.Value,
            slots = new int[PlayerInventory.SlotCount],
            ammo = new int[PlayerInventory.SlotCount],
            position = transform.position,
            yaw = transform.eulerAngles.y,
            hasPosition = true,
        };
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            entry.slots[i] = inventory.Slots[i];
            entry.ammo[i] = inventory.Ammo[i];
        }
        return entry;
    }
}
