using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Visão e audição do perseguidor. A visão é um medidor de suspeita por jogador (0..1) que enche mais rápido
    /// com atenção alta, perto, em movimento e com o corpo todo à vista; o jogador só é "notado" ao chegar em 1.
    /// A audição é um INoiseHearer: o NoiseSystem do framework chama OnNoiseHeard, e aqui o volume real
    /// (distância + paredes + audição do nível de atenção) decide se o som vira um StalkerNoise.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Stalker/Stalker Senses")]
    public class StalkerSenses : MonoBehaviour, INoiseHearer
    {
        [Tooltip("Cabeça do monstro (olhos e ouvidos). Vazio = 1,6 m acima do objeto.")]
        public Transform eyes;

        /// <summary>Um som passou pelo filtro de volume. A decisão de reagir (ou ignorar uma isca) é do cérebro.</summary>
        public event Action<StalkerNoise> NoiseHeard;

        /// <summary>O jogador notado (suspeita completa) mais próximo, ou null.</summary>
        public StalkerTarget Best { get; private set; }

        /// <summary>Jogador que acabou de passar do limiar de "vi algo" neste quadro (só uma vez por vez).</summary>
        public StalkerTarget GlimpseTarget { get; private set; }

        StalkerBrain brain;
        readonly Dictionary<StalkerTarget, float> awareness = new Dictionary<StalkerTarget, float>();
        readonly Dictionary<StalkerTarget, float> lastSeen = new Dictionary<StalkerTarget, float>();
        readonly Dictionary<StalkerTarget, Vector3> lastVisible = new Dictionary<StalkerTarget, Vector3>();
        readonly HashSet<StalkerTarget> seen = new HashSet<StalkerTarget>();
        readonly HashSet<StalkerTarget> glimpsed = new HashSet<StalkerTarget>();
        static readonly RaycastHit[] hits = new RaycastHit[12];

        public Vector3 EyePos { get { return eyes != null ? eyes.position : transform.position + Vector3.up * 1.6f; } }
        Vector3 Forward { get { return eyes != null ? eyes.forward : transform.forward; } }

        void Awake()
        {
            brain = GetComponent<StalkerBrain>();
            // o NoiseSystem só avisa quem tem um Collider dentro do raio do barulho: sem corpo, cria "orelhas"
            if (GetComponentInChildren<Collider>() == null)
            {
                SphereCollider c = gameObject.AddComponent<SphereCollider>();
                c.isTrigger = true; c.radius = 0.5f; c.center = Vector3.up * 1.5f;
            }
        }

        void OnDisable()
        {
            awareness.Clear(); lastSeen.Clear(); lastVisible.Clear(); seen.Clear(); glimpsed.Clear();
            Best = null; GlimpseTarget = null;
        }

        // ------------------------------------------------------------------ consultas
        public bool Sees(StalkerTarget t) { return seen.Contains(t); }

        public float Awareness(StalkerTarget t) { float a; return awareness.TryGetValue(t, out a) ? a : 0f; }

        /// <summary>Último lugar onde ele REALMENTE viu o jogador (não acompanha o jogador atrás de uma parede).</summary>
        public Vector3 LastVisiblePos(StalkerTarget t) { Vector3 p; return lastVisible.TryGetValue(t, out p) ? p : transform.position; }

        public bool SawRecently(StalkerTarget t, float seconds)
        {
            float s;
            return lastSeen.TryGetValue(t, out s) && Time.time - s <= seconds;
        }

        // ------------------------------------------------------------------ visão
        public void Tick(float dt)
        {
            StalkerProfile prof = brain.Profile;
            AttentionTuning tune = brain.Tune;
            List<StalkerTarget> list = StalkerBridge.Players;
            Vector3 eye = EyePos;
            StalkerTarget best = null;
            float bestDist = float.MaxValue;
            GlimpseTarget = null;

            for (int i = 0; i < list.Count; i++)
            {
                StalkerTarget t = list[i];
                if (!t.Valid || !t.alive)
                {
                    awareness.Remove(t); seen.Remove(t); glimpsed.Remove(t);
                    continue;
                }

                float vis = Visibility(t, eye, tune, prof);
                float a;
                awareness.TryGetValue(t, out a);
                if (vis > 0f)
                {
                    float dist = Vector3.Distance(eye, t.Position);
                    float near = Mathf.Lerp(1.6f, 0.7f, Mathf.Clamp01(dist / Mathf.Max(1f, tune.visionRange)));
                    float confirm = Mathf.Max(0.05f, tune.confirmSeconds / (1f + 0.25f * brain.Skill));
                    a += vis * near * dt / confirm;
                    lastVisible[t] = t.Position;
                }
                else a -= dt / Mathf.Max(0.1f, prof.forgetSeconds);
                a = Mathf.Clamp01(a);
                awareness[t] = a;

                // notado com histerese: não pisca quando o jogador some por um instante
                bool isSeen = a >= 1f || (seen.Contains(t) && a > 0.55f);
                if (isSeen) { seen.Add(t); lastSeen[t] = Time.time; }
                else seen.Remove(t);

                if (isSeen)
                {
                    float d = Vector3.Distance(transform.position, t.Position);
                    if (d < bestDist) { bestDist = d; best = t; }
                }
                else if (a >= prof.glimpseAt)
                {
                    if (glimpsed.Add(t)) GlimpseTarget = t;
                }
                if (a < prof.glimpseAt * 0.5f) glimpsed.Remove(t);
            }
            Best = best;
        }

        float Visibility(StalkerTarget t, Vector3 eye, AttentionTuning tune, StalkerProfile prof)
        {
            if (t.hidden) return 0f;
            Vector3 chest = StalkerBridge.ChestPos(t);
            Vector3 to = chest - eye;
            float dist = to.magnitude;
            if (dist > tune.visionRange) return 0f;
            if (dist > prof.nearSenseRange && Vector3.Angle(Forward, to) > tune.visionFov * 0.5f) return 0f;

            int visible = 0;
            if (LineClear(eye, StalkerBridge.EyePos(t), t)) visible++;
            if (LineClear(eye, chest, t)) visible++;
            if (LineClear(eye, StalkerBridge.FeetPos(t), t)) visible++;
            if (visible == 0) return 0f;

            float k = dist / Mathf.Max(1f, tune.visionRange);
            float v = (visible / 3f) * (1f - 0.6f * k * k);
            if (t.speed < 0.25f) v *= prof.stillFactor;
            else if (t.speed > 4.2f) v *= prof.runningFactor;
            v = brain.ModifyVisibility(t, v);        // traços exclusivos (cego à luz, visão noturna...)
            return Mathf.Clamp01(v);
        }

        bool LineClear(Vector3 from, Vector3 to, StalkerTarget t)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.05f) return true;
            int n = Physics.RaycastNonAlloc(from, d / dist, hits, dist, brain.Profile.sightMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Transform h = hits[i].collider.transform;
                if (h.IsChildOf(transform) || h.IsChildOf(t.root)) continue;
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ audição
        public void OnNoiseHeard(NoiseEvent ev)
        {
            if (brain == null || !isActiveAndEnabled) return;
            if (ev.IsFrom(transform)) return;
            Transform src = StalkerBridge.SourceOf(ev);
            if (src != null && src.IsChildOf(transform)) return;
            if (src != null && src.GetComponentInParent<StalkerBrain>() != null) return;     // perseguidores não se alertam uns aos outros

            AttentionTuning tune = brain.Tune;
            float vol = StalkerBridge.Loudness(ev, EyePos, tune.hearing * (1f + 0.15f * brain.Skill), brain.Profile.hearingOcclusion);
            if (vol <= 0f) return;

            StalkerNoiseTag tag = src != null ? src.GetComponentInParent<StalkerNoiseTag>() : null;
            StalkerNoise n = new StalkerNoise();
            n.position = ev.position;
            n.loudness = vol;
            n.kind = StalkerBridge.KindName(ev);
            n.source = src;
            n.time = Time.time;
            n.target = StalkerBridge.TargetOf(src);
            n.playerMade = n.target != null;
            n.tagMode = tag != null ? tag.mode : NoiseTagMode.Learn;
            n.key = KeyOf(src, n.kind, tag);

            if (n.tagMode != NoiseTagMode.AlwaysInvestigate && vol < tune.noiseThreshold) return;
            if (NoiseHeard != null) NoiseHeard(n);
        }

        static string KeyOf(Transform src, string kind, StalkerNoiseTag tag)
        {
            if (tag != null && !string.IsNullOrEmpty(tag.key)) return tag.key;
            if (src == null) return "?:" + kind;
            string n = src.name.Replace("(Clone)", "");
            int p = n.IndexOf(" (", StringComparison.Ordinal);
            if (p > 0) n = n.Substring(0, p);
            return n.Trim() + ":" + kind;
        }
    }
}
