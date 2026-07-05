using UnityEngine;

// gemi icindeki bir yol noktasi; komsu node'lar birlikte bir graf olusturur.
// baglantilar iki yonlu olmali (A, B'nin komsusuysa B de A'nin komsusu olmali) -
// bu Inspector'dan elle yapilir, kod otomatik aynalamiyor
public class WaypointNode : MonoBehaviour
{
    [SerializeField] private WaypointNode[] neighbors;

    public WaypointNode[] Neighbors => neighbors;
}
