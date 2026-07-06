using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// dunyayi olusturan fabrika: yeni oyunda AI gemi / oyuncu teknesi / silah uretir, kayittan geri
// kurarken SaveManager'in cagirdigi tekil spawn metotlarini saglar. Spawn'lar host (sunucu) tarafinda
// yapilir; cagiran (SaveManager) sunucu kontrolunu zaten yapmis olur.
public class GameManager : MonoBehaviour
{
    [Header("AI Ships")]
    [SerializeField] private GameObject aiShipPrefab;
    [SerializeField] private int initialAiShipCount = 3;

    [Header("Player Boats")]
    [SerializeField] private GameObject playerBoatPrefab;         // ShipController'li oyuncu teknesi prefabi
    [SerializeField] private Vector3 playerBoatSpawnPosition = new Vector3(33f, -3.5f, 5.5f);
    [SerializeField] private int initialPlayerBoatCount = 1;

    [Header("Weapons")]
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private int initialWeaponCount = 6;

    // --- yeni dunya (SaveManager, yuklenecek kayit yoksa cagirir) ---
    public void GenerateNewWorld()
    {
        for (int i = 0; i < initialAiShipCount; i++)
        {
            SpawnAIShip();
        }
        for (int i = 0; i < initialPlayerBoatCount; i++)
        {
            SpawnPlayerBoat(playerBoatSpawnPosition, Quaternion.identity);
        }
        for (int i = 0; i < initialWeaponCount; i++)
        {
            SpawnRandomWeapon();
        }
    }

    private void SpawnAIShip()
    {
        if (aiShipPrefab == null)
        {
            return;
        }

        // konum önemsiz - AIShipController kendi OnNetworkSpawn'ında rastgele bir adaya yanaşır
        GameObject obj = Instantiate(aiShipPrefab);
        obj.GetComponent<NetworkObject>().Spawn();
    }

    public void SpawnPlayerBoat(Vector3 position, Quaternion rotation)
    {
        if (playerBoatPrefab == null)
        {
            return;
        }

        GameObject obj = Instantiate(playerBoatPrefab, position, rotation);
        obj.GetComponent<NetworkObject>().Spawn();
    }

    private void SpawnRandomWeapon()
    {
        if (Island.All.Count == 0 || itemDatabase == null)
        {
            return;
        }

        int itemId = RandomWeaponItemId();
        ItemDefinition def = itemDatabase.Get(itemId);
        if (def == null || def.WorldPrefab == null)
        {
            return;
        }

        Island island = Island.All[Random.Range(0, Island.All.Count)];
        Vector3 point = island.RandomSurfacePoint();

        GameObject obj = Instantiate(def.WorldPrefab, point, Quaternion.identity);
        obj.GetComponent<WorldItem>().Configure(itemId);
        WorldItemUtility.SnapToGround(obj, point.y);
        obj.GetComponent<NetworkObject>().Spawn();
    }

    // silahlar (HeldPrefab'inda Weapon component'i olan kayıtlar) arasından rastgele bir itemId seçer
    private int RandomWeaponItemId()
    {
        var weaponIds = new List<int>();
        for (int i = 0; i < itemDatabase.Items.Length; i++)
        {
            ItemDefinition def = itemDatabase.Items[i];
            if (def.HeldPrefab != null && def.HeldPrefab.GetComponent<Weapon>() != null)
            {
                weaponIds.Add(i);
            }
        }
        return weaponIds.Count > 0 ? weaponIds[Random.Range(0, weaponIds.Count)] : -1;
    }

    // --- kayittan geri kurma fabrikasi (SaveManager cagirir) ---
    // AI gemiyi kayitli konumda, rastgele uretimi kapatilmis ve seyre devam edecek sekilde spawn eder
    public GameObject SpawnRestoredShip(Vector3 position, Quaternion rotation)
    {
        if (aiShipPrefab == null)
        {
            return null;
        }

        GameObject shipObj = Instantiate(aiShipPrefab, position, rotation);

        ShipRandomizer randomizer = shipObj.GetComponent<ShipRandomizer>();
        if (randomizer != null)
        {
            randomizer.Suppress();
        }

        AIShipController controller = shipObj.GetComponent<AIShipController>();
        if (controller != null)
        {
            controller.RestoreAt(position, rotation);
        }

        shipObj.GetComponent<NetworkObject>().Spawn();
        return shipObj;
    }

    public void SpawnRestoredCrew(GameObject shipObj, Vector3 worldPosition, int health)
    {
        ShipRandomizer randomizer = shipObj.GetComponent<ShipRandomizer>();
        if (randomizer == null)
        {
            return;
        }

        CrewMember member = randomizer.SpawnCrewAt(worldPosition);
        if (member != null && member.GetComponent<CrewMemberHealth>() is CrewMemberHealth memberHealth)
        {
            memberHealth.SetHealth(health);
        }
    }

    // yerdeki/gemideki bir item'i kayittan spawn eder; parent verilirse o gemiye baglar
    public void SpawnRestoredItem(int itemId, int ammo, Vector3 position, Quaternion rotation, NetworkObject parent)
    {
        ItemDefinition def = itemDatabase.Get(itemId);
        if (def == null || def.WorldPrefab == null)
        {
            return;
        }

        GameObject obj = Instantiate(def.WorldPrefab, position, rotation);
        obj.GetComponent<WorldItem>().Configure(itemId, ammo);

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        netObj.Spawn();
        if (parent != null)
        {
            netObj.TrySetParent(parent, true);
        }
    }
}
