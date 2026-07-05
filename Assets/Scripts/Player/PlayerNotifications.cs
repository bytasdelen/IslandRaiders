using Unity.Netcode;
using UnityEngine;

// sunucudan sadece kendi sahibine (owner) gecici bir bildirim gondermek icin ortak yol;
// PlayerWeapon/PlayerInteraction/PlayerHealth gibi sunucu tarafinda calisan yerler kullanir
public class PlayerNotifications : NetworkBehaviour
{
    [SerializeField] private NotificationUI notificationUI;

    public void NotifyOwner(string message)
    {
        if (!IsServer)
        {
            return;
        }

        ClientRpcParams targetOwnerOnly = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        ShowClientRpc(message, targetOwnerOnly);
    }

    [ClientRpc]
    private void ShowClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        notificationUI.Show(message);
    }
}
