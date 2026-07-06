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

    // kayittan yuklenen gemilerde rastgele uretimi engeller (sandik/mürettebat kayittan gelir)
    public void Suppress()
    {
        hasRolled = true;
    }

    // havuzdan gelen gemi bir önceki yaşamdan "zaten zar attım" bayrağını taşıyabilir - yeni bir
    // normal spawn için bu sıfırlanmalı, yoksa yeni gemi hiç sandık/mürettebat üretmez
    public void ResetForReuse()
    {
        hasRolled = false;
    }

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
        if (Random.value > chestSpawnChance)
        {
            return;
        }
        SpawnChestAtRandomPoint();
    }

    // QA/test amacli: normal rastgele sans olmadan chestSpawnPoints'ten birine dogrudan sandik spawnlar
    public bool SpawnChestAtRandomPoint()
    {
        if (!IsServer || chestSpawnPoints.Length == 0 || chestPrefab == null)
        {
            return false;
        }

        Transform point = chestSpawnPoints[Random.Range(0, chestSpawnPoints.Length)];
        NetworkObject chestNetObj = PoolManager.Instance.Get(chestPrefab, point.position, point.rotation);
        chestNetObj.Spawn();
        chestNetObj.TrySetParent(NetworkObject, true);
        return true;
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
            SpawnCrewAt(spawnNode.transform.position);
        }
    }

    // verilen dunya konumuna en yakin node'dan baslayan rastgele bir devriyeyle mürettebat olusturur.
    // hem rastgele uretim hem kayittan geri yukleme bunu kullanir.
    public CrewMember SpawnCrewAt(Vector3 worldPosition)
    {
        if (crewPrefab == null || graphEntryNode == null)
        {
            return null;
        }

        List<WaypointNode> allNodes = ShipPathfinder.CollectReachable(graphEntryNode);
        if (allNodes.Count == 0)
        {
            return null;
        }

        WaypointNode startNode = ShipPathfinder.FindNearest(allNodes, worldPosition);
        WaypointNode[] route = ShipPathfinder.BuildRandomPatrolRoute(startNode, Random.Range(4, patrolMaxWalkSteps));

        NetworkObject netObj = PoolManager.Instance.Get(crewPrefab.gameObject, startNode.transform.position, startNode.transform.rotation);
        CrewMember instance = netObj.GetComponent<CrewMember>();
        netObj.Spawn();
        netObj.TrySetParent(NetworkObject, true);
        instance.SetPatrolRoute(route);
        return instance;
    }
}
