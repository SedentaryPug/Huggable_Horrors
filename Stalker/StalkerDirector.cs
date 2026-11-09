using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorFPS
{
    /// <summary>Estado que o Diretor guarda por perseguidor (ritmo de pistas e "sossego").</summary>
    public sealed class StalkerDirectorState
    {
        public float nextPressure;
        public float reliefUntil;
        public readonly Queue<float> stamps = new Queue<float>();
    }

    /// <summary>
    /// O Diretor SABE tudo (posição, sanidade, esconderijo e tensão de cada jogador) mas só entrega ao perseguidor
    /// uma pista quando uma regra libera: (1) pressão: uma área aproximada quando a coisa está calma demais;
    /// (2) esconderijo: se ele VIU o jogador entrar num; (3) ping de roteiro. Entre perseguições há "sossego"
    /// (sem pistas) para o ritmo respirar. Nasce sozinho na cena quando o primeiro perseguidor liga.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Stalker Director")]
    public class StalkerDirector : MonoBehaviour
    {
        static StalkerDirector instance;

        public static StalkerDirector Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<StalkerDirector>();
                    if (instance == null) instance = new GameObject("[Stalker] Director").AddComponent<StalkerDirector>();
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        readonly List<StalkerBrain> brains = new List<StalkerBrain>();
        readonly Dictionary<StalkerTarget, Vector3> lastPos = new Dictionary<StalkerTarget, Vector3>();
        readonly Dictionary<StalkerTarget, bool> wasHidden = new Dictionary<StalkerTarget, bool>();
        float nextTick, lastTickTime;
        float lastPressureAt = -999f;      // última pista de pressão entregue a QUALQUER perseguidor

        public void Register(StalkerBrain b)
        {
            if (b == null || brains.Contains(b)) return;
            brains.Add(b);
            b.dir.nextPressure = Time.time + b.Profile.pressureMinInterval + Random.Range(0f, 8f);   // vários: não começam em sincronia
        }

        /// <summary>Um perseguidor notou alguém: colegas do mesmo grupo, perto o bastante, recebem uma pista (Share Radius &gt; 0 no perfil).</summary>
        public void Share(StalkerBrain from, StalkerTarget t, Vector3 pos)
        {
            string group = from.Group;
            if (string.IsNullOrEmpty(group)) return;
            float r = from.Profile.shareRadius;
            for (int i = 0; i < brains.Count; i++)
            {
                StalkerBrain o = brains[i];
                if (o == null || o == from || !o.isActiveAndEnabled || o.Frozen) continue;
                if (o.Group != group || o.Knowledge.hasVisual) continue;
                if ((o.transform.position - from.transform.position).sqrMagnitude > r * r) continue;
                o.ReceiveShared(t, pos, from.Profile.shareConfidence);
            }
        }

        public void Unregister(StalkerBrain b) { brains.Remove(b); }

        // ------------------------------------------------------------------ API para roteiro
        /// <summary>Entrega uma pista a todos os perseguidores (ex.: alarme disparou, o jogador abriu a porta proibida).</summary>
        public void Ping(Vector3 position, float radius, float confidence)
        {
            for (int i = 0; i < brains.Count; i++)
            {
                if (brains[i] == null) continue;
                StalkerHint h = new StalkerHint();
                h.position = position; h.origin = position; h.radius = radius; h.confidence = confidence; h.source = HintSource.Ping;
                brains[i].ReceiveHint(h);
            }
        }

        /// <summary>Sossego: nenhum perseguidor recebe pistas de pressão por estes segundos (momentos de roteiro, puzzles).</summary>
        public void GiveRelief(float seconds)
        {
            for (int i = 0; i < brains.Count; i++)
                if (brains[i] != null) brains[i].dir.reliefUntil = Mathf.Max(brains[i].dir.reliefUntil, Time.time + seconds);
        }

        // ------------------------------------------------------------------ laço
        void Update()
        {
            if (Time.time < nextTick) return;
            float dt = Time.time - lastTickTime;
            lastTickTime = Time.time;
            nextTick = Time.time + 0.2f;
            if (dt <= 0f || dt > 2f) dt = 0.2f;

            Sample(dt);
            for (int i = brains.Count - 1; i >= 0; i--)
            {
                if (brains[i] == null) { brains.RemoveAt(i); continue; }
                if (brains[i].isActiveAndEnabled) Serve(brains[i]);
            }

            // cada memória envelhece uma vez por intervalo, não importa quantos perseguidores a dividem
            for (int i = 0; i < brains.Count; i++)
            {
                StalkerBrain mb = brains[i];
                if (mb != null) mb.Memory.Decay(mb.Profile.baitDecayPerMinute, mb.Profile.heatDecayPerMinute);
            }
            StalkerMemory.FlushAll(false);
        }

        void OnDestroy() { if (instance == this) instance = null; }

        /// <summary>O Diretor lê o estado de todos os jogadores. Isto é a onisciência: nada daqui chega ao monstro sem passar por Serve.</summary>
        void Sample(float dt)
        {
            List<StalkerTarget> list = StalkerBridge.Players;
            for (int i = 0; i < list.Count; i++)
            {
                StalkerTarget t = list[i];
                if (!t.Valid) continue;
                Vector3 pos = t.Position, prev;
                if (lastPos.TryGetValue(t, out prev))
                {
                    Vector3 v = (pos - prev) / dt;
                    Vector3 flat = new Vector3(v.x, 0f, v.z);
                    if (flat.magnitude > 15f) { v = Vector3.zero; flat = Vector3.zero; }   // teleporte / troca de nível
                    t.velocity = v; t.speed = flat.magnitude;
                }
                lastPos[t] = pos;

                t.alive = StalkerBridge.IsAlive(t);
                t.sanity01 = StalkerBridge.Sanity01(t);
                t.tension = StalkerBridge.Tension01(t);
                bool h = StalkerBridge.IsHidden(t), was;
                wasHidden.TryGetValue(t, out was);
                t.hidden = h;
                wasHidden[t] = h;
                if (h && !was) OnEnteredHiding(t);
            }

            for (int i = 0; i < list.Count; i++)
            {
                StalkerTarget t = list[i];
                t.isolation = 0f;
                if (!t.Valid || !t.alive) continue;
                float nearest = float.MaxValue;
                for (int j = 0; j < list.Count; j++)
                {
                    if (j == i || !list[j].Valid || !list[j].alive) continue;
                    nearest = Mathf.Min(nearest, Vector3.Distance(t.Position, list[j].Position));
                }
                if (nearest < float.MaxValue) t.isolation = nearest;
            }
        }

        /// <summary>Esconderijo: só vira pista se o perseguidor VIU o jogador entrar (notou-o nos últimos segundos).</summary>
        void OnEnteredHiding(StalkerTarget t)
        {
            for (int i = 0; i < brains.Count; i++)
            {
                StalkerBrain b = brains[i];
                if (b == null || !b.isActiveAndEnabled || b.Senses == null) continue;
                if (!b.Senses.SawRecently(t, b.Profile.seenHidingWindow)) continue;
                Component spot = StalkerBridge.SpotOf(t);
                if (spot == null) continue;
                StalkerHint h = new StalkerHint();
                h.position = spot.transform.position; h.origin = h.position; h.radius = 1.5f;
                h.confidence = b.Profile.hidingHintConfidence; h.source = HintSource.DirectorHiding; h.spot = spot;
                if (b.ReceiveHint(h)) b.Memory.NoteSpotUse(StalkerBridge.SpotKey(spot));
            }
        }

        static float Exposure(StalkerTarget t, StalkerProfile p)
        {
            float s = 1f;
            s += p.isolationBias * Mathf.Clamp01(t.isolation / 30f);
            s += p.lowSanityBias * (1f - t.sanity01);
            s += p.tensionBias * t.tension;
            if (t.speed > 4f) s += 0.3f;
            return s;
        }

        /// <summary>Decide se este perseguidor recebe uma pista de pressão agora, e sobre quem.</summary>
        void Serve(StalkerBrain b)
        {
            StalkerProfile p = b.Profile;
            List<StalkerTarget> list = StalkerBridge.Players;

            StalkerTarget best = null;
            float bestScore = -1f;
            for (int i = 0; i < list.Count; i++)
            {
                StalkerTarget t = list[i];
                if (!t.Valid || !t.alive) continue;
                float s = Exposure(t, p);
                if (t == b.pressureTarget) s *= 1.2f;      // não fica trocando de alvo a cada tique
                if (PressedByOther(b, t)) s *= p.targetSharePenalty;   // vários: espalha a pressão entre os jogadores
                if (s > bestScore) { bestScore = s; best = t; }
            }
            b.pressureTarget = best;

            float now = Time.time;
            if (best == null || best.hidden) return;                          // não entrega quem está escondido
            if (b.Mode != StalkerMode.Patrol) return;                         // ele já está ocupado com algo
            if (b.Frozen || StalkerManager.Cinematic) return;                 // em cutscene ninguém recebe pistas
            if (now < b.dir.nextPressure || now < b.dir.reliefUntil) return;  // ritmo e sossego
            if (now - lastPressureAt < p.globalPressureGap) return;           // vários: pistas de pressão não chegam todas juntas
            if (p.pressureOnlyWhenCalm && b.Level > StalkerAttention.Alert) return;

            while (b.dir.stamps.Count > 0 && now - b.dir.stamps.Peek() > 60f) b.dir.stamps.Dequeue();
            if (b.dir.stamps.Count >= p.maxHintsPerMinute) return;

            Vector2 off = Random.insideUnitCircle * p.pressureRadius;
            Vector3 pos = best.Position + new Vector3(off.x, 0f, off.y);
            NavMeshHit nh;
            if (NavMesh.SamplePosition(pos, out nh, p.pressureRadius, NavMesh.AllAreas)) pos = nh.position;

            StalkerHint h = new StalkerHint();
            h.position = pos; h.origin = pos; h.radius = p.pressureRadius;
            h.confidence = p.pressureConfidence; h.source = HintSource.DirectorPressure;
            if (b.ReceiveHint(h)) { b.dir.stamps.Enqueue(now); lastPressureAt = now; }
            b.dir.nextPressure = now + Random.Range(p.pressureMinInterval, p.pressureMaxInterval) / (1f + 0.4f * b.Skill);
        }

        /// <summary>Algum OUTRO perseguidor já está pressionando este jogador?</summary>
        bool PressedByOther(StalkerBrain me, StalkerTarget t)
        {
            for (int i = 0; i < brains.Count; i++)
            {
                StalkerBrain o = brains[i];
                if (o != null && o != me && o.isActiveAndEnabled && o.pressureTarget == t) return true;
            }
            return false;
        }
    }
}
