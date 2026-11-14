using System;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorFPS
{
    // =====================================================================================================
    //  Nós de árvore do pacote "rotas e cenário". Aparecem nos menus da janela da árvore sob "Mundo/...".
    //  Falam com o cenário só por StalkerWorldBridge / StalkerSceneryCache e com as rotas pelo StalkerRouteRunner.
    // =====================================================================================================

    public enum StalkerGoalKind
    {
        [Tooltip("Onde viu alguém agora; senão a pista. Sem nenhum dos dois, o nó falha.")] Auto = 0,
        [Tooltip("O último ponto onde viu alguém.")] Visual = 1,
        [Tooltip("A pista (onde acha que o jogador está).")] Lead = 2,
        [Tooltip("A casa dele.")] Home = 3
    }

    public enum StalkerLightsMode { TurnOff = 0, TurnOn = 1, Toggle = 2 }

    public enum StalkerLightState { Lit = 0, Unlit = 1, Any = 2 }

    public enum StalkerActPhase { Idle, Approach, Align, Act, Recover, Done, Failed }

    /// <summary>
    /// Sequência "chegar → virar → animar → EFEITO → recuperar" para os nós. É uma struct (cada monstro tem a sua cópia
    /// dentro do nó, sem depender de OnBuild). O efeito é avisado uma vez, no instante certo, por <c>effect</c>.
    /// </summary>
    public struct StalkerActSeq
    {
        const float ArriveDist = 0.9f;

        StalkerActPhase phase;
        Vector3 stand, face;
        string trigger;
        float effectDelay, duration, recover, timeout;
        float elapsed, actTime, phaseTime;
        bool effectFired;

        public StalkerActPhase Phase { get { return phase; } }

        public void Begin(Vector3 standPos, Vector3 facePos, string animTrigger, float effectAt, float totalDuration, float recoverSeconds, float timeoutSeconds)
        {
            phase = StalkerActPhase.Approach;
            stand = standPos;
            face = facePos;
            trigger = animTrigger;
            effectDelay = Mathf.Max(0f, effectAt);
            duration = Mathf.Max(effectDelay, totalDuration);
            recover = Mathf.Max(0f, recoverSeconds);
            timeout = Mathf.Max(1f, timeoutSeconds);
            elapsed = 0f;
            actTime = 0f;
            phaseTime = 0f;
            effectFired = false;
        }

        public StalkerActPhase Tick(StalkerBrain b, float dt, out bool effect)
        {
            effect = false;
            if (phase == StalkerActPhase.Idle || phase == StalkerActPhase.Done || phase == StalkerActPhase.Failed) return phase;
            elapsed += dt;
            Vector3 pos = b.transform.position;
            switch (phase)
            {
                case StalkerActPhase.Approach:
                {
                    if (elapsed > timeout) { phase = StalkerActPhase.Failed; break; }
                    if (StalkerWorldBridge.HorizontalDistance(pos, stand) <= ArriveDist)
                    {
                        b.Stop();
                        phase = StalkerActPhase.Align;
                        phaseTime = 0f;
                        break;
                    }
                    b.MoveTo(stand, b.Tune.walkSpeed);
                    NavMeshAgent ag = b.Agent;
                    if (ag != null && ag.enabled && ag.isOnNavMesh && !ag.pathPending && elapsed > 1.5f && ag.pathStatus != NavMeshPathStatus.PathComplete)
                        phase = StalkerActPhase.Failed;
                    break;
                }
                case StalkerActPhase.Align:
                {
                    phaseTime += dt;
                    b.Stop();
                    b.FaceToward(face, 300f);
                    Vector3 to = StalkerWorldBridge.Flat(face - pos);
                    Vector3 fw = StalkerWorldBridge.Flat(b.transform.forward);
                    float ang = (to.sqrMagnitude < 0.01f || fw.sqrMagnitude < 0.01f) ? 0f : Vector3.Angle(fw, to);
                    if (ang < 12f || phaseTime > 1f)
                    {
                        phase = StalkerActPhase.Act;
                        actTime = 0f;
                        if (!string.IsNullOrEmpty(trigger)) StalkerWorldBridge.AnimTrigger(b, trigger);
                    }
                    break;
                }
                case StalkerActPhase.Act:
                {
                    actTime += dt;
                    b.Stop();
                    b.FaceToward(face, 300f);
                    if (!effectFired && actTime >= effectDelay)
                    {
                        effectFired = true;
                        effect = true;
                    }
                    if (actTime >= duration)
                    {
                        if (!effectFired)
                        {
                            effectFired = true;
                            effect = true;
                        }
                        phase = StalkerActPhase.Recover;
                        phaseTime = 0f;
                    }
                    break;
                }
                case StalkerActPhase.Recover:
                {
                    phaseTime += dt;
                    b.Stop();
                    if (phaseTime >= recover) phase = StalkerActPhase.Done;
                    break;
                }
            }
            return phase;
        }

        public void Cancel(StalkerBrain b)
        {
            if (phase == StalkerActPhase.Act || phase == StalkerActPhase.Align)
            {
                if (b != null) StalkerWorldBridge.InterruptAnim(b);
            }
            phase = StalkerActPhase.Idle;
        }
    }

    /// <summary>Buscas no cenário usadas pelos nós (todas baratas: usam o cache de 1 s do StalkerSceneryCache).</summary>
    public static class StalkerWorldQuery
    {
        public static bool GoalPos(StalkerBrain b, StalkerGoalKind g, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (b == null) return false;
            if (g == StalkerGoalKind.Home)
            {
                pos = b.Home;
                return true;
            }
            if ((g == StalkerGoalKind.Auto || g == StalkerGoalKind.Visual) && b.Knowledge.hasVisual)
            {
                pos = b.Knowledge.visualPos;
                return true;
            }
            if ((g == StalkerGoalKind.Auto || g == StalkerGoalKind.Lead) && b.Knowledge.hasLead)
            {
                pos = b.Knowledge.leadPos;
                return true;
            }
            return false;
        }

        /// <summary>Um ponto no chão a <paramref name="distance"/> m do alvo, do lado em que o monstro está. Sem NavMesh por perto, devolve a posição dele.</summary>
        public static Vector3 StandNear(Vector3 target, Vector3 me, float distance)
        {
            if (StalkerWorldBridge.HorizontalDistance(me, target) <= distance + 0.6f) return me;
            Vector3 to = StalkerWorldBridge.Flat(me - target);
            if (to.sqrMagnitude < 0.01f) to = Vector3.forward;
            Vector3 p = target + to * distance;
            p.y = me.y;
            NavMeshHit h;
            if (NavMesh.SamplePosition(p, out h, 2.5f, NavMesh.AllAreas)) return h.position;
            return me;
        }

        /// <summary>A porta aberta e destrancada mais perto que ficou atrás ou ao lado do monstro.</summary>
        public static Door FindDoorBehind(StalkerBrain b, float radius)
        {
            if (b == null) return null;
            Vector3 me = b.transform.position;
            Vector3 fwd = StalkerWorldBridge.Flat(b.transform.forward);
            Door best = null;
            float bestD = radius;
            Door[] all = StalkerSceneryCache.Doors;
            for (int i = 0; i < all.Length; i++)
            {
                Door d = all[i];
                if (d == null || !d.gameObject.activeInHierarchy) continue;
                if (StalkerWorldBridge.HorizontalDistance(me, d.transform.position) > radius + 2.5f) continue;
                if (!StalkerWorldBridge.IsOpen(d) || StalkerWorldBridge.IsLocked(d)) continue;
                if (StalkerSceneryCache.Age(b, d, "closed") < 8f) continue;
                Vector3 c = StalkerWorldBridge.BoundsOf(d).center;
                if (Mathf.Abs(c.y - me.y) > 2.5f) continue;
                Vector3 to = c - me;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > bestD) continue;
                if (dist > 0.8f && Vector3.Dot(fwd, to / dist) > 0.15f) continue;   // ainda está à frente dele
                best = d;
                bestD = dist;
            }
            return best;
        }

        public static SwitchableLights FindLights(StalkerBrain b, StalkerLightState state, float radius, float playerRadius, float memory, out Vector3 center)
        {
            center = Vector3.zero;
            if (b == null) return null;
            Vector3 me = b.transform.position;
            SwitchableLights best = null;
            float bestD = float.MaxValue;
            SwitchableLights[] all = StalkerSceneryCache.Lights;
            for (int i = 0; i < all.Length; i++)
            {
                SwitchableLights l = all[i];
                if (l == null || !l.gameObject.activeInHierarchy) continue;
                bool lit = StalkerWorldBridge.LightsLit(l);
                if (state == StalkerLightState.Lit && !lit) continue;
                if (state == StalkerLightState.Unlit && lit) continue;
                if (StalkerSceneryCache.Age(b, l, "lights") < memory) continue;
                if (StalkerWorldBridge.HorizontalDistance(me, l.transform.position) > radius + 6f) continue;
                Vector3 c = StalkerWorldBridge.BoundsOf(l).center;
                float h = StalkerWorldBridge.HorizontalDistance(me, c);
                if (h > radius || Mathf.Abs(c.y - me.y) > 5f) continue;
                if (playerRadius > 0f && !StalkerWorldBridge.AnyPlayerWithin(c, playerRadius)) continue;
                if (h < bestD)
                {
                    best = l;
                    bestD = h;
                    center = c;
                }
            }
            return best;
        }

        /// <summary>Onde ele fica para apagar/acender: junto do interruptor mais perto das luzes (até 10 m), senão embaixo delas.</summary>
        public static void LightsStand(StalkerBrain b, Component lights, Vector3 center, out Vector3 stand, out Vector3 face)
        {
            Vector3 target = center;
            float best = 10f;
            LightSwitch[] sw = StalkerSceneryCache.Switches;
            for (int i = 0; i < sw.Length; i++)
            {
                if (sw[i] == null || !sw[i].gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(sw[i].transform.position, center);
                if (d < best)
                {
                    best = d;
                    target = sw[i].transform.position;
                }
            }
            face = target;
            stand = StandNear(target, b.transform.position, 0.9f);
        }

        public static HidingSpot FindHiding(StalkerBrain b, float radius, float memory)
        {
            if (b == null) return null;
            Vector3 me = b.transform.position;
            HidingSpot best = null;
            float bestD = float.MaxValue;
            HidingSpot[] all = StalkerSceneryCache.Hiding;
            for (int i = 0; i < all.Length; i++)
            {
                HidingSpot h = all[i];
                if (h == null || !h.gameObject.activeInHierarchy) continue;
                if (StalkerSceneryCache.Age(b, h, "searched") < memory) continue;
                Vector3 p = h.transform.position;
                float d = StalkerWorldBridge.HorizontalDistance(me, p);
                if (d > radius || Mathf.Abs(p.y - me.y) > 3f) continue;
                if (d < bestD)
                {
                    best = h;
                    bestD = d;
                }
            }
            return best;
        }

        /// <summary>
        /// O melhor ponto de emboscada livre que ele alcança andando, longe dos jogadores. Pontua pelo caminho até lá
        /// MAIS a distância do ponto até a pista (quando há uma): prefere se esconder perto de onde acha que o jogador vai.
        /// </summary>
        public static StalkerAmbushSpot FindAmbush(StalkerBrain b, float radius, string marker, float minPlayerDistance)
        {
            if (b == null) return null;
            Vector3 me = b.transform.position;
            bool hasLead = b.Knowledge.hasLead;
            Vector3 lead = hasLead ? b.Knowledge.leadPos : Vector3.zero;
            StalkerAmbushSpot best = null;
            float bestLen = float.MaxValue;
            System.Collections.Generic.List<StalkerAmbushSpot> all = StalkerAmbushSpot.All;
            for (int i = 0; i < all.Count; i++)
            {
                StalkerAmbushSpot s = all[i];
                if (s == null || !s.Allows(b)) continue;
                if (!string.IsNullOrEmpty(marker) && s.Marker != marker) continue;
                if (StalkerWorldBridge.HorizontalDistance(me, s.HidePos) > radius) continue;
                if (minPlayerDistance > 0f && StalkerWorldBridge.AnyPlayerWithin(s.HidePos, minPlayerDistance)) continue;
                Vector3 nav;
                if (!s.NavApproach(out nav)) continue;
                bool complete;
                float len = StalkerRoutePlanner.PathLength(me, nav, out complete);
                if (!complete) continue;
                float score = len + (hasLead ? StalkerWorldBridge.HorizontalDistance(s.HidePos, lead) : 0f);
                if (score < bestLen)
                {
                    best = s;
                    bestLen = score;
                }
            }
            return best;
        }
    }

    // =====================================================================================================
    //  AÇÕES
    // =====================================================================================================

    [Serializable]
    [StalkerNodeMenu("Mundo/Fechar porta atrás", "Fecha a porta aberta que ele deixou para trás (a menos de N m, atrás ou ao lado). Falha se não há porta, se alguém está no vão ou se ele vê um jogador.")]
    public class StalkerCloseDoorBehindTask : StalkerTask
    {
        [Tooltip("Procura portas abertas até tantos metros dele.")]
        public float radius = 3.5f;
        [Tooltip("Raio do barulho da porta fechando (0 = silenciosa).")]
        public float noise = 8f;
        [Tooltip("Segundos até a mão tocar a porta (o efeito).")]
        public float useDelay = 0.55f;
        [Tooltip("Duração total da animação.")]
        public float duration = 1.1f;
        public float recover = 0.3f;
        [Tooltip("Desiste depois de tantos segundos.")]
        public float timeout = 8f;
        [Tooltip("Falha na hora se ele vir um jogador (quem decide perseguir é a árvore, não este nó).")]
        public bool abortWhenSeen = true;

        StalkerActSeq seq;
        Door door;
        bool failed, effectOk;

        public override string Summary() { return "raio " + radius.ToString("0.#") + " m"; }

        protected override void OnEnter()
        {
            failed = false;
            effectOk = false;
            door = StalkerWorldQuery.FindDoorBehind(Brain, radius);
            if (door == null)
            {
                failed = true;
                return;
            }
            Vector3 c = StalkerWorldBridge.BoundsOf(door).center;
            seq.Begin(StalkerWorldQuery.StandNear(c, Brain.transform.position, 1.3f), c, StalkerWorldAnim.UseDoor, useDelay, duration, recover, timeout);
        }

        protected override BTStatus OnTick()
        {
            if (failed || door == null) return BTStatus.Failure;
            if (abortWhenSeen && Brain.Knowledge.hasVisual)
            {
                seq.Cancel(Brain);
                return BTStatus.Failure;
            }
            bool effect;
            StalkerActPhase ph = seq.Tick(Brain, Time.deltaTime, out effect);
            if (effect)
            {
                Vector3 c = StalkerWorldBridge.BoundsOf(door).center;
                if (!StalkerWorldBridge.AnyPlayerWithin(c, 0.9f) && StalkerWorldBridge.SetOpen(door, false))
                {
                    effectOk = true;
                    StalkerSceneryCache.Mark(Brain, door, "closed");
                    StalkerWorldBridge.Noise(c, noise, Brain.gameObject);
                }
            }
            if (ph == StalkerActPhase.Failed) return BTStatus.Failure;
            if (ph == StalkerActPhase.Done) return effectOk ? BTStatus.Success : BTStatus.Failure;
            return BTStatus.Running;
        }

        protected override void OnAbort() { seq.Cancel(Brain); }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Luzes: apagar, acender ou alternar", "Vai até o interruptor (ou até embaixo das luzes) mais perto e muda um grupo de luzes. Falha se não há luzes no estado certo, ou se ele vir um jogador.")]
    public class StalkerLightsTask : StalkerTask
    {
        public StalkerLightsMode mode = StalkerLightsMode.TurnOff;
        [Tooltip("Procura grupos de luz até tantos metros dele.")]
        public float radius = 14f;
        [Tooltip("Maior que 0: só mexe em luzes a até tantos metros de um jogador vivo (apagar a luz para caçar no escuro).")]
        public float playerRadius = 0f;
        [Tooltip("Segundos antes de poder mexer de novo no mesmo grupo de luzes.")]
        public float memorySeconds = 20f;
        [Tooltip("Raio do clique do interruptor (0 = silencioso).")]
        public float noise = 4f;
        public float useDelay = 0.5f;
        public float duration = 1f;
        public float recover = 0.3f;
        public float timeout = 12f;
        public bool abortWhenSeen = true;

        StalkerActSeq seq;
        SwitchableLights lights;
        Vector3 center;
        bool desired, failed, effectOk;

        public override string Summary() { return mode + ", raio " + radius.ToString("0.#") + " m"; }

        protected override void OnEnter()
        {
            failed = false;
            effectOk = false;
            StalkerLightState want = mode == StalkerLightsMode.TurnOff ? StalkerLightState.Lit : (mode == StalkerLightsMode.TurnOn ? StalkerLightState.Unlit : StalkerLightState.Any);
            lights = StalkerWorldQuery.FindLights(Brain, want, radius, playerRadius, memorySeconds, out center);
            if (lights == null)
            {
                failed = true;
                return;
            }
            desired = mode == StalkerLightsMode.Toggle ? !StalkerWorldBridge.LightsLit(lights) : mode == StalkerLightsMode.TurnOn;
            Vector3 stand, face;
            StalkerWorldQuery.LightsStand(Brain, lights, center, out stand, out face);
            seq.Begin(stand, face, StalkerWorldAnim.Switch, useDelay, duration, recover, timeout);
        }

        protected override BTStatus OnTick()
        {
            if (failed || lights == null) return BTStatus.Failure;
            if (abortWhenSeen && Brain.Knowledge.hasVisual)
            {
                seq.Cancel(Brain);
                return BTStatus.Failure;
            }
            bool effect;
            StalkerActPhase ph = seq.Tick(Brain, Time.deltaTime, out effect);
            if (effect && StalkerWorldBridge.SetLights(lights, desired))
            {
                effectOk = true;
                StalkerSceneryCache.Mark(Brain, lights, "lights");
                StalkerWorldBridge.Noise(Brain.transform.position, noise, Brain.gameObject);
            }
            if (ph == StalkerActPhase.Failed) return BTStatus.Failure;
            if (ph == StalkerActPhase.Done) return effectOk ? BTStatus.Success : BTStatus.Failure;
            return BTStatus.Running;
        }

        protected override void OnAbort() { seq.Cancel(Brain); }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Revistar esconderijos próximos", "Revista, um depois do outro, os esconderijos (armários, camas...) a até N m que ele não revistou há pouco. Sucesso ao terminar ou ao achar alguém; falha se não há nenhum.")]
    public class StalkerSearchHidingTask : StalkerTask
    {
        public float radius = 12f;
        [Tooltip("Quantos esconderijos revista por vez.")]
        public int maxSpots = 3;
        [Tooltip("Segundos antes de revistar o mesmo esconderijo de novo.")]
        public float memorySeconds = 45f;
        [Tooltip("Bônus de chance de achar quem está dentro (HidingSpot.Inspect).")]
        public float findBonus = 0f;
        [Tooltip("Abre e fecha as portinhas (OpenablePart) do esconderijo.")]
        public bool openParts = true;
        public float useDelay = 0.7f;
        public float duration = 1.4f;
        public float recover = 0.4f;
        public float timeout = 12f;
        [Tooltip("Variável do monstro ligada (1) quando acha alguém. Vazio = não grava.")]
        public string foundVariable = "";
        public bool abortWhenSeen = true;

        StalkerActSeq seq;
        HidingSpot spot;
        OpenablePart[] parts;
        int visited;
        bool searchedAny, found, partsOpen;

        public override string Summary() { return "até " + maxSpots + ", raio " + radius.ToString("0.#") + " m"; }

        protected override void OnEnter()
        {
            spot = null;
            parts = null;
            visited = 0;
            searchedAny = false;
            found = false;
            partsOpen = false;
        }

        protected override BTStatus OnTick()
        {
            if (abortWhenSeen && Brain.Knowledge.hasVisual)
            {
                Cleanup();
                return BTStatus.Failure;
            }
            if (spot == null)
            {
                if (visited >= maxSpots) return searchedAny ? BTStatus.Success : BTStatus.Failure;
                spot = StalkerWorldQuery.FindHiding(Brain, radius, memorySeconds);
                if (spot == null) return searchedAny ? BTStatus.Success : BTStatus.Failure;
                StalkerSceneryCache.Mark(Brain, spot, "searched");
                Vector3 c = StalkerWorldBridge.BoundsOf(spot).center;
                seq.Begin(StalkerWorldQuery.StandNear(c, Brain.transform.position, 1.2f), c, StalkerWorldAnim.Inspect, useDelay, duration, recover, timeout);
                return BTStatus.Running;
            }
            bool effect;
            StalkerActPhase ph = seq.Tick(Brain, Time.deltaTime, out effect);
            if (effect)
            {
                if (openParts)
                {
                    parts = spot.GetComponentsInChildren<OpenablePart>(true);
                    for (int i = 0; i < parts.Length; i++) StalkerWorldBridge.SetOpen(parts[i], true);
                    partsOpen = true;
                }
                searchedAny = true;
                found = spot.Inspect(Brain.transform, findBonus);
                if (found && !string.IsNullOrEmpty(foundVariable)) Brain.Blackboard.SetBool(foundVariable, true);
            }
            if (ph == StalkerActPhase.Done || ph == StalkerActPhase.Failed)
            {
                CloseParts();
                spot = null;
                visited++;
                if (found) return BTStatus.Success;
            }
            return BTStatus.Running;
        }

        protected override void OnAbort() { Cleanup(); }

        void Cleanup()
        {
            seq.Cancel(Brain);
            CloseParts();
            spot = null;
        }

        void CloseParts()
        {
            if (!partsOpen || parts == null) return;
            for (int i = 0; i < parts.Length; i++) StalkerWorldBridge.SetOpen(parts[i], false);
            partsOpen = false;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Usar rota (escada, duto, vão, pulo)", "Anda até a entrada de uma rota e a percorre (o corpo vai à mão, fora do NavMesh). Sucesso ao sair do outro lado; falha se não há rota, se não deu para chegar ou se passou do tempo. Precisa do Stalker Route Runner no monstro.")]
    public class StalkerUseRouteTask : StalkerTask
    {
        public StalkerRouteFilter kind = StalkerRouteFilter.Any;
        [Tooltip("Só rotas com esta marca. Vazio = qualquer.")]
        public string marker = "";
        [Tooltip("Só rotas com uma ponta a até tantos metros dele (em linha reta). 0 = sem limite.")]
        public float radius = 25f;
        public StalkerRoutePick pick = StalkerRoutePick.TowardGoal;
        [Tooltip("Para 'Toward Goal': aonde ele quer chegar.")]
        public StalkerGoalKind goal = StalkerGoalKind.Auto;
        [Tooltip("Com 'Toward Goal' e o destino alcançável andando, só usa a rota se ela for mais rápida que ir direto.")]
        public bool onlyIfFaster = true;
        [Tooltip("Segundos para chegar à entrada. Passou disso (e ainda não entrou na rota), desiste.")]
        public float timeout = 45f;

        StalkerRouteRunner runner;
        float elapsed;
        bool failed;

        public override string Summary() { return kind + ", " + pick; }

        protected override void OnEnter()
        {
            failed = false;
            elapsed = 0f;
            runner = StalkerRouteRunner.Of(Brain);
            if (runner == null)
            {
                StalkerWorldBridge.WarnOnce("noRunner", "O nó 'Usar rota' precisa do componente Stalker Route Runner no monstro (menu Tools > Horror FPS > Stalker > Add Route Runner To Selected Stalker).");
                failed = true;
                return;
            }
            if (runner.Busy)
            {
                failed = true;
                return;
            }
            bool needGoal = pick == StalkerRoutePick.TowardGoal;
            Vector3 g = Vector3.zero;
            if (needGoal && !StalkerWorldQuery.GoalPos(Brain, goal, out g))
            {
                failed = true;
                return;
            }
            StalkerRoutePlan plan = StalkerRoutePlanner.Plan(Brain, g, needGoal, StalkerRoutePlanner.MaskOf(kind), marker, radius, pick, needGoal && onlyIfFaster);
            if (!plan.valid || !runner.Begin(plan.route, plan.forward)) failed = true;
        }

        protected override BTStatus OnTick()
        {
            if (failed || runner == null) return BTStatus.Failure;
            elapsed += Time.deltaTime;
            if (!runner.Busy) return runner.LastResult == StalkerRunnerResult.Done ? BTStatus.Success : BTStatus.Failure;
            if (elapsed > timeout && !runner.Traversing)
            {
                runner.Cancel();
                return BTStatus.Failure;
            }
            return BTStatus.Running;
        }

        // Percurso em andamento é atômico: só a aproximação pode ser cortada.
        protected override void OnAbort()
        {
            if (runner != null && runner.Busy && !runner.Traversing) runner.Cancel();
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Emboscada (esconder e saltar)", "Vai até um ponto de emboscada livre, se esconde e espera um jogador chegar perto; então salta. Sucesso depois do salto (ou da saída quieta); falha se não há ponto livre. Precisa do Stalker Route Runner.")]
    public class StalkerAmbushTask : StalkerTask
    {
        [Tooltip("Só pontos com uma ponta a até tantos metros dele.")]
        public float radius = 25f;
        [Tooltip("Só pontos com esta marca. Vazio = qualquer.")]
        public string marker = "";
        [Tooltip("Não escolhe pontos com um jogador vivo a menos de tantos metros.")]
        public float minPlayerDistance = 6f;
        [Tooltip("Segundos para chegar ao esconderijo. A espera lá dentro é o 'Max Wait' do ponto.")]
        public float timeout = 40f;

        StalkerRouteRunner runner;
        float elapsed;
        bool failed;

        public override string Summary() { return "raio " + radius.ToString("0.#") + " m"; }

        protected override void OnEnter()
        {
            failed = false;
            elapsed = 0f;
            runner = StalkerRouteRunner.Of(Brain);
            if (runner == null)
            {
                StalkerWorldBridge.WarnOnce("noRunner", "O nó 'Emboscada' precisa do componente Stalker Route Runner no monstro (menu Tools > Horror FPS > Stalker > Add Route Runner To Selected Stalker).");
                failed = true;
                return;
            }
            if (runner.Busy)
            {
                failed = true;
                return;
            }
            StalkerAmbushSpot s = StalkerWorldQuery.FindAmbush(Brain, radius, marker, minPlayerDistance);
            if (s == null || !runner.BeginAmbush(s)) failed = true;
        }

        protected override BTStatus OnTick()
        {
            if (failed || runner == null) return BTStatus.Failure;
            elapsed += Time.deltaTime;
            if (!runner.Busy) return runner.LastResult == StalkerRunnerResult.Done ? BTStatus.Success : BTStatus.Failure;
            if (elapsed > timeout && !runner.Traversing)
            {
                runner.Cancel();
                return BTStatus.Failure;
            }
            return BTStatus.Running;
        }

        // Escondido, o corpo está nas mãos do runner e o cérebro congelado: a árvore nem roda. Só a aproximação é cortada.
        protected override void OnAbort()
        {
            if (runner != null && runner.Busy && !runner.Traversing) runner.Cancel();
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Fazer barulho", "Para, toca a animação e faz um barulho no lugar onde está (bater na parede para atrair). Termina bem sempre.")]
    public class StalkerMakeNoiseTask : StalkerTask
    {
        [Tooltip("Raio do barulho em metros.")]
        public float radius = 15f;
        [Tooltip("Quantos barulhos seguidos.")]
        public int repeat = 1;
        [Tooltip("Segundos entre um barulho e o seguinte.")]
        public float interval = 0.8f;
        [Tooltip("Segundos até o primeiro barulho.")]
        public float firstDelay = 0.4f;
        [Tooltip("Gatilho do Animator tocado ao começar. Vazio = nenhum.")]
        public string trigger = StalkerWorldAnim.Interact;

        float t;
        int fired;

        public override string Summary() { return radius.ToString("0.#") + " m x" + repeat; }
        public override bool CanFail { get { return false; } }

        protected override void OnEnter()
        {
            t = 0f;
            fired = 0;
            Brain.Stop();
            if (!string.IsNullOrEmpty(trigger)) StalkerWorldBridge.AnimTrigger(Brain, trigger);
        }

        protected override BTStatus OnTick()
        {
            t += Time.deltaTime;
            Brain.Stop();
            int n = Mathf.Max(1, repeat);
            if (fired < n && t >= firstDelay + fired * interval)
            {
                StalkerWorldBridge.Noise(Brain.transform.position, radius, Brain.gameObject);
                fired++;
            }
            return fired >= n && t >= firstDelay + (n - 1) * interval + 0.4f ? BTStatus.Success : BTStatus.Running;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Definir variável da partida", "Liga ou desliga uma variável da partida (GameSession), a mesma que portas, luzes (Power Variable), diálogos e SmartEvents leem. Ex.: cortar a energia. Termina bem sempre.")]
    public class StalkerSetGameVarTask : StalkerTask
    {
        public string variable = "";
        public bool value = true;

        public override string Summary() { return (string.IsNullOrEmpty(variable) ? "(vazio)" : variable) + " = " + (value ? "ligado" : "desligado"); }
        public override bool CanFail { get { return false; } }

        protected override void OnEnter()
        {
            if (string.IsNullOrEmpty(variable)) StalkerWorldBridge.WarnOnce("emptyVar", "O nó 'Definir variável da partida' está sem nome de variável.");
            else StalkerWorldBridge.SetVar(variable, value);
        }

        protected override BTStatus OnTick() { return BTStatus.Success; }
    }

    // =====================================================================================================
    //  CONDIÇÕES  (cada uma guarda o resultado por uns décimos de segundo: varreduras do cenário não rodam todo quadro)
    // =====================================================================================================

    [Serializable]
    [StalkerNodeMenu("Mundo/Porta aberta atrás de mim", "Vale quando há uma porta aberta e destrancada que ele deixou para trás (a menos de N m).")]
    public class StalkerDoorBehindCondition : StalkerCondition
    {
        public float radius = 3.5f;

        float nextCheck;
        bool last;

        public override string Summary() { return "raio " + radius.ToString("0.#") + " m"; }

        protected override bool Check()
        {
            if (Time.time < nextCheck) return last;
            nextCheck = Time.time + 0.25f;
            last = StalkerWorldQuery.FindDoorBehind(Brain, radius) != null;
            return last;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Há luzes por perto", "Vale quando existe um grupo de luzes no estado escolhido (acesas, apagadas ou qualquer) a até N m dele.")]
    public class StalkerLightsNearCondition : StalkerCondition
    {
        public StalkerLightState state = StalkerLightState.Lit;
        public float radius = 14f;
        [Tooltip("Maior que 0: só conta luzes a até tantos metros de um jogador vivo.")]
        public float playerRadius = 0f;
        [Tooltip("Ignora grupos que ele mexeu há menos de tantos segundos (o mesmo valor do nó 'Luzes').")]
        public float memorySeconds = 20f;

        float nextCheck;
        bool last;

        public override string Summary() { return state + ", raio " + radius.ToString("0.#") + " m"; }

        protected override bool Check()
        {
            if (Time.time < nextCheck) return last;
            nextCheck = Time.time + 0.4f;
            Vector3 c;
            last = StalkerWorldQuery.FindLights(Brain, state, radius, playerRadius, memorySeconds, out c) != null;
            return last;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Há rota por perto", "Vale quando existe uma rota (escada, duto, vão, pulo) que ele pode usar com uma ponta a até N m (em linha reta; não confere o caminho).")]
    public class StalkerRoutesNearCondition : StalkerCondition
    {
        public StalkerRouteFilter kind = StalkerRouteFilter.Any;
        public string marker = "";
        public float radius = 15f;

        float nextCheck;
        bool last;

        public override string Summary() { return kind + ", raio " + radius.ToString("0.#") + " m"; }

        protected override bool Check()
        {
            if (Time.time < nextCheck) return last;
            nextCheck = Time.time + 0.4f;
            last = false;
            int mask = StalkerRoutePlanner.MaskOf(kind);
            Vector3 me = Brain.transform.position;
            System.Collections.Generic.List<StalkerRoute> all = StalkerRoute.All;
            for (int i = 0; i < all.Count && !last; i++)
            {
                StalkerRoute r = all[i];
                if (r == null || !r.Allows(Brain) || !StalkerRoutePlanner.KindAllowed(mask, r.Kind)) continue;
                if (!string.IsNullOrEmpty(marker) && r.Marker != marker) continue;
                float d = Mathf.Min(StalkerWorldBridge.HorizontalDistance(me, r.EntryRaw(true)), StalkerWorldBridge.HorizontalDistance(me, r.ExitRaw(true)));
                if (d <= radius) last = true;
            }
            return last;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Destino só alcançável por rota", "Vale quando o destino escolhido não se alcança andando mas alguma rota (do filtro) resolve. Confere o NavMesh (a cada 0,6 s).")]
    public class StalkerNeedsRouteCondition : StalkerCondition
    {
        public StalkerGoalKind goal = StalkerGoalKind.Auto;
        public StalkerRouteFilter kind = StalkerRouteFilter.Any;
        public string marker = "";
        public float radius = 40f;

        float nextCheck;
        bool last;

        public override string Summary() { return goal + ", " + kind; }

        protected override bool Check()
        {
            if (Time.time < nextCheck) return last;
            nextCheck = Time.time + 0.6f;
            last = false;
            Vector3 g;
            if (!StalkerWorldQuery.GoalPos(Brain, goal, out g)) return false;
            if (StalkerRoutePlanner.WalkReachable(Brain.transform.position, g)) return false;
            StalkerRoutePlan plan = StalkerRoutePlanner.Plan(Brain, g, true, StalkerRoutePlanner.MaskOf(kind), marker, radius, StalkerRoutePick.TowardGoal, false);
            last = plan.valid;
            return last;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Esconderijo por revistar por perto", "Vale quando há um esconderijo (HidingSpot) a até N m que ele não revistou há pouco.")]
    public class StalkerHidingNearCondition : StalkerCondition
    {
        public float radius = 12f;
        [Tooltip("O mesmo valor do nó 'Revistar esconderijos próximos'.")]
        public float memorySeconds = 45f;

        float nextCheck;
        bool last;

        public override string Summary() { return "raio " + radius.ToString("0.#") + " m"; }

        protected override bool Check()
        {
            if (Time.time < nextCheck) return last;
            nextCheck = Time.time + 0.4f;
            last = StalkerWorldQuery.FindHiding(Brain, radius, memorySeconds) != null;
            return last;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Ponto de emboscada livre", "Vale quando há um ponto de emboscada livre, que ele pode usar e alcançar andando, a até N m.")]
    public class StalkerAmbushAvailableCondition : StalkerCondition
    {
        public float radius = 25f;
        public string marker = "";
        public float minPlayerDistance = 6f;

        float nextCheck;
        bool last;

        public override string Summary() { return "raio " + radius.ToString("0.#") + " m"; }

        protected override bool Check()
        {
            if (Time.time < nextCheck) return last;
            nextCheck = Time.time + 0.6f;
            last = StalkerWorldQuery.FindAmbush(Brain, radius, marker, minPlayerDistance) != null;
            return last;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Está numa rota ou emboscada", "Vale enquanto o Stalker Route Runner está usando uma rota ou um esconderijo (inclusive o uso automático).")]
    public class StalkerOnRouteCondition : StalkerCondition
    {
        [Tooltip("Ligado: só vale enquanto está escondido esperando.")]
        public bool onlyHidden = false;

        public override string Summary() { return onlyHidden ? "escondido" : "em uso"; }

        protected override bool Check()
        {
            StalkerRouteRunner r = StalkerRouteRunner.Of(Brain);
            if (r == null) return false;
            return onlyHidden ? r.Hidden : r.Busy;
        }
    }

    [Serializable]
    [StalkerNodeMenu("Mundo/Variável da partida", "Compara uma variável da partida (GameSession) com ligado/desligado. Variável nunca definida vale desligada.")]
    public class StalkerGameVarCondition : StalkerCondition
    {
        public string variable = "";
        public bool expected = true;

        public override string Summary() { return (string.IsNullOrEmpty(variable) ? "(vazio)" : variable) + " = " + (expected ? "ligado" : "desligado"); }

        protected override bool Check() { return StalkerWorldBridge.GetVar(variable) == expected; }
    }
}
