using Unity.Netcode;
using UnityEngine;

// secili slottaki item'in modelini elde gösterir; her client'ta çalışır
public class HeldItemDisplay : NetworkBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Transform holder;

    private GameObject currentModel;

    public Weapon CurrentWeapon { get; private set; }

    public override void OnNetworkSpawn()
    {
        inventory.Slots.OnListChanged += OnSlotsChanged;
        inventory.SelectedSlot.OnValueChanged += OnSelectedChanged;
        UpdateHeldItem();
    }

    public override void OnNetworkDespawn()
    {
        inventory.Slots.OnListChanged -= OnSlotsChanged;
        inventory.SelectedSlot.OnValueChanged -= OnSelectedChanged;
    }

    private void OnSlotsChanged(NetworkListEvent<int> change)
    {
        UpdateHeldItem();
    }

    private void OnSelectedChanged(int previous, int current)
    {
        UpdateHeldItem();
    }

    private void UpdateHeldItem()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
            CurrentWeapon = null;
        }

        ItemDefinition item = inventory.Database.Get(inventory.GetSelectedItemId());
        if (item == null || item.HeldPrefab == null)
        {
            return;
        }

        currentModel = Instantiate(item.HeldPrefab, holder);
        currentModel.transform.localPosition = Vector3.zero;
        currentModel.transform.localRotation = Quaternion.identity;
        CurrentWeapon = currentModel.GetComponent<Weapon>();
    }
}
