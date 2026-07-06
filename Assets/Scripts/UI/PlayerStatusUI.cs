using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// oyuncunun can barini, para miktarini ve (elinde silah varsa) mermi sayisini gösterir;
public class PlayerStatusUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Image healthFill;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private float fillSmoothSpeed = 4f;

    private float targetFill = 1f;

    private void Start()
    {
        if (!health.IsOwner)
        {
            gameObject.SetActive(false);
            return;
        }

        health.CurrentHealth.OnValueChanged += OnHealthChanged;
        inventory.Coins.OnValueChanged += OnCoinsChanged;
        inventory.Ammo.OnListChanged += OnAmmoListChanged; 
        inventory.Slots.OnListChanged += OnAmmoListChanged;
        inventory.SelectedSlot.OnValueChanged += OnSelectedSlotChanged;

        RefreshHealth(health.CurrentHealth.Value);
        healthFill.fillAmount = targetFill;
        RefreshCoins(inventory.Coins.Value);
        RefreshAmmo();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.CurrentHealth.OnValueChanged -= OnHealthChanged;
        }
        if (inventory != null)
        {
            inventory.Coins.OnValueChanged -= OnCoinsChanged;
            inventory.Ammo.OnListChanged -= OnAmmoListChanged;
            inventory.Slots.OnListChanged -= OnAmmoListChanged;
            inventory.SelectedSlot.OnValueChanged -= OnSelectedSlotChanged;
        }
    }

    private void Update()
    {
        healthFill.fillAmount = Mathf.MoveTowards(healthFill.fillAmount, targetFill, fillSmoothSpeed * Time.deltaTime);
    }

    private void OnHealthChanged(int previous, int current)
    {
        RefreshHealth(current);
    }

    private void OnCoinsChanged(int previous, int current)
    {
        RefreshCoins(current);
    }

    private void OnAmmoListChanged(NetworkListEvent<int> change)
    {
        RefreshAmmo();
    }

    private void OnSelectedSlotChanged(int previous, int current)
    {
        RefreshAmmo();
    }

    private void RefreshHealth(int value)
    {
        targetFill = (float)value / health.MaxHealth;
        healthText.text = value.ToString();
    }

    private void RefreshCoins(int value)
    {
        coinsText.text = value.ToString();
    }
     
    private void RefreshAmmo()
    {
        ItemDefinition def = inventory.Database.Get(inventory.GetSelectedItemId());
        bool isWeapon = def != null && def.HeldPrefab != null && def.HeldPrefab.GetComponent<Weapon>() != null;

        ammoText.gameObject.SetActive(isWeapon);
        if (isWeapon)
        {
            ammoText.text = inventory.GetSelectedAmmo().ToString();
        }
    }
}
