using UnityEngine;


public class WaypointNode : MonoBehaviour
{
    [SerializeField] private WaypointNode[] neighbors;

    public WaypointNode[] Neighbors => neighbors;
}
