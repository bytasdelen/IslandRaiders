using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float headMultiplier = 2f;
    [SerializeField] private PlayerNotifications notifications;

    public int MaxHealth => maxHealth;
    public readonly NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();

    // bolgeye gelen hasari yuzde olarak azaltir (0 = korumasiz, 0.5 = %50 azaltma)
    private readonly NetworkVariable<float> headArmor = new NetworkVariable<float>();
    private readonly NetworkVariable<float> bodyArmor = new NetworkVariable<float>();

    private Vector3 spawnPosition;
    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        spawnPosition = transform.position;

        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
        }
    }

    public void ApplyDamage(int weaponDamage, HitRegion region, ulong attackerClientId)
    {
        if (!IsServer)
        {
            return;
        }

        float damage = weaponDamage;
        if (region == HitRegion.Head)
        {
            damage *= headMultiplier;
            damage *= 1f - headArmor.Value;
        }
        else
        {
            damage *= 1f - bodyArmor.Value;
        }

        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(damage));
        CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - finalDamage);

        if (CurrentHealth.Value == 0)
        {
            Respawn();
        }
    }

    // kask/zirh takildiginda çağrılır
    public void SetArmor(HitRegion region, float reduction)
    {
        if (!IsServer)
        {
            return;
        }

        if (region == HitRegion.Head)
        {
            headArmor.Value = Mathf.Clamp01(reduction);
        }
        else
        {
            bodyArmor.Value = Mathf.Clamp01(reduction);
        }
    }

    private void Respawn()
    {
        CurrentHealth.Value = maxHealth;
        notifications.NotifyOwner("You died!");

        ClientRpcParams targetOwnerOnly = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        TeleportToSpawnClientRpc(spawnPosition, targetOwnerOnly);
    }

    [ClientRpc]
    private void TeleportToSpawnClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
    {
        characterController.enabled = false;
        transform.position = position;
        characterController.enabled = true;
    }
}
