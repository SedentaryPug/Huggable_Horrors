using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Coração de UM jogador. O ritmo sobe com esforço (correr), sanidade baixa, ameaças por perto, sustos e segurar a respiração,
    /// e desce devagar quando tudo se acalma. Consequências:
    ///   • som (tum-tum) que fica audível quando o coração dispara;
    ///   • tela e câmera pulsam junto (Pulse alimenta SanityFX e DynamicHeadBob);
    ///   • mãos tremem (tremor de câmera) e o fôlego pesa (velocidade cai um pouco) quando está disparado;
    ///   • o barulho do coração é ouvido por inimigos próximos (NoiseSystem), então se esconder assustado denuncia a posição.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Heartbeat")]
    [PlayerModule("Corpo", 1)]
    public class HeartbeatComponent : MonoBehaviour
    {
        [Header("Ritmo (batimentos por minuto)")]
        [SerializeField] float restBpm = 66f;
        [SerializeField] float maxBpm = 178f;
        [SerializeField] float riseBpmPerSecond = 28f;
        [SerializeField] float fallBpmPerSecond = 7f;
        [Tooltip("BPM somado ao correr (andar soma uma fração).")]
        [SerializeField] float runBpm = 42f;
        [Tooltip("BPM somado com a sanidade em zero.")]
        [SerializeField] float insanityBpm = 38f;
        [Tooltip("BPM somado com uma ameaça no máximo (SetThreat).")]
        [SerializeField] float threatBpm = 62f;
        [SerializeField] float holdBreathBpm = 12f;

        [Header("Som")]
        [SerializeField] AudioMode audioMode = AudioMode.Auto;
        [SerializeField, Range(0f, 1f)] float volume = 0.85f;
        [Tooltip("Intensidade (0..1) a partir da qual o batimento passa a ser ouvido.")]
        [SerializeField, Range(0f, 1f)] float audibleFrom = 0.2f;
        [Tooltip("Vazio = batimento gerado por código.")]
        [SerializeField] AudioClip customBeat;

        [Header("Efeitos negativos")]
        [SerializeField] bool shakeHands = true;
        [Tooltip("Força do tremor de câmera com o coração no máximo (0..1).")]
        [SerializeField, Range(0f, 0.4f)] float maxTremor = 0.12f;
        [SerializeField] bool slowWhenRacing = true;
        [SerializeField, Range(0.5f, 1f)] float racingSpeedMultiplier = 0.9f;
        [SerializeField, Range(0.3f, 1f)] float racingFrom = 0.78f;

        [Header("Barulho que a IA ouve")]
        [SerializeField] bool emitNoise = true;
        [SerializeField, Range(0f, 1f)] float noiseFrom = 0.55f;
        [SerializeField] float noiseMaxRadius = 6f;

        struct BoostEntry { public float bpm, left, total; }

        readonly List<BoostEntry> boosts = new List<BoostEntry>();
        readonly Dictionary<string, float> threats = new Dictionary<string, float>();
        static readonly List<HeartbeatComponent> all = new List<HeartbeatComponent>();

        PlayerMotionProbe motion;
        SanityComponent sanity;
        BreathingComponent breath;
        DynamicHeadBob bob;
        AudioSource src;
        float bpm, phase, pulse, dubTimer, nextBlendUpdate, lastSpeedMult = 1f;
        bool dubPending;

        public float Bpm { get { return bpm; } }
        public float RestBpm { get { return restBpm; } }
        /// <summary>0 calmo ... 1 disparado.</summary>
        public float Intensity { get; private set; }
        /// <summary>0..1: sobe a cada batida e decai (a tela e a câmera usam para pulsar).</summary>
        public float Pulse { get { return pulse; } }
        public float BeatsPerSecond { get { return bpm / 60f; } }

        /// <summary>A cada "tum" principal.</summary>
        public event Action<HeartbeatComponent> Beat;

        void Awake() { bpm = restBpm; }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }

        void OnDisable()
        {
            all.Remove(this);
            FrameworkBridge.SetSpeedMultiplier(this, "heart", 1f);
            lastSpeedMult = 1f;
            if (bob != null) bob.SetConstantTrauma("heart", 0f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        // ------------------------------------------------------------------ API
        /// <summary>Soma 'extraBpm' (negativo acalma) por 'seconds', diminuindo até zero.</summary>
        public void Boost(float extraBpm, float seconds)
        {
            if (seconds <= 0f || Mathf.Approximately(extraBpm, 0f)) return;
            boosts.Add(new BoostEntry { bpm = extraBpm, left = seconds, total = seconds });
            if (extraBpm > 0f) bpm = Mathf.Min(maxBpm, bpm + extraBpm * 0.25f);       // o coração "dá um salto" na hora
        }

        /// <summary>Susto: 0..1 (1 = pavor). Dispara o coração e treme a câmera.</summary>
        public void Scare(float amount01)
        {
            amount01 = Mathf.Clamp01(amount01);
            if (amount01 <= 0f) return;
            Boost(65f * amount01, 4f + 5f * amount01);
            pulse = 1f;
            if (bob != null) bob.AddTrauma(0.25f * amount01);
        }

        /// <summary>Ameaça contínua (ex.: um monstro te perseguindo). 'source' é qualquer nome estável; 0 encerra.</summary>
        public void SetThreat(string source, float level01)
        {
            if (string.IsNullOrEmpty(source)) return;
            if (level01 <= 0.001f) threats.Remove(source);
            else threats[source] = Mathf.Clamp01(level01);
        }

        public void ClearThreat(string source) { if (!string.IsNullOrEmpty(source)) threats.Remove(source); }

        /// <summary>Assusta todos os jogadores no raio (e com o ponto à vista, se pedido). Mais perto = mais forte.</summary>
        public static void Alert(Vector3 position, float radius, float level01, bool requireSight = false)
        {
            for (int i = 0; i < all.Count; i++)
            {
                HeartbeatComponent h = all[i];
                if (h == null || !h.isActiveAndEnabled) continue;
                SanityComponent s = SanityComponent.Of(h);
                Vector3 eye = s != null ? s.EyePosition : h.transform.position;
                float d = Vector3.Distance(eye, position);
                if (radius > 0f && d > radius) continue;
                if (requireSight && s != null && !s.CanSee(position)) continue;
                h.Scare(level01 * (radius > 0f ? Mathf.Lerp(1f, 0.4f, d / radius) : 1f));
            }
        }

        // ------------------------------------------------------------------ laço
        void Bind()
        {
            if (motion == null) motion = PlayerRef.Find<PlayerMotionProbe>(this);
            if (sanity == null) sanity = PlayerRef.Find<SanityComponent>(this);
            if (breath == null) breath = PlayerRef.Find<BreathingComponent>(this);
            if (bob == null) bob = PlayerRef.Find<DynamicHeadBob>(this);
            if (src == null)
            {
                Camera c = PlayerRef.CameraOf(this);
                src = PlayerAudio.Create(c != null ? c.transform : transform, "[Heartbeat]");
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (motion == null || sanity == null || src == null || (Time.frameCount % 120 == 0)) Bind();

            // ritmo desejado
            float target = restBpm;
            if (motion != null) target += runBpm * Mathf.Clamp01(motion.Exertion);
            if (sanity != null) target += insanityBpm * HorrorUtil.Smooth01(0.2f, 1f, sanity.Insanity);
            if (threats.Count > 0)
            {
                float th = 0f;
                foreach (KeyValuePair<string, float> kv in threats) th = Mathf.Max(th, kv.Value);
                target += threatBpm * th;
            }
            for (int i = boosts.Count - 1; i >= 0; i--)
            {
                BoostEntry b = boosts[i];
                b.left -= dt;
                if (b.left <= 0f) { boosts.RemoveAt(i); continue; }
                boosts[i] = b;
                target += b.bpm * (b.left / b.total);
            }
            if (breath != null && breath.HoldingBreath) target += holdBreathBpm;
            target = Mathf.Clamp(target, restBpm * 0.85f, maxBpm);

            bpm = Mathf.MoveTowards(bpm, target, (target > bpm ? riseBpmPerSecond : fallBpmPerSecond) * dt);
            Intensity = Mathf.Clamp01((bpm - (restBpm + 6f)) / Mathf.Max(1f, maxBpm - restBpm - 20f));

            // batidas
            phase += bpm / 60f * dt;
            if (phase >= 1f) { phase -= 1f; DoBeat(); }
            if (dubPending)
            {
                dubTimer -= dt;
                if (dubTimer <= 0f) { dubPending = false; pulse = Mathf.Max(pulse, 0.6f); }
            }
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 5f);

            ApplyBodyEffects();
        }

        void DoBeat()
        {
            pulse = 1f;
            dubPending = true;
            dubTimer = 0.27f;
            Action<HeartbeatComponent> h = Beat; if (h != null) h(this);

            if (src != null && Intensity >= audibleFrom)
            {
                if (Time.unscaledTime >= nextBlendUpdate) { nextBlendUpdate = Time.unscaledTime + 2f; src.spatialBlend = PlayerAudio.SpatialBlend(audioMode); }
                AudioClip clip = customBeat != null ? customBeat : ProceduralAudio.Heartbeat();
                src.pitch = Mathf.Lerp(0.92f, 1.08f, Intensity);
                src.PlayOneShot(clip, volume * HorrorUtil.Smooth01(audibleFrom, 1f, Intensity));
            }

            if (emitNoise && Intensity >= noiseFrom)
            {
                float k = HorrorUtil.Smooth01(noiseFrom, 1f, Intensity);
                if (breath != null && breath.HoldingBreath) k *= 0.5f;
                NoiseSystem.Emit(transform.position, noiseMaxRadius * k, NoiseKind.Heartbeat, gameObject);
            }
        }

        void ApplyBodyEffects()
        {
            float mult = 1f;
            if (slowWhenRacing && Intensity > racingFrom)
                mult = Mathf.Lerp(1f, racingSpeedMultiplier, (Intensity - racingFrom) / Mathf.Max(0.01f, 1f - racingFrom));
            if (Mathf.Abs(mult - lastSpeedMult) > 0.004f)
            {
                lastSpeedMult = mult;
                FrameworkBridge.SetSpeedMultiplier(this, "heart", mult);
            }
            if (bob != null) bob.SetConstantTrauma("heart", shakeHands ? Intensity * Intensity * maxTremor : 0f);
        }
    }
}
