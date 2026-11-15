using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Quão alto o jogador está sendo agora para a IA (para o medidor de ruído e para dicas).</summary>
    public enum NoiseLevel
    {
        /// <summary>Parado (ou quase): não faz barulho nenhum.</summary>
        Silent,
        /// <summary>Agachado ou muito devagar, ou em piso que abafa.</summary>
        Quiet,
        /// <summary>Andando normalmente.</summary>
        Normal,
        /// <summary>Correndo ou em piso barulhento: ouvem de longe.</summary>
        Loud
    }

    /// <summary>
    /// Mede como o jogador se move (velocidade, chão, agachado, correndo) olhando só a posição do corpo — não depende do código do
    /// FirstPersonController. Dá aos outros módulos: passos, pulo, pouso, e "esforço" (0 parado, ~0,3 andando, 1 correndo)
    /// que alimenta batimento e respiração. Também emite o barulho dos passos para o NoiseSystem.
    ///
    /// Barulho dos passos = raio da postura (agachado &lt; andando &lt; correndo) × velocidade (devagar faz menos) × piso (NoiseSurface)
    /// × modificadores (SetNoiseModifier: botas silenciosas, um nível ventando...). Parado não faz barulho. O resultado fica em
    /// NoiseRadius / Level, que o NoiseMeterHUD mostra na tela.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Body/Player Motion Probe")]
    [PlayerModule("Corpo", 0)]
    public class PlayerMotionProbe : MonoBehaviour
    {
        [Header("Classificação do movimento (m/s)")]
        [SerializeField] float movingMin = 0.4f;
        [Tooltip("Acima disto conta como correndo. Ajuste para a velocidade de corrida do seu FirstPersonController.")]
        [SerializeField] float runningMin = 4.6f;
        [Tooltip("Altura dos olhos abaixo desta fração da altura em pé conta como agachado (só quando não lê o FirstPersonController).")]
        [SerializeField, Range(0.5f, 0.98f)] float crouchRatio = 0.86f;
        [Tooltip("Lê 'agachado' e 'correndo' direto do FirstPersonController (vale na hora, sem esperar a câmera descer). Desligue se usa outro controlador.")]
        [SerializeField] bool readControllerState = true;
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] float groundCheckDistance = 0.28f;

        [Header("Passos")]
        [SerializeField] float stepLengthWalk = 1.9f;
        [SerializeField] float stepLengthRun = 2.6f;
        [SerializeField] float stepLengthCrouch = 1.3f;
        [Tooltip("Ligue se você já tem os passos do FirstPersonController e vai chamar NotifyStep() a partir do evento dele.")]
        [SerializeField] bool useExternalSteps;

        [Header("Barulho que a IA ouve (raio em metros)")]
        [SerializeField] bool emitNoise = true;
        [SerializeField] float crouchNoise = 1.5f;
        [SerializeField] float walkNoise = 5f;
        [SerializeField] float runNoise = 11f;
        [SerializeField] float landNoiseMax = 18f;
        [Tooltip("O barulho do passo cai junto com a velocidade: andar bem devagar (analógico de leve, agachado) faz quase nada. 0 = não depende da velocidade; 1 = totalmente proporcional.")]
        [SerializeField, Range(0f, 1f)] float speedInfluence = 0.7f;
        [Tooltip("Velocidade 'normal' de cada postura, para saber se o jogador está indo devagar (iguale ao FirstPersonController).")]
        [SerializeField] float crouchSpeedRef = 1.5f;
        [SerializeField] float walkSpeedRef = 3.2f;
        [SerializeField] float runSpeedRef = 5.4f;

        [Header("Medidor de ruído (raio em metros)")]
        [Tooltip("Abaixo disto o jogador está 'quieto'.")]
        [SerializeField] float quietBelow = 2.5f;
        [Tooltip("A partir disto o jogador está 'barulhento'. Entre os dois é 'normal'.")]
        [SerializeField] float loudFrom = 8f;

        Transform body;
        CharacterController cc;
        FirstPersonController controller;
        Camera cam;
        readonly Dictionary<object, float> noiseModifiers = new Dictionary<object, float>();
        float noiseModifier = 1f, surfaceMultiplier = 1f, surfaceTimer;
        Vector3 lastPos;
        bool haveLast;
        float stepDistance, standEye, airMinVy;
        bool wasGrounded = true;

        public Vector3 Velocity { get; private set; }
        public float Speed { get { return Velocity.magnitude; } }
        public float HorizontalSpeed { get; private set; }
        public bool Grounded { get; private set; }
        public bool Moving { get; private set; }
        public bool Running { get; private set; }
        public bool Crouching { get; private set; }
        /// <summary>Raio (m) do barulho que o jogador faz AGORA, suavizado: 0 parado, pequeno agachado, grande correndo.</summary>
        public float NoiseRadius { get; private set; }
        public NoiseLevel Level { get; private set; }
        /// <summary>Raio e hora (Time.time) do último passo que fez barulho (o medidor pulsa a cada passo).</summary>
        public float LastStepRadius { get; private set; }
        public float LastStepTime { get; private set; }
        /// <summary>Multiplicador do piso sob os pés (NoiseSurface); 1 = piso comum.</summary>
        public float SurfaceMultiplier { get { return surfaceMultiplier; } }
        /// <summary>Produto dos modificadores de SetNoiseModifier.</summary>
        public float NoiseModifier { get { return noiseModifier; } }
        /// <summary>0 parado, ~0,3 andando, 1 correndo (suavizado).</summary>
        public float Exertion { get; private set; }

        /// <summary>A cada passo, com a intensidade 0..1 (agachado fraco, correndo forte).</summary>
        public event Action<float> Step;
        public event Action Jumped;
        /// <summary>Pouso, com a velocidade de queda em m/s.</summary>
        public event Action<float> Landed;

        void Start() { Resolve(); }

        void Resolve()
        {
            PlayerContext ctx = PlayerRef.ContextOf(this);
            Transform root = ctx != null ? ctx.transform : transform.root;
            cc = root.GetComponentInChildren<CharacterController>(true);
            body = cc != null ? cc.transform : root;
            controller = ctx != null ? ctx.Controller : root.GetComponentInChildren<FirstPersonController>(true);
            cam = PlayerRef.CameraOf(this);
            haveLast = false;
        }

        /// <summary>
        /// Multiplicador nomeado do barulho dos passos (botas silenciosas = 0,5; sapato de salto = 1,5). 'multiplier' = 1 remove.
        /// Cada sistema usa a sua chave; os valores se multiplicam.
        /// </summary>
        public void SetNoiseModifier(object key, float multiplier)
        {
            if (key == null) return;
            if (Mathf.Approximately(multiplier, 1f)) noiseModifiers.Remove(key);
            else noiseModifiers[key] = Mathf.Max(0f, multiplier);
            float m = 1f;
            foreach (float v in noiseModifiers.Values) m *= v;
            noiseModifier = m;
        }

        /// <summary>Raio (m) que um passo faria agora (postura × velocidade × piso × modificadores), sem emitir nada.</summary>
        public float CurrentStepRadius
        {
            get { return PostureRadius() * SpeedFactor() * surfaceMultiplier * noiseModifier; }
        }

        float PostureRadius() { return Running ? runNoise : (Crouching ? crouchNoise : walkNoise); }

        float SpeedFactor()
        {
            if (speedInfluence <= 0f) return 1f;
            float reference = Running ? runSpeedRef : (Crouching ? crouchSpeedRef : walkSpeedRef);
            float f = Mathf.Clamp(HorizontalSpeed / Mathf.Max(0.1f, reference), 0.25f, 1f);
            return Mathf.Lerp(1f, f, speedInfluence);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (body == null) Resolve();
            if (body == null) return;
            if (cam == null) cam = PlayerRef.CameraOf(this);

            Vector3 pos = body.position;
            if (!haveLast) { lastPos = pos; haveLast = true; return; }
            Vector3 v = (pos - lastPos) / dt;
            lastPos = pos;
            if (v.sqrMagnitude > 60f * 60f) return;         // teletransporte (troca de nível, spawn): não conta como movimento
            Velocity = v;
            HorizontalSpeed = new Vector2(v.x, v.z).magnitude;

            Grounded = CheckGround();
            Moving = Grounded && HorizontalSpeed > movingMin;
            Running = Moving && HorizontalSpeed >= runningMin;

            if (readControllerState && controller != null)
            {
                // direto do controlador: vale no instante em que o botão é apertado (a câmera ainda está descendo)
                Crouching = controller.IsCrouching;
                if (controller.IsSprinting) Running = Moving;
                if (Crouching) Running = false;
            }
            else if (cam != null)
            {
                // sem controlador: a câmera fica bem mais baixa que a maior altura de olhos já vista
                float eye = cam.transform.position.y - pos.y;
                standEye = Mathf.Max(standEye, eye);
                Crouching = standEye > 0.3f && eye < standEye * crouchRatio;
            }

            // piso sob os pés (NoiseSurface): relê de tempos em tempos enquanto anda
            if (Moving)
            {
                surfaceTimer -= dt;
                if (surfaceTimer <= 0f)
                {
                    surfaceTimer = 0.15f;
                    surfaceMultiplier = NoiseSurface.MultiplierAt(pos, groundMask);
                }
            }
            else surfaceTimer = 0f;

            // raio do barulho agora (suavizado: sobe rápido, desce devagar) e o nível do medidor
            float targetRadius = Moving ? CurrentStepRadius : 0f;
            NoiseRadius = Mathf.MoveTowards(NoiseRadius, targetRadius, dt * (targetRadius > NoiseRadius ? 40f : 10f));
            Level = NoiseRadius < 0.3f ? NoiseLevel.Silent : (NoiseRadius < quietBelow ? NoiseLevel.Quiet : (NoiseRadius < loudFrom ? NoiseLevel.Normal : NoiseLevel.Loud));

            float wanted = !Moving ? 0f : (Running ? 1f : (Crouching ? 0.15f : 0.32f));
            Exertion = Mathf.MoveTowards(Exertion, wanted, dt * (wanted > Exertion ? 1.2f : 0.35f));

            // pulo e pouso (a pior velocidade de queda no ar vira a força do impacto)
            if (!Grounded) airMinVy = Mathf.Min(airMinVy, v.y);
            if (wasGrounded && !Grounded)
            {
                airMinVy = 0f;
                if (v.y > 1.5f)
                {
                    Action j = Jumped; if (j != null) j();
                    if (emitNoise) NoiseSystem.Emit(pos, walkNoise * 0.6f * surfaceMultiplier * noiseModifier, NoiseKind.Footstep, gameObject);
                }
            }
            if (!wasGrounded && Grounded)
            {
                float impact = Mathf.Max(0f, -airMinVy);
                airMinVy = 0f;
                if (impact > 2.5f)
                {
                    Action<float> l = Landed; if (l != null) l(impact);
                    if (emitNoise) NoiseSystem.Emit(pos, Mathf.Min(landNoiseMax, 4f + impact * 1.4f) * Mathf.Lerp(1f, surfaceMultiplier, 0.5f) * noiseModifier, NoiseKind.Land, gameObject);
                }
            }
            wasGrounded = Grounded;

            // passos por distância percorrida
            if (!useExternalSteps && Moving)
            {
                float len = Running ? stepLengthRun : (Crouching ? stepLengthCrouch : stepLengthWalk);
                stepDistance += HorizontalSpeed * dt;
                if (stepDistance >= len) { stepDistance -= len; RaiseStep(); }
            }
            else if (!Moving) stepDistance = Mathf.Min(stepDistance, 0.5f);
        }

        /// <summary>Para quem usa os passos do FirstPersonController: chame a cada passo dele (e ligue 'useExternalSteps').</summary>
        public void NotifyStep(float intensity = 1f)
        {
            RaiseStep(Mathf.Clamp01(intensity));
        }

        void RaiseStep()
        {
            RaiseStep(Running ? 1f : (Crouching ? 0.25f : 0.55f));
        }

        void RaiseStep(float intensity)
        {
            Action<float> h = Step; if (h != null) h(intensity);
            float radius = CurrentStepRadius * Mathf.Lerp(0.85f, 1f, intensity);
            LastStepRadius = radius;
            LastStepTime = Time.time;
            if (!emitNoise) return;
            NoiseSystem.Emit(body != null ? body.position : transform.position, radius, NoiseKind.Footstep, gameObject);
        }

        bool CheckGround()
        {
            if (cc != null)
            {
                if (cc.isGrounded) return true;
                Bounds b = cc.bounds;
                Vector3 origin = new Vector3(b.center.x, b.min.y + cc.radius, b.center.z);
                return Physics.SphereCast(origin, cc.radius * 0.9f, Vector3.down, out hitInfo, groundCheckDistance + 0.05f, groundMask, QueryTriggerInteraction.Ignore);
            }
            return Physics.Raycast(body.position + Vector3.up * 0.15f, Vector3.down, out hitInfo, 0.15f + groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
        }

        RaycastHit hitInfo;
    }
}
