using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform aimSource;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerRider rider;
    [SerializeField] private float interactRange = 3f;

    private InputAction interactAction;
    private InputAction dropAction;
    private SteeringWheel currentWheel;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player");
        interactAction = playerMap.FindAction("Interact");
        dropAction = playerMap.FindAction("Drop");
        playerMap.Enable();

        interactAction.performed += OnInteract;
        dropAction.performed += OnDrop;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            if (interactAction != null)
            {
                interactAction.performed -= OnInteract;
            }
            if (dropAction != null)
            {
                dropAction.performed -= OnDrop;
            }
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        // dumendeyken E her kosulda cikis yapar (nisan almaya gerek yok)
        if (currentWheel != null)
        {
            currentWheel.RequestToggle(NetworkObject);
            rider.SetDriving(false);
            currentWheel = null;
            return;
        }

        bool didHit = Physics.Raycast(aimSource.position, aimSource.forward, out RaycastHit hit, interactRange);
        if (!didHit)
        {
            return;
        }

        if (hit.collider.GetComponentInParent<WorldItem>() is WorldItem item && item.NetworkObject.IsSpawned)
        {
            PickupServerRpc(item.NetworkObject);
        }
        else if (hit.collider.GetComponentInParent<SteeringWheel>() is SteeringWheel wheel && !wheel.HasDriver)
        {
            rider.SetPendingSeat(wheel.SeatPoint);
            rider.SetDriving(true);
            wheel.RequestToggle(NetworkObject);
            currentWheel = wheel;
        }
    }

    private void OnDrop(InputAction.CallbackContext context)
    {
        DropServerRpc();
    }

    [ServerRpc]
    private void PickupServerRpc(NetworkObjectReference itemRef)
    {
        if (!itemRef.TryGet(out NetworkObject netObj))
        {
            return;
        }

        WorldItem item = netObj.GetComponent<WorldItem>();
        if (item != null && inventory.TryAddItem(item.ItemId))
        {
            netObj.Despawn();
        }
    }

    [ServerRpc]
    private void DropServerRpc()
    {
        int itemId = inventory.GetSelectedItemId();
        if (itemId == -1)
        {
            return;
        }

        ItemDefinition def = inventory.Database.Get(itemId);
        if (def == null || def.WorldPrefab == null)
        {
            return;
        }

        inventory.RemoveSelectedItem();

        Vector3 dropPos = aimSource.position + aimSource.forward * 1.5f;
        if (Physics.Raycast(dropPos, Vector3.down, out RaycastHit groundHit, 5f))
        {
            dropPos = groundHit.point;
        }

        GameObject obj = Instantiate(def.WorldPrefab, dropPos, Quaternion.identity);
        obj.GetComponent<WorldItem>().Configure(itemId);
        obj.GetComponent<NetworkObject>().Spawn();
    }
}
