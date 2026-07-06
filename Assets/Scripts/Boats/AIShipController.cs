using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// yapay zeka gemisi; adalar arasında NavMesh üzerinden otomatik yol bulur ve ada duraklarında
// bir süre bekler. Seyir ile YANASMA ayrı: açık denizde NavMesh + araç kinematigi (desiredVelocity
// yönüne, kendi dönüş yarıçapıyla, yalnız burnü yönünde ilerler). YANAŞMA iki adımlı: gemi önce
// dock'un deniz normali ekseninde uzak bir noktaya, oradan yakın noktaya gider (bu düz segment onu
// eksene hizalar), sonra son metreleri deterministik bir kübik Bezier ile dümdüz ve yumuşak girer.
// Kalkışta gemi düz geri geri (astern) denize açılır, dokta dönmez; dönüşü açık suda NavMesh yol
// alırken doğal bir yayla yapar (dönüş yayı minTurnRadius yüzünden adaya en fazla o kadar sokulur).

[RequireComponent(typeof(NavMeshAgent))]
public class AIShipController : NetworkBehaviour, IShipDeck
{
    private enum State { Docked, Traveling, WaitingForDock, Berthing, Undocking }

    [Header("Stations")]
    [SerializeField] private float dockWaitTime = 10f;      // adada bekleme (ilk kalkış / son durak)
    [SerializeField] private float arriveDistance = 1.5f;   // yaklaşma noktasına varmış sayılma mesafesi
    [SerializeField] private float slowdownDistance = 12f;  // seyir sonunda bu mesafede yavaşlar
    [SerializeField] private float dockStandoff = 4f;       // dock noktasından bu kadar önce durur (burun karaya girmesin)
    [SerializeField] private float berthSpeed = 4f;         // yanasma/kalkış eğrisi ilerleme hızı
    [SerializeField] private float minBerthTime = 3f;       // manevra en az bu kadar sürer: kısa eğride ani "yerinde dönüş"ü engeller
    [SerializeField] private float lineupDistance = 25f;    // yanaşmadan önce eksene hizalanma koşusu: yakın noktanın bu kadar denizinde başlar
    [SerializeField] private float orbitCaptureTime = 3f;   // yakın bandda bu süre boyunca hiç ilerleyemezse (orbit'e girmişse) varmış sayılır

    [Header("Ship height")]
    [SerializeField] private float heightOffset = 0f;     // gemiyi dikey kaydırır (negatif = suya gömer)

    [Header("Wave motion")]
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 1f;
    [SerializeField] private float bankAngle = 6f;
    [SerializeField] private float bankSmooth = 2f;
    [Header("Maneuver")]
    [SerializeField] private float turnRate = 35f;        // maksimum dönüş hızı
    [SerializeField] private float minTurnRadius = 6f;    // en dar dönüş yarıçapı

    private NavMeshAgent agent;
    private NavMeshPath scratchPath;   

    private State state;
    private Island currentIsland;      
    private Island destinationIsland; 
    private int dockSlot = -1;
    private float resumeTime;
    private Vector3 pendingApproach;   // Traveling'in gittiği güncel navmesh hedefi (önce uzak lane, sonra yakın nokta)
    private bool lineupPending;        // true = iki adımlı yaklaşmanın 1. adımında (uzak lane noktasına gidiyor)
    private float closestRemaining = float.MaxValue;   // hedefe en yakın geçiş mesafesi (ilerleme takibi)
    private float stallTime = -1f;                      // bandda ilerlemenin durduğu andan beri geçen süre işareti

    private float baseHeight;          // dalga salınımı bu yükseklik etrafında olur
    private float currentBank;
    private Quaternion heading;
    private float currentSpeed;        // yumuşak hızlanma-yavaşlama için mevcut ilerleme hızı

    // scripted manevra (yanasma / kalkış): NavMesh ve kinematik devre dışı, gemi bu eğriyi izler
    private Vector3 mP0, mP1, mP2, mP3;
    private float mT;
    private float mDuration;           // manevranın toplam süresi (sn) - hız değil süre ilerletir
    private bool mUseBezier;           // true = yanasma (burun tanjantı izler) / false = kalkış (burun slerp döner)
    private Quaternion mStartHeading, mEndHeading;

