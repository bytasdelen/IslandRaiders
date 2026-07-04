using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// owner'in ekran alti hotbar arayuzu; slot ikonlari ve secili slot vurgusu
public class HotbarUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private HotbarSlotUI[] slotViews;

    private void Start()
    {
        // hotbar sadece bu makinedeki oyuncuya (owner) gosterilir
        if (inventory.IsOwner)
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                int index = i;
                slotViews[i].Button.onClick.AddListener(() => inventory.SelectSlot(index));
            }

            inventory.Slots.OnListChanged += OnSlotsChanged;
            inventory.SelectedSlot.OnValueChanged += OnSelectedChanged;
            Refresh();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (inventory != null && inventory.IsOwner)
        {
            inventory.Slots.OnListChanged -= OnSlotsChanged;
            inventory.SelectedSlot.OnValueChanged -= OnSelectedChanged;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // 1-6 tuslari ile slot secimi
            for (int i = 0; i < slotViews.Length; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    inventory.SelectSlot(i);
                }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0f)
            {
                SelectRelative(-1);
            }
            else if (scroll < 0f)
            {
                SelectRelative(1);
            }
        }
    }

    // slotlar arasinda loop yaparak (bas/son sarmali) ilerler
    private void SelectRelative(int delta)
    {
        int count = PlayerInventory.SlotCount;
        int next = (inventory.SelectedSlot.Value + delta + count) % count;
        inventory.SelectSlot(next);
    }

    private void OnSlotsChanged(NetworkListEvent<int> change)
    {
        Refresh();
    }

    private void OnSelectedChanged(int previous, int current)
    {
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < slotViews.Length; i++)
        {
            int itemId = i < inventory.Slots.Count ? inventory.Slots[i] : -1;
            ItemDefinition item = inventory.Database.Get(itemId);
            slotViews[i].SetIcon(item != null ? item.Icon : null);
            slotViews[i].SetSelected(i == inventory.SelectedSlot.Value);
        }
    }
}
