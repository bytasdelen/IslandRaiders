using Unity.Netcode;
using UnityEngine;

public class CrewMemberHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 80;
    [SerializeField] private float headMultiplier = 2f;
    [SerializeField] private float despawnDelay = 4f;

    
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private int weaponItemId = -1;

    private readonly NetworkVariable<int> currentHealth = new NetworkVariable<int>();
    private readonly NetworkVariable<bool> isDead = new NetworkVariable<bool>();

    public bool IsDead => isDead.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        isDead.OnValueChanged += OnDeadChanged;
        if (isDead.Value)
        {
            OnDeadChanged(false, true);
        }
    }

    public override void OnNetworkDespawn()
    {
        isDead.OnValueChanged -= OnDeadChanged;
    }

    public void ApplyDamage(int weaponDamage, HitRegion region, ulong attackerClientId)
    {
        if (!IsServer || isDead.Value)
        {
            return;
        }

        float damage = region == HitRegion.Head ? weaponDamage * headMultiplier : weaponDamage;
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - Mathf.RoundToInt(damage));

        if (currentHealth.Value == 0)
        {
            isDead.Value = true;
            DropWeapon();
            Invoke(nameof(DespawnSelf), despawnDelay);
        }
    }

    private void DespawnSelf()
    {
        if (IsServer && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    private void DropWeapon()
    {
        if (weaponItemId < 0 || itemDatabase == null)
        {
            return;
        }

        ItemDefinition def = itemDatabase.Get(weaponItemId);
        if (def == null || def.WorldPrefab == null)
        {
            return;
        }

        Vector3 dropPos = transform.position;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f))
        {
            dropPos = hit.point;
        }

        // mürettebat mermi takip etmiyor, silahi hep dolu bırakır
        Weapon heldWeapon = def.HeldPrefab != null ? def.HeldPrefab.GetComponent<Weapon>() : null;
        int ammo = heldWeapon != null ? heldWeapon.MaxAmmo : -1;

        GameObject obj = Instantiate(def.WorldPrefab, dropPos, Quaternion.identity);
        obj.GetComponent<WorldItem>().Configure(weaponItemId, ammo);
        WorldItemUtility.SnapToGround(obj, dropPos.y);

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        netObj.Spawn();

        // gemideysek gemiye parentlanmazsa gemi yol alinca silah olduğu yerde kalir
        if (GetComponentInParent<IShipDeck>() is IShipDeck ship)
        {
            netObj.TrySetParent(ship.NetworkObject, true);
        }
    }
     
    private void OnDeadChanged(bool previous, bool current)
    {
        if (!current)
        {
            return;
        }

        transform.Rotate(90f, 0f, 0f);
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
    }
}
