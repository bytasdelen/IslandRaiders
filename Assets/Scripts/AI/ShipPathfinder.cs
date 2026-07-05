using System.Collections.Generic;
using UnityEngine;

// WaypointNode grafi uzerinde yol bulma islemleri (BFS - en az dugum sayili yol yeterli,
// gemi ici graflar kucuk oldugu icin agirlikli Dijkstra'ya gerek yok)
public static class ShipPathfinder
{
    public static List<WaypointNode> FindPath(WaypointNode start, WaypointNode goal)
    {
        if (start == null || goal == null)
        {
            return null;
        }

        var cameFrom = new Dictionary<WaypointNode, WaypointNode>();
        var visited = new HashSet<WaypointNode> { start };
        var queue = new Queue<WaypointNode>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            WaypointNode current = queue.Dequeue();
            if (current == goal)
            {
                break;
            }

            foreach (WaypointNode neighbor in current.Neighbors)
            {
                if (neighbor != null && visited.Add(neighbor))
                {
                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (start != goal && !cameFrom.ContainsKey(goal))
        {
            return null;
        }

        var path = new List<WaypointNode> { goal };
        WaypointNode node = goal;
        while (node != start)
        {
            node = cameFrom[node];
            path.Add(node);
        }
        path.Reverse();
        return path;
    }

    // grafa herhangi bir node'dan baslayip baglantilarla ulasilabilen tum node'lari toplar;
    // CrewMember kendi graf parcasini boylece tek tek elle listelemek zorunda kalmiyor
    public static List<WaypointNode> CollectReachable(WaypointNode start)
    {
        var result = new List<WaypointNode>();
        if (start == null)
        {
            return result;
        }

        var visited = new HashSet<WaypointNode> { start };
        var queue = new Queue<WaypointNode>();
        queue.Enqueue(start);
        result.Add(start);

        while (queue.Count > 0)
        {
            WaypointNode current = queue.Dequeue();
            foreach (WaypointNode neighbor in current.Neighbors)
            {
                if (neighbor != null && visited.Add(neighbor))
                {
                    result.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return result;
    }

    // baslangictan itibaren komsuluk iliskilerini rastgele takip ederek bir devriye
    // rotasi olusturur; ayni node'a hemen geri donmemeye calisir (mumkunse)
    public static WaypointNode[] BuildRandomPatrolRoute(WaypointNode start, int steps)
    {
        var route = new List<WaypointNode> { start };
        WaypointNode current = start;
        WaypointNode previous = null;

        for (int i = 0; i < steps; i++)
        {
            WaypointNode[] neighbors = current.Neighbors;
            if (neighbors.Length == 0)
            {
                break;
            }

            WaypointNode next = neighbors[Random.Range(0, neighbors.Length)];
            if (neighbors.Length > 1 && next == previous)
            {
                next = neighbors[(System.Array.IndexOf(neighbors, next) + 1) % neighbors.Length];
            }

            route.Add(next);
            previous = current;
            current = next;
        }

        return route.ToArray();
    }

    public static WaypointNode FindNearest(IReadOnlyList<WaypointNode> nodes, Vector3 worldPosition)
    {
        WaypointNode nearest = null;
        float bestDistance = float.MaxValue;

        foreach (WaypointNode node in nodes)
        {
            float distance = Vector3.Distance(node.transform.position, worldPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = node;
            }
        }
        return nearest;
    }
}