    private bool restored;              // kayittan geri yuklendiyse ada secmez, kayitli konumdan devam eder
    private Vector3 restorePosition;
    private Quaternion restoreRotation;

    // dunya kaydindan geri kurulurken Spawn'dan ONCE cagirilir
    public void RestoreAt(Vector3 position, Quaternion rotation)
    {
        restored = true;
        restorePosition = position;
        restoreRotation = rotation;
    }

    // havuzdan gelen gemi bir onceki yasamdan "restored" bayragini tasiyabilir - normal (kayitsiz)
    // bir spawn icin bu Spawn'dan ONCE sifirlanmali, yoksa OnNetworkSpawn eski restore konumunu kullanir
    public void ResetForReuse()
    {
        restored = false;
    }

    // kayit icin TEMIZ konum/yon: transform.position dalga salinimini + heightOffset'i icerir; onu
    // kaydedip geri yuklersek her load'da offset birikir ve gemi giderek batar. Bunun yerine gercek
    // deniz-seviyesi Y'si (baseHeight) ve sadece yaw iceren heading kaydedilir.
    public Vector3 SavePosition => new Vector3(transform.position.x, baseHeight, transform.position.z);
    public Quaternion SaveRotation => heading;

    // QA/test amacli: rastgele ada secimini atlayip gemiyi dogrudan verilen adaya yanastirir
    public void ForceDock(Island island)
    {
        if (!IsServer || island == null)
        {
            return;
        }

        if (currentIsland != null && dockSlot >= 0)
        {
            currentIsland.ReleaseDock(dockSlot, this);
        }

        currentIsland = island;
        dockSlot = island.TryReserveDock(this);

        Vector3 startPos = dockSlot >= 0 ? island.GetDockPoint(dockSlot).position : island.ApproachPointFor(transform.position);
        heading = dockSlot >= 0 ? Quaternion.LookRotation(-island.SeawardDir(dockSlot)) : transform.rotation;

        baseHeight = startPos.y;
        transform.position = startPos;
        transform.rotation = heading;

        agent.enabled = true;
        agent.Warp(startPos);

        state = State.Docked;
        resumeTime = Time.time + dockWaitTime;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // Y (dalga), rotasyon (burun -Z) ve yatma bizde; agent yalnız XZ path/kaçınma hesaplasın
        agent.updatePosition = false;
        agent.updateRotation = false;
        // client'lar hareketi NetworkTransform'dan alır; agent sadece sunucuda çalışır
        agent.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        scratchPath = new NavMeshPath();

        if (restored)
        {
            RestoreSpawn();
            return;
        }

        heading = transform.rotation;

        Vector3 startPos = transform.position;
        currentIsland = PickRandomIsland();
        if (currentIsland != null)
        {
            dockSlot = currentIsland.TryReserveDock(this);
            if (dockSlot >= 0)
            {
                startPos = currentIsland.GetDockPoint(dockSlot).position;
                // spawn'daki keyfi rotasyonu (prefab/instantiate rotasyonu adaya göre hizalı olmayabilir)
                // gerçek bir yanaşmanın bittiği yönle aynı yap - burun adaya dönük. Aksi halde ilk kalkışta
                // Undocking düz geri kayma yapar (burun sabit varsayar) ve gemi adaya paralel durup yandan kayar
                heading = Quaternion.LookRotation(-currentIsland.SeawardDir(dockSlot));
            }
        }

        baseHeight = startPos.y;
        transform.position = startPos;
        transform.rotation = heading;

        agent.enabled = true;
        agent.Warp(startPos);

        state = State.Docked;
        resumeTime = Time.time + dockWaitTime;
    }

