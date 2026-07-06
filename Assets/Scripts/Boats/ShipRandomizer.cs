using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ShipRandomizer : NetworkBehaviour
{
    [SerializeField] private Transform[] chestSpawnPoints;
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] [Range(0f, 1f)] private float chestSpawnChance = 0.5f;

    [SerializeField] private CrewMember crewPrefab;
    [SerializeField] private WaypointNode graphEntryNode;
    [SerializeField] private int minCrewCount = 0;
    [SerializeField] private int maxCrewCount = 4;
    [SerializeField] private int patrolMaxWalkSteps = 14;

    private bool hasRolled;

    private void Update()
    {
        if (!IsServer || hasRolled)
        {
            return;
        }
        hasRolled = true;

        SpawnRandomChest();
        SpawnRandomCrew();
    }

    private void SpawnRandomChest()
    {
        if (chestSpawnPoints.Length == 0 || Random.value > chestSpawnChance)
        {
            return;
        }

        Transform point = chestSpawnPoints[Random.Range(0, chestSpawnPoints.Length)];
        GameObject chestObj = Instantiate(chestPrefab, point.position, point.rotation);

        NetworkObject chestNetObj = chestObj.GetComponent<NetworkObject>();
        chestNetObj.Spawn();
        chestNetObj.TrySetParent(NetworkObject, true);
    }

    private void SpawnRandomCrew()
    {
        if (crewPrefab == null || graphEntryNode == null)
        {
            return;
        }

        List<WaypointNode> allNodes = ShipPathfinder.CollectReachable(graphEntryNode);
        if (allNodes.Count == 0)
        {
            return;
        }

        int count = Random.Range(minCrewCount, maxCrewCount + 1);
        for (int i = 0; i < count; i++)
        {
            WaypointNode spawnNode = allNodes[Random.Range(0, allNodes.Count)];
            WaypointNode[] route = ShipPathfinder.BuildRandomPatrolRoute(spawnNode, Random.Range(4, patrolMaxWalkSteps));

            CrewMember instance = Instantiate(crewPrefab, spawnNode.transform.position, spawnNode.transform.rotation);
            instance.NetworkObject.Spawn();
            instance.NetworkObject.TrySetParent(NetworkObject, true);
            instance.SetPatrolRoute(route);
        }
    }
}
