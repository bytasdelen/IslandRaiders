using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// gemide dolasan mürettebat, dort durumlu:
// Patrol      - kendi rotasini kesintisiz yürür
// Investigate - duyulan gurultuye / hedefin son gorulen yerine graf uzerinden kosar
// Return      - olay bitince patrol rotasina graf uzerinden geri doner (duz cizgi duvardan gecebilirdi)
// Combat      - oyuncuyu gordugu surece durur, doner, ates eder
// Tum hesap sunucuda yapilir (IsServer), NetworkTransform senkronu hallediyor
public class CrewMember : NetworkBehaviour
{
    private enum State { Patrol, Investigate, Return, Combat }

    [Header("Route")]
    [SerializeField] private WaypointNode[] patrolRoute;
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3f;
    [SerializeField] private float arriveThreshold = 0.3f;
    // node'lar zemin seviyesinde duruyor ama pivot merkezdeyse zemine gomulmemek icin
    // hedef pozisyona eklenen yukseklik (capsule yariyuksekligiyle eslesmeli)
    [SerializeField] private float groundOffset = 1f;

    [Header("Detect")]
    [SerializeField] private float detectRange = 15f;
    [SerializeField] private float loseTargetGraceTime = 0.5f;

    [Header("Attack")]
    [SerializeField] private Weapon weapon;
    [SerializeField] private BulletVisual bulletPrefab;
    [SerializeField] private float turnSpeed = 180f;
    [SerializeField] private float aimTolerance = 5f;

    private CrewMemberHealth health;
    private State state;
    private PlayerHealth target;
    private float nextFireTime;
    private float nextRepathTime;
    private float lastSeenTime = float.NegativeInfinity;

    // Alert() cagrilana kadar (sandik hirsizligi / silah sesi) mürettebat oyuncuyu
    // gorse bile saldirmaz - sadece kendi rotasinda dolasir
    private bool isHostile;

    private List<WaypointNode> allNodes;
    private int patrolIndex;
    private int patrolDirection = 1;

    private List<WaypointNode> graphPath;
    private int graphPathIndex;

    private void Awake()
    {
        health = GetComponent<CrewMemberHealth>();
        if (patrolRoute.Length > 0)
        {
            SetPatrolRoute(patrolRoute);
        }
    }

    // ShipRandomizer tarafindan dinamik spawn sirasinda cagrilir (Inspector'da elle
    // atanmis patrolRoute yerine gecer); rota + erisilebilir tum graf burada kurulur
    public void SetPatrolRoute(WaypointNode[] route)
    {
        patrolRoute = route;
        allNodes = route.Length > 0 ? ShipPathfinder.CollectReachable(route[0]) : new List<WaypointNode>();
        patrolIndex = 0;
        patrolDirection = 1;

        // spawn aninda ilk noktaya (yukseklik duzeltmesiyle) aninda oturt, ilk MoveTowards'i beklemeye gerek yok
        if (route.Length > 0)
        {
            transform.position = NodePosition(route[0]);
        }
    }

    private Vector3 NodePosition(WaypointNode node)
    {
        return node.transform.position + Vector3.up * groundOffset;
    }

    private void Update()
    {
        if (!IsServer || health.IsDead)
        {
            return;
        }

        PlayerHealth visible = isHostile ? FindVisibleTarget() : null;
        if (visible != null)
        {
            target = visible;
            state = State.Combat;
            lastSeenTime = Time.time;
        }
        // tek bir frame'lik gorus kaybinda (kapi kenari, ince engel vs.) hemen pes etmesin diye
        // kucuk bir tolerans suresi var - o sure dolmadan Combat'tan cikilmaz, TickCombat calismaya devam eder
        else if (state == State.Combat && Time.time - lastSeenTime > loseTargetGraceTime)
        {
            // hedef gercekten bir sureden beri gorulmuyor, son bilinen yerine bakmaya git
            if (target != null)
            {
                GoInvestigate(target.transform.position);
            }
            else
            {
                state = State.Patrol;
            }
            target = null;
        }

        switch (state)
        {
            case State.Combat:
                TickCombat();
                break;
            case State.Investigate:
                TickInvestigate();
                break;
            case State.Return:
                TickReturn();
                break;
            default:
                TickPatrol();
                break;
        }
    }

    // CrewAlertSystem tarafindan cagrilir (silah sesi, sandik hirsizligi gibi olaylarda).
    // bu noktadan sonra mürettebat kalici olarak dusman moduna gecer (bir daha pasiflesmez).
    // otomatik silahta her mermi icin yol yeniden hesaplanmasin diye cooldown burada.
    // true donerse bu cagriyla YENI dusman oldu (onceden pasifti) - CrewAlertSystem
    // bunu "dikkatli ol" bildirimini spamlamamak icin kullanir
    public bool Alert(Vector3 sourcePosition)
    {
        bool wasHostile = isHostile;
        isHostile = true;

        if (state == State.Combat || Time.time < nextRepathTime)
        {
            return !wasHostile;
        }
        nextRepathTime = Time.time + 0.5f;
        GoInvestigate(sourcePosition);
        return !wasHostile;
    }

    // her cagrildiginda state'i mutlaka Combat'tan cikarir (Investigate ya da Patrol);
    // aksi halde hedef kaybinda Combat'ta null target ile kalinabilirdi
    private void GoInvestigate(Vector3 worldPosition)
    {
        // iki uc da "duz cizgide engel olmayan" en yakin node'dan secilir; aksi halde
        // duvarin arkasindaki node secilir ve mürettebat duvara dogru yurumeye kalkar
        WaypointNode from = FindNearestClearNode(transform.position);
        WaypointNode to = FindNearestClearNode(worldPosition);
        graphPath = ShipPathfinder.FindPath(from, to);
        graphPathIndex = 0;
        state = graphPath != null ? State.Investigate : State.Patrol;
    }

