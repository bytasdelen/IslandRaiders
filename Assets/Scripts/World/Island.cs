using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Island : MonoBehaviour
{
    [SerializeField] private Transform[] dockPoints;    // gemilerin yanasacagi noktalar
    [SerializeField] private float approachMargin = 20f; // yaklasma noktasi dock halkasindan bu kadar açıkta olur

    private static readonly List<Island> all = new List<Island>();
    public static IReadOnlyList<Island> All => all;

    private AIShipController[] occupants;

    private void Awake()
    {
        occupants = new AIShipController[dockPoints.Length];
    }

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    // ada merkezinden dock'a dogru = disari (deniz) yönü; gemi yanasma egrisini buna göre kurar
    public Vector3 SeawardDir(int slot)
    {
        Vector3 dir = dockPoints[slot].position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            return Vector3.forward;
        }
        return dir.normalized;
    }

    // belli bir dock noktasinin TAM ONUNDEKI (deniz tarafi) açık su noktasi: gemi buraya gelince dock'a dogru hizalidir, dumduz iceri girer, varista donmesi gerekmez
    public Vector3 ApproachPointForDock(int slot)
    {
        Vector3 dockPos = dockPoints[slot].position;
        Vector3 target = dockPos + SeawardDir(slot) * approachMargin;
        target.y = dockPos.y;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, approachMargin, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return target;
    }

    // adaya genel yaklasma noktasi (slot yokken/bekleme icin)
    public Vector3 ApproachPointFor(Vector3 fromPosition)
    {
        float radius = approachMargin;
        foreach (Transform dock in dockPoints)
        {
            float dist = Vector3.Distance(transform.position, dock.position) + approachMargin;
            if (dist > radius)
            {
                radius = dist;
            }
        }

        Vector3 dir = fromPosition - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = Vector3.forward;
        }

        Vector3 target = transform.position + dir.normalized * radius;
        target.y = transform.position.y;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return target;
    }

    // bos bir slotu RANDOM ayirir; hepsi doluysa -1 döner. Yaklasma noktasi zaten secilen dock'un onunde hesaplandigi icin gemi hizali gelir 
    public int TryReserveDock(AIShipController ship)
    {
        var free = new List<int>();
        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == null)
            {
                free.Add(i);
            }
        }

        if (free.Count == 0)
        {
            return -1;
        }

        int slot = free[Random.Range(0, free.Count)];
        occupants[slot] = ship;
        return slot;
    }

    public Transform GetDockPoint(int slot)
    {
        return dockPoints[slot];
    } 
    public void ReleaseDock(int slot, AIShipController ship)
    {
        if (slot >= 0 && slot < occupants.Length && occupants[slot] == ship)
        {
            occupants[slot] = null;
        }
    }
}
