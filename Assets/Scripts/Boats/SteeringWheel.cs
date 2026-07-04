using Unity.Netcode;
using UnityEngine;

// dumen: oyuncu E ile gecer/birakir, gemi kontrolunu alir
public class SteeringWheel : NetworkBehaviour
{
    [SerializeField] private BoatController boat;
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform wheelVisual;
    [SerializeField] private float maxWheelSpin = 180f;

    private const ulong NoDriver = ulong.MaxValue;
    private readonly NetworkVariable<ulong> driverClientId = new NetworkVariable<ulong>(NoDriver);

    public bool HasDriver => driverClientId.Value != NoDriver;
    public Transform SeatPoint => seatPoint;

    private void Update()
    {
        // dumen simidi, senkronize dumen acisina gore her makinede doner
        if (wheelVisual != null)
        {
            wheelVisual.localRotation = Quaternion.Euler(0f, 0f, boat.Rudder * -maxWheelSpin);
        }
    }

    public void RequestToggle(NetworkObject player)
    {
        ToggleServerRpc(player);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleServerRpc(NetworkObjectReference playerRef)
    {
        if (!playerRef.TryGet(out NetworkObject player))
        {
            return;
        }

        // parent yonetimi PlayerRider'da; burada sadece gemi kontrolu (ownership) devredilir
        if (driverClientId.Value == player.OwnerClientId)
        {
            driverClientId.Value = NoDriver;
            boat.NetworkObject.RemoveOwnership();
        }
        else if (!HasDriver)
        {
            driverClientId.Value = player.OwnerClientId;
            boat.NetworkObject.ChangeOwnership(player.OwnerClientId);
        }
    }
}