    // olay yeri incelendi, patrol rotasina graf uzerinden geri don
    private void StartReturn()
    {
        WaypointNode from = FindNearestClearNode(transform.position);
        WaypointNode nearestPatrol = ShipPathfinder.FindNearest(patrolRoute, transform.position);
        graphPath = ShipPathfinder.FindPath(from, nearestPatrol);
        graphPathIndex = 0;
        state = graphPath != null ? State.Return : State.Patrol;
    }

    private void TickPatrol()
    {
        if (patrolRoute.Length < 2)
        {
            return;
        }

        if (MoveTowardsPoint(NodePosition(patrolRoute[patrolIndex]), patrolSpeed))
        {
            AdvancePatrolIndex();
        }
    }

    // rotanin ucuna/basina gelince yon degistirip geri sarar
    private void AdvancePatrolIndex()
    {
        if (patrolIndex + patrolDirection < 0 || patrolIndex + patrolDirection >= patrolRoute.Length)
        {
            patrolDirection = -patrolDirection;
        }
        patrolIndex += patrolDirection;
    }

    private void TickInvestigate()
    {
        if (WalkGraphPath(chaseSpeed))
        {
            StartReturn();
        }
    }

    private void TickReturn()
    {
        if (!WalkGraphPath(patrolSpeed))
        {
            return;
        }

        // patrol rotasindaki hangi node'a vardiysak devriyeye oradan devam et
        if (graphPath != null && graphPath.Count > 0)
        {
            int index = System.Array.IndexOf(patrolRoute, graphPath[graphPath.Count - 1]);
            if (index >= 0)
            {
                patrolIndex = index;
            }
        }
        state = State.Patrol;
    }

    // true dondugunde yol tamamlanmistir
    private bool WalkGraphPath(float speed)
    {
        if (graphPath == null || graphPathIndex >= graphPath.Count)
        {
            return true;
        }

        if (MoveTowardsPoint(NodePosition(graphPath[graphPathIndex]), speed))
        {
            graphPathIndex++;
        }
        return graphPathIndex >= graphPath.Count;
    }

    // hedefe gercek 3D pozisyonda yurur (alt/ust kat node'lari icin Y farki da uygulanir).
    // bakis yonu (rotasyon) sadece yatay duzlemde hesaplanir ki merdivende govde
    // one/arkaya egilmesin, sadece dogru yone donsun
    private bool MoveTowardsPoint(Vector3 targetPos, float speed)
    {
        Vector3 toTarget = targetPos - transform.position;
        Vector3 flatDirection = toTarget;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(flatDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, turnSpeed * Time.deltaTime);
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
        return Vector3.Distance(transform.position, targetPos) < arriveThreshold;
    }

    private void TickCombat()
    {
        Vector3 aimPoint = target.transform.position + Vector3.up;
        Vector3 toTarget = aimPoint - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion lookRotation = Quaternion.LookRotation(toTarget);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, turnSpeed * Time.deltaTime);

        bool aimedIn = Quaternion.Angle(transform.rotation, lookRotation) < aimTolerance;
        if (aimedIn && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + 1f / weapon.FireRate;
            Fire(aimPoint);
        }
    }

    private PlayerHealth FindVisibleTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange);
        foreach (Collider col in hits)
        {
            if (col.GetComponentInParent<PlayerHealth>() is PlayerHealth candidate && CanSee(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    // gozden hedefe isin atilir, kendi collider'lari atlanir; ilk carpilan sey hedefin
    // kendisiyse gorus aciktir. LayerMask ayarina bagimli degildir (yanlis mask = kor AI)
    private bool CanSee(PlayerHealth player)
    {
        Vector3 origin = transform.position + Vector3.up;
        Vector3 point = player.transform.position + Vector3.up;
        Vector3 diff = point - origin;

        if (diff.magnitude > detectRange)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, diff.normalized, diff.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }
            return hit.collider.GetComponentInParent<PlayerHealth>() == player;
        }
        return true;
    }

    // duz cizgide engel olmadan gorulebilen en yakin node; hicbiri acikta degilse duz en yakin
    private WaypointNode FindNearestClearNode(Vector3 position)
    {
        WaypointNode best = null;
        float bestDistance = float.MaxValue;
        Vector3 origin = position + Vector3.up * 0.5f;

        foreach (WaypointNode node in allNodes)
        {
            float distance = Vector3.Distance(node.transform.position, position);
            if (distance >= bestDistance)
            {
                continue;
            }

            Vector3 point = node.transform.position + Vector3.up * 0.5f;
            bool blocked = Physics.Linecast(origin, point, out RaycastHit hit,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform);
            if (blocked)
            {
                continue;
            }

            best = node;
            bestDistance = distance;
        }

        return best != null ? best : ShipPathfinder.FindNearest(allNodes, position);
    }

    private void Fire(Vector3 aimPoint)
    {
        Vector3 origin = weapon.Muzzle.position;
        Vector3 direction = (aimPoint - origin).normalized;

        // kendi govdesine/silahina carpmamak icin tum isabetler taranir, ilk yabanci hedef alinir
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, weapon.Range,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 endPoint = origin + direction * weapon.Range;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            endPoint = hit.point;
            if (hit.collider.GetComponent<Hitbox>() is Hitbox hitbox)
            {
                hitbox.TakeHit(weapon.Damage, OwnerClientId);
            }
            break;
        }

        FireEffectClientRpc(origin, endPoint);
    }

    [ClientRpc]
    private void FireEffectClientRpc(Vector3 start, Vector3 end)
    {
        BulletVisual bullet = Instantiate(bulletPrefab, start, Quaternion.identity);
        bullet.Launch(start, end);
    }
}
