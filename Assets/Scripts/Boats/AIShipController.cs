using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// yapay zeka gemisi; adalar arasinda NavMesh uzerinden otomatik yol bulur, hedef adayi
// rastgele secer, sadece ada duraklarinda (kalkis + varis) bir sure bekler - aradaki yolda
// durmadan gider. Yanasma noktalari (dock) dolu ise bosalana kadar bekler. Gemiler arasi
// carpisma NavMeshAgent'in dahili avoidance'i ile onlenir.
// NavMeshAgent SADECE yol + kacinma yonunu hesaplar (desiredVelocity); gemi bu yone dogru
// kendi donus yaricapiyla, yalniz burnu yonunde ilerler ve dumen ancak yol alirken tutar
// (BoatController'daki hiz orantili donusun aynisi) - duran gemi yerinde donemez, hep
// ileri gidip yay cizer. Hedefe yaklasinca yavaslayip hedefe suzulur (liman manevrasi),
// boylece genis donus yaricapi yuzunden hedefin etrafinda donup durmaz. Yukseklik (dalga)
// ve yatma transform'a bindirilir. Hareket sunucuda, NetworkTransform herkese senkronlar.
[RequireComponent(typeof(NavMeshAgent))]
public class AIShipController : NetworkBehaviour, IShipDeck
{
    private enum State { Docked, Traveling, WaitingForDock, Docking }

    [Header("Duraklama")]
    [SerializeField] private float dockWaitTime = 10f;      // adada bekleme (ilk kalkis / son durak)
    [SerializeField] private float arriveDistance = 1.5f;   // hedefe varmis sayilma mesafesi
    [SerializeField] private float slowdownDistance = 12f;  // hedefe bu mesafede yavaslayip suzulmeye baslar

    [Header("Baslangic")]
    [SerializeField] private Island startIsland;          // spawn'da yanasacagi ada

    [Header("Dalga hareketi")]
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 1f;
    [SerializeField] private float bankAngle = 6f;
    [SerializeField] private float bankSmooth = 2f;
    [SerializeField] private float turnRate = 35f;        // burnun donme hizi (derece/sn) - dusuk = genis, gemi gibi kavis
    [SerializeField] [Range(0f, 1f)] private float minTurnSpeed = 0.4f; // keskin donuste hizin inecegi taban (gemi donerken yavaslar)
    [SerializeField] private float steerSmooth = 2f;      // rota yonundeki ani sicramalari (kose/kacinma) yumusatma hizi

    private NavMeshAgent agent;
    private NavMeshPath scratchPath;   // ulasabilirlik kontrolu icin tekrar kullanilan tampon

    private State state;
    private Island currentIsland;      // su an yanasik / kalktigi ada
    private Island destinationIsland;  // yolda ise hedef ada
    private int dockSlot = -1;
    private float resumeTime;

    private float baseHeight;          // dalga salinimi bu yukseklik etrafinda olur
    private float currentBank;
    private Quaternion heading;
    private Vector3 smoothedSteer;     // yumusatilmis rota yonu/siddeti (dogal hizlanma-yavaslama da buradan)

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // Y (dalga), rotasyon (burun -Z) ve yatma bizde; agent yalniz XZ path/kacinma hesaplasin
        agent.updatePosition = false;
        agent.updateRotation = false;
        // client'lar hareketi NetworkTransform'dan alir; agent sadece sunucuda calisir
        agent.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        heading = transform.rotation;

        // baslangic adasina yanas; slot bulunursa dock noktasina otur
        Vector3 startPos = transform.position;
        currentIsland = startIsland;
        if (currentIsland != null)
        {
            dockSlot = currentIsland.TryReserveDock(this);
            if (dockSlot >= 0)
            {
                startPos = currentIsland.GetDockPoint(dockSlot).position;
            }
        }

        baseHeight = startPos.y;
        transform.position = startPos;

        agent.enabled = true;
        agent.Warp(startPos);
        scratchPath = new NavMeshPath();

        state = State.Docked;
        resumeTime = Time.time + dockWaitTime;
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        switch (state)
        {
            case State.Docked:
                TickDocked();
                break;
            case State.Traveling:
                TickTraveling();
                break;
            case State.WaitingForDock:
                TickWaitingForDock();
                break;
            case State.Docking:
                TickDocking();
                break;
        }

