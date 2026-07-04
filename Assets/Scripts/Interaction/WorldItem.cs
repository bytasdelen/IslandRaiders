using Unity.Netcode;
using UnityEngine;

// yerde duran, alinabilir item
public class WorldItem : NetworkBehaviour
{
    [SerializeField] private int itemId;

    private readonly NetworkVariable<int> networkItemId = new NetworkVariable<int>();

    public int ItemId => networkItemId.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            networkItemId.Value = itemId;
        }
    }

    // drop ile spawn edilirken Spawn'dan once cagrilir
    public void Configure(int id)
    {
        itemId = id;
    }
}
