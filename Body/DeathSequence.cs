using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// A morte de UM jogador, vista por ele e pelos colegas:
    ///  • câmera (só a imagem, via CameraFx): o olho cai até o chão, a cabeça tomba para o lado contrário ao golpe, o campo de visão
    ///    fecha e há um quique curto ao bater no chão;
    ///  • corpo (visto pelos outros jogadores): o modelo tomba para longe de quem bateu (só rotação do corpo; o clipe de morte do
    ///    Animator, se você tiver um, toca por cima);
    ///  • som: baque do corpo e um zumbido grave que some; o coração e a respiração do jogador morto ficam em silêncio.
    /// Começa só 'graceSeconds' depois da morte: se nesse tempo o jogador é revivido ou vira "caído" (módulo Revive, que desfaz a
    /// morte logo depois que ela acontece), nada disso aparece. Se ele for revivido depois, a câmera e o corpo voltam suavemente.
    /// O texto de "como morreu" vem de <see cref="DeathCause"/>; a tela do jogador (vinheta escura, legenda) é o DamageHUD.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Death Sequence")]
    [PlayerModule("Corpo", 5)]
    public class DeathSequence : MonoBehaviour, ICameraModifier, IPoseModifier
    {
        [Header("Tempo")]
        [Tooltip("Segundos de espera depois da morte antes de começar (para o módulo de reanimação poder desfazê-la sem a cena de morte aparecer).")]
        [SerializeField] float graceSeconds = 0.3f;
        [Tooltip("Do instante em que começa a cair até bater no chão.")]
        [SerializeField] float fallSeconds = 0.85f;

        [Header("Câmera")]
        [Tooltip("Quanto a cabeça tomba de lado (graus de rolagem), para longe do golpe.")]
        [SerializeField] float rollDegrees = 68f;
        [Tooltip("Para onde o olhar termina: negativo = olhando para cima, para o teto.")]
        [SerializeField] float endPitch = -14f;
        [Tooltip("Distância do olho ao chão quando já caído (m).")]
        [SerializeField] float groundClearance = 0.24f;
        [Tooltip("Graus tirados do campo de visão ao cair (a visão fecha).")]
        [SerializeField] float fovClose = 14f;
        [Tooltip("Metros de quique ao bater no chão.")]
        [SerializeField] float bounce = 0.07f;

        [Header("Corpo")]
        [SerializeField] bool collapseBody = true;
        [Tooltip("Graus que o corpo tomba (90 = deitado).")]
        [SerializeField] float bodyFallDegrees = 86f;
        [Tooltip("Se o Animator do corpo já tem um clipe de morte (nome com 'death' ou 'dying'), ele cuida da queda e o tombo por código não é somado.")]
        [SerializeField] bool yieldToDeathClip = true;

        [Header("Som")]
        [SerializeField] bool playSounds = true;
        [Tooltip("Desliga o batimento e a respiração enquanto o jogador está morto.")]
        [SerializeField] bool silenceHeartAndBreath = true;
        [Tooltip("Barulho do corpo caindo para a IA (raio em metros; 0 = nenhum).")]
        [SerializeField] float fallNoiseRadius = 12f;

        // ------------------------------------------------------------------ estado
        PlayerContext player;
        StatsComponent stats;
        Camera cam;
        PlayerBodyPresenter body;
        HeartbeatComponent heart;
        BreathingComponent breath;
        bool heartWasOn, breathWasOn;
        bool hooked, bodyAdded;
        bool bodyHasClip;
        float nextResolve;

        bool pending, active, landed;
        float graceLeft, t, weight, side = 1f, drop;
        Vector3 hitFrom;
        bool hasHit;
        DamageInfo lastHit, fatal;
        bool hasFatal;
        string cause = string.Empty;
        Vector3 fallDir = Vector3.back;          // direção (no espaço do corpo) para onde ele tomba

        /// <summary>A sequência está tocando (o jogador morreu e o tempo de espera passou).</summary>
        public bool IsActive { get { return active; } }
        /// <summary>Segundos desde que a queda começou.</summary>
        public float Elapsed { get { return t; } }
        /// <summary>0 a 1: o quanto a câmera e o corpo estão na pose de morte (cai para 0 sozinho depois de reviver).</summary>
        public float Weight { get { return weight; } }
        /// <summary>O corpo já bateu no chão.</summary>
        public bool HasLanded { get { return landed; } }
        /// <summary>Texto de como o jogador morreu (vazio enquanto está vivo).</summary>
        public string Cause { get { return cause; } }
        /// <summary>De onde veio o golpe final, se se sabe.</summary>
        public bool TryGetHitOrigin(out Vector3 origin) { origin = hitFrom; return hasHit; }
        public float FallSeconds { get { return Mathf.Max(0.2f, fallSeconds); } }

        public event Action<DeathSequence> Started;
        public event Action<DeathSequence> Landed;
        public event Action<DeathSequence> Ended;

        // ------------------------------------------------------------------ ciclo de vida
        void OnEnable() { CameraFx.Register(this); }

        void OnDisable()
        {
            CameraFx.Unregister(this);
            Unhook();
            if (active) RestoreVitals();
        }

        void Start() { Resolve(); ResolveOptional(); }

        bool Resolve()
        {
            if (player != null && stats != null && cam != null) return true;
            if (Time.unscaledTime < nextResolve) return player != null;
            nextResolve = Time.unscaledTime + 0.5f;

            if (player == null) player = PlayerRef.ContextOf(this);
            if (player == null) return false;
            if (stats == null) stats = player.Stats != null ? player.Stats : PlayerRef.Find<StatsComponent>(this);
            if (cam == null) cam = player.Camera != null ? player.Camera : PlayerRef.CameraOf(this);
            if (stats != null && !hooked)
            {
                stats.Damaged += OnDamaged;
                stats.Died += OnDied;
                hooked = true;
            }
            return player != null;
        }

        /// <summary>Corpo animado, coração e respiração são opcionais e podem ser instalados depois: procura de novo a cada morte.</summary>
        void ResolveOptional()
        {
            if (body == null) body = PlayerRef.Find<PlayerBodyPresenter>(this);
            if (body != null && !bodyAdded) { body.AddModifier(this); bodyAdded = true; }
            if (heart == null) heart = PlayerRef.Find<HeartbeatComponent>(this);
            if (breath == null) breath = PlayerRef.Find<BreathingComponent>(this);
        }

        void Unhook()
        {
            if (!hooked || stats == null) { hooked = false; return; }
            stats.Damaged -= OnDamaged;
            stats.Died -= OnDied;
            hooked = false;
        }

        void OnDestroy() { Unhook(); }

        void OnDamaged(StatsComponent s, DamageInfo info, float applied) { lastHit = info; }

        void OnDied(StatsComponent s, DamageInfo info)
        {
            fatal = info;
            hasFatal = true;
        }

        // ------------------------------------------------------------------ quadro
        void Update()
        {
            if (!Resolve()) return;
            float dt = Time.deltaTime;
            bool dead = player.IsDead;

            if (dead && !active && !pending)
            {
                pending = true;
                graceLeft = Mathf.Max(0f, graceSeconds);
            }
            if (!dead && pending) pending = false;

            if (pending)
            {
                graceLeft -= dt;
                if (graceLeft <= 0f) { pending = false; Begin(); }
            }
            else if (active && !dead)
            {
                End();
            }

            if (active)
            {
                float before = t;
                t += dt;
                float fall = FallSeconds;
                if (!landed && before < fall && t >= fall) Land();
            }
            weight = Mathf.MoveTowards(weight, active ? 1f : 0f, dt * (active ? 6f : 1.6f));
            if (!active && weight <= 0f && cause.Length > 0) cause = string.Empty;
        }

        // ------------------------------------------------------------------ começo, batida e fim
        void Begin()
        {
            ResolveOptional();
            active = true;
            landed = false;
            t = 0f;

            DamageInfo info = hasFatal ? fatal : lastHit;
            cause = DeathCause.Describe(info);

            Vector3 here = cam != null ? cam.transform.position : transform.position;
            hasHit = DeathCause.TryGetOrigin(info, transform.position, out hitFrom);
            if (hasHit)
            {
                Vector3 toHit = hitFrom - here;
                toHit.y = 0f;
                float dot = cam != null ? Vector3.Dot(cam.transform.right, toHit.normalized) : 0f;
                side = dot > 0.15f ? -1f : (dot < -0.15f ? 1f : (UnityEngine.Random.value < 0.5f ? -1f : 1f));
                Vector3 away = -toHit;
                if (away.sqrMagnitude > 0.0001f)
                {
                    away.Normalize();
                    Quaternion yaw = Quaternion.Euler(0f, body != null && body.Driver != null ? body.Driver.State.yaw : transform.eulerAngles.y, 0f);
                    fallDir = Quaternion.Inverse(yaw) * away;
                    fallDir.y = 0f;
                    fallDir = fallDir.sqrMagnitude > 0.0001f ? fallDir.normalized : Vector3.back;
                }
            }
            else
            {
                side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                fallDir = Vector3.back;
            }

            drop = MeasureDrop(here);
            bodyHasClip = yieldToDeathClip && HasDeathClip(body);

            if (silenceHeartAndBreath) MuteVitals();
            if (playSounds) PlayStart(here);
            Action<DeathSequence> h = Started; if (h != null) h(this);
        }

        void Land()
        {
            landed = true;
            if (playSounds)
            {
                Vector3 feet = transform.position;
                SoundEmitter.PlayAt(ProceduralAudio.BodyFall(), feet, Mathf.Max(0f, fallNoiseRadius), NoiseKind.Impact, gameObject, 1f, 28f);
            }
            Action<DeathSequence> h = Landed; if (h != null) h(this);
        }

        void End()
        {
            active = false;
            landed = false;
            hasFatal = false;
            hasHit = false;
            RestoreVitals();
            Action<DeathSequence> h = Ended; if (h != null) h(this);
        }

        void MuteVitals()
        {
            if (heart != null) { heartWasOn = heart.enabled; heart.enabled = false; }
            if (breath != null) { breathWasOn = breath.enabled; breath.enabled = false; }
        }

        void RestoreVitals()
        {
            if (!silenceHeartAndBreath) return;
            if (heart != null && heartWasOn) heart.enabled = true;
            if (breath != null && breathWasOn) breath.enabled = true;
            heartWasOn = breathWasOn = false;
        }

        void PlayStart(Vector3 at)
        {
            SoundEmitter.PlayAt(ProceduralAudio.DeathDrone(), at, 0f, NoiseKind.Custom, gameObject, 0.75f, 6f);
        }

        /// <summary>Quanto o olho precisa descer para ficar a 'groundClearance' do chão.</summary>
        float MeasureDrop(Vector3 eye)
        {
            float height = 1.6f;
            Transform root = player != null ? player.transform : transform;
            RaycastHit[] hits = Physics.RaycastAll(eye, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != null && hits[i].collider.transform.IsChildOf(root)) continue;
                if (hits[i].distance < best) best = hits[i].distance;
            }
            if (best < float.MaxValue) height = best;
            return Mathf.Clamp(height - groundClearance, 0f, 2.2f);
        }

        // ------------------------------------------------------------------ curvas
        /// <summary>0..1: gravidade (começa devagar, acelera) até bater no chão.</summary>
        float FallCurve(float time)
        {
            float k = Mathf.Clamp01(time / FallSeconds);
            return k * k;
        }

        float SoftScale { get { return GameSettings.SoftDamageFx ? 0.45f : 1f; } }

        // ------------------------------------------------------------------ câmera
        public void Contribute(Camera c, ref CameraDelta delta)
        {
            if (weight <= 0.001f || c == null || cam == null || c != cam) return;
            float soft = SoftScale;
            float f = FallCurve(t);
            float after = Mathf.Max(0f, t - FallSeconds);
            float bounceY = after > 0f ? bounce * soft * Mathf.Exp(-after * 7f) * Mathf.Abs(Mathf.Sin(after * 16f)) : 0f;

            Vector3 down = cam.transform.InverseTransformDirection(Vector3.down);
            Vector3 pos = down * (drop * f) + Vector3.up * bounceY;
            pos += new Vector3(side * 0.22f * f, 0f, 0.12f * f);

            float roll = side * rollDegrees * soft * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (FallSeconds * 1.1f)));
            float dip = 12f * soft * Mathf.Sin(Mathf.Clamp01(t / FallSeconds) * Mathf.PI);            // a cabeça cai para frente antes de tombar
            float pitch = Mathf.Lerp(0f, endPitch, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (FallSeconds * 1.6f)))) + dip;
            float yaw = side * 8f * soft * f;

            delta.position += pos * weight;
            delta.euler += new Vector3(pitch, yaw, roll) * weight;
            delta.fov -= fovClose * soft * Mathf.Clamp01(t / (FallSeconds * 1.3f)) * weight;
        }

        // ------------------------------------------------------------------ corpo
        /// <summary>O Animator do corpo tem um clipe de morte? (O construtor do menu Animation monta o estado "Dead" com ele.)</summary>
        static bool HasDeathClip(PlayerBodyPresenter presenter)
        {
            PlayerBodyRig rig = presenter as PlayerBodyRig;
            if (rig == null || rig.animator == null || rig.animator.runtimeAnimatorController == null) return false;
            AnimationClip[] clips = rig.animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                string n = clips[i].name.ToLowerInvariant();
                if (n.Contains("death") || n.Contains("dying")) return true;
            }
            return false;
        }

        public void Contribute(PlayerBodyPresenter presenter, PlayerAnimState state, ref PoseDelta pose, float dt)
        {
            if (!collapseBody || bodyHasClip || weight <= 0.001f || presenter != body) return;
            float f = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (FallSeconds * 1.15f)));
            float tilt = bodyFallDegrees * f * weight;
            // tombar para frente (z > 0 no espaço do corpo) = girar em x positivo; para a direita (x > 0) = girar em z positivo
            pose.bodyEuler += new Vector3(fallDir.z * tilt, 0f, fallDir.x * tilt);
        }
    }
}
