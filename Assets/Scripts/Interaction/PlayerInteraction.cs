using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform aimSource;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerRider rider;
    [SerializeField] private PlayerNotifications notifications;
    [SerializeField] private InteractionPromptUI promptUI;
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

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }
         
        if (currentWheel != null)
        {
            promptUI.Hide();
            return;
        }

        if (Physics.Raycast(aimSource.position, aimSource.forward, out RaycastHit hit, interactRange))
        {
            if (hit.collider.GetComponentInParent<WorldItem>() is WorldItem item && item.NetworkObject.IsSpawned)
            {
                promptUI.Show("(E) Loot");
                return;
            }
            if (hit.collider.GetComponentInParent<SteeringWheel>() is SteeringWheel wheel && !wheel.HasDriver)
            {
                promptUI.Show("(E) Interact");
                return;
            }
        }

        promptUI.Hide();
    }

    private void OnInteract(InputAction.CallbackContext context)
    { 
        if (currentWheel != null)
        {
            currentWheel.RequestToggle(NetworkObject);
            rider.SetDriving(false);
            currentWheel = null;
            return;
        }
         
        if (inventory.Database.Get(inventory.GetSelectedItemId())?.IsChest == true)
        {
            OpenChestServerRpc();
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
        if (item != null && inventory.TryAddItem(item.ItemId, item.Ammo))
        {
            // sandık çalındıysa, despawn'dan önce (parent hala gemiyken) o geminin mürettebatını uyar
            if (netObj.GetComponent<Chest>() != null && netObj.GetComponentInParent<IShipDeck>() is IShipDeck ship
                && CrewAlertSystem.Notify(netObj.transform.position, ship))
            {
                notifications.NotifyOwner("The crew went on attack!");
            }
            netObj.Despawn();
        }
    }

    [ServerRpc]
    private void OpenChestServerRpc()
    {
        int itemId = inventory.GetSelectedItemId();
        ItemDefinition def = inventory.Database.Get(itemId);
        if (def == null || !def.IsChest)
        {
            return;
        }

        inventory.RemoveSelectedItem();
        int reward = Random.Range(100, 1001);
        inventory.AddCoins(reward);
        notifications.NotifyOwner($"You earned {reward} coins!");
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

        // kalan mermi RemoveSelectedItem'dan ÖNCE okunmalı, o sıfırlıyor
        int remainingAmmo = inventory.GetSelectedAmmo();
        inventory.RemoveSelectedItem();

        Vector3 dropPos = aimSource.position + aimSource.forward * 1.5f;
        IShipDeck ship = null;
        if (Physics.Raycast(dropPos, Vector3.down, out RaycastHit groundHit, 5f))
        {
            dropPos = groundHit.point;
            ship = groundHit.collider.GetComponentInParent<IShipDeck>();
        }

        GameObject obj = Instantiate(def.WorldPrefab, dropPos, Quaternion.identity);
        obj.GetComponent<WorldItem>().Configure(itemId, remainingAmmo);
        WorldItemUtility.SnapToGround(obj, dropPos.y);

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        netObj.Spawn();

        // gemideyken bırakılan eşya gemiye parentlanmazsa gemi yol alınca olduğu yerde kalır
        if (ship != null)
        {
            netObj.TrySetParent(ship.NetworkObject, true);
        }
    }
}
