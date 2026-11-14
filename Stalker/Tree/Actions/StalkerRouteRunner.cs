using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HorrorFPS
{
    public enum StalkerRouteFilter { Any = 0, Ladder = 1, Duct = 2, Vault = 3, Drop = 4 }

    public enum StalkerRoutePick
    {
        [Tooltip("A rota com a entrada mais perto (pelo caminho andando).")] Nearest = 0,
        [Tooltip("A rota que leva ao destino com o menor custo (andar até a entrada + percurso + andar da saída até o destino).")] TowardGoal = 1,
        [Tooltip("Uma das rotas possíveis, sorteada.")] Random = 2
    }

    public enum StalkerRunnerState { Idle, Approach, Enter, Traverse, Exit, AmbushApproach, AmbushEnter, AmbushWait, AmbushBurst }

    public enum StalkerRunnerResult { None, Done, Failed, Cancelled }

    /// <summary>Resultado do planejador: qual rota, em que sentido, e onde ele pisa no NavMesh ao entrar e ao sair.</summary>
    public struct StalkerRoutePlan
    {
        public bool valid;
        public StalkerRoute route;
        public bool forward;
        public Vector3 entryNav;
        public Vector3 exitNav;
        public float cost;
    }

    /// <summary>
    /// Escolhe rotas com <c>NavMesh.CalculatePath</c>. Só olha as rotas mais perto (no máximo 8 por consulta) para não pesar.
    /// Uma rota vale quando o monstro consegue ANDAR até a entrada e, se há destino, andar da saída até ele.
    /// </summary>
    public static class StalkerRoutePlanner
    {
        const int MaxCandidates = 8;

        static NavMeshPath scratch;
        static readonly Vector3[] corners = new Vector3[64];
        static readonly List<StalkerRoute> cand = new List<StalkerRoute>();
        static readonly List<float> candDist = new List<float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            scratch = null;
            cand.Clear();
            candDist.Clear();
        }

        public static int MaskOf(StalkerRouteFilter f)
        {
            if (f == StalkerRouteFilter.Any) return -1;
            return 1 << ((int)f - 1);
        }

        public static bool KindAllowed(int mask, StalkerRouteKind kind) { return (mask & (1 << (int)kind)) != 0; }

        /// <summary>Comprimento do caminho andando entre dois pontos. <paramref name="complete"/> = o caminho chega até o fim.</summary>
        public static float PathLength(Vector3 a, Vector3 b, out bool complete)
        {
            complete = false;
            if (scratch == null) scratch = new NavMeshPath();
            if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, scratch)) return float.MaxValue;
            complete = scratch.status == NavMeshPathStatus.PathComplete;
            int n = scratch.GetCornersNonAlloc(corners);
            float len = 0f;
            for (int i = 0; i < n - 1; i++) len += Vector3.Distance(corners[i], corners[i + 1]);
            return len;
        }

        /// <summary>O monstro chega a <paramref name="goal"/> só andando?</summary>
        public static bool WalkReachable(Vector3 from, Vector3 goal)
        {
            NavMeshHit h;
            if (!NavMesh.SamplePosition(goal, out h, 3f, NavMesh.AllAreas)) return false;
            bool complete;
            PathLength(from, h.position, out complete);
            return complete;
        }

        static void Gather(Vector3 me, StalkerBrain brain, int kindMask, string marker, float radius)
        {
            cand.Clear();
            candDist.Clear();
            List<StalkerRoute> all = StalkerRoute.All;
            for (int i = 0; i < all.Count; i++)
            {
                StalkerRoute r = all[i];
                if (r == null || !r.Allows(brain) || !KindAllowed(kindMask, r.Kind)) continue;
                if (!string.IsNullOrEmpty(marker) && r.Marker != marker) continue;
                float d = Mathf.Min(StalkerWorldBridge.HorizontalDistance(me, r.EntryRaw(true)), StalkerWorldBridge.HorizontalDistance(me, r.ExitRaw(true)));
                if (radius > 0f && d > radius) continue;
                int pos = candDist.Count;
                while (pos > 0 && candDist[pos - 1] > d) pos--;
                if (pos >= MaxCandidates) continue;
                cand.Insert(pos, r);
                candDist.Insert(pos, d);
                if (cand.Count > MaxCandidates)
                {
                    cand.RemoveAt(MaxCandidates);
                    candDist.RemoveAt(MaxCandidates);
                }
            }
        }

        /// <summary>
        /// Procura a melhor rota. <paramref name="needGoal"/>: exige que dê para andar da saída até <paramref name="goal"/>.
        /// <paramref name="requireFaster"/>: com destino alcançável andando, só aceita a rota se ela for mais rápida que ir direto.
        /// </summary>
        public static StalkerRoutePlan Plan(StalkerBrain brain, Vector3 goal, bool needGoal, int kindMask, string marker, float radius, StalkerRoutePick pick, bool requireFaster)
        {
            StalkerRoutePlan best = new StalkerRoutePlan();
            best.cost = float.MaxValue;
            if (brain == null) return best;
            Vector3 me = brain.transform.position;
            Vector3 goalNav = goal;
            if (needGoal)
            {
                NavMeshHit gh;
                if (!NavMesh.SamplePosition(goal, out gh, 3f, NavMesh.AllAreas)) return best;
                goalNav = gh.position;
            }
            float walk = Mathf.Max(0.5f, brain.Tune.walkSpeed);
            float direct = float.MaxValue;
            if (needGoal && requireFaster)
            {
                bool cd;
                float ld = PathLength(me, goalNav, out cd);
                if (cd) direct = ld / walk;
            }
            Gather(me, brain, kindMask, marker, radius);
            int valid = 0;
            for (int c = 0; c < cand.Count; c++)
            {
                StalkerRoute r = cand[c];
                for (int d = 0; d < 2; d++)
                {
                    bool fwd = d == 0;
                    if (!fwd && !r.BothWays) continue;
                    Vector3 inNav, outNav;
                    if (!r.NavPoint(r.EntryRaw(fwd), out inNav) || !r.NavPoint(r.ExitRaw(fwd), out outNav)) continue;
                    bool c1;
                    float l1 = PathLength(me, inNav, out c1);
                    if (!c1) continue;
                    float cost = l1 / walk;
                    if (needGoal)
                    {
                        bool c2;
                        float l2 = PathLength(outNav, goalNav, out c2);
                        if (!c2) continue;
                        cost += r.Duration + r.ExtraCost + l2 / walk;
                        if (cost >= direct) continue;
                    }
                    bool take;
                    if (pick == StalkerRoutePick.Random)
                    {
                        valid++;
                        take = UnityEngine.Random.value < 1f / valid;
                    }
                    else take = cost < best.cost;
                    if (!take) continue;
                    best.valid = true;
                    best.route = r;
                    best.forward = fwd;
                    best.entryNav = inNav;
                    best.exitNav = outNav;
                    best.cost = cost;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// Executa os percursos fora do NavMesh no corpo do monstro: <see cref="StalkerRoute"/> (escada, duto, vão, pulo) e
    /// <see cref="StalkerAmbushSpot"/> (esconder e saltar). Vai no MESMO objeto do <c>StalkerBrain</c>.
    /// <para>Como funciona: o monstro ANDA até a entrada (o runner assume o movimento no fim de cada quadro), e na entrada o cérebro é
    /// congelado e o runner leva o corpo à mão pelo percurso (o percurso é atômico: nada o interrompe no meio).
    /// Na saída o corpo volta ao NavMesh, o cérebro descongela e a árvore recomeça.</para>
    /// <para>Quem pede: os nós "Usar rota" e "Emboscada", ou o reflexo automático (<c>Auto Use</c>): quando o caminho até o destino do
    /// monstro é só parcial, ele acha sozinho uma rota que resolva.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StalkerBrain))]
    [AddComponentMenu("HorrorFPS/Stalker/Scenery/Route Runner")]
    public class StalkerRouteRunner : MonoBehaviour
    {
        [Header("Uso automático (reflexo)")]
        [Tooltip("Quando o caminho até o destino do monstro é só parcial (o destino está em outro andar, depois de um duto...), ele procura sozinho uma rota que resolva. Desligue para usar rotas só pelos nós da árvore.")]
        [SerializeField] bool autoUse = true;
        [Tooltip("Segundos entre as verificações do reflexo.")]
        [SerializeField] float scanInterval = 0.5f;
        [Tooltip("Segundos de descanso do reflexo depois de usar ou falhar uma rota (evita ir e voltar).")]
        [SerializeField] float autoCooldown = 4f;
        [Tooltip("Só considera rotas com uma ponta a até tantos metros (em linha reta). 0 = sem limite.")]
        [SerializeField] float autoRadius = 40f;
        [Tooltip("O reflexo só age se o fim do caminho parcial ficar a mais de tantos metros do destino.")]
        [SerializeField] float autoMinGap = 2.5f;

        [Header("Movimento")]
        [Tooltip("A que distância da entrada (horizontal) ele considera que chegou.")]
        [SerializeField] float arriveDistance = 0.7f;
        [Tooltip("Segundos para chegar à entrada antes de desistir.")]
        [SerializeField] float approachTimeout = 25f;
        [Tooltip("Giro do corpo durante o percurso, graus por segundo.")]
        [SerializeField] float turnSpeed = 360f;
        [Tooltip("Desliga o NavMeshAgent durante o percurso (recomendado). Desligado: o agente fica ligado, mas sem mexer no corpo.")]
        [SerializeField] bool disableAgent = true;
        [Tooltip("Desiste de andar até a entrada se um jogador vivo chegar a menos de tantos metros (para não ignorar a vítima ao alcance). 0 = nunca.")]
        [SerializeField] float abortPlayerDistance = 2.2f;

        [Header("Eventos e teste")]
        public UnityEvent onBegin = new UnityEvent();
        public UnityEvent onEnd = new UnityEvent();
        [Tooltip("Escreve no Console cada passo (teste).")]
        [SerializeField] bool debugLog = false;

        StalkerBrain brain;
        NavMeshAgent agent;
        StalkerRunnerState state = StalkerRunnerState.Idle;
        StalkerRoute route;
        StalkerAmbushSpot spot;
        bool forward = true;
        Vector3 entryNav, exitNav, entryRaw;
        Vector3 spotApproachNav, spotExitNav;
        Quaternion burstRot = Quaternion.identity;
        float stateTime, approachTimer, travelled, noiseTimer, slideDuration, checkTimer, scanTimer, cooldownUntil;
        Vector3 slideFrom;
        bool frozenByMe, ambushQuiet, savedUpdatePos = true, savedUpdateRot = true;
        readonly List<Renderer> hidden = new List<Renderer>();

        // ---------- leitura ----------
        public StalkerRunnerState State { get { return state; } }
        public bool Busy { get { return state != StalkerRunnerState.Idle; } }
        public StalkerRunnerResult LastResult { get; private set; }
        public bool LastAmbushBurst { get; private set; }
        public StalkerRoute Route { get { return route; } }
        public StalkerAmbushSpot Spot { get { return spot; } }

        /// <summary>O corpo já está nas mãos do runner (cérebro congelado): a árvore não manda mais nada até o fim.</summary>
        public bool Traversing { get { return frozenByMe; } }

        /// <summary>Escondido numa emboscada, esperando.</summary>
        public bool Hidden { get { return state == StalkerRunnerState.AmbushWait; } }

        public static StalkerRouteRunner Of(StalkerBrain b) { return b != null ? b.GetComponent<StalkerRouteRunner>() : null; }

        StalkerBrain B
        {
            get
            {
                if (brain == null) brain = GetComponent<StalkerBrain>();
                return brain;
            }
        }

        NavMeshAgent Agent
        {
            get
            {
                if (agent == null)
                {
                    StalkerBrain b = B;
                    agent = b != null && b.Agent != null ? b.Agent : GetComponent<NavMeshAgent>();
                }
                return agent;
            }
        }

        // ---------- pedidos ----------
        /// <summary>Começa a usar uma rota (anda até a entrada e percorre). Falso se já está ocupado ou a rota não vale.</summary>
        public bool Begin(StalkerRoute r, bool fwd)
        {
            StalkerBrain b = B;
            if (state != StalkerRunnerState.Idle || r == null || b == null) return false;
            if (!r.Allows(b) || (!fwd && !r.BothWays)) return false;
            Vector3 inNav, outNav;
            if (!r.NavPoint(r.EntryRaw(fwd), out inNav) || !r.NavPoint(r.ExitRaw(fwd), out outNav))
            {
                StalkerWorldBridge.WarnOnce("routenav:" + r.GetInstanceID(), "A rota '" + r.name + "' tem a entrada ou a saída longe demais do NavMesh. Aproxime os pontos ou aumente o Nav Search da rota.");
                return false;
            }
            if (!r.TryReserve(b)) return false;
            route = r;
            forward = fwd;
            entryNav = inNav;
            exitNav = outNav;
            entryRaw = r.EntryRaw(fwd);
            approachTimer = 0f;
            LastResult = StalkerRunnerResult.None;
            state = StalkerRunnerState.Approach;
            Log("indo até a rota '" + r.name + "' (" + r.Kind + (fwd ? ", ida" : ", volta") + ")");
            onBegin.Invoke();
            return true;
        }

        /// <summary>Vai até o esconderijo, entra e espera um jogador chegar perto.</summary>
        public bool BeginAmbush(StalkerAmbushSpot s)
        {
            StalkerBrain b = B;
            if (state != StalkerRunnerState.Idle || s == null || b == null) return false;
            if (!s.Allows(b)) return false;
            Vector3 ap, ex;
            if (!s.NavApproach(out ap) || !s.NavExit(out ex))
            {
                StalkerWorldBridge.WarnOnce("ambushnav:" + s.GetInstanceID(), "O ponto de emboscada '" + s.name + "' tem a entrada ou a saída fora do NavMesh.");
                return false;
            }
            if (!s.TryReserve(b)) return false;
            spot = s;
            spotApproachNav = ap;
            spotExitNav = ex;
            approachTimer = 0f;
            ambushQuiet = false;
            LastResult = StalkerRunnerResult.None;
            LastAmbushBurst = false;
            state = StalkerRunnerState.AmbushApproach;
            Log("indo se esconder em '" + s.name + "'");
            onBegin.Invoke();
            return true;
        }

        /// <summary>Escondido: salta agora (sem esperar o jogador).</summary>
        public void BurstNow()
        {
            if (state == StalkerRunnerState.AmbushWait) StartBurst(null, false);
        }

        /// <summary>Interrompe. Se o corpo está no meio do percurso, volta para o NavMesh (entrada ou saída, a mais perto).</summary>
        public void Cancel()
        {
            switch (state)
            {
                case StalkerRunnerState.Idle:
                    return;
                case StalkerRunnerState.Approach:
                case StalkerRunnerState.AmbushApproach:
                    if (B != null) B.Stop();
                    ReleaseReservations();
                    Finish(StalkerRunnerResult.Cancelled);
                    return;
                default:
                {
                    Vector3 pos = SnapPosition();
                    EndVisuals();
                    ReleaseFromTravel(pos, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                    ReleaseReservations();
                    Finish(StalkerRunnerResult.Cancelled);
                    return;
                }
            }
        }

        // ---------- laço ----------
        void Update()
        {
            float dt = Time.deltaTime;
            switch (state)
            {
                case StalkerRunnerState.Idle:
                    AutoScan(dt);
                    break;
                case StalkerRunnerState.Enter:
                case StalkerRunnerState.Traverse:
                case StalkerRunnerState.Exit:
                    TickRoute(dt);
                    break;
                case StalkerRunnerState.AmbushEnter:
                case StalkerRunnerState.AmbushWait:
                case StalkerRunnerState.AmbushBurst:
                    TickAmbush(dt);
                    break;
            }
        }

        // No fim do quadro, depois da árvore: o último MoveTo do quadro é o do runner.
        void LateUpdate()
        {
            if ((state == StalkerRunnerState.Approach && route == null) || (state == StalkerRunnerState.AmbushApproach && spot == null))
            {
                Cancel();   // a rota ou o esconderijo foi destruído no caminho
                return;
            }
            if (state == StalkerRunnerState.Approach)
            {
                int r = StepApproach(entryNav, Time.deltaTime);
                if (r < 0)
                {
                    Log("não consegui chegar à entrada da rota");
                    if (B != null) B.Stop();
                    ReleaseReservations();
                    Finish(StalkerRunnerResult.Failed);
                }
                else if (r > 0) BeginTraverse();
            }
            else if (state == StalkerRunnerState.AmbushApproach)
            {
                int r = StepApproach(spotApproachNav, Time.deltaTime);
                if (r < 0)
                {
                    Log("não consegui chegar ao esconderijo");
                    if (B != null) B.Stop();
                    ReleaseReservations();
                    Finish(StalkerRunnerResult.Failed);
                }
                else if (r > 0) StartHide();
            }
        }

        // 0 = indo, 1 = chegou, -1 = desistiu
        int StepApproach(Vector3 target, float dt)
        {
            StalkerBrain b = B;
            if (b == null) return -1;
            approachTimer += dt;
            if (b.Frozen || approachTimer > approachTimeout) return -1;
            if (b.Mode == StalkerMode.Attack) return -1;
            Vector3 p = transform.position;
            if (abortPlayerDistance > 0f && StalkerWorldBridge.AnyPlayerWithin(p, abortPlayerDistance)) return -1;
            if (StalkerWorldBridge.HorizontalDistance(p, target) <= arriveDistance && Mathf.Abs(p.y - target.y) < 1.5f) return 1;
            if (StalkerWorldBridge.InteractorBusy(b)) return 0;
            bool run = b.Knowledge.hasVisual || (int)b.Level >= (int)StalkerAttention.Hunting;
            b.MoveTo(target, run ? b.Tune.runSpeed : b.Tune.walkSpeed);
            NavMeshAgent ag = Agent;
            if (ag != null && ag.enabled && ag.isOnNavMesh && !ag.pathPending && approachTimer > 1.2f && ag.pathStatus != NavMeshPathStatus.PathComplete) return -1;
            return 0;
        }

        // ---------- rota ----------
        void BeginTraverse()
        {
            FreezeForTravel();
            state = StalkerRunnerState.Enter;
            stateTime = 0f;
            travelled = 0f;
            noiseTimer = 0f;
            slideFrom = transform.position;
            slideDuration = Mathf.Clamp(Vector3.Distance(slideFrom, entryRaw) / 2.5f, 0.25f, 1f);
            string ab = route.AnimBoolName;
            if (!string.IsNullOrEmpty(ab)) StalkerWorldBridge.AnimBool(B, ab, true);
            string at = route.AnimTriggerName;
            if (!string.IsNullOrEmpty(at)) StalkerWorldBridge.AnimTrigger(B, at);
            route.onEnter.Invoke();
            Log("entrou na rota");
        }

        void TickRoute(float dt)
        {
            if (route == null || B == null)
            {
                Cancel();
                return;
            }
            KeepFrozen();
            stateTime += dt;
            switch (state)
            {
                case StalkerRunnerState.Enter:
                {
                    float k = Smooth(stateTime / slideDuration);
                    transform.position = Vector3.Lerp(slideFrom, entryRaw, k);
                    Vector3 d0;
                    route.Evaluate(0f, forward, out d0);
                    Turn(RouteRotation(d0), dt);
                    if (stateTime >= slideDuration)
                    {
                        state = StalkerRunnerState.Traverse;
                        stateTime = 0f;
                        if (route.HideBody) StalkerWorldBridge.HideBody(this, hidden);
                    }
                    break;
                }
                case StalkerRunnerState.Traverse:
                {
                    float len = route.Length;
                    travelled += route.EffectiveSpeed * dt;
                    Vector3 dir;
                    Vector3 p = route.Evaluate(Mathf.Min(travelled, len), forward, out dir);
                    transform.position = p;
                    Turn(RouteRotation(dir), dt);
                    noiseTimer -= dt;
                    if (noiseTimer <= 0f && route.NoiseRadius > 0f)
                    {
                        noiseTimer = route.NoiseInterval;
                        StalkerWorldBridge.Noise(p, route.NoiseRadius, gameObject);
                    }
                    if (travelled >= len)
                    {
                        state = StalkerRunnerState.Exit;
                        stateTime = 0f;
                        slideFrom = p;
                        slideDuration = Mathf.Clamp(Vector3.Distance(p, exitNav) / 2.5f, 0.25f, 1f);
                        EndVisuals();
                        route.onExit.Invoke();
                    }
                    break;
                }
                case StalkerRunnerState.Exit:
                {
                    float k = Smooth(stateTime / slideDuration);
                    transform.position = Vector3.Lerp(slideFrom, exitNav, k);
                    Quaternion target;
                    if (route.Kind == StalkerRouteKind.Ladder)
                    {
                        Vector3 f = route.FaceDirection;
                        target = Quaternion.LookRotation(forward ? f : -f);
                    }
                    else
                    {
                        Vector3 dir;
                        route.Evaluate(route.Length, forward, out dir);
                        target = RouteRotation(dir);
                    }
                    Turn(target, dt);
                    if (stateTime >= slideDuration)
                    {
                        StalkerRoute done = route;
                        ReleaseFromTravel(exitNav, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                        done.Release(B);
                        Log("saiu da rota");
                        Finish(StalkerRunnerResult.Done);
                    }
                    break;
                }
            }
        }

        Quaternion RouteRotation(Vector3 dir)
        {
            if (route.Kind == StalkerRouteKind.Ladder) return Quaternion.LookRotation(route.FaceDirection);
            Vector3 f = StalkerWorldBridge.Flat(dir);
            if (f.sqrMagnitude < 0.01f) return transform.rotation;
            return Quaternion.LookRotation(f);
        }

        // ---------- emboscada ----------
        void StartHide()
        {
            FreezeForTravel();
            state = StalkerRunnerState.AmbushEnter;
            stateTime = 0f;
            slideFrom = transform.position;
            slideDuration = Mathf.Clamp(Vector3.Distance(slideFrom, spot.HidePos) / 1.6f, 0.4f, 1.5f);
            StalkerWorldBridge.AnimBool(B, StalkerWorldAnim.Hidden, true);
            Log("entrando no esconderijo");
        }

        void TickAmbush(float dt)
        {
            if (spot == null || B == null)
            {
                Cancel();
                return;
            }
            KeepFrozen();
            stateTime += dt;
            switch (state)
            {
                case StalkerRunnerState.AmbushEnter:
                {
                    float k = Smooth(stateTime / slideDuration);
                    transform.position = Vector3.Lerp(slideFrom, spot.HidePos, k);
                    Turn(spot.HideRot, dt);
                    if (stateTime >= slideDuration)
                    {
                        SetParts(false);
                        if (spot.HideBody) StalkerWorldBridge.HideBody(this, hidden);
                        spot.onHide.Invoke();
                        state = StalkerRunnerState.AmbushWait;
                        stateTime = 0f;
                        checkTimer = 0f;
                        Log("escondido, esperando");
                    }
                    break;
                }
                case StalkerRunnerState.AmbushWait:
                {
                    checkTimer += dt;
                    if (checkTimer >= 0.1f)
                    {
                        checkTimer = 0f;
                        PlayerContext who;
                        if (spot.PlayerTriggers(out who)) { StartBurst(who, false); break; }
                    }
                    if (stateTime >= spot.MaxWait)
                    {
                        if (spot.BurstOnTimeout) StartBurst(null, false);
                        else StartBurst(null, true);
                    }
                    break;
                }
                case StalkerRunnerState.AmbushBurst:
                {
                    float k = Smooth(stateTime / slideDuration);
                    transform.position = Vector3.Lerp(slideFrom, spotExitNav, k);
                    Turn(burstRot, dt);
                    if (stateTime >= slideDuration) FinishAmbush();
                    break;
                }
            }
        }

        void StartBurst(PlayerContext who, bool quiet)
        {
            ambushQuiet = quiet;
            LastAmbushBurst = !quiet;
            state = StalkerRunnerState.AmbushBurst;
            stateTime = 0f;
            slideFrom = transform.position;
            slideDuration = quiet ? 0.8f : 0.35f;
            SetParts(true);
            StalkerWorldBridge.ShowBody(hidden);
            StalkerWorldBridge.AnimBool(B, StalkerWorldAnim.Hidden, false);
            Vector3 toward = who != null ? who.transform.position - transform.position : spotExitNav - transform.position;
            Vector3 f = StalkerWorldBridge.Flat(toward);
            burstRot = f.sqrMagnitude > 0.01f ? Quaternion.LookRotation(f) : transform.rotation;
            if (!quiet)
            {
                StalkerWorldBridge.AnimTrigger(B, StalkerWorldAnim.Ambush);
                StalkerWorldBridge.Noise(spot.HidePos, spot.NoiseOnBurst, gameObject);
            }
            Log(quiet ? "ninguém veio: saindo quieto" : "SALTO!");
        }

        void FinishAmbush()
        {
            StalkerAmbushSpot s = spot;
            bool quiet = ambushQuiet;
            ReleaseFromTravel(spotExitNav, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            if (!quiet && B != null) B.ForceAttention(Mathf.Max(B.Attention01, s.AttentionOnBurst));
            if (!quiet) s.onBurst.Invoke();
            s.Release(B);
            Finish(StalkerRunnerResult.Done);
        }

        void SetParts(bool open)
        {
            if (spot == null || spot.DoorParts == null) return;
            for (int i = 0; i < spot.DoorParts.Length; i++)
                if (spot.DoorParts[i] != null) StalkerWorldBridge.SetOpen(spot.DoorParts[i], open);
        }

        // ---------- corpo e cérebro ----------
        void FreezeForTravel()
        {
            StalkerBrain b = B;
            NavMeshAgent ag = Agent;
            b.Stop();
            if (disableAgent)
            {
                b.Freeze(true, true);
                if (ag != null && ag.enabled) ag.enabled = false;
            }
            else
            {
                b.Freeze(true, false);
                if (ag != null)
                {
                    savedUpdatePos = ag.updatePosition;
                    savedUpdateRot = ag.updateRotation;
                    ag.updatePosition = false;
                    ag.updateRotation = false;
                }
            }
            frozenByMe = true;
        }

        // Alguém (fim de uma cutscene, por exemplo) pode descongelar todos os monstros: congela de novo.
        void KeepFrozen()
        {
            if (frozenByMe && B != null && !B.Frozen) B.Freeze(true, disableAgent);
        }

        void ReleaseFromTravel(Vector3 pos, Quaternion rot)
        {
            StalkerBrain b = B;
            NavMeshAgent ag = Agent;
            transform.SetPositionAndRotation(pos, rot);
            if (ag != null)
            {
                if (!disableAgent)
                {
                    ag.updatePosition = savedUpdatePos;
                    ag.updateRotation = savedUpdateRot;
                }
                if (!ag.enabled) ag.enabled = true;
            }
            if (b != null)
            {
                b.Warp(pos, rot);
                b.Freeze(false, false);
            }
            frozenByMe = false;
        }

        void EndVisuals()
        {
            StalkerWorldBridge.ShowBody(hidden);
            StalkerBrain b = B;
            if (b == null) return;
            if (route != null)
            {
                string ab = route.AnimBoolName;
                if (!string.IsNullOrEmpty(ab)) StalkerWorldBridge.AnimBool(b, ab, false);
            }
            if (spot != null) StalkerWorldBridge.AnimBool(b, StalkerWorldAnim.Hidden, false);
        }

        Vector3 SnapPosition()
        {
            switch (state)
            {
                case StalkerRunnerState.Enter:
                    return entryNav;
                case StalkerRunnerState.Traverse:
                    return route != null && travelled >= route.Length * 0.5f ? exitNav : entryNav;
                case StalkerRunnerState.Exit:
                    return exitNav;
                case StalkerRunnerState.AmbushEnter:
                    return spotApproachNav;
                default:
                    return spotExitNav;
            }
        }

        void ReleaseReservations()
        {
            if (route != null) route.Release(B);
            if (spot != null) spot.Release(B);
        }

        void Finish(StalkerRunnerResult result)
        {
            state = StalkerRunnerState.Idle;
            LastResult = result;
            cooldownUntil = Time.time + (result == StalkerRunnerResult.Done ? 0.5f : autoCooldown);
            route = null;
            spot = null;
            onEnd.Invoke();
        }

        // ---------- reflexo automático ----------
        void AutoScan(float dt)
        {
            if (!autoUse) return;
            scanTimer -= dt;
            if (scanTimer > 0f) return;
            scanTimer = Mathf.Max(0.1f, scanInterval);
            if (Time.time < cooldownUntil) return;
            StalkerBrain b = B;
            NavMeshAgent ag = Agent;
            if (b == null || ag == null || b.Frozen || !ag.enabled || !ag.isOnNavMesh) return;
            if (StalkerWorldBridge.InteractorBusy(b)) return;
            if (ag.pathPending || !ag.hasPath || ag.pathStatus != NavMeshPathStatus.PathPartial) return;
            Vector3 dest = ag.destination;
            if (Vector3.Distance(ag.pathEndPosition, dest) < autoMinGap) return;
            StalkerRoutePlan plan = StalkerRoutePlanner.Plan(b, dest, true, -1, "", autoRadius, StalkerRoutePick.TowardGoal, false);
            if (!plan.valid)
            {
                cooldownUntil = Time.time + 2f;
                return;
            }
            Log("o destino só se alcança por uma rota");
            Begin(plan.route, plan.forward);
        }

        // ---------- ciclo de vida ----------
        void OnDisable()
        {
            if (state == StalkerRunnerState.Idle && !frozenByMe) return;
            // monstro desativado no meio (troca de cena, Dismiss): devolve tudo ao normal sem chamar o cérebro por cima
            EndVisuals();
            if (frozenByMe)
            {
                Vector3 pos = SnapPosition();
                transform.position = pos;
                NavMeshAgent ag = Agent;
                if (ag != null)
                {
                    if (!disableAgent)
                    {
                        ag.updatePosition = savedUpdatePos;
                        ag.updateRotation = savedUpdateRot;
                    }
                    ag.enabled = true;
                }
                try { if (B != null) B.Freeze(false, false); }
                catch (System.Exception) { }
                frozenByMe = false;
            }
            ReleaseReservations();
            state = StalkerRunnerState.Idle;
            route = null;
            spot = null;
        }

        void OnValidate()
        {
            scanInterval = Mathf.Max(0.1f, scanInterval);
            arriveDistance = Mathf.Max(0.2f, arriveDistance);
            approachTimeout = Mathf.Max(3f, approachTimeout);
            turnSpeed = Mathf.Max(30f, turnSpeed);
        }

        void OnDrawGizmosSelected()
        {
            if (state == StalkerRunnerState.Approach)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.3f);
                Gizmos.DrawLine(transform.position, entryNav);
                Gizmos.DrawWireSphere(entryNav, 0.3f);
            }
            else if (state == StalkerRunnerState.AmbushApproach)
            {
                Gizmos.color = new Color(0.8f, 0.2f, 1f);
                Gizmos.DrawLine(transform.position, spotApproachNav);
                Gizmos.DrawWireSphere(spotApproachNav, 0.3f);
            }
        }

        // ---------- utilidades ----------
        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        void Turn(Quaternion target, float dt)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
        }

        void Log(string message)
        {
            if (!debugLog) return;
            Debug.Log("[Stalker] Rotas (" + (brain != null ? brain.Id : name) + "): " + message, this);
        }
    }
}