        ApplyMotion();
    }

    private void TickDocked()
    {
        if (Time.time >= resumeTime)
        {
            BeginNextLeg();
        }
    }

    // random bir hedef ada secip yola cikar; hedef yoksa biraz sonra tekrar dener
    private void BeginNextLeg()
    {
        if (currentIsland == null)
        {
            resumeTime = Time.time + dockWaitTime;
            return;
        }

        Island destination = PickDestination();
        if (destination == null)
        {
            resumeTime = Time.time + dockWaitTime;
            return;
        }

        currentIsland.ReleaseDock(dockSlot, this);
        dockSlot = -1;

        destinationIsland = destination;
        agent.SetDestination(destination.ApproachPoint.position);
        state = State.Traveling;
    }

    // mevcut ada haric, NavMesh uzerinden yol bulunabilen adalardan rastgele birini secer
    private Island PickDestination()
    {
        var candidates = new List<Island>();
        Vector3 from = agent.nextPosition;

        foreach (Island island in Island.All)
        {
            if (island == currentIsland || island.ApproachPoint == null)
            {
                continue;
            }

            if (NavMesh.CalculatePath(from, island.ApproachPoint.position, NavMesh.AllAreas, scratchPath)
                && scratchPath.status == NavMeshPathStatus.PathComplete)
            {
                candidates.Add(island);
            }
        }

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    private void TickTraveling()
    {
        if (ReachedDestination())
        {
            TryDockOrWait();
        }
    }

    // hedef adanin yaklasma noktasina varildi: bos slot varsa yanas, yoksa bekle
    private void TryDockOrWait()
    {
        dockSlot = destinationIsland.TryReserveDock(this);
        if (dockSlot >= 0)
        {
            agent.SetDestination(destinationIsland.GetDockPoint(dockSlot).position);
            state = State.Docking;
        }
        else
        {
            // yol birakilir ki agent gemiyi hedefe cekmeye devam etmesin, gemi suzulup dursun
            agent.ResetPath();
            state = State.WaitingForDock;
        }
    }

    private void TickWaitingForDock()
    {
        // yaklasma noktasinda bekler (agent orada durdu), slot bosaldigi frame yanasmaya baslar
        dockSlot = destinationIsland.TryReserveDock(this);
        if (dockSlot >= 0)
        {
            agent.SetDestination(destinationIsland.GetDockPoint(dockSlot).position);
            state = State.Docking;
        }
    }

    private void TickDocking()
    {
        if (ReachedDestination())
        {
            agent.ResetPath();
            currentIsland = destinationIsland;
            destinationIsland = null;
            state = State.Docked;
            resumeTime = Time.time + dockWaitTime;
        }
    }

    private bool ReachedDestination()
    {
        return !agent.pathPending && agent.remainingDistance <= arriveDistance;
    }

    // agent yol + kacinma yonunu (desiredVelocity) verir; yon yumusatilir, gemi yalniz burnu
    // (-Z) yonunde ilerler, dumen ancak yol alirken tutar (duran gemi yerinde donemez).
    // Hedefe yaklasinca yavaslayip hedefe suzulur - genis donus yaricapi yuzunden hedefin
    // etrafinda sonsuza kadar donmesin diye. Konum agent uzerinden navmesh'e kelepcelenir.
    private void ApplyMotion()
    {
        Vector3 desired = agent.desiredVelocity;
        desired.y = 0f;

        // hedefe yaklasma orani: 0 = serbest seyir, 1 = hedefin dibinde (liman manevrasi)
        float approach = 0f;
        if (agent.hasPath && !agent.pathPending && !float.IsInfinity(agent.remainingDistance))
        {
            approach = Mathf.Clamp01(1f - agent.remainingDistance / slowdownDistance);
        }
        desired *= Mathf.Lerp(1f, 0.2f, approach);

        // kose gecisi / kacinma kaynakli ani yon sicramalarini yumusat; buyukluk de
        // yumusadigi icin dogal hizlanma-yavaslama bedavaya gelir
        smoothedSteer = Vector3.Lerp(smoothedSteer, desired, steerSmooth * Time.deltaTime);

        Vector3 bow = heading * Vector3.back;
        bow.y = 0f;
        bow.Normalize();

        float steerMag = smoothedSteer.magnitude;
        Vector3 steerDir = steerMag > 0.01f ? smoothedSteer / steerMag : bow;

        // burun hedefe donukse tam hiz, degilse taban hiz - yine de ilerler ki donus yay cizsin
        float alignment = Mathf.Clamp01(Vector3.Dot(bow, steerDir));
        float speed = steerMag * Mathf.Lerp(minTurnSpeed, 1f, alignment);

        // serbest seyirde salt burun yonunde gider; limana yaklastikca hedefe dogru suzulur
        Vector3 moveDir = Vector3.Slerp(bow, steerDir, approach);
        Vector3 candidate = transform.position + moveDir * (speed * Time.deltaTime);

        // agent'a yazip geri okuyarak adimi navmesh'e kelepcele - donus yayi genis diye
        // gemi karaya/ada uzerine tasamaz, kiyi boyunca kayar
        candidate.y = agent.nextPosition.y;
        agent.nextPosition = candidate;
        Vector3 clamped = agent.nextPosition;
        transform.position = new Vector3(clamped.x,
            baseHeight + Mathf.Sin(Time.time * bobSpeed) * bobHeight, clamped.z);

        // dumen hiza oranli tutar (BoatController'daki speedFactor deseni) - yerinde donme olmaz;
        // tabani 0.3 ki dusuk hizda da az cok manevra kalsin (kalkista kilitlenmesin)
        float speedFactor = agent.speed > 0.01f ? Mathf.Clamp01(speed / agent.speed) : 0f;
        float maxStep = turnRate * Mathf.Lerp(0.3f, 1f, speedFactor) * Time.deltaTime;
        float turnRatio = 0f;
        if (steerMag > 0.01f && maxStep > 0f)
        {
            float previousYaw = heading.eulerAngles.y;
            heading = Quaternion.RotateTowards(heading, Quaternion.LookRotation(-steerDir), maxStep);
            turnRatio = Mathf.Clamp(Mathf.DeltaAngle(previousYaw, heading.eulerAngles.y) / maxStep, -1f, 1f);
        }

        // donerken hafifce yana yat; yavas liman manevrasinda daha az yatsin
        float targetBank = -turnRatio * bankAngle * speedFactor;
        currentBank = Mathf.Lerp(currentBank, targetBank, bankSmooth * Time.deltaTime);
        transform.rotation = heading * Quaternion.Euler(0f, 0f, currentBank);
    }
}
