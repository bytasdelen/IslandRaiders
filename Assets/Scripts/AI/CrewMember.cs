using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CrewMember : NetworkBehaviour
{
    private enum State { Patrol, Investigate, Return, Combat }

    [Header("Route")]
    [SerializeField] private WaypointNode[] patrolRoute;
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3f;
    [SerializeField] private float arriveThreshold = 0.3f;

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

    // havuzdan gelen mürettebat bir önceki yaşamdan kalma durumu taşıyabilir (hedef, düşmanlık, savaş
    // durumu) - bu yüzden hem ilk spawn'da hem havuzdan yeniden kullanımda tüm AI durumu burada sıfırlanır
    public void SetPatrolRoute(WaypointNode[] route)
    {
        patrolRoute = route;
        allNodes = route.Length > 0 ? ShipPathfinder.CollectReachable(route[0]) : new List<WaypointNode>();
        patrolIndex = 0;
        patrolDirection = 1;

        state = State.Patrol;
        target = null;
        isHostile = false;
        lastSeenTime = float.NegativeInfinity;
        nextFireTime = 0f;
        nextRepathTime = 0f;
        graphPath = null;
        graphPathIndex = 0;

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
        else if (state == State.Combat && Time.time - lastSeenTime > loseTargetGraceTime)
        {
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

    // QA/test amacli: gorus/mesafe kontrolu olmadan dogrudan Combat durumuna gecirir
    public void ForceCombat(PlayerHealth targetPlayer)
    {
        if (!IsServer || targetPlayer == null)
        {
            return;
        }

        isHostile = true;
        target = targetPlayer;
        state = State.Combat;
        lastSeenTime = Time.time;
    }

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

    private void GoInvestigate(Vector3 worldPosition)
    {
        WaypointNode from = FindNearestClearNode(transform.position);
        WaypointNode to = FindNearestClearNode(worldPosition);
        graphPath = ShipPathfinder.FindPath(from, to);
        graphPathIndex = 0;
        state = graphPath != null ? State.Investigate : State.Patrol;
    }

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

        // kendi govdesine/silahina çarpmamak icin tüm isabetler taranir, ilk yabanci hedef alınır
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
        BulletVisual bullet = PoolManager.Instance.GetBullet(bulletPrefab);
        bullet.Launch(start, end);
    }
}