    // kayitli konum/rotasyondan basla ve dogrudan rastgele bir adaya dogru yelken ac (kesin dok
    // durumu saklanmaz; gemi kaldigi yerden seyre devam eder)
    private void RestoreSpawn()
    {
        heading = restoreRotation;
        baseHeight = restorePosition.y;
        transform.position = restorePosition;
        transform.rotation = restoreRotation;

        agent.enabled = true;
        agent.Warp(restorePosition);

        currentIsland = null;
        destinationIsland = PickDestination();
        if (destinationIsland == null)
        {
            state = State.Docked;
            resumeTime = Time.time + dockWaitTime;
            return;
        }

        dockSlot = destinationIsland.TryReserveDock(this);
        if (dockSlot >= 0)
        {
            pendingApproach = destinationIsland.LanePoint(dockSlot, destinationIsland.ApproachMargin + lineupDistance);
            lineupPending = true;
        }
        else
        {
            pendingApproach = destinationIsland.ApproachPointFor(transform.position);
            lineupPending = false;
        }

        TravelTo(pendingApproach);
        state = State.Traveling;
    }

    // spawn'da elle ada atamak yerine sahnedeki adalardan rastgele biri seçilir
    private Island PickRandomIsland()
    {
        return Island.All.Count > 0 ? Island.All[Random.Range(0, Island.All.Count)] : null;
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
            case State.Berthing:
            case State.Undocking:
                TickManeuver();
                break;
        }

