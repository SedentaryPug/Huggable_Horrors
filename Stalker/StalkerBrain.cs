using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorFPS
{
    /// <summary>
    /// O cérebro do perseguidor: junta os sentidos, o quadro de conhecimento, a atenção e a árvore de comportamento.
    /// Fluxo por quadro: sentidos -> (o que ele notou/ouviu vira pista ou atenção) -> atenção -> envelhece as pistas -> árvore.
    /// A árvore (StalkerActions) só lê o quadro de conhecimento, nunca os jogadores. Precisa de NavMesh.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Stalker Brain")]
    [RequireComponent(typeof(NavMeshAgent))]
    public class StalkerBrain : MonoBehaviour
    {
        [Tooltip("Vazio = o perfil da Definição, ou os números padrão (StalkerProfile.Default). Create > HorrorFPS > Stalker Profile para ajustar.")]
        public StalkerProfile profile;
        [Tooltip("Mostra um painel de debug na tela (modo, atenção, pista, memória).")]
        public bool showHud;

        [Header("Árvore de comportamento")]
        [Tooltip("Vazio = a árvore padrão do código (tudo como sempre foi). Com um asset: a árvore montada na janela Tools > Horror FPS > Stalker > Stalker Tree Editor. Se este campo estiver vazio, vale a da Definição.")]
        public StalkerTreeAsset tree;

        [Header("Identidade (vários perseguidores e troca de cena)")]
        [Tooltip("Quem este perseguidor é para o sistema entre cenas (seguir, ser invocado, recuar). Opcional se ele vive só na própria cena.")]
        public StalkerDefinition definition;
        [Tooltip("Sem Definição: o id deste perseguidor (vazio = nome do objeto). Cada perseguidor precisa de um id único.")]
        public string id;
        [Tooltip("Perseguidores do mesmo grupo trocam pistas (precisa de Share Radius > 0 no perfil). Vazio = o grupo da Definição.")]
        public string group;
        [Tooltip("Mesma chave = mesma memória adaptativa (eles aprendem juntos). Vazio = memória só deste perseguidor.")]
        public string memoryGroup;

        public event System.Action<StalkerMode, StalkerMode> ModeChanged;
        public event System.Action<StalkerAttention, StalkerAttention> AttentionChanged;
        public event System.Action<StalkerTarget> Spotted;       // notou um jogador (bom para música/susto)
        public event System.Action<StalkerHint> HintReceived;    // recebeu uma pista (bom para um "tell" para o jogador)

        public StalkerProfile Profile
        {
            get
            {
                if (profile != null) return profile;
                if (definition != null && definition.profile != null) return definition.profile;
                return StalkerProfile.Default;
            }
        }

        /// <summary>Id único (da Definição, do campo id ou do nome do objeto).</summary>
        public string Id
        {
            get
            {
                if (definition != null && !string.IsNullOrEmpty(definition.id)) return definition.id;
                if (string.IsNullOrEmpty(id)) id = name.Replace("(Clone)", "").Trim();
                return id;
            }
        }

        public string Group { get { return !string.IsNullOrEmpty(group) ? group : (definition != null ? definition.group : ""); } }

        public string MemoryKey
        {
            get
            {
                if (!string.IsNullOrEmpty(memoryGroup)) return memoryGroup;
                if (definition != null && !string.IsNullOrEmpty(definition.memoryGroup)) return definition.memoryGroup;
                return Id;
            }
        }

        /// <summary>A memória adaptativa deste perseguidor (ou do grupo dele). Resolve sozinha se o id mudar depois do Awake.</summary>
        public StalkerMemory Memory
        {
            get
            {
                string key = MemoryKey;
                if (memory == null || memoryKey != key)
                {
                    memoryKey = key;
                    memory = StalkerMemory.Of(key);
                    memory.SkillMaxEvents = Profile.skillMaxEvents;
                    memory.Sync();
                }
                return memory;
            }
        }

        public AttentionTuning Tune { get; private set; }
        public float Skill { get { return Memory.Skill01; } }
        public StalkerSenses Senses { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public StalkerModules Modules { get; private set; }

        /// <summary>A árvore editável em uso: a deste cérebro ou, se não houver, a da Definição. null = a árvore padrão do código.</summary>
        public StalkerTreeAsset Tree
        {
            get
            {
                if (tree != null) return tree;
                return definition != null ? definition.tree : null;
            }
        }

        /// <summary>Variáveis soltas para os nós da árvore conversarem entre si ("rugiu", "fase"...). Zera ao reativar; não vai para o save.</summary>
        public readonly StalkerBlackboard Blackboard = new StalkerBlackboard();

        /// <summary>Estado de execução de cada nó da árvore editável (para a janela do Editor). null sem árvore editável ou fora do Editor/Development Build.</summary>
        public StalkerTreeTrace TreeTrace { get; private set; }

        /// <summary>O "lar" do perseguidor (centro da ronda sem PatrolPoint).</summary>
        public Vector3 Home { get { return actions != null ? actions.Home : transform.position; } }

        /// <summary>Parado por uma cutscene própria (ver Freeze). Enquanto durar, o cérebro não pensa nem se move.</summary>
        public bool Frozen { get; private set; }

        public float SecondsSinceStimulus { get { return Time.time - lastStimulus; } }

        public readonly StalkerKnowledge Knowledge = new StalkerKnowledge();
        public readonly StalkerDirectorState dir = new StalkerDirectorState();
        [System.NonSerialized] public StalkerTarget pressureTarget;
        [System.NonSerialized] public float Attention01;
        [System.NonSerialized] public StalkerAttention Level;

        StalkerMode mode;
        public StalkerMode Mode
        {
            get { return mode; }
            set
            {
                if (mode == value) return;
                StalkerMode old = mode;
                mode = value;
                bool wasChasing = old == StalkerMode.Chase || old == StalkerMode.Attack;
                bool isChasing = value == StalkerMode.Chase || value == StalkerMode.Attack;
                if (wasChasing && !isChasing) OnChaseEnded();
                if (ModeChanged != null) ModeChanged(old, value);
                if (Modules != null) Modules.ModeChanged(old, value);
            }
        }

        StalkerActions actions;
        BTNode root;
        StalkerDirector director;
        float lastStimulus, floorUntil, nextThreat, lastDestSet, senseAcc;
        Vector3 lastDest;
        bool ready, held;
        StalkerMemory memory;
        string memoryKey, threatKey;

        /// <summary>Os ramos da árvore desta instância, por prioridade (debug).</summary>
        public string TreeSummary { get; private set; }

        // ------------------------------------------------------------------ ciclo de vida
        void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Senses = GetComponent<StalkerSenses>();
            if (Senses == null) Senses = gameObject.AddComponent<StalkerSenses>();
            Tune = Profile.Get(StalkerAttention.Calm);
            Modules = new StalkerModules(this);
            Modules.Collect();                          // os módulos se ligam ao cérebro aqui (use OnAttach, não o Awake deles)
            actions = new StalkerActions(this);
            BuildTree();
        }

        /// <summary>
        /// Monta a árvore: primeiro o "estoque" de ramos nomeados (os de série + o que cada módulo acrescenta, troca ou remove);
        /// depois, com um StalkerTreeAsset, a árvore vem do asset (os ramos do estoque entram pelo nó "Ramo padrão");
        /// sem asset (ou se ele estiver vazio) vale a árvore padrão do código, ordenada por prioridade.
        /// </summary>
        void BuildTree()
        {
            StalkerTreeBuilder pool = new StalkerTreeBuilder();
            actions.Populate(pool);                     // ramos padrão...
            Modules.BuildBranches(pool);                // ...e o que cada traço acrescenta, troca ou remove

            BTNode built = null;
            string summary = null;
            TreeTrace = null;
            StalkerTreeAsset asset = Tree;
            if (asset != null)
            {
                StalkerTreeAsset.BuildResult r = asset.Build(this, pool);
                for (int i = 0; i < r.warnings.Count; i++)
                    Debug.LogWarning("[Stalker] '" + name + "', árvore '" + asset.name + "': " + r.warnings[i], asset);
                built = r.root;
                summary = r.summary;
                TreeTrace = r.trace;
                if (built == null)
                    Debug.LogWarning("[Stalker] A árvore '" + asset.name + "' não gerou comportamento nenhum (vazia ou toda desligada): '" + name + "' usa a árvore padrão do código.", asset);
            }
            if (built == null)
            {
                built = pool.Build();
                summary = pool.Describe();
                TreeTrace = null;
            }
            root = built;
            TreeSummary = summary;
        }

        /// <summary>Troca a árvore em jogo (ex.: outra fase do chefe). null = volta à da Definição ou à padrão do código.</summary>
        public void SetTree(StalkerTreeAsset asset)
        {
            tree = asset;
            RebuildTree();
        }

        /// <summary>Monta a árvore de novo (depois de editar o asset em Play). Zera o estado interno dos nós; a memória e o conhecimento ficam.</summary>
        public void RebuildTree()
        {
            if (actions == null) return;        // o Awake ainda não rodou: ele monta sozinho
            if (root != null) root.Abort();
            BuildTree();
        }

        void OnEnable()
        {
            Memory.Sync();                              // traz a memória da partida (cria a deste perseguidor/grupo)
            Senses.NoiseHeard += OnNoise;
            Attention01 = 0f; Level = StalkerAttention.Calm; mode = StalkerMode.Patrol;
            Knowledge.Clear(); Knowledge.LoseVisual();
            Blackboard.Clear();
            held = false; senseAcc = 0f;
            ready = EnsureOnNavMesh();
            Agent.avoidancePriority = 30 + Mathf.Abs(Id.GetHashCode() % 40);    // com vários, cada um cede a passagem de um jeito
            if (ready) { director = StalkerDirector.Instance; director.Register(this); }
            StalkerManager.Register(this);
        }

        void OnDisable()
        {
            if (Senses != null) Senses.NoiseHeard -= OnNoise;
            if (director != null) director.Unregister(this);
            director = null;
            StalkerManager.Unregister(this);
            if (root != null) root.Abort();
            SetAllThreat(0f);
            Memory.Flush(true);
        }

        bool EnsureOnNavMesh()
        {
            if (Agent.isOnNavMesh) return true;
            NavMeshHit h;
            if (NavMesh.SamplePosition(transform.position, out h, 4f, NavMesh.AllAreas)) { Agent.Warp(h.position); return Agent.isOnNavMesh; }
            return false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Frozen || StalkerManager.Cinematic)     // cutscene própria ou de outro perseguidor: todos prendem a respiração
            {
                if (!held) { held = true; EnterHold(); }
                return;
            }
            if (held) { held = false; ExitHold(); }

            if (!ready || !Agent.isOnNavMesh)
            {
                ready = EnsureOnNavMesh();       // o NavMesh pode chegar depois (cena aditiva)
                if (!ready) { StalkerBridge.WarnOnce("'" + name + "' não está sobre um NavMesh. Faça o bake (NavMeshSurface ou Window > AI > Navigation)."); return; }
                if (director == null) { director = StalkerDirector.Instance; director.Register(this); }
            }
            if (director == null) { director = StalkerDirector.Instance; director.Register(this); }

            senseAcc += dt;
            if (senseAcc >= Profile.senseInterval)      // a visão (raios) roda em intervalo: com vários perseguidores poupa CPU
            {
                Senses.Tick(senseAcc);
                ProcessSenses(senseAcc);
                senseAcc = 0f;
            }
            UpdateAttention(dt);
            Knowledge.Tick(dt, Profile);
            AttentionTuning tuning = Profile.Get(Level);
            Modules.ModifyTuning(ref tuning);           // traços exclusivos ajustam os números do nível atual
            Tune = tuning;
            Modules.Tick(dt);
            root.Tick();
            UpdateThreat();
        }

        void EnterHold()
        {
            if (root != null) root.Abort();
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh) { Agent.isStopped = true; Agent.ResetPath(); }
            lastDest = new Vector3(1e9f, 1e9f, 1e9f);
        }

        void ExitHold()
        {
            if (Agent == null || !Agent.enabled) return;
            if (!Agent.isOnNavMesh) ready = EnsureOnNavMesh();
            if (Agent.isOnNavMesh) Agent.isStopped = false;
        }

        // ------------------------------------------------------------------ API para roteiro
        /// <summary>Força a atenção (0..1), ex.: cutscene em que ele "acorda".</summary>
        public void ForceAttention(float value)
        {
            Attention01 = Mathf.Clamp01(value);
            lastStimulus = Time.time;
        }

        public void Stimulate(float amount)
        {
            if (amount <= 0f) return;
            Attention01 = Mathf.Clamp01(Attention01 + amount * (1f + 0.25f * Skill));
            lastStimulus = Time.time;
        }

        /// <summary>Entrega uma pista (usado pelo Diretor). Devolve false se foi descartada.</summary>
        public bool ReceiveHint(StalkerHint h)
        {
            if (!Knowledge.Give(h, Time.time)) return false;
            if (h.source == HintSource.DirectorPressure && pressureTarget != null) Knowledge.focus = pressureTarget;
            Stimulate(Profile.gainHint);
            if (HintReceived != null) HintReceived(h);
            Modules.Hint(h);
            return true;
        }

        /// <summary>Um colega do mesmo grupo notou alguém (usado pelo Diretor). Vira uma pista de confiança média.</summary>
        public bool ReceiveShared(StalkerTarget t, Vector3 pos, float confidence)
        {
            StalkerHint h = new StalkerHint();
            h.position = pos; h.origin = pos; h.radius = 4f; h.confidence = confidence; h.source = HintSource.Shared;
            if (!Knowledge.Give(h, Time.time)) return false;
            if (t != null && !Knowledge.hasVisual) Knowledge.focus = t;
            Stimulate(Profile.gainHint * 2f);
            if (HintReceived != null) HintReceived(h);
            Modules.Hint(h);
            return true;
        }

        /// <summary>
        /// Para o perseguidor por uma cutscene própria (finalizador, evento de roteiro). O cérebro não pensa nem se move até
        /// Freeze(false). disableAgent desliga o NavMeshAgent para a cutscene poder mover o objeto livremente.
        /// </summary>
        public void Freeze(bool frozen, bool disableAgent)
        {
            if (Frozen == frozen) return;
            Frozen = frozen;
            if (frozen)
            {
                if (disableAgent && Agent != null && Agent.enabled)
                {
                    if (Agent.isOnNavMesh) Agent.ResetPath();
                    Agent.enabled = false;
                }
            }
            else if (Agent != null && !Agent.enabled)
            {
                Agent.enabled = true;
                ready = EnsureOnNavMesh();
            }
        }

        /// <summary>Leva o perseguidor para um ponto do NavMesh (chegada, cutscene). Devolve false se não achou NavMesh perto.</summary>
        public bool Warp(Vector3 pos, Quaternion rot)
        {
            NavMeshHit h;
            Vector3 p = NavMesh.SamplePosition(pos, out h, 6f, NavMesh.AllAreas) ? h.position : pos;
            transform.rotation = rot;
            if (Agent.enabled) { Agent.Warp(p); ready = Agent.isOnNavMesh; }
            else { transform.position = p; ready = false; }
            lastDest = new Vector3(1e9f, 1e9f, 1e9f);
            if (actions != null) actions.SetHome(p);
            return ready;
        }

        /// <summary>
        /// Entra em cena vindo de outra cena (ou invocado): já vem atento e, se houver pista, sabe aproximadamente
        /// para onde ir. Motivo: "follow", "time", "noise" ou "script".
        /// </summary>
        public void Arrive(string reason, bool hasLead, Vector3 leadPos, float leadRadius, float attention)
        {
            Knowledge.Clear();
            Knowledge.LoseVisual();
            if (hasLead)
            {
                StalkerHint h = new StalkerHint();
                h.position = leadPos; h.origin = leadPos; h.radius = leadRadius; h.confidence = 0.8f; h.source = HintSource.Ping;
                Knowledge.Give(h, Time.time);
            }
            ForceAttention(attention);
            Modules.Arrived(reason);
        }

        /// <summary>O módulo (ou a ação de ataque) consulta os traços antes de aplicar o golpe.</summary>
        public AttackDecision DecideAttack(StalkerTarget t, ref float damage) { return Modules.DecideAttack(t, ref damage); }

        /// <summary>Os traços podem mudar o quanto ele enxerga um alvo (cego à luz, visão noturna...).</summary>
        public float ModifyVisibility(StalkerTarget t, float visibility) { return Modules.ModifyVisibility(t, visibility); }

        // ------------------------------------------------------------------ sentidos -> conhecimento
        void ProcessSenses(float dt)
        {
            StalkerProfile p = Profile;
            StalkerTarget best = Senses.Best;
            if (best != null)
            {
                Vector3 pos = Senses.LastVisiblePos(best);
                if (!Knowledge.hasVisual) OnSpotted(best, pos);
                Knowledge.SetVisual(best, pos);
                Stimulate(p.gainSeenPerSecond * dt);
                Memory.AddHeat(pos, dt * 0.5f);
            }
            else if (Knowledge.hasVisual) OnLostSight();

            StalkerTarget g = Senses.GlimpseTarget;
            if (g != null && best == null)
            {
                Vector3 gp = Senses.LastVisiblePos(g);
                StalkerHint h = new StalkerHint();
                h.position = gp; h.origin = gp; h.radius = 3.5f; h.confidence = 0.55f; h.source = HintSource.Glimpse;
                Knowledge.focus = g;
                if (Knowledge.Give(h, Time.time)) Stimulate(p.gainGlimpse);
            }
        }

        void OnSpotted(StalkerTarget t, Vector3 pos)
        {
            StalkerProfile p = Profile;
            Stimulate(p.gainSpotted);
            // aprendizado: o barulho que o trouxe até aqui levou a um jogador, então não era (só) isca
            if (!string.IsNullOrEmpty(Knowledge.leadKey) && !Knowledge.leadPlayerMade
                && Time.time - Knowledge.leadTime < p.baitHitWindow
                && Vector3.Distance(Knowledge.leadOrigin, pos) < p.baitHitRadius)
                Memory.NoteBaitHit(Knowledge.leadKey, p.baitSuspicionOnHit);
            Knowledge.Clear();
            StalkerBridge.FrightNear(transform.position + Vector3.up, 25f, 0.7f);
            if (Spotted != null) Spotted(t);
            Modules.Spotted(t);
            if (p.shareRadius > 0f && director != null) director.Share(this, t, pos);     // avisa os colegas do grupo
        }

        void OnLostSight()
        {
            StalkerTarget t = Knowledge.focus;
            Knowledge.LoseVisual();
            if (t == null || !t.Valid) return;
            Modules.LostSight(t);
            if (Knowledge.SuspectsHiding) return;     // viu o jogador entrar num esconderijo: mantém essa pista em vez da última posição

            // antecipa para onde o jogador ia (quanto mais atento, mais longe ele aposta)
            Vector3 pos = Knowledge.visualPos;
            Vector3 vel = new Vector3(t.velocity.x, 0f, t.velocity.z);
            float ahead = Mathf.Min(vel.magnitude * Tune.predictSeconds, 7f);
            if (ahead > 0.5f) pos += vel.normalized * ahead;
            NavMeshHit nh;
            if (NavMesh.SamplePosition(pos, out nh, 6f, NavMesh.AllAreas)) pos = nh.position;

            StalkerHint h = new StalkerHint();
            h.position = pos; h.origin = Knowledge.visualPos; h.radius = 3f; h.confidence = 1f; h.source = HintSource.Seen;
            Knowledge.Give(h, Time.time);
        }

        void OnNoise(StalkerNoise n)
        {
            if (Frozen || StalkerManager.Cinematic) return;      // em cutscene ninguém reage a barulho
            if (!Modules.FilterNoise(ref n)) return;             // traços exclusivos (surdo, só ouve passos...)
            StalkerProfile p = Profile;
            if (!n.playerMade && n.tagMode != NoiseTagMode.AlwaysInvestigate)
            {
                if (n.tagMode == NoiseTagMode.NeverInvestigate) return;
                // som conhecido de isca: quanto mais atento, mais ele "sabe" e ignora
                float suspicion = Memory.BaitSuspicion(n.key);
                if (suspicion > 0f && Random.value < Mathf.Clamp01(suspicion * Tune.baitDiscernment))
                {
                    Memory.NoteIgnored(n.key);
                    return;
                }
            }
            if (Knowledge.hasVisual) { Stimulate(n.loudness * p.gainHeard * 0.3f); return; }

            Stimulate(n.loudness * p.gainHeard);
            StalkerHint h = new StalkerHint();
            h.position = n.position; h.origin = n.position;
            h.radius = Mathf.Lerp(8f, 2f, Mathf.Clamp01(n.loudness));
            h.confidence = Mathf.Clamp01(0.45f + 0.45f * n.loudness);
            h.source = HintSource.Heard; h.key = n.key; h.playerMade = n.playerMade;
            if (n.target != null) { Knowledge.focus = n.target; Memory.AddHeat(n.position, 0.5f); }
            if (Knowledge.Give(h, Time.time)) Modules.Hint(h);
        }

        void OnChaseEnded()
        {
            StalkerTarget t = Knowledge.focus;
            bool caught = t != null && t.Valid && !t.alive;
            Memory.NoteChaseEnded(caught);
            Memory.AddExperience(Profile.skillPerEvent);
            dir.reliefUntil = Time.time + Profile.reliefSeconds;
            floorUntil = Time.time + Profile.afterChaseFloorSeconds;
        }

        // ------------------------------------------------------------------ atenção
        void UpdateAttention(float dt)
        {
            StalkerProfile p = Profile;
            bool engaged = mode == StalkerMode.Chase || mode == StalkerMode.Attack;
            if (engaged) Attention01 = Mathf.Max(Attention01, p.huntingAt);
            else if (Time.time - lastStimulus > p.decayHoldSeconds)
            {
                float rate = p.decayPerSecond * ((mode == StalkerMode.Search || mode == StalkerMode.CheckHiding) ? 0.4f : 1f);
                Attention01 = Mathf.Max(0f, Attention01 - rate * dt);
            }
            if (Time.time < floorUntil) Attention01 = Mathf.Max(Attention01, p.afterChaseFloor);

            StalkerAttention old = Level;
            int lvl = (int)Level;
            while (lvl < 3 && Attention01 >= Threshold(lvl + 1, p)) lvl++;
            while (lvl > 0 && Attention01 < Threshold(lvl, p) - p.hysteresis) lvl--;
            Level = (StalkerAttention)lvl;
            if (Level != old)
            {
                if (AttentionChanged != null) AttentionChanged(old, Level);
                Modules.AttentionChanged(old, Level);
            }
        }

        static float Threshold(int level, StalkerProfile p)
        {
            return level == 1 ? p.alertAt : (level == 2 ? p.huntingAt : p.frenzyAt);
        }

        // ------------------------------------------------------------------ o jogador sente o monstro (coração)
        void UpdateThreat()
        {
            if (Time.time < nextThreat) return;
            nextThreat = Time.time + 0.25f;
            List<StalkerTarget> list = StalkerBridge.Players;
            for (int i = 0; i < list.Count; i++)
            {
                StalkerTarget t = list[i];
                if (!t.Valid || !t.alive) continue;
                float d = Vector3.Distance(transform.position, t.Position);
                float level = 0f;
                if (Knowledge.hasVisual && Knowledge.focus == t) level = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(d / 25f));
                else if (Level >= StalkerAttention.Hunting && d < 14f) level = 0.25f * (1f - d / 14f);
                StalkerBridge.SetThreat(t, level, ThreatKey);
            }
        }

        void SetAllThreat(float level)
        {
            List<StalkerTarget> list = StalkerBridge.Players;
            for (int i = 0; i < list.Count; i++) if (list[i].Valid) StalkerBridge.SetThreat(list[i], level, ThreatKey);
        }

        /// <summary>Cada perseguidor é uma fonte de ameaça própria no coração do jogador (senão um apagaria o outro).</summary>
        string ThreatKey
        {
            get
            {
                string wanted = "perseguidor:" + Id;
                if (threatKey != wanted) threatKey = wanted;
                return threatKey;
            }
        }

        // ------------------------------------------------------------------ movimento (usado pelas ações)
        public void MoveTo(Vector3 dest, float speed)
        {
            Agent.speed = speed;
            if (Agent.isStopped) Agent.isStopped = false;
            if (Time.time - lastDestSet > 0.15f && ((dest - lastDest).sqrMagnitude > 0.04f || !Agent.hasPath))
            {
                Agent.SetDestination(dest);
                lastDest = dest;
                lastDestSet = Time.time;
            }
        }

        public void Stop()
        {
            if (!Agent.isOnNavMesh) return;
            Agent.isStopped = true;
            Agent.ResetPath();
            lastDest = new Vector3(1e9f, 1e9f, 1e9f);
        }

        public bool Arrived(float dist)
        {
            if (Agent.pathPending) return false;
            if (!Agent.hasPath) return (transform.position - lastDest).sqrMagnitude <= dist * dist;
            if (Agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
            return Agent.remainingDistance <= Mathf.Max(dist, Agent.stoppingDistance) + 0.05f;
        }

        public void FaceToward(Vector3 p, float degPerSecond)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degPerSecond * Time.deltaTime);
        }

        // ------------------------------------------------------------------ debug
        void OnDrawGizmosSelected()
        {
            StalkerProfile p = Profile;
            if (!p.drawGizmos) return;
            AttentionTuning t = Application.isPlaying ? Tune : p.calm;
            Vector3 eye = Senses != null ? Senses.EyePos : transform.position + Vector3.up * 1.6f;
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.6f);
            Gizmos.DrawRay(eye, Quaternion.Euler(0f, -t.visionFov * 0.5f, 0f) * transform.forward * t.visionRange);
            Gizmos.DrawRay(eye, Quaternion.Euler(0f, t.visionFov * 0.5f, 0f) * transform.forward * t.visionRange);
            Gizmos.DrawWireSphere(transform.position, p.nearSenseRange);
            if (Application.isPlaying && Knowledge.hasLead)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                Gizmos.DrawWireSphere(Knowledge.leadPos, Mathf.Max(0.3f, Knowledge.leadRadius));
                Gizmos.DrawLine(transform.position + Vector3.up, Knowledge.leadPos);
            }
        }

        void OnGUI()
        {
            if (!showHud) return;
            string s = Id + ": " + (Frozen ? "CONGELADO" : mode.ToString()) + "   atenção " + Level + " (" + Attention01.ToString("0.00") + ")"
                     + "\nvisual: " + Knowledge.hasVisual
                     + "   pista: " + (Knowledge.hasLead ? Knowledge.leadSource + " conf " + Knowledge.leadConfidence.ToString("0.00") + " raio " + Knowledge.leadRadius.ToString("0") + "m" : "-")
                     + "\n" + Memory.Summary();
            GUI.Label(new Rect(10f, 10f + 118f * StalkerManager.HudIndex(this), 520f, 118f), s);
        }
    }
}
