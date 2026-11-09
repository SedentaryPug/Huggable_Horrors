using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Respiração de UM jogador. Acelera e fica mais alta com esforço, coração disparado e pânico; o peito sobe e desce
    /// (a câmera acompanha); e o barulho dela é ouvido por inimigos próximos. O jogador pode SEGURAR A RESPIRAÇÃO
    /// (tecla H / clique do analógico direito, ou SetHoldingBreath por script — esconderijos): fica em silêncio e de mão firme,
    /// mas o coração sobe, e sem ar o fôlego estoura num suspiro alto que denuncia onde ele está.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Breathing")]
    [PlayerModule("Corpo", 2)]
    public class BreathingComponent : MonoBehaviour
    {
        [Header("Ritmo (respirações por minuto)")]
        [SerializeField] float restRate = 13f;
        [SerializeField] float maxRate = 44f;

        [Header("Som")]
        [SerializeField] AudioMode audioMode = AudioMode.Auto;
        [SerializeField, Range(0f, 1f)] float volume = 0.7f;
        [Tooltip("Esforço (0..1) a partir do qual a respiração passa a ser ouvida.")]
        [SerializeField, Range(0f, 1f)] float audibleFrom = 0.3f;
        [Tooltip("Vazio = sons gerados por código.")]
        [SerializeField] AudioClip customInhale;
        [SerializeField] AudioClip customExhale;
        [SerializeField] AudioClip customGasp;

        [Header("Segurar a respiração")]
        [SerializeField] bool allowHold = true;
        [SerializeField] float maxHoldSeconds = 9f;
        [Tooltip("Depois do suspiro forçado, quanto tempo até poder segurar de novo.")]
        [SerializeField] float cooldownSeconds = 4f;

        [Header("Barulho que a IA ouve")]
        [SerializeField] bool emitNoise = true;
        [SerializeField, Range(0f, 1f)] float noiseFrom = 0.4f;
        [SerializeField] float noiseMaxRadius = 5f;
        [SerializeField] float gaspRadius = 9f;

        const float InhaleEnd = 0.42f;

        PlayerMotionProbe motion;
        HeartbeatComponent heart;
        SanityComponent sanity;
        DynamicHeadBob bob;
        ItemInput input;
        AudioSource src;
        float effort, phase, holdTime, cooldown, nextBlendUpdate;
        bool scriptHold, exhaleDone;

        /// <summary>0..1: quanto o corpo está pedindo ar.</summary>
        public float Effort { get { return effort; } }
        /// <summary>Respirações por minuto agora.</summary>
        public float Rate { get { return Mathf.Lerp(restRate, maxRate, effort); } }
        /// <summary>-1 (peito vazio) .. +1 (cheio): serve para balançar a câmera de leve.</summary>
        public float Sway { get; private set; }
        public bool HoldingBreath { get; private set; }
        /// <summary>0..1: quanto do fôlego guardado já foi gasto (1 = prestes a estourar).</summary>
        public float HoldStrain { get { return maxHoldSeconds > 0f ? Mathf.Clamp01(holdTime / maxHoldSeconds) : 0f; } }

        public event Action Inhaled;
        public event Action Exhaled;
        public event Action Gasped;

        /// <summary>Para esconderijos: enquanto true, o jogador segura a respiração (como se apertasse a tecla).</summary>
        public void SetHoldingBreath(bool hold) { scriptHold = hold; }

        void Bind()
        {
            if (motion == null) motion = PlayerRef.Find<PlayerMotionProbe>(this);
            if (heart == null) heart = PlayerRef.Find<HeartbeatComponent>(this);
            if (sanity == null) sanity = PlayerRef.Find<SanityComponent>(this);
            if (bob == null) bob = PlayerRef.Find<DynamicHeadBob>(this);
            if (input == null) input = PlayerRef.Find<ItemInput>(this);
            if (src == null)
            {
                Camera c = PlayerRef.CameraOf(this);
                src = PlayerAudio.Create(c != null ? c.transform : transform, "[Breath]", 12f);
            }
        }

        void OnDisable()
        {
            HoldingBreath = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (motion == null || src == null || Time.frameCount % 120 == 0) Bind();

            // esforço: correr, coração disparado e pânico
            float want = 0.06f;
            if (motion != null) want = Mathf.Max(want, motion.Exertion);
            if (heart != null) want = Mathf.Max(want, heart.Intensity * 0.9f);
            if (sanity != null) want = Mathf.Max(want, HorrorUtil.Smooth01(0.55f, 1f, sanity.Insanity) * 0.6f);
            effort = Mathf.MoveTowards(effort, want, dt * (want > effort ? 0.9f : 0.18f));

            UpdateHold(dt);
            if (HoldingBreath) { Sway = Mathf.MoveTowards(Sway, 0f, dt * 2f); return; }

            // ciclo respiratório
            float prev = phase;
            phase += Rate / 60f * dt;
            if (phase >= 1f)
            {
                phase -= 1f;
                exhaleDone = false;
                DoInhale();
            }
            else if (!exhaleDone && prev < InhaleEnd && phase >= InhaleEnd)
            {
                exhaleDone = true;
                DoExhale();
            }

            float chest = phase < InhaleEnd
                ? HorrorUtil.Smooth01(0f, 1f, phase / InhaleEnd)
                : 1f - HorrorUtil.Smooth01(0f, 1f, (phase - InhaleEnd) / (1f - InhaleEnd));
            Sway = chest * 2f - 1f;
        }

        void UpdateHold(float dt)
        {
            bool pressed = scriptHold || (allowHold && input != null && ItemInput.Held(input.HoldBreath));
            if (cooldown > 0f) cooldown -= dt;
            bool wasHolding = HoldingBreath;
            HoldingBreath = pressed && cooldown <= 0f;

            if (HoldingBreath)
            {
                holdTime += dt;
                if (holdTime >= maxHoldSeconds)
                {
                    HoldingBreath = false;
                    cooldown = cooldownSeconds;
                    holdTime = 0f;
                    scriptHold = false;          // o suspiro forçado também solta quem estava "escondido"
                    Gasp(1f);
                }
            }
            else
            {
                holdTime = Mathf.Max(0f, holdTime - dt * 1.5f);
                if (wasHolding && !pressed) { phase = 0.5f; exhaleDone = true; PlayClip(ExhaleClip(), 0.5f); }    // solta o ar devagar
            }
        }

        /// <summary>Suspiro alto e brusco (susto, fôlego estourado). Barulho que denuncia a posição.</summary>
        public void Gasp(float loudness01 = 1f)
        {
            loudness01 = Mathf.Clamp01(loudness01);
            PlayClip(customGasp != null ? customGasp : ProceduralAudio.Gasp(), 0.55f + 0.45f * loudness01, true);
            if (emitNoise) NoiseSystem.Emit(transform.position, gaspRadius * loudness01, NoiseKind.Gasp, gameObject);
            if (heart != null) heart.Boost(14f * loudness01, 3f);
            if (bob != null) bob.AddTrauma(0.1f * loudness01);
            effort = Mathf.Max(effort, 0.7f);
            Action h = Gasped; if (h != null) h();
        }

        void DoInhale()
        {
            PlayClip(customInhale != null ? customInhale : ProceduralAudio.BreathIn(), 1f);
            Action h = Inhaled; if (h != null) h();
        }

        void DoExhale()
        {
            PlayClip(ExhaleClip(), 1f);
            if (emitNoise && effort >= noiseFrom)
                NoiseSystem.Emit(transform.position, noiseMaxRadius * HorrorUtil.Smooth01(noiseFrom, 1f, effort), NoiseKind.Breath, gameObject);
            Action h = Exhaled; if (h != null) h();
        }

        AudioClip ExhaleClip() { return customExhale != null ? customExhale : ProceduralAudio.BreathOut(); }

        void PlayClip(AudioClip clip, float scale, bool force = false)
        {
            if (src == null || clip == null) return;
            if (!force && effort < audibleFrom) return;
            if (Time.unscaledTime >= nextBlendUpdate) { nextBlendUpdate = Time.unscaledTime + 2f; src.spatialBlend = PlayerAudio.SpatialBlend(audioMode); }
            float v = force ? 1f : HorrorUtil.Smooth01(audibleFrom, 1f, effort);
            src.pitch = Mathf.Lerp(0.95f, 1.12f, effort);
            src.PlayOneShot(clip, volume * v * scale);
        }
    }
}
