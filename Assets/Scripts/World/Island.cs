using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Island : MonoBehaviour
{
    [SerializeField] private Transform[] dockPoints;    // gemilerin yanasacagi noktalar
    [SerializeField] private float approachMargin = 20f; // yaklasma noktasi dock halkasindan bu kadar açıkta olur
    // collider kutusu adanin gercek (duzensiz) seklinden genis oldugu icin spawn alanini merkeze dogru daraltir (1 = tum kutu, 0.5 = yarisi)
    [SerializeField] [Range(0.1f, 1f)] private float spawnAreaScale = 0.5f;

    private static readonly List<Island> all = new List<Island>();
    public static IReadOnlyList<Island> All => all;

    private AIShipController[] occupants;
    private Collider islandCollider;

    private void Awake()
    {
        occupants = new AIShipController[dockPoints.Length];
        islandCollider = GetComponent<Collider>();
    }

    // adanın kendi collider sınırları içinde rastgele bir yüzey noktası (silah/sandık spawn'ı için)
    public Vector3 RandomSurfacePoint()
    {
        if (islandCollider == null)
        {
            return transform.position;
        }

        Bounds bounds = islandCollider.bounds;
        float halfX = bounds.extents.x * spawnAreaScale;
        float halfZ = bounds.extents.z * spawnAreaScale;
        float x = bounds.center.x + Random.Range(-halfX, halfX);
        float z = bounds.center.z + Random.Range(-halfZ, halfZ);
        return new Vector3(x, bounds.max.y + 0.75f, z);
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
    public float ApproachMargin => approachMargin;

    // dock'un deniz normali ekseninde, dock'tan 'distance' kadar açıktaki navmesh noktası (yanaşma lane'i)
    public Vector3 LanePoint(int slot, float distance)
    {
        Vector3 dockPos = dockPoints[slot].position;
        Vector3 target = dockPos + SeawardDir(slot) * distance;
        target.y = dockPos.y;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, Mathf.Max(distance, 1f), NavMesh.AllAreas))
        {
            return hit.position;
        }
        return target;
    }

    // dock'un TAM ÖNÜNDEKİ (deniz tarafı) yakın açık su noktası
    public Vector3 ApproachPointForDock(int slot)
    {
        return LanePoint(slot, approachMargin);
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