        // yanasma/kalkış eğrisi hareketini kendi yapar; diğerleri kinematikle
        if (state == State.Docked || state == State.Traveling || state == State.WaitingForDock)
        {
            ApplyMotion();
        }
    }

    private void TickDocked()
    {
        if (Time.time >= resumeTime)
        {
            BeginNextLeg();
        }
    }

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

        // önce mevcut adadan denize düz GERİ (astern) çık: burun karaya dönük kalır, dönmez;
        // dönüşü açık suda yol alırken yapar. Çıkış noktası dock'un ~20m denizinde
        int leavingSlot = dockSlot;
        Vector3 seaExit = leavingSlot >= 0
            ? currentIsland.ApproachPointForDock(leavingSlot)
            : currentIsland.ApproachPointFor(transform.position);

        currentIsland.ReleaseDock(dockSlot, this);
        destinationIsland = destination;

        // hedef adada bir dock kap; kaptıysa iki adımlı yaklaşmanın 1. adımına (uzak lane noktası)
        // yönel - böylece son navmesh parçası deniz normali ekseni boyunca gelir ve gemi hizalanır.
        // Dock doluysa genel yaklaşma noktasına gidip bekler (lane yok)
        dockSlot = destination.TryReserveDock(this);
        if (dockSlot >= 0)
        {
            pendingApproach = destination.LanePoint(dockSlot, destination.ApproachMargin + lineupDistance);
            lineupPending = true;
        }
        else
        {
            pendingApproach = destination.ApproachPointFor(transform.position);
            lineupPending = false;
        }

        StartUndock(seaExit);
        state = State.Undocking;
    }

    // mevcut ada haric, NavMesh üzerinden yol bulunabilen adalardan rastgele birini seçer
    private Island PickDestination()
    {
        var candidates = new List<Island>();
        Vector3 from = agent.nextPosition;

        foreach (Island island in Island.All)
        {
            if (island == currentIsland)
            {
                continue;
            }

            if (NavMesh.CalculatePath(from, island.ApproachPointFor(from), NavMesh.AllAreas, scratchPath)
                && scratchPath.status == NavMeshPathStatus.PathComplete)
            {
                candidates.Add(island);
            }
        }

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    private void TickTraveling()
    {
        if (!ReachedDestination())
        {
            return;
        }

        if (lineupPending)
        {
            // uzak lane noktasına varıldı; şimdi eksende yakın noktaya in. Bu düz segment (ikisi de
            // aynı deniz normali ekseninde) gemiyi dock'a hizalar, varışta dümdüz girer
            lineupPending = false;
            TravelTo(destinationIsland.ApproachPointForDock(dockSlot));
        }
        else
        {
            TryDockOrWait();
        }
    }

    // yakın yaklaşma noktasına varıldı (eksene hizalı): dock zaten bizimse dümdüz gir. Dock henüz
    // yoksa şimdi kapmayı dene - kaparsak önce hizalanma koşusuna dön, o da yoksa bekle
    private void TryDockOrWait()
    {
        if (dockSlot >= 0)
        {
            StartDockBerth();
            return;
        }

        dockSlot = destinationIsland.TryReserveDock(this);
        if (dockSlot >= 0)
        {
            BeginLineup();
        }
        else
        {
            // yol bırakılır ki agent gemiyi hedefe çekmeye devam etmesin, gemi süzülüp dursun
            agent.ResetPath();
            state = State.WaitingForDock;
        }
    }

    // iki adımlı yaklaşmayı başlat: önce uzak lane noktasına git (eksene hizalanma), oradan yakın noktaya
    private void BeginLineup()
    {
        lineupPending = true;
        TravelTo(destinationIsland.LanePoint(dockSlot, destinationIsland.ApproachMargin + lineupDistance));
        state = State.Traveling;
    }

    private void TickWaitingForDock()
    {
        // yaklaşma noktasında bekler; slot boşaldığı frame iki adımlı yaklaşmayla eksene hizalanıp girer
        dockSlot = destinationIsland.TryReserveDock(this);
        if (dockSlot >= 0)
        {
            BeginLineup();
        }
    }

    // Yanasma: dock'un önündeki duruş noktasına, adaya tam hızalı biteçek bir Bezier eğrisi kur.
    // P0 = şu anki poz, P1 = burun yönünde çıkış, P2 = deniz tarafından düz giriş, P3 = duruş noktası.
    private void StartDockBerth()
    {
        Vector3 dockPos = destinationIsland.GetDockPoint(dockSlot).position;
        Vector3 seaward = destinationIsland.SeawardDir(dockSlot);
        Vector3 endPos = dockPos + seaward * dockStandoff; // burun karaya girmesin diye biraz önde
        agent.ResetPath();
        StartBezierBerth(endPos, -seaward);                // burun adaya dönük (deniz yönünün tersi) biter
        state = State.Berthing;
    }

    private bool ReachedDestination()
    {
        if (agent.pathPending)
        {
            return false;
        }

        float remaining = agent.remainingDistance;
        if (float.IsInfinity(remaining))
        {
            return false;
        }

        // gemi bir noktaya dönüş yarıçapından daha çok yaklaşamaz; arriveDistance bu fiziksel bantla sınırlanır
        // (aşırı büyük değer verilse bile gemi uzaktan "vardım" deyip Bézier'i erken başlatmaz)
        float captureBand = minTurnRadius * 2f;
        float arrive = Mathf.Min(arriveDistance, captureBand);

        if (remaining <= arrive)
        {
            ResetArrivalTrackers();
            return true;
        }

        // Banda girince ilerlemeyi izle: mesafe azalmaya devam ediyorsa gemi hâlâ yaklaşıyor, bırak
        // arriveDistance'a insin. Ters açıda gelip hedefin etrafında dönüyorsa (orbit) mesafe azalmayı
        // bırakır; orbitCaptureTime kadar takılı kalırsa varmış say -> Bézier yanaşma devralır ve hizalar.
        if (remaining <= captureBand)
        {
            if (remaining < closestRemaining - 0.1f)
            {
                closestRemaining = remaining;
                stallTime = Time.time;
            }
            else if (Time.time - stallTime >= orbitCaptureTime)
            {
                ResetArrivalTrackers();
                return true;
            }
        }
        else
        {
            ResetArrivalTrackers();
        }
        return false;
    }

    private void ResetArrivalTrackers()
    {
        closestRemaining = float.MaxValue;
        stallTime = -1f;
    }

    // yeni bir seyir hedefi ata; ilerleme takibini sıfırla ki önceki hedeften devralmasın
    private void TravelTo(Vector3 destination)
    {
        pendingApproach = destination;
        agent.SetDestination(destination);
        ResetArrivalTrackers();
    }

    // Araç kinematigi (yalnız seyir): gemi burnü (-Z) yönünde ilerler, dönüş hızı ilerleme hızına
    // bağlı (dönüş = hız / yarıçap). Hız sıfıra yakınsa dönüş de sıfıra yakın -> gemi olduğu yerde
    // dönemez, keskin açıda yay çizer. Adım navmesh'e kelepçelenir. Dalga + yatma en son bindirilir.
    private void ApplyMotion()
    {
        bool traveling = state == State.Traveling;

        // hedef hız: seyirde tam gaz; sadece yolun son düzlüğünde yavaşlar
        float targetSpeed = 0f;
        if (traveling)
        {
            targetSpeed = agent.speed;
            if (agent.hasPath && !agent.pathPending && !float.IsInfinity(agent.remainingDistance))
            {
                float slow = Mathf.Clamp01(agent.remainingDistance / slowdownDistance);
                targetSpeed *= Mathf.Lerp(0.25f, 1f, slow);
            }
        }
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, agent.acceleration * Time.deltaTime);

        Vector3 bow = heading * Vector3.back;
        bow.y = 0f;
        bow.Normalize();

        // gidilecek yön: agent'in yol+kaçınma yönü; o zayıfsa (varışta frenlerken) sonraki köşe
        Vector3 steerDir = bow;
        Vector3 desired = agent.desiredVelocity;
        desired.y = 0f;
        if (desired.sqrMagnitude > 0.04f)
        {
            steerDir = desired.normalized;
        }
        else if (traveling)
        {
            Vector3 toCorner = agent.steeringTarget - transform.position;
            toCorner.y = 0f;
            if (toCorner.sqrMagnitude > 0.04f)
            {
                steerDir = toCorner.normalized;
            }
        }

        // burnü bu yöne çevir - dönüş hızı = hız / yarıçap, üst sınır turnRate. Hız düştükçe
        // dönüş de düşer; currentSpeed ~0 iken dönüş yok (anti-pivot garantisi buradan gelir)
        float turnRatio = 0f;
        if (currentSpeed > 0.05f)
        {
            float degPerSec = Mathf.Min(currentSpeed / Mathf.Max(minTurnRadius, 0.1f) * Mathf.Rad2Deg, turnRate);
            float step = degPerSec * Time.deltaTime;
            float previousYaw = heading.eulerAngles.y;
            heading = Quaternion.RotateTowards(heading, Quaternion.LookRotation(-steerDir), step);
            if (step > 0f)
            {
                turnRatio = Mathf.Clamp(Mathf.DeltaAngle(previousYaw, heading.eulerAngles.y) / step, -1f, 1f);
            }
            bow = heading * Vector3.back;
            bow.y = 0f;
            bow.Normalize();
        }

        // burnü yönünde ilerle; adımı agent'a yazıp geri okuyarak navmesh'e kelepçele
        Vector3 candidate = transform.position + bow * (currentSpeed * Time.deltaTime);
        candidate.y = agent.nextPosition.y;
        agent.nextPosition = candidate;
        Vector3 clamped = agent.nextPosition;
        transform.position = new Vector3(clamped.x, WaveY(), clamped.z);

        // dönerken hafifçe yat (hıza oranı)
        float speedFactor = agent.speed > 0.01f ? Mathf.Clamp01(currentSpeed / agent.speed) : 0f;
        float targetBank = -turnRatio * bankAngle * speedFactor;
        currentBank = Mathf.Lerp(currentBank, targetBank, bankSmooth * Time.deltaTime);
        transform.rotation = heading * Quaternion.Euler(0f, 0f, currentBank);
    }

    // Yanasma/kalkış eğrisini ilerlet. Yanasma da poz Bezier'den, burun tanjanttan gelir (gemi eğriyi burnuyla izler). Kalkışta poz düz denize kayar, burun ada->deniz slerp ile döner
    // (önce geri çıkıp sonra denize dönen doğal manevra). Bitince ilgili duruma geçer.
    private void TickManeuver()
    {
        // süre-tabanı ilerleme + smoothstep: dönüş hep sabit MIN süreye yayılır, kısa eğride bile
        // ani "yerinde dönüş" (snap) olmaz; giriş/çıkış yumuşaktır (s'(0)=s'(1)=0)
        mT += Time.deltaTime / mDuration;
        bool done = mT >= 1f;
        float s = Mathf.Clamp01(mT);
        s = s * s * (3f - 2f * s);

        Vector3 pos;
        if (mUseBezier)
        {
            pos = Bezier(mP0, mP1, mP2, mP3, s);
            Vector3 tan = BezierTangent(mP0, mP1, mP2, mP3, s);
            tan.y = 0f;
            if (tan.sqrMagnitude > 0.0001f)
            {
                heading = Quaternion.LookRotation(-tan.normalized);
            }
        }
        else
        {
            pos = Vector3.Lerp(mP0, mP3, s);
            heading = Quaternion.Slerp(mStartHeading, mEndHeading, s);
        }

        Vector3 np = agent.nextPosition;
        agent.nextPosition = new Vector3(pos.x, np.y, pos.z);
        transform.position = new Vector3(pos.x, WaveY(), pos.z);
        currentBank = Mathf.Lerp(currentBank, 0f, bankSmooth * Time.deltaTime);
        transform.rotation = heading * Quaternion.Euler(0f, 0f, currentBank);

        if (done)
        {
            currentSpeed = 0f;
            if (state == State.Berthing)
            {
                currentIsland = destinationIsland;
                destinationIsland = null;
                state = State.Docked;
                resumeTime = Time.time + dockWaitTime;
            }
            else // Undocking: açık sudayız (burun hâlâ adaya dönük), NavMesh yol alırken çevirir
            {
                TravelTo(pendingApproach);
                state = State.Traveling;
            }
        }
    }

    // Yanasma eğrisi: gemi şu anki pozunda ve burun yönünde başlar, endPos'ta endForward yönüne
    // tam hızalı biter. P2 hedefin biraz denizinde olur ki eğri dock'a dümdüz girsin.
    private void StartBezierBerth(Vector3 endPos, Vector3 endForward)
    {
        Vector3 p0 = transform.position;
        p0.y = 0f;
        Vector3 bow = heading * Vector3.back;
        bow.y = 0f;
        bow.Normalize();
        Vector3 end = endPos;
        end.y = 0f;
        Vector3 fwd = endForward;
        fwd.y = 0f;
        fwd.Normalize();

        float h = Mathf.Max(Vector3.Distance(p0, end) * 0.5f, 0.5f);
        mP0 = p0;
        mP1 = p0 + bow * h;
        mP2 = end - fwd * h;
        mP3 = end;
        mUseBezier = true;
        mT = 0f;
        mDuration = Mathf.Max(ApproxBezierLength() / berthSpeed, minBerthTime);
    }

    // Kalkış manevrası: dock'tan denizdeki çıkış noktasına DÜZ GERİ (astern) kayma. Burun SABİT
    // kalır (dönmez) - gemi geri geri denize açılır, dönüşü sonra açık suda seyir ederken yapar.
    private void StartUndock(Vector3 seaExit)
    {
        Vector3 p0 = transform.position;
        p0.y = 0f;
        mP0 = p0;
        mP3 = new Vector3(seaExit.x, 0f, seaExit.z);
        mStartHeading = heading;
        mEndHeading = heading; // burun sabit: dokta dönmez, sadece geri geri çıkar
        mUseBezier = false;
        mT = 0f;
        mDuration = Mathf.Max(Vector3.Distance(mP0, mP3) / berthSpeed, minBerthTime);
    }

    private float ApproxBezierLength()
    {
        float len = 0f;
        Vector3 prev = mP0;
        for (int i = 1; i <= 12; i++)
        {
            Vector3 p = Bezier(mP0, mP1, mP2, mP3, i / 12f);
            len += Vector3.Distance(prev, p);
            prev = p;
        }
        return Mathf.Max(len, 0.5f);
    }

    private float WaveY()
    {
        return baseHeight + heightOffset + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    private static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
    }
}
