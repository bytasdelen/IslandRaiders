using Unity.Netcode;
using UnityEngine;

// yerde duran, alinabilir item
public class WorldItem : NetworkBehaviour
{
    [SerializeField] private int itemId;
    [SerializeField] private int ammo = -1;

    private readonly NetworkVariable<int> networkItemId = new NetworkVariable<int>();
    private readonly NetworkVariable<int> networkAmmo = new NetworkVariable<int>();

    public int ItemId => networkItemId.Value;
    public int Ammo => networkAmmo.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            networkItemId.Value = itemId;
            networkAmmo.Value = ammo;
        }
    }

    // drop ile spawn edilirken Spawn'dan once cagirilir
    public void Configure(int id, int ammoCount = -1)
    {
        itemId = id;
        ammo = ammoCount;
    }
}
