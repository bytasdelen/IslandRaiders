using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Sadece host (sunucu) tarafinda calisir.
public class SaveManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Key saveKey = Key.F5;

    private void Start()
    {
        if (!IsServer())
        {
            return;
        }

        SaveSystem.WorldSave load = SaveSystem.ConsumePendingLoad();
        if (load != null)
        {
            RestoreWorld(load);
        }
        else
        {
            gameManager.GenerateNewWorld();
        }
    }

    private void Update()
    {
        if (!IsServer() || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[saveKey].wasPressedThisFrame)
        {
            CaptureAndSave();
        }
    }

    private void OnApplicationQuit()
    {
        if (IsServer())
        {
            CaptureAndSave();
        }
    }

    private static bool IsServer()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }

    // --- kayittan geri kurma ---
    private void RestoreWorld(SaveSystem.WorldSave save)
    { 
        var shipObjects = new List<GameObject>();

        foreach (SaveSystem.ShipEntry entry in save.ships)
        {
            GameObject shipObj = gameManager.SpawnRestoredShip(entry.position, entry.rotation);
            shipObjects.Add(shipObj);
            if (shipObj == null)
            {
                continue;
            }

            foreach (SaveSystem.CrewEntry crew in entry.crew)
            {
                Vector3 worldPos = shipObj.transform.TransformPoint(crew.localPosition);
                gameManager.SpawnRestoredCrew(shipObj, worldPos, crew.health);
            }
        }

        foreach (SaveSystem.BoatEntry entry in save.boats)
        {
            gameManager.SpawnPlayerBoat(entry.position, entry.rotation);
        }

        foreach (SaveSystem.ItemEntry entry in save.items)
        {
            RestoreItem(entry, shipObjects);
        }
    }

    private void RestoreItem(SaveSystem.ItemEntry entry, List<GameObject> shipObjects)
    {
        GameObject shipObj = entry.shipIndex >= 0 && entry.shipIndex < shipObjects.Count
            ? shipObjects[entry.shipIndex]
            : null;
         
        Vector3 position = shipObj != null ? shipObj.transform.TransformPoint(entry.position) : entry.position;
        Quaternion rotation = shipObj != null ? shipObj.transform.rotation * entry.rotation : entry.rotation;
        NetworkObject parent = shipObj != null ? shipObj.GetComponent<NetworkObject>() : null;

        gameManager.SpawnRestoredItem(entry.itemId, entry.ammo, position, rotation, parent);
    }
     
    public void CaptureAndSave()
    {
        if (!IsServer())
        {
            return;
        }

        SaveSystem.WorldSave save = new SaveSystem.WorldSave();

        // canli oyuncularin son durumunu onbellege yaz, sonra tum bilinen oyuncular (cikmislar dahil) kaydedilir
        foreach (PlayerSaveState player in FindObjectsByType<PlayerSaveState>(FindObjectsSortMode.None))
        {
            SaveSystem.SetPlayer(player.BuildEntry());
        }
        save.players = SaveSystem.SessionPlayers();

        var ships = new List<AIShipController>(FindObjectsByType<AIShipController>(FindObjectsSortMode.None));
        foreach (AIShipController ship in ships)
        {
            save.ships.Add(CaptureShip(ship));
        }

        foreach (WorldItem item in FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
        {
            save.items.Add(CaptureItem(item, ships));
        }

        foreach (ShipController boat in FindObjectsByType<ShipController>(FindObjectsSortMode.None))
        {
            save.boats.Add(new SaveSystem.BoatEntry
            {
                position = boat.transform.position,
                rotation = boat.transform.rotation,
            });
        }

        SaveSystem.WriteWorld(save);
         
        foreach (PlayerNotifications notifications in FindObjectsByType<PlayerNotifications>(FindObjectsSortMode.None))
        {
            notifications.NotifyOwner("Game saved");
        }
    }

    private SaveSystem.ShipEntry CaptureShip(AIShipController ship)
    {
        SaveSystem.ShipEntry entry = new SaveSystem.ShipEntry
        {
            position = ship.SavePosition,
            rotation = ship.SaveRotation,
        };

        foreach (CrewMember member in ship.GetComponentsInChildren<CrewMember>())
        {
            CrewMemberHealth memberHealth = member.GetComponent<CrewMemberHealth>();
            if (memberHealth == null || memberHealth.IsDead)
            {
                continue;
            }

            entry.crew.Add(new SaveSystem.CrewEntry
            {
                localPosition = ship.transform.InverseTransformPoint(member.transform.position),
                localRotation = Quaternion.Inverse(ship.transform.rotation) * member.transform.rotation,
                health = memberHealth.CurrentHealth,
            });
        }

        return entry;
    }

    private SaveSystem.ItemEntry CaptureItem(WorldItem item, List<AIShipController> ships)
    {
        AIShipController parentShip = item.GetComponentInParent<AIShipController>();
        int shipIndex = parentShip != null ? ships.IndexOf(parentShip) : -1;

        SaveSystem.ItemEntry entry = new SaveSystem.ItemEntry
        {
            itemId = item.ItemId,
            ammo = item.Ammo,
            shipIndex = shipIndex,
        };

        if (shipIndex >= 0)
        {
            Transform shipTransform = ships[shipIndex].transform;
            entry.position = shipTransform.InverseTransformPoint(item.transform.position);
            entry.rotation = Quaternion.Inverse(shipTransform.rotation) * item.transform.rotation;
        }
        else
        {
            entry.position = item.transform.position;
            entry.rotation = item.transform.rotation;
        }

        return entry;
    }
}
