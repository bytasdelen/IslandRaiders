#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// F1 ile acilip kapanan runtime QA/test paneli. Sadece Editor veya development build'de derlenir
public class QATestPanel : NetworkBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerRider rider;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerLook look;
    [SerializeField] private int giveCoinsAmount = 500;

    private bool visible;
    private Vector2 scroll;
    private readonly List<string> log = new List<string>();
    private GUIStyle buttonStyle;
    private GUIStyle titleStyle;

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current[Key.F1].wasPressedThisFrame)
        {
            SetVisible(!visible);
        }
    }

    // panel acilinca kamera dönmesin diye PlayerLook kapatilir, imlec serbest birakilir
    private void SetVisible(bool value)
    {
        visible = value;

        if (look != null)
        {
            look.enabled = !visible;
        }

        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
    }

    private void OnGUI()
    {
        if (!IsOwner || !visible)
        {
            return;
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20 };
        }

        GUILayout.BeginArea(new Rect(10, 10, 440, 780), GUI.skin.box);
        GUILayout.Label("QA Test Panel (F1 to close)", titleStyle);
        GUILayout.Space(6);

        if (GUILayout.Button("Give Gun", buttonStyle, GUILayout.Height(48))) GiveGunServerRpc();
        if (GUILayout.Button("Give Player Ammo", buttonStyle, GUILayout.Height(48))) GiveAmmoServerRpc();
        if (GUILayout.Button("Give Player Coins", buttonStyle, GUILayout.Height(48))) GiveCoinsServerRpc();
        if (GUILayout.Button("Full Heal", buttonStyle, GUILayout.Height(48))) FullHealServerRpc();
        if (GUILayout.Button("Save Game", buttonStyle, GUILayout.Height(48))) SaveGameServerRpc();
        if (GUILayout.Button("Spawn Chest", buttonStyle, GUILayout.Height(48))) SpawnChestServerRpc();
        if (GUILayout.Button("Spawn AI Ship", buttonStyle, GUILayout.Height(48))) SpawnAIShipServerRpc();
        if (GUILayout.Button("Spawn Crew", buttonStyle, GUILayout.Height(48))) SpawnCrewServerRpc();
        if (GUILayout.Button("Kill All Crew", buttonStyle, GUILayout.Height(48))) KillAllCrewServerRpc();
        if (GUILayout.Button("Force Crew Combat", buttonStyle, GUILayout.Height(48))) ForceCrewCombatServerRpc();

        GUILayout.Space(8);
        GUILayout.Label("Results:");
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(180));
        for (int i = log.Count - 1; i >= 0; i--)
        {
            GUILayout.Label(log[i]);
        }
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    // --- Give Player Ammo ---

    [ServerRpc]
    private void GiveAmmoServerRpc()
    {
        int itemId = inventory.GetSelectedItemId();
        if (itemId == -1)
        {
            ReportResultClientRpc("Give Player Ammo", false, "No item held in the current slot", OwnerOnly());
            return;
        }

        inventory.RefillSelectedAmmo();
        ReportResultClientRpc("Give Player Ammo", true, $"Refilled ammo for item {itemId}", OwnerOnly());
    }

    // --- Give Player Coins ---

    [ServerRpc]
    private void GiveCoinsServerRpc()
    {
        inventory.AddCoins(giveCoinsAmount);
        ReportResultClientRpc("Give Player Coins", true, $"Added {giveCoinsAmount} coins (total: {inventory.Coins.Value})", OwnerOnly());
    }

    // --- Give Gun ---

    [ServerRpc]
    private void GiveGunServerRpc()
    {
        GameManager manager = FindFirstObjectByType<GameManager>();
        if (manager == null)
        {
            ReportResultClientRpc("Give Gun", false, "GameManager not found in scene", OwnerOnly());
            return;
        }

        int itemId = manager.RandomWeaponItemId();
        if (itemId == -1)
        {
            ReportResultClientRpc("Give Gun", false, "No weapons found in the ItemDatabase", OwnerOnly());
            return;
        }

        bool added = inventory.TryAddItem(itemId);
        ReportResultClientRpc("Give Gun", added, added ? $"Added weapon {itemId} to inventory" : "Inventory is full", OwnerOnly());
    }

    // --- Full Heal ---

    [ServerRpc]
    private void FullHealServerRpc()
    {
        health.CurrentHealth.Value = health.MaxHealth;
        ReportResultClientRpc("Full Heal", true, $"Health restored to {health.MaxHealth}", OwnerOnly());
    }

    // --- Save Game ---

    [ServerRpc]
    private void SaveGameServerRpc()
    {
        SaveManager manager = FindFirstObjectByType<SaveManager>();
        if (manager == null)
        {
            ReportResultClientRpc("Save Game", false, "SaveManager not found in scene", OwnerOnly());
            return;
        }

        manager.CaptureAndSave();
        ReportResultClientRpc("Save Game", true, "World saved to disk", OwnerOnly());
    }

    // --- Spawn Chest ---

    [ServerRpc]
    private void SpawnChestServerRpc()
    {
        IShipDeck ship = rider.CurrentShip;
        if (ship == null)
        {
            ReportResultClientRpc("Spawn Chest", false, "Not currently standing on a ship", OwnerOnly());
            return;
        }

        ShipRandomizer randomizer = ship.NetworkObject.GetComponent<ShipRandomizer>();
        if (randomizer == null)
        {
            ReportResultClientRpc("Spawn Chest", false, "This ship has no ShipRandomizer", OwnerOnly());
            return;
        }

        bool ok = randomizer.SpawnChestAtRandomPoint();
        ReportResultClientRpc("Spawn Chest", ok, ok ? "Chest spawned at a ship spawn point" : "Ship has no chest spawn points configured", OwnerOnly());
    }

    // --- Spawn AI Ship ---

    [ServerRpc]
    private void SpawnAIShipServerRpc()
    {
        GameManager manager = FindFirstObjectByType<GameManager>();
        if (manager == null)
        {
            ReportResultClientRpc("Spawn AI Ship", false, "GameManager not found in scene", OwnerOnly());
            return;
        }

        Island nearest = FindNearestIsland(transform.position);
        if (nearest == null)
        {
            ReportResultClientRpc("Spawn AI Ship", false, "No islands found in scene", OwnerOnly());
            return;
        }

        GameObject shipObj = manager.SpawnAIShipAt(nearest);
        bool ok = shipObj != null;
        string detail = ok ? $"AI ship docked at {nearest.name}" : "GameManager has no AI ship prefab assigned";
        ReportResultClientRpc("Spawn AI Ship", ok, detail, OwnerOnly());
    }

    private static Island FindNearestIsland(Vector3 position)
    {
        Island nearest = null;
        float bestDist = float.MaxValue;
        foreach (Island island in Island.All)
        {
            float dist = Vector3.Distance(island.transform.position, position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = island;
            }
        }
        return nearest;
    }

    // --- Spawn Crew ---

    [ServerRpc]
    private void SpawnCrewServerRpc()
    {
        IShipDeck ship = rider.CurrentShip;
        if (ship == null)
        {
            ReportResultClientRpc("Spawn Crew", false, "Not currently standing on a ship", OwnerOnly());
            return;
        }

        ShipRandomizer randomizer = ship.NetworkObject.GetComponent<ShipRandomizer>();
        if (randomizer == null)
        {
            ReportResultClientRpc("Spawn Crew", false, "This ship has no ShipRandomizer", OwnerOnly());
            return;
        }

        CrewMember crew = randomizer.SpawnCrewAt(transform.position);
        bool ok = crew != null;
        ReportResultClientRpc("Spawn Crew", ok, ok ? "Crew member spawned" : "ShipRandomizer has no crew prefab/graph configured", OwnerOnly());
    }

    // --- Kill All Crew ---

    [ServerRpc]
    private void KillAllCrewServerRpc()
    {
        IShipDeck ship = rider.CurrentShip;
        if (ship == null)
        {
            ReportResultClientRpc("Kill All Crew", false, "Not currently standing on a ship", OwnerOnly());
            return;
        }

        CrewMemberHealth[] crew = ship.NetworkObject.GetComponentsInChildren<CrewMemberHealth>();
        int killed = 0;
        foreach (CrewMemberHealth memberHealth in crew)
        {
            if (memberHealth.IsDead)
            {
                continue;
            }

            memberHealth.ApplyDamage(memberHealth.CurrentHealth, HitRegion.Body, OwnerClientId);
            killed++;
        }

        bool ok = killed > 0;
        ReportResultClientRpc("Kill All Crew", ok, ok ? $"{killed} crew member(s) killed" : "No living crew members found on this ship", OwnerOnly());
    }

    // --- Force Crew Combat ---

    [ServerRpc]
    private void ForceCrewCombatServerRpc()
    {
        IShipDeck ship = rider.CurrentShip;
        if (ship == null)
        {
            ReportResultClientRpc("Force Crew Combat", false, "Not currently standing on a ship", OwnerOnly());
            return;
        }

        CrewMember[] crew = ship.NetworkObject.GetComponentsInChildren<CrewMember>();
        if (crew.Length == 0)
        {
            ReportResultClientRpc("Force Crew Combat", false, "No crew members found on this ship", OwnerOnly());
            return;
        }

        foreach (CrewMember member in crew)
        {
            member.ForceCombat(health);
        }
        ReportResultClientRpc("Force Crew Combat", true, $"{crew.Length} crew member(s) forced into combat", OwnerOnly());
    }

    // --- ortak yardimcilar ---

    private ClientRpcParams OwnerOnly()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
    }

    [ClientRpc]
    private void ReportResultClientRpc(string action, bool pass, string detail, ClientRpcParams clientRpcParams = default)
    {
        string line = $"[QA] {action}: {(pass ? "PASS" : "FAIL")} - {detail}";
        if (pass)
        {
            Debug.Log(line);
        }
        else
        {
            Debug.LogWarning(line);
        }

        log.Add(line);
        if (log.Count > 30)
        {
            log.RemoveAt(0);
        }
    }
}
#endif
